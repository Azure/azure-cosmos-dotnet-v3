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

        private int retryCount;
        private Stopwatch durationTimer = new Stopwatch();
        private int waitTimeInMilliSeconds;
        private int initialBackoffTimeInMilliseconds = SessionTokenMismatchRetryPolicy.defaultInitialBackoffTimeInMilliseconds;
        private int maximumBackoffTimeInMilliseconds = SessionTokenMismatchRetryPolicy.defaultMaximumBackoffTimeInMilliseconds;

        private int currentBackoffInMilliSeconds;

        public SessionTokenMismatchRetryPolicy(int waitTimeInMilliSeconds = defaultWaitTimeInMilliSeconds)
        {
            this.durationTimer.Start();
            this.retryCount = 0;
            this.waitTimeInMilliSeconds = waitTimeInMilliSeconds;

            string sessionRetryInitialBackoffConfig = Environment.GetEnvironmentVariable(sessionRetryInitialBackoff);
            if (!string.IsNullOrWhiteSpace(sessionRetryInitialBackoffConfig))
            {
                int value;
                if (int.TryParse(sessionRetryInitialBackoffConfig, out value) && value >= 0)
                {
                    this.initialBackoffTimeInMilliseconds = value;
                }
                else
                {
                    DefaultTrace.TraceCritical("The value of AZURE_COSMOS_SESSION_RETRY_INITIAL_BACKOFF is invalid.  Value: {0}", value);
                }
            }

            string sessionRetryMaximumBackoffConfig = Environment.GetEnvironmentVariable(sessionRetryMaximumBackoff);
            if (!string.IsNullOrWhiteSpace(sessionRetryMaximumBackoffConfig))
            {
                int value;
                if (int.TryParse(sessionRetryMaximumBackoffConfig, out value) && value >= 0)
                {
                    this.maximumBackoffTimeInMilliseconds = value;
                }
                else
                {
                    DefaultTrace.TraceCritical("The value of AZURE_COSMOS_SESSION_RETRY_MAXIMUM_BACKOFF is invalid.  Value: {0}", value);
                }
            }

            currentBackoffInMilliSeconds = this.initialBackoffTimeInMilliseconds;
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(Exception exception, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ShouldRetryResult result = ShouldRetryResult.NoRetry();

            DocumentClientException dce = exception as DocumentClientException;
            if (dce != null)
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
            if ((statusCode.HasValue && statusCode.Value == HttpStatusCode.NotFound)
                && (subStatusCode.HasValue && subStatusCode.Value == SubStatusCodes.ReadSessionNotAvailable))
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
                    // Get the backoff time by selecting the smallest value between the remaining time and the current back off time
                    backoffTime = TimeSpan.FromMilliseconds(
                        Math.Min(this.currentBackoffInMilliSeconds, remainingTimeInMilliSeconds));

                    // Update the current back off time
                    this.currentBackoffInMilliSeconds =
                        Math.Min(
                            this.currentBackoffInMilliSeconds * SessionTokenMismatchRetryPolicy.backoffMultiplier,
                            this.maximumBackoffTimeInMilliseconds);
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
