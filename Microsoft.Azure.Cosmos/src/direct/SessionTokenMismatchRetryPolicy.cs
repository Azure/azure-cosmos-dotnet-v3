//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class SessionTokenMismatchRetryPolicy : IRetryPolicy
    {
        private const string sessionRetryInitialBackoff = "AZURE_COSMOS_SESSION_RETRY_INITIAL_BACKOFF";
        private const string sessionRetryMaximumBackoff = "AZURE_COSMOS_SESSION_RETRY_MAXIMUM_BACKOFF";

        private const int defaultWaitTimeInMilliSeconds = 5000;
        private const int defaultInitialBackoffTimeInMilliseconds = 5;
        private const int defaultMaximumBackoffTimeInMilliseconds = 50;
        private const int backoffMultiplier = 2;

        private static readonly Lazy<int> sessionRetryInitialBackoffConfig;
        private static readonly Lazy<int> sessionRetryMaximumBackoffConfig;

        private int retryCount;
        private Stopwatch durationTimer = new Stopwatch();
        private int waitTimeInMilliSeconds;

        private int? currentBackoffInMilliSeconds;

        static SessionTokenMismatchRetryPolicy()
        {
            // Lazy load the environment variable to avoid the overhead of checking it and parsing the value
            sessionRetryInitialBackoffConfig = new Lazy<int>(() =>
            {
                string sessionRetryInitialBackoffConfig = Environment.GetEnvironmentVariable(sessionRetryInitialBackoff);
                if (!string.IsNullOrWhiteSpace(sessionRetryInitialBackoffConfig))
                {
                    if (int.TryParse(sessionRetryInitialBackoffConfig, out int value) && value >= 0)
                    {
                        return value;
                    }
                    else
                    {
                        DefaultTrace.TraceCritical("The value of AZURE_COSMOS_SESSION_RETRY_INITIAL_BACKOFF is invalid.  Value: {0}", value);
                    }
                }

                return SessionTokenMismatchRetryPolicy.defaultInitialBackoffTimeInMilliseconds;
            });

            sessionRetryMaximumBackoffConfig = new Lazy<int>(() =>
            {
                string sessionRetryMaximumBackoffConfig = Environment.GetEnvironmentVariable(sessionRetryMaximumBackoff);
                if (!string.IsNullOrWhiteSpace(sessionRetryMaximumBackoffConfig))
                {
                    if (int.TryParse(sessionRetryMaximumBackoffConfig, out int value) && value >= 0)
                    {
                        return value;
                    }
                    else
                    {
                        DefaultTrace.TraceCritical("The value of AZURE_COSMOS_SESSION_RETRY_MAXIMUM_BACKOFF is invalid.  Value: {0}", value);
                    }
                }

                return SessionTokenMismatchRetryPolicy.defaultMaximumBackoffTimeInMilliseconds;
            });
        }

        public SessionTokenMismatchRetryPolicy(int waitTimeInMilliSeconds = defaultWaitTimeInMilliSeconds)
        {
            this.durationTimer.Start();
            this.retryCount = 0;
            this.waitTimeInMilliSeconds = waitTimeInMilliSeconds;
            this.currentBackoffInMilliSeconds = null;
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(Exception exception, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ShouldRetryResult result = ShouldRetryResult.NoRetry();

            if (exception is DocumentClientException dce)
            {
                result = this.ShouldRetryInternalAsync(
                    dce?.StatusCode,
                    dce?.GetSubStatus());
            }

            return Task.FromResult(result);
        }

        private ShouldRetryResult ShouldRetryInternalAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode)
        {
            if (statusCode.HasValue && statusCode.Value == HttpStatusCode.NotFound
                && subStatusCode.HasValue && subStatusCode.Value == SubStatusCodes.ReadSessionNotAvailable)
            {
                int remainingTimeInMilliSeconds = this.waitTimeInMilliSeconds - Convert.ToInt32(this.durationTimer.Elapsed.TotalMilliseconds);

                if (remainingTimeInMilliSeconds <= 0)
                {
                    this.durationTimer.Stop();
                    DefaultTrace.TraceInformation("SessionTokenMismatchRetryPolicy not retrying because it has exceeded the time limit. Retry count = {0}", this.retryCount);

                    return ShouldRetryResult.NoRetry();
                }

                TimeSpan backoffTime = TimeSpan.Zero;

                // Don't penalize first retry with delay
                if (this.retryCount > 0)
                {
                    if (!this.currentBackoffInMilliSeconds.HasValue)
                    {
                        this.currentBackoffInMilliSeconds = SessionTokenMismatchRetryPolicy.sessionRetryInitialBackoffConfig.Value;
                    }

                    // Get the backoff time by selecting the smallest value between the remaining time and the current back off time
                    backoffTime = TimeSpan.FromMilliseconds(
                        Math.Min(this.currentBackoffInMilliSeconds.Value, remainingTimeInMilliSeconds));

                    // Update the current back off time
                    this.currentBackoffInMilliSeconds =
                        Math.Min(
                            this.currentBackoffInMilliSeconds.Value * SessionTokenMismatchRetryPolicy.backoffMultiplier,
                            SessionTokenMismatchRetryPolicy.sessionRetryMaximumBackoffConfig.Value);
                }

                this.retryCount++;
                DefaultTrace.TraceInformation("SessionTokenMismatchRetryPolicy will retry. Retry count = {0}.  Backoff time = {1} ms", this.retryCount, backoffTime.Milliseconds);

                return ShouldRetryResult.RetryAfter(backoffTime);
            }

            this.durationTimer.Stop();
            DefaultTrace.TraceInformation("SessionTokenMismatchRetryPolicy not retrying because StatusCode or SubStatusCode not found.");

            return ShouldRetryResult.NoRetry();
        }
    }
}
