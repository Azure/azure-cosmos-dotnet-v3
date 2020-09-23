//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
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
        // Making the default 20 minutes gives a 40 minute buffer for retries and other transient issue to resolve before the 
        // token expires and causes an availability issue.
        public static readonly TimeSpan DefaultBackgroundTokenCredentialRefreshInterval = TimeSpan.FromMinutes(20);

        public readonly TimeSpan backgroundTokenCredentialRefreshInterval;
        private const string ScopeFormat = "https://{0}/.default";
        private readonly TokenRequestContext tokenRequestContext;
        private readonly TokenCredential tokenCredential;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationToken cancellationToken;

        private readonly SemaphoreSlim getTokenRefreshLock = new SemaphoreSlim(1);
        private readonly SemaphoreSlim backgroundRefreshLock = new SemaphoreSlim(1);

        private AccessToken cachedAccessToken;
        private bool isBackgroundTaskRunning = false;
        private bool isDisposed = false;

        internal TokenCredentialCache(
            TokenCredential tokenCredential,
            string accountEndpointHost,
            TimeSpan? backgroundTokenCredentialRefreshInterval)
        {
            this.tokenCredential = tokenCredential;
            this.tokenRequestContext = new TokenRequestContext(new string[]
            {
                string.Format(TokenCredentialCache.ScopeFormat, accountEndpointHost)
            });

            this.backgroundTokenCredentialRefreshInterval = backgroundTokenCredentialRefreshInterval ?? TokenCredentialCache.DefaultBackgroundTokenCredentialRefreshInterval;
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

        }

        internal async ValueTask<string> GetTokenAsync(
            CosmosDiagnosticsContext diagnosticsContext)
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
                        await this.RefreshCachedTokenWithRetryHelperAsync(diagnosticsContext);
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

        private async ValueTask RefreshCachedTokenWithRetryHelperAsync(
            CosmosDiagnosticsContext diagnosticsContext)
        {
            // A different thread is already updating the access token
            bool skipRefreshBecause = this.backgroundRefreshLock.CurrentCount == 1;
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

                    try
                    {
                        using (diagnosticsContext.CreateScope(nameof(this.RefreshCachedTokenWithRetryHelperAsync)))
                        {
                            this.cachedAccessToken = await this.tokenCredential.GetTokenAsync(
                                this.tokenRequestContext,
                                this.cancellationToken);
                            return;
                        }
                    }
                    catch (RequestFailedException requestFailedException)
                    {
                        lastException = requestFailedException;
                        diagnosticsContext.AddDiagnosticsInternal(
                            new PointOperationStatistics(
                                activityId: Trace.CorrelationManager.ActivityId.ToString(),
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
                    catch (Exception exception)
                    {
                        lastException = exception;
                        diagnosticsContext.AddDiagnosticsInternal(
                            new PointOperationStatistics(
                                activityId: Trace.CorrelationManager.ActivityId.ToString(),
                                statusCode: HttpStatusCode.InternalServerError,
                                subStatusCode: SubStatusCodes.Unknown,
                                responseTimeUtc: DateTime.UtcNow,
                                requestCharge: default,
                                errorMessage: exception.ToString(),
                                method: default,
                                requestUri: default,
                                requestSessionToken: default,
                                responseSessionToken: default));

                        DefaultTrace.TraceError(
                        $"TokenCredential.GetToken() failed. scope = {string.Join(";", this.tokenRequestContext.Scopes)}, retry = {retry}, Exception = {lastException}");
                    }

                    DefaultTrace.TraceError(
                        $"TokenCredential.GetToken() failed. scope = {string.Join(";", this.tokenRequestContext.Scopes)}, retry = {retry}, Exception = {lastException}");
                }

                throw CosmosExceptionFactory.CreateUnauthorizedException(
                    ClientResources.FailedToGetAadToken,
                    (int)SubStatusCodes.FailedToGetAadToken,
                    lastException);
            }
            finally
            {
                this.backgroundRefreshLock.Release();
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
                    await Task.Delay(this.backgroundTokenCredentialRefreshInterval, this.cancellationToken);

                    DefaultTrace.TraceInformation("StartRefreshToken() - Invoking refresh");

                    await this.RefreshCachedTokenWithRetryHelperAsync(EmptyCosmosDiagnosticsContext.Singleton);
                }
                catch (Exception ex)
                {
                    if (this.cancellationTokenSource.IsCancellationRequested &&
                        (ex is TaskCanceledException || ex is ObjectDisposedException))
                    {
                        return;
                    }

                    DefaultTrace.TraceCritical(
                        "StartRefreshToken() - Unable to refresh token credential cache. Exception: {0}",
                        ex.ToString());
                }
            }
        }
    }
}
