//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This is a token credential cache. 
    /// It starts a background task that refreshes the token at a set interval. 
    /// This way refreshing the token does not cause additional latency and also
    /// allows for transient issue to resolve before the token expires.
    /// </summary>
    internal sealed class TokenCredentialCache : IDisposable
    {
        // Default token expiration time is 1hr.
        // Making the default 25% of the token life span. This gives 75% of the tokens life for transient error
        // to get resolved before the token expires.
        public static readonly double DefaultBackgroundTokenCredentialRefreshIntervalPercentage = .25;

        // The maximum time a task delayed is allowed is Int32.MaxValue in Milliseconds which is roughly 24 days
        public static readonly TimeSpan MaxBackgroundRefreshInterval = TimeSpan.FromMilliseconds(Int32.MaxValue);

        private const string ScopeFormat = "https://{0}/.default";
        private readonly TokenRequestContext tokenRequestContext;
        private readonly TokenCredential tokenCredential;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationToken cancellationToken;
        private readonly TimeSpan? userDefinedBackgroundTokenCredentialRefreshInterval;
        private readonly TimeSpan requestTimeout;

        private readonly SemaphoreSlim getTokenRefreshLock = new SemaphoreSlim(1);
        private readonly SemaphoreSlim backgroundRefreshLock = new SemaphoreSlim(1);

        private TimeSpan? systemBackgroundTokenCredentialRefreshInterval;
        private AccessToken cachedAccessToken;
        private bool isBackgroundTaskRunning = false;
        private bool isDisposed = false;

        internal TokenCredentialCache(
            TokenCredential tokenCredential,
            Uri accountEndpoint,
            TimeSpan requestTimeout,
            TimeSpan? backgroundTokenCredentialRefreshInterval)
        {
            this.tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));

            if (accountEndpoint == null)
            {
                throw new ArgumentNullException(nameof(accountEndpoint));
            }

            this.tokenRequestContext = new TokenRequestContext(new string[]
            {
                string.Format(TokenCredentialCache.ScopeFormat, accountEndpoint.Host)
            });

            if (requestTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentException($"{nameof(requestTimeout)} must be a positive value greater than 0. Value '{requestTimeout.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)}'Milliseconds.");
            }

            if (backgroundTokenCredentialRefreshInterval.HasValue)
            {
                if (backgroundTokenCredentialRefreshInterval.Value <= TimeSpan.Zero)
                {
                    throw new ArgumentException($"{nameof(backgroundTokenCredentialRefreshInterval)} must be a positive value greater than 0. Value '{backgroundTokenCredentialRefreshInterval.Value.TotalMilliseconds}'.");
                }

                // TimeSpan.MaxValue disables the background refresh
                if (backgroundTokenCredentialRefreshInterval.Value > TokenCredentialCache.MaxBackgroundRefreshInterval &&
                    backgroundTokenCredentialRefreshInterval.Value != TimeSpan.MaxValue)
                {
                    throw new ArgumentException($"{nameof(backgroundTokenCredentialRefreshInterval)} must be less than or equal to {TokenCredentialCache.MaxBackgroundRefreshInterval}. Value '{backgroundTokenCredentialRefreshInterval.Value}'.");
                }
            }

            this.userDefinedBackgroundTokenCredentialRefreshInterval = backgroundTokenCredentialRefreshInterval;
            this.requestTimeout = requestTimeout;
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;
        }

        public TimeSpan? BackgroundTokenCredentialRefreshInterval =>
            this.userDefinedBackgroundTokenCredentialRefreshInterval ?? this.systemBackgroundTokenCredentialRefreshInterval;

        internal async ValueTask<string> GetTokenAsync(ITrace trace)
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException("TokenCredentialCache");
            }

            if (this.cachedAccessToken.ExpiresOn <= DateTime.UtcNow)
            {
                await this.getTokenRefreshLock.WaitAsync();

                // Don't refresh if another thread already updated it.
                if (this.cachedAccessToken.ExpiresOn <= DateTime.UtcNow)
                {
                    try
                    {
                        await this.RefreshCachedTokenWithRetryHelperAsync(trace);
                        this.StartRefreshToken();
                    }
                    finally
                    {
                        this.getTokenRefreshLock.Release();
                    }
                }
            }

            return this.cachedAccessToken.Token;
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();
            this.isDisposed = true;
        }

        private async ValueTask RefreshCachedTokenWithRetryHelperAsync(ITrace trace)
        {
            // A different thread is already updating the access token. Count starts off at 1.
            bool skipRefreshBecause = this.backgroundRefreshLock.CurrentCount != 1;
            await this.backgroundRefreshLock.WaitAsync();
            try
            {
                // Token was already refreshed successfully from another thread.
                if (skipRefreshBecause && this.cachedAccessToken.ExpiresOn > DateTime.UtcNow)
                {
                    return;
                }

                Exception lastException = null;
                const int totalRetryCount = 3;
                for (int retry = 0; retry < totalRetryCount; retry++)
                {
                    if (this.cancellationToken.IsCancellationRequested)
                    {
                        DefaultTrace.TraceInformation(
                            "Stop RefreshTokenWithIndefiniteRetries because cancellation is requested");

                        break;
                    }

                    using (ITrace getTokenTrace = trace.StartChild(
                        name: nameof(this.RefreshCachedTokenWithRetryHelperAsync),
                        component: TraceComponent.Authorization,
                        level: Tracing.TraceLevel.Info))
                    {
                        try
                        {
                            await this.ExecuteGetTokenWithRequestTimeoutAsync();
                            return;
                        }
                        catch (RequestFailedException requestFailedException)
                        {
                            lastException = requestFailedException;
                            getTokenTrace.AddDatum(
                                "Request Failed Exception",
                                new PointOperationStatisticsTraceDatum(
                                    activityId: System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                    statusCode: (HttpStatusCode)requestFailedException.Status,
                                    subStatusCode: SubStatusCodes.Unknown,
                                    responseTimeUtc: DateTime.UtcNow,
                                    requestCharge: default,
                                    errorMessage: requestFailedException.ToString(),
                                    method: default,
                                    requestUri: null,
                                    requestSessionToken: default,
                                    responseSessionToken: default));

                            DefaultTrace.TraceError($"TokenCredential.GetToken() failed with RequestFailedException. scope = {string.Join(";", this.tokenRequestContext.Scopes)}, retry = {retry}, Exception = {lastException}");

                            // Don't retry on auth failures
                            if (requestFailedException.Status == (int)HttpStatusCode.Unauthorized ||
                                requestFailedException.Status == (int)HttpStatusCode.Forbidden)
                            {
                                this.cachedAccessToken = default;
                                throw;
                            }
                        }
                        catch (OperationCanceledException operationCancelled)
                        {
                            lastException = operationCancelled;
                            getTokenTrace.AddDatum(
                                "Request Timeout Exception",
                                new PointOperationStatisticsTraceDatum(
                                    activityId: System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                    statusCode: HttpStatusCode.RequestTimeout,
                                    subStatusCode: SubStatusCodes.Unknown,
                                    responseTimeUtc: DateTime.UtcNow,
                                    requestCharge: default,
                                    errorMessage: operationCancelled.ToString(),
                                    method: default,
                                    requestUri: null,
                                    requestSessionToken: default,
                                    responseSessionToken: default));

                            DefaultTrace.TraceError(
                                $"TokenCredential.GetTokenAsync() failed. scope = {string.Join(";", this.tokenRequestContext.Scopes)}, retry = {retry}, Exception = {lastException}");

                            throw CosmosExceptionFactory.CreateRequestTimeoutException(
                                message: ClientResources.FailedToGetAadToken,
                                subStatusCode: (int)SubStatusCodes.FailedToGetAadToken,
                                innerException: lastException,
                                trace: getTokenTrace);
                        }
                        catch (Exception exception)
                        {
                            lastException = exception;
                            getTokenTrace.AddDatum(
                                "Internal Server Error Exception",
                                new PointOperationStatisticsTraceDatum(
                                    activityId: System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                    statusCode: HttpStatusCode.InternalServerError,
                                    subStatusCode: SubStatusCodes.Unknown,
                                    responseTimeUtc: DateTime.UtcNow,
                                    requestCharge: default,
                                    errorMessage: exception.ToString(),
                                    method: default,
                                    requestUri: null,
                                    requestSessionToken: default,
                                    responseSessionToken: default));

                            DefaultTrace.TraceError(
                                $"TokenCredential.GetTokenAsync() failed. scope = {string.Join(";", this.tokenRequestContext.Scopes)}, retry = {retry}, Exception = {lastException}");
                        }
                    }

                    DefaultTrace.TraceError(
                        $"TokenCredential.GetTokenAsync() failed. scope = {string.Join(";", this.tokenRequestContext.Scopes)}, retry = {retry}, Exception = {lastException}");
                }

                throw CosmosExceptionFactory.CreateUnauthorizedException(
                    message: ClientResources.FailedToGetAadToken,
                    subStatusCode: (int)SubStatusCodes.FailedToGetAadToken,
                    innerException: lastException,
                    trace: trace);
            }
            finally
            {
                this.backgroundRefreshLock.Release();
            }
        }

        private async ValueTask ExecuteGetTokenWithRequestTimeoutAsync()
        {
            using CancellationTokenSource singleRequestCancellationTokenSource = new CancellationTokenSource(this.requestTimeout);

            Task[] valueTasks = new Task[2];
            valueTasks[0] = Task.Delay(this.requestTimeout);

            Task<AccessToken> valueTaskTokenCredential = this.tokenCredential.GetTokenAsync(
                this.tokenRequestContext,
                singleRequestCancellationTokenSource.Token).AsTask();

            valueTasks[1] = valueTaskTokenCredential;
            await Task.WhenAny(valueTasks);

            // Time out completed and the GetTokenAsync did not
            if (valueTasks[0].IsCompleted && !valueTasks[1].IsCompleted)
            {
                throw new OperationCanceledException($"TokenCredential.GetTokenAsync request timed out after {this.requestTimeout}");
            }

            this.cachedAccessToken = await valueTaskTokenCredential;

            if (!this.userDefinedBackgroundTokenCredentialRefreshInterval.HasValue)
            {
                double totalSecondUntilExpire = (this.cachedAccessToken.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds * DefaultBackgroundTokenCredentialRefreshIntervalPercentage;
                this.systemBackgroundTokenCredentialRefreshInterval = TimeSpan.FromSeconds(totalSecondUntilExpire);
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void StartRefreshToken()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (this.isBackgroundTaskRunning)
            {
                return;
            }

            this.isBackgroundTaskRunning = true;
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (!this.BackgroundTokenCredentialRefreshInterval.HasValue)
                    {
                        throw new ArgumentException(nameof(this.BackgroundTokenCredentialRefreshInterval));
                    }

                    // Stop the background refresh if the interval is greater than Task.Delay allows. 
                    if (this.BackgroundTokenCredentialRefreshInterval.Value > TokenCredentialCache.MaxBackgroundRefreshInterval)
                    {
                        DefaultTrace.TraceWarning(
                            "StartRefreshToken() Stopped - The BackgroundTokenCredentialRefreshInterval is {0} which is greater than the maximum allow.",
                            this.BackgroundTokenCredentialRefreshInterval.Value);

                        return;
                    }

                    await Task.Delay(this.BackgroundTokenCredentialRefreshInterval.Value, this.cancellationToken);

                    DefaultTrace.TraceInformation("StartRefreshToken() - Invoking refresh");

                    await this.RefreshCachedTokenWithRetryHelperAsync(NoOpTrace.Singleton);
                }
                catch (Exception ex)
                {
                    if (this.cancellationTokenSource.IsCancellationRequested &&
                        (ex is TaskCanceledException || ex is ObjectDisposedException))
                    {
                        return;
                    }

                    DefaultTrace.TraceWarning(
                        "StartRefreshToken() - Unable to refresh token credential cache. Exception: {0}",
                        ex.ToString());
                }
            }
        }
    }
}
