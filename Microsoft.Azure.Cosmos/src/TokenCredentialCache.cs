//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;

    internal sealed class TokenCredentialCache : IDisposable
    {
        private const string ScopeFormat = "https://{0}/user_impersonation";
        private readonly string scope;
        private readonly TokenCredential tokenCredential;
        private AccessToken cachedAccessToken;
        private readonly TimeSpan tokenCredentialRefreshBuffer;
        private readonly int requestTimeoutInSeconds;
        private TimerPool timerPool;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly object statusLock;
        private Status status;
        private TokenRefreshTask tokenRefreshTask;
        private bool isDisposed;

        internal TokenCredentialCache(
            TokenCredential tokenCredential,
            string accountEndpointHost,
            TimeSpan tokenCredentialRefreshBuffer,
            int requestTimeoutInSeconds,
            int timerPoolGranularityInSeconds)
        {
            this.tokenCredential = tokenCredential;
            this.scope = string.Format(TokenCredentialCache.ScopeFormat, accountEndpointHost);
            this.tokenCredentialRefreshBuffer = tokenCredentialRefreshBuffer;
            this.requestTimeoutInSeconds = requestTimeoutInSeconds;
            this.timerPool = new TimerPool(timerPoolGranularityInSeconds);
            this.cancellationTokenSource = new CancellationTokenSource();
            this.statusLock = new object();
            this.status = Status.Stopped;
            this.isDisposed = false;
        }

        internal ValueTask<string> GetTokenAsync()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException("TokenCredentialCache");
            }

            AccessToken cachedAccessToken = this.cachedAccessToken;

            bool tokenUsable = cachedAccessToken.Token != null && cachedAccessToken.ExpiresOn > DateTimeOffset.UtcNow;

            if (tokenUsable)
            {
                bool closeToExpiry = this.IsTokenRefreshRequired(cachedAccessToken);

                if (closeToExpiry)
                {
                    this.GetOrCreateTokenRefreshTask();
                }

                return new ValueTask<string>(cachedAccessToken.Token);
            }
            else
            {
                return new ValueTask<string>(this.AwaitTaskWithTimeoutAsync(this.GetOrCreateTokenRefreshTask()));
            }
        }

        /// <summary>
        /// This is a state machine which ensures that at most one <see cref="TokenRefreshTask"/> is running
        /// at any time, and updates <see cref="cachedAccessToken"/> when the TokenRefreshTask completes.
        /// <see cref="Status.Stopped"/> indicates that no TokenRefreshTask is running, and
        /// <see cref="Status.Started"/> indicates that one TokenRefreshTask is running.
        /// When a new TokenRefreshTask is created, status transitions from Stopped to Started.
        /// When the TokenRefreshTask finishes, status transitions from Started to Stopped.
        /// </summary>
        private TokenRefreshTask GetOrCreateTokenRefreshTask()
        {
            TokenRefreshTask newTokenRefreshTask;

            lock (this.statusLock)
            {
                if (this.status == Status.Started)
                {
                    Debug.Assert(this.tokenRefreshTask != null);

                    return this.tokenRefreshTask;
                }

                Debug.Assert(this.tokenRefreshTask == null);

                AccessToken cachedAccessToken = this.cachedAccessToken;

                if (!this.IsTokenRefreshRequired(cachedAccessToken))
                {
                    return new TokenRefreshTask(cachedAccessToken);
                }

                newTokenRefreshTask = new TokenRefreshTask(
                   this.tokenCredential,
                   this.scope,
                   this.cancellationTokenSource.Token);

                this.tokenRefreshTask = newTokenRefreshTask;
                this.status = Status.Started;
            }

            Task updateCachedAccessTokenTask = newTokenRefreshTask.AccessTokenTask.ContinueWith(completedTask =>
            {
                bool updatedCachedAccessToken = false;
                AccessToken newAccessToken;

                lock (this.statusLock)
                {
                    Debug.Assert(this.status == Status.Started);
                    Debug.Assert(this.tokenRefreshTask != null);
                    Debug.Assert(this.tokenRefreshTask.AccessTokenTask.IsCompleted);

                    Debug.Assert(object.ReferenceEquals(
                        this.tokenRefreshTask.AccessTokenTask,
                        completedTask));

                    newAccessToken = completedTask.Result;

                    if (newAccessToken.Token != null)
                    {
                        this.cachedAccessToken = newAccessToken;
                        updatedCachedAccessToken = true;
                    }

                    this.status = Status.Stopped;
                    this.tokenRefreshTask = null;
                }

                if (updatedCachedAccessToken)
                {
                    DefaultTrace.TraceInformation(
                        "Updated AccessToken. Token expires on {0}, remaining time = {1}",
                        newAccessToken,
                        newAccessToken.ExpiresOn - DateTimeOffset.UtcNow);
                }
            });

            updateCachedAccessTokenTask.ContinueWith(task =>
            {
                DefaultTrace.TraceError(
                    "Failed to update cached AccessToken. Exception = {0}",
                    task.Exception);
            },
            TaskContinuationOptions.OnlyOnFaulted);

            return newTokenRefreshTask;
        }

        private async Task<string> AwaitTaskWithTimeoutAsync(TokenRefreshTask tokenRefreshTask)
        {
            PooledTimer timer = this.timerPool.GetPooledTimer(this.requestTimeoutInSeconds);

            Task<AccessToken> accessTokenTask = tokenRefreshTask.AccessTokenTask;

            Task completedTask = await Task.WhenAny(new Task[]
            {
                accessTokenTask,
                timer.StartTimerAsync()
            });

            if (object.ReferenceEquals(completedTask, accessTokenTask))
            {
                timer.CancelTimer();

                AccessToken accessToken = await accessTokenTask;

                if (accessToken.Token != null)
                {
                    return accessToken.Token;
                }
            }
            else
            {
                DefaultTrace.TraceError(
                    "TokenRefreshTask did not complete within timeout. requestTimeoutInSeconds = {0}, LastException = {1}",
                    this.requestTimeoutInSeconds,
                    tokenRefreshTask.LastException);
            }

            throw CosmosExceptionFactory.CreateUnauthorizedException(
                ClientResources.FailedToGetAadToken,
                (int)SubStatusCodes.FailedToGetAadToken,
                tokenRefreshTask.LastException);
        }

        private bool IsTokenRefreshRequired(AccessToken accessToken)
        {
            return accessToken.Token == null ||
                accessToken.ExpiresOn <= DateTimeOffset.UtcNow + this.tokenCredentialRefreshBuffer;
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            if (this.timerPool != null)
            {
                this.timerPool.Dispose();
                this.timerPool = null;
            }

            this.cancellationTokenSource.Cancel();
            this.cancellationTokenSource.Dispose();

            this.isDisposed = true;
        }

        private enum Status
        {
            Stopped,
            Started
        }

        private sealed class TokenRefreshTask
        {
            private const int MaxRetryCount = 3;

            private const int DelayInMilliseconds = 1000;

            private Exception lastException;

            /// <summary>
            /// The task to obtain AccessToken. This task is guaranteed to not throw exception.
            /// The last exception thrown by <see cref="TokenCredential.GetToken"/> can be
            /// retrieved from <see cref="LastException"/>. If cancellation is requested before
            /// a token is obtained, or a token cannot be obtained after retries,
            /// default(AccessToken) will be the result of this task.
            /// </summary>
            internal Task<AccessToken> AccessTokenTask
            {
                get;
                private set;
            }

            internal Exception LastException
            {
                get
                {
                    return this.lastException ?? new Exception(ClientResources.TokenRefreshInProgress);
                }
            }

            internal TokenRefreshTask(AccessToken accessToken)
            {
                this.AccessTokenTask = Task.FromResult(accessToken);
            }

            internal TokenRefreshTask(
                TokenCredential tokenCredential,
                string scope,
                CancellationToken cancellationToken)
            {
                this.AccessTokenTask = Task.Factory.StartNewOnCurrentTaskSchedulerAsync<AccessToken>(() =>
                {
                    int retry = 0;

                    while (true)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            DefaultTrace.TraceInformation(
                                "Stop RefreshTokenWithIndefiniteRetries because cancellation is requested");

                            break;
                        }

                        try
                        {
                            return tokenCredential.GetToken(
                                new TokenRequestContext(new string[] { scope }),
                                cancellationToken);
                        }
                        catch (Exception exception)
                        {
                            this.lastException = exception;

                            DefaultTrace.TraceError(
                                "TokenCredential.GetToken() failed. scope = {0}, retry = {1}, Exception = {2}",
                                scope, retry, exception);

                            if (retry < TokenRefreshTask.MaxRetryCount)
                            {
                                retry++;
                                Task.Delay(TokenRefreshTask.DelayInMilliseconds).Wait();
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    return default;
                });
            }
        }
    }
}
