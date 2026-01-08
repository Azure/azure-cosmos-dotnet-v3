//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#nullable enable
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Authorization;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
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
        public static readonly TimeSpan MaxBackgroundRefreshInterval = TimeSpan.FromMilliseconds(int.MaxValue);

        // The token refresh retries half the time. Given default of 1hr it will retry at 30m, 15, 7.5, 3.75, 1.875
        // If the background refresh fails with less than a minute then just allow the request to hit the exception.
        public static readonly TimeSpan MinimumTimeBetweenBackgroundRefreshInterval = TimeSpan.FromMinutes(1);

        private readonly IScopeProvider scopeProvider;
        private readonly TokenCredential tokenCredential;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly CancellationToken cancellationToken;
        private readonly TimeSpan? userDefinedBackgroundTokenCredentialRefreshInterval;

        private readonly SemaphoreSlim isTokenRefreshingLock = new SemaphoreSlim(1);
        private readonly object backgroundRefreshLock = new object();

        private TimeSpan? systemBackgroundTokenCredentialRefreshInterval;
        private Task<AccessToken>? currentRefreshOperation = null;
        private AccessToken? cachedAccessToken = null;
        private bool isBackgroundTaskRunning = false;
        private bool isDisposed = false;
        private string? cachedClaimsChallenge = null;

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

            this.scopeProvider = new Microsoft.Azure.Cosmos.Authorization.CosmosScopeProvider(accountEndpoint);

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

            AccessToken accessToken = await this.GetNewTokenAsync(trace);
            if (!this.isBackgroundTaskRunning)
            {
                // This is a background thread so no need to await
                Task backgroundThread = Task.Run(this.StartBackgroundTokenRefreshLoop);
            }

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

        /// <summary>
        /// Resets the cached token and stores claims challenge for AAD token revocation.
        /// The stored claims will be merged with client capabilities (cp1) in the next token request.
        /// </summary>
        /// <param name="claimsChallenge">Optional claims challenge (base64-encoded) from WWW-Authenticate header to merge with client capabilities</param>
        internal void ResetCachedToken(string? claimsChallenge = null)
        {
            if (this.isDisposed)
            {
                return;
            }

            lock (this.backgroundRefreshLock)
            {
                this.cachedAccessToken = null;
                this.currentRefreshOperation = null;
                this.isBackgroundTaskRunning = false;
                this.cachedClaimsChallenge = claimsChallenge;
            }

            DefaultTrace.TraceInformation(
                $"TokenCredentialCache: Token cache reset due to AAD revocation signal. HasClaims={claimsChallenge != null}");
        }

        private async Task<AccessToken> GetNewTokenAsync(
           ITrace trace)
        {
            // Use a local variable to avoid the possibility the task gets changed
            // between the null check and the await operation.
            Task<AccessToken>? currentTask = this.currentRefreshOperation;
            if (currentTask != null)
            {
                // The refresh is already occurring wait on the existing task
                return await currentTask;
            }

            try
            {
                await this.isTokenRefreshingLock.WaitAsync();

                // avoid doing the await in the semaphore to unblock the parallel requests
                if (this.currentRefreshOperation == null)
                {
                    // ValueTask can not be awaited multiple times
                    currentTask = this.RefreshCachedTokenWithRetryHelperAsync(trace).AsTask();
                    this.currentRefreshOperation = currentTask;
                }
                else
                {
                    currentTask = this.currentRefreshOperation;
                }
            }
            finally
            {
                this.isTokenRefreshingLock.Release();
            }

            return await currentTask;
        }

        /// <summary>
        /// Merges claims with client capabilities for token requests.
        /// For Token Revocation: Returns cp1 + claims challenge
        /// For Normal requests: Returns only cp1
        /// </summary>
        /// <param name="claimsChallenge">The base64-encoded claims challenge from WWW-Authenticate header (null for normal requests)</param>
        /// <returns>JSON string with client capabilities and optional claims (NOT base64-encoded)</returns>
        internal static string MergeClaimsWithClientCapabilities(string? claimsChallenge)
        {
            const string clientCapabilitiesJson = "{\"access_token\":{\"xms_cc\":{\"values\":[\"cp1\"]}}}";

            // Return only cp1 capability
            if (string.IsNullOrEmpty(claimsChallenge))
            {
                return clientCapabilitiesJson;
            }

            // Token Revocation: Merge claims challenge with cp1
            try
            {
                byte[] claimsBytes = Convert.FromBase64String(claimsChallenge);
                string claimsJson = System.Text.Encoding.UTF8.GetString(claimsBytes);

                int accessTokenIndex = claimsJson.IndexOf("\"access_token\"", StringComparison.Ordinal);
                if (accessTokenIndex < 0)
                {
                    DefaultTrace.TraceWarning("TokenCredentialCache: CAE claims challenge missing 'access_token' key, using client capabilities only");
                    return clientCapabilitiesJson;
                }

                int openBraceIndex = claimsJson.IndexOf('{', accessTokenIndex);
                if (openBraceIndex < 0)
                {
                    DefaultTrace.TraceWarning("TokenCredentialCache: Malformed CAE claims challenge, using client capabilities only");
                    return clientCapabilitiesJson;
                }

                // Find the matching closing brace
                int braceCount = 1;
                int currentIndex = openBraceIndex + 1;
                int closeBraceIndex = -1;

                while (currentIndex < claimsJson.Length && braceCount > 0)
                {
                    if (claimsJson[currentIndex] == '{')
                    {
                        braceCount++;
                    }
                    else if (claimsJson[currentIndex] == '}')
                    {
                        braceCount--;
                        if (braceCount == 0)
                        {
                            closeBraceIndex = currentIndex;
                            break;
                        }
                    }
                    currentIndex++;
                }

                if (closeBraceIndex < 0)
                {
                    return clientCapabilitiesJson;
                }

                string mergedJson = claimsJson.Substring(0, closeBraceIndex) +
                                    ",\"xms_cc\":{\"values\":[\"cp1\"]}" +
                                    claimsJson.Substring(closeBraceIndex);

                return mergedJson;
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning($"TokenCredentialCache: Failed to merge claims challenge: {ex.Message}. Using client capabilities only.");
                return clientCapabilitiesJson;
            }
        }

        private async ValueTask<AccessToken> RefreshCachedTokenWithRetryHelperAsync(
            ITrace trace)
        {
            Exception? lastException = null;
            const int totalRetryCount = 2;
            TokenRequestContext tokenRequestContext = default;

            try
            {
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
                            tokenRequestContext = this.scopeProvider.GetTokenRequestContext();

                            string mergedClaims = MergeClaimsWithClientCapabilities(this.cachedClaimsChallenge);

                            if (string.IsNullOrEmpty(this.cachedClaimsChallenge))
                            {
                                DefaultTrace.TraceInformation(
                                    $"Requesting AAD token with CAE client capabilities (cp1). Retry={retry}");
                            }
                            else
                            {
                                DefaultTrace.TraceInformation(
                                    $"Requesting AAD token for revocation with claims challenge and client capabilities (cp1). Retry={retry}");
                            }

                            tokenRequestContext = new TokenRequestContext(
                                scopes: tokenRequestContext.Scopes,
                                parentRequestId: tokenRequestContext.ParentRequestId,
                                claims: mergedClaims,
                                tenantId: tokenRequestContext.TenantId,
                                isCaeEnabled: tokenRequestContext.IsCaeEnabled);

                            this.cachedAccessToken = await this.tokenCredential.GetTokenAsync(
                                requestContext: tokenRequestContext,
                                cancellationToken: this.cancellationToken);

                            if (!this.cachedAccessToken.HasValue)
                            {
                                throw new ArgumentNullException("TokenCredential.GetTokenAsync returned a null token.");
                            }

                            if (this.cachedAccessToken.Value.ExpiresOn < DateTimeOffset.UtcNow)
                            {
                                throw new ArgumentOutOfRangeException(
                                    $"TokenCredential.GetTokenAsync returned a token that is already expired. " +
                                    $"Current Time:{DateTime.UtcNow:O}; Token expire time:{this.cachedAccessToken.Value.ExpiresOn:O}");
                            }

                            // Clear claims challenge after successful token acquisition
                            this.cachedClaimsChallenge = null;

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
                        catch (OperationCanceledException operationCancelled)
                        {
                            lastException = operationCancelled;
                            getTokenTrace.AddDatum(
                                $"OperationCanceledException at {DateTime.UtcNow.ToString(CultureInfo.InvariantCulture)}",
                                operationCancelled.Message);

                            DefaultTrace.TraceError(
                               $"TokenCredential.GetTokenAsync() failed. scope = {string.Join(";", tokenRequestContext.Scopes)}, retry = {retry}, Exception = {lastException.Message}");

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
                                exception.Message);

                            DefaultTrace.TraceError(
                                $"TokenCredential.GetTokenAsync() failed. " +
                                $"scope = {string.Join(";", tokenRequestContext.Scopes)}, " +
                                $"hasClaimsChallenge = {this.cachedClaimsChallenge != null}, " +
                                $"retry = {retry}, " +
                                $"Exception = {lastException.Message}");

                            // Don't retry on auth failures
                            if (exception is RequestFailedException requestFailedException &&
                                   (requestFailedException.Status == (int)HttpStatusCode.Unauthorized ||
                                    requestFailedException.Status == (int)HttpStatusCode.Forbidden))
                            {
                                this.cachedAccessToken = default;
                                this.cachedClaimsChallenge = null;
                                throw;
                            }

                            bool didFallback = this.scopeProvider.TryFallback(exception);

                            if (didFallback)
                            {
                                DefaultTrace.TraceInformation($"TokenCredential.GetTokenAsync() failed. scope = {string.Join(";", tokenRequestContext.Scopes)}, retry = {retry}, Exception = {lastException.Message}. Fallback attempted: {didFallback}");
                            }
                        }
                    }
                }

                if (lastException == null)
                {
                    throw new ArgumentException("Last exception is null.");
                }

                this.cachedClaimsChallenge = null;

                // The retries have been exhausted. Throw the last exception.
                throw lastException;
            }
            finally
            {
                try
                {
                    await this.isTokenRefreshingLock.WaitAsync();
                    this.currentRefreshOperation = null;
                }
                finally
                {
                    this.isTokenRefreshingLock.Release();
                }
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

                    await this.GetNewTokenAsync(Tracing.Trace.GetRootTrace("TokenCredentialCacheBackground refresh"));
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
                        ex.Message);

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
