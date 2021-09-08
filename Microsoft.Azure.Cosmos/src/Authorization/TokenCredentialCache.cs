//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
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
        // Making the default 50% of the token life span. This gives 50% of the tokens life for transient error
        // to get resolved before the token expires.
        public static readonly double DefaultBackgroundTokenCredentialRefreshIntervalPercentage = .50;

        // The maximum time a task delayed is allowed is Int32.MaxValue in Milliseconds which is roughly 24 days
        public static readonly TimeSpan MaxBackgroundRefreshInterval = TimeSpan.FromMilliseconds(Int32.MaxValue);

        // The token refresh retries half the time. Given default of 1hr it will retry at 30m, 15, 7.5, 3.75, 1.875
        // If the background refresh fails with less than a minute then just allow the request to hit the exception.
        public static readonly TimeSpan MinimumTimeBetweenBackgroundRefreshInterval = TimeSpan.FromMinutes(1);

        private const string ScopeFormat = "https://{0}/.default";
        private readonly TokenRequestContext tokenRequestContext;
        private readonly TokenCredential tokenCredential;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationToken cancellationToken;
        private readonly TimeSpan? userDefinedBackgroundTokenCredentialRefreshInterval;

        private readonly SemaphoreSlim isTokenRefreshingLock = new SemaphoreSlim(1);
        private readonly object backgroundRefreshLock = new object();

        private TimeSpan? systemBackgroundTokenCredentialRefreshInterval;
        private AccessToken? cachedAccessToken;
        private bool isBackgroundTaskRunning = false;
        private bool isDisposed = false;

        internal TokenCredentialCache(
            TokenCredential tokenCredential,
            Uri accountEndpoint,
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

            // Use the cached token if it is still valid
            if (this.cachedAccessToken.HasValue &&
                DateTime.UtcNow < this.cachedAccessToken.Value.ExpiresOn)
            {
                return this.cachedAccessToken.Value.Token;
            }

            AccessToken accessToken = await this.RefreshCachedTokenWithRetryHelperAsync(trace);
            this.StartBackgroundTokenRefreshLoop();
            return accessToken.Token;
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

        private async ValueTask<AccessToken> RefreshCachedTokenWithRetryHelperAsync(
            ITrace trace)
        {
            Exception? lastException = null;
            const int totalRetryCount = 2;
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
                        return await this.UpdateCachedTokenAsync();
                    }
                    catch (RequestFailedException requestFailedException)
                    {
                        lastException = requestFailedException;
                        getTokenTrace.AddDatum(
                            $"RequestFailedException at {DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)}",
                            requestFailedException);

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
                            $"OperationCanceledException at {DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)}",
                            operationCancelled);

                        DefaultTrace.TraceError(
                            $"TokenCredential.GetTokenAsync() failed. scope = {string.Join(";", this.tokenRequestContext.Scopes)}, retry = {retry}, Exception = {lastException}");

                        throw CosmosExceptionFactory.CreateRequestTimeoutException(
                            message: ClientResources.FailedToGetAadToken,
                            headers: new Headers()
                            {
                                SubStatusCode = SubStatusCodes.FailedToGetAadToken,
                            },
                            innerException: lastException,
                            trace: getTokenTrace);
                    }
                    catch (Exception exception)
                    {
                        lastException = exception;
                        getTokenTrace.AddDatum(
                            $"Exception at {DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)}",
                            exception);

                        DefaultTrace.TraceError(
                            $"TokenCredential.GetTokenAsync() failed. scope = {string.Join(";", this.tokenRequestContext.Scopes)}, retry = {retry}, Exception = {lastException}");
                    }
                }
            }

            if (lastException == null)
            {
                throw new ArgumentException("Last exception is null.");
            }

            // The retries have been exhausted. Throw the last exception.
            throw lastException;
        }

        /// <summary>
        /// This method takes a lock to only allow one thread to update the token 
        /// at a time. If the token was updated while it was waiting for the lock it
        /// returns the new cached token.
        /// </summary>
        private async Task<AccessToken> UpdateCachedTokenAsync()
        {
            DateTimeOffset? initialExpireTime = this.cachedAccessToken?.ExpiresOn;

            await this.isTokenRefreshingLock.WaitAsync();

            // Token was already refreshed successfully from another thread.
            if (this.cachedAccessToken.HasValue &&
                (!initialExpireTime.HasValue || this.cachedAccessToken.Value.ExpiresOn != initialExpireTime.Value))
            {
                return this.cachedAccessToken.Value;
            }

            try
            {
                this.cachedAccessToken = await this.tokenCredential.GetTokenAsync(
                    requestContext: this.tokenRequestContext,
                    cancellationToken: default);

                if (!this.cachedAccessToken.HasValue)
                {
                    throw new ArgumentNullException("TokenCredential.GetTokenAsync returned a null token.");
                }

                if (this.cachedAccessToken.Value.ExpiresOn < DateTimeOffset.UtcNow)
                {
                    throw new ArgumentOutOfRangeException($"TokenCredential.GetTokenAsync returned a token that is already expired. Current Time:{DateTime.UtcNow:O}; Token expire time:{this.cachedAccessToken.Value.ExpiresOn:O}");
                }

                if (!this.userDefinedBackgroundTokenCredentialRefreshInterval.HasValue)
                {
                    double refreshIntervalInSeconds = (this.cachedAccessToken.Value.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds * DefaultBackgroundTokenCredentialRefreshIntervalPercentage;
                    
                    // Ensure the background refresh interval is a valid range.
                    refreshIntervalInSeconds = Math.Max(refreshIntervalInSeconds, TokenCredentialCache.MinimumTimeBetweenBackgroundRefreshInterval.TotalSeconds);
                    refreshIntervalInSeconds = Math.Min(refreshIntervalInSeconds, TokenCredentialCache.MaxBackgroundRefreshInterval.TotalSeconds);
                    this.systemBackgroundTokenCredentialRefreshInterval = TimeSpan.FromSeconds(refreshIntervalInSeconds);
                }

                return this.cachedAccessToken.Value;
            }
            finally
            {
                this.isTokenRefreshingLock.Release();
            }
        }

#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void StartBackgroundTokenRefreshLoop()
#pragma warning restore VSTHRD100 // Avoid async void methods
        {
            if (this.isBackgroundTaskRunning)
            {
                return;
            }

            lock (this.backgroundRefreshLock)
            {
                if (this.isBackgroundTaskRunning)
                {
                    return;
                }

                this.isBackgroundTaskRunning = true;
            }

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
                            "BackgroundTokenRefreshLoop() Stopped - The BackgroundTokenCredentialRefreshInterval is {0} which is greater than the maximum allow.",
                            this.BackgroundTokenCredentialRefreshInterval.Value);

                        return;
                    }

                    await Task.Delay(this.BackgroundTokenCredentialRefreshInterval.Value, this.cancellationToken);

                    DefaultTrace.TraceInformation("BackgroundTokenRefreshLoop() - Invoking refresh");

                    await this.UpdateCachedTokenAsync();
                }
                catch (Exception ex)
                {
                    if (this.cancellationTokenSource.IsCancellationRequested &&
                        (ex is OperationCanceledException || ex is ObjectDisposedException))
                    {
                        return;
                    }

                    DefaultTrace.TraceWarning(
                        "BackgroundTokenRefreshLoop() - Unable to refresh token credential cache. Exception: {0}",
                        ex.ToString());

                    // Since it failed retry again in with half the token life span again.
                    if (!this.userDefinedBackgroundTokenCredentialRefreshInterval.HasValue && this.cachedAccessToken.HasValue)
                    {
                        double totalSecondUntilExpire = (this.cachedAccessToken.Value.ExpiresOn - DateTimeOffset.UtcNow).TotalSeconds * DefaultBackgroundTokenCredentialRefreshIntervalPercentage;
                        this.systemBackgroundTokenCredentialRefreshInterval = TimeSpan.FromSeconds(totalSecondUntilExpire);

                        // Refresh interval is less than the minimum. Stop the background refresh.
                        // The background refresh will start again on the next successful token refresh.
                        if (this.systemBackgroundTokenCredentialRefreshInterval < TokenCredentialCache.MinimumTimeBetweenBackgroundRefreshInterval)
                        {
                            lock (this.backgroundRefreshLock)
                            {
                                this.isBackgroundTaskRunning = false;
                            }

                            return;
                        }
                    }
                }
            }
        }
    }
}
