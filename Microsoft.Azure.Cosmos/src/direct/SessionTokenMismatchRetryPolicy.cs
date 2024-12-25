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
    using HdrHistogram.Utilities;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class SessionTokenMismatchRetryPolicy : IRetryPolicy
    {
        private const string sessionRetryInitialBackoff = "AZURE_COSMOS_SESSION_RETRY_INITIAL_BACKOFF";
        private const string sessionRetryMaximumBackoff = "AZURE_COSMOS_SESSION_RETRY_MAXIMUM_BACKOFF";

        private const int defaultWaitTimeInMilliSeconds = 5000;
        private const int defaultInitialBackoffTimeInMilliseconds = 5;
        private const int defaultMaximumBackoffTimeInMilliseconds = 500;
        //private const int backoffMultiplier = 2;
        private const int backoffMultiplier = 5; // before it was very aggressive
        private readonly SessionRetryOptions sessionRetryOptions;

        private const int DEFAULT_MAX_RETRIES_IN_LOCAL_REGION_WHEN_REMOTE_REGION_PREFERRED = 1;
        internal const int MIN_MIN_IN_REGION_RETRY_TIME_FOR_WRITES_MS = 100;
        private const int DEFAULT_MIN_IN_REGION_RETRY_TIME_FOR_WRITES_MS = 500;
        internal const int MIN_MAX_RETRIES_IN_LOCAL_REGION_WHEN_REMOTE_REGION_PREFERRED = 1;
        private readonly DateTimeOffset startTime = DateTime.UtcNow;


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
                        DefaultTrace.TraceCritical("The value of AZURE_COSMOS_SESSION_RETRY_INITIAL_BACKOFF is invalid. Value: {0}", value);
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
                        DefaultTrace.TraceCritical("The value of AZURE_COSMOS_SESSION_RETRY_MAXIMUM_BACKOFF is invalid. Value: {0}", value);
                    }
                }

                return SessionTokenMismatchRetryPolicy.defaultMaximumBackoffTimeInMilliseconds;
            });
        }

        public SessionTokenMismatchRetryPolicy(int waitTimeInMilliSeconds = defaultWaitTimeInMilliSeconds,
            SessionRetryOptions sessionRetryOptions = null)
        {
            this.durationTimer.Start();
            this.retryCount = 0;
            this.waitTimeInMilliSeconds = waitTimeInMilliSeconds;
            this.currentBackoffInMilliSeconds = null;
            this.sessionRetryOptions = sessionRetryOptions;
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(Exception exception, CancellationToken cancellationToken)
        {
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

                if (!this.shouldRetryLocally())
                {
                    DefaultTrace.TraceInformation("SessionTokenMismatchRetryPolicy not retrying because it a retry attempt for the current region and " +
                                                                    "fallback to a different region is preferred ");
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
                // For remote region preference ensure that the last retry is long enough (even when exceeding max backoff time)
                // to consume the entire minRetryTimeInLocalRegion
                if(this.retryCount >= (this.sessionRetryOptions.MaxInRegionRetryCount - 1))
                {
                    
                    long elapsed =  DateTimeOffset.Now.ToUnixTimeMilliseconds() - this.startTime.ToUnixTimeMilliseconds();
                    TimeSpan remainingMinRetryTimeInLocalRegion = TimeSpan.FromMilliseconds(this.sessionRetryOptions.MinInRegionRetryTime - elapsed);

                    if(remainingMinRetryTimeInLocalRegion.CompareTo(backoffTime) > 0)
                    {
                        backoffTime = remainingMinRetryTimeInLocalRegion;
                    }

                }

                DefaultTrace.TraceInformation("SessionTokenMismatchRetryPolicy will retry. Retry count = {0}. Backoff time = {1} ms", this.retryCount, backoffTime.TotalMilliseconds);

                return ShouldRetryResult.RetryAfter(backoffTime);
            }

            this.durationTimer.Stop();

            return ShouldRetryResult.NoRetry();
        }

        private Boolean shouldRetryLocally()
        {
            /*if (regionSwitchHint != CosmosRegionSwitchHint.REMOTE_REGION_PREFERRED)
            {
                return true;
            }*/

            // SessionTokenMismatchRetryPolicy is invoked after 1 attempt on a region
            // sessionTokenMismatchRetryAttempts increments only after shouldRetry triggers
            // another attempt on the same region
            // hence to curb the retry attempts on a region,
            // compare sessionTokenMismatchRetryAttempts with max retry attempts allowed on the region - 1
            return this.retryCount <= (this.sessionRetryOptions.MaxInRegionRetryCount - 1);
            
        }

       

    }
}
