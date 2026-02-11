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

    internal sealed class SessionTokenMismatchRetryPolicy : IRetryPolicy, IRequestRetryPolicy<DocumentServiceRequest, StoreResponse>
    {
        private const string sessionRetryInitialBackoff = "AZURE_COSMOS_SESSION_RETRY_INITIAL_BACKOFF";
        private const string sessionRetryMaximumBackoff = "AZURE_COSMOS_SESSION_RETRY_MAXIMUM_BACKOFF";

        private const int defaultWaitTimeInMilliSeconds = 5000;
        private const int defaultInitialBackoffTimeInMilliseconds = 5;
        private const int defaultMaximumBackoffTimeInMilliseconds = 500;
        private const int backoffMultiplier = 5; // before it was very aggressive

        private readonly ISessionRetryOptions sessionRetryOptions;
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
            ISessionRetryOptions sessionRetryOptions = null)
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
                    null,
                    dce?.StatusCode,
                    dce?.GetSubStatus(),
                    dce?.LSN);
            }

            return Task.FromResult(result);
        }

        // IRequestRetryPolicy<DocumentServiceRequest, StoreResponse>
        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
        }

        // IRequestRetryPolicy<DocumentServiceRequest, StoreResponse>
        public Task<ShouldRetryResult> ShouldRetryAsync(DocumentServiceRequest request,
            StoreResponse response,
            Exception exception,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        // IRequestRetryPolicy<DocumentServiceRequest, StoreResponse>
        public bool TryHandleResponseSynchronously(DocumentServiceRequest request,
            StoreResponse response,
            Exception exception,
            out ShouldRetryResult shouldRetryResult)
        {
            HttpStatusCode? httpStatusCode = response?.StatusCode ?? (exception as DocumentClientException)?.StatusCode;
            SubStatusCodes? httpSubStatusCode = response?.SubStatusCode ?? (exception as DocumentClientException)?.GetSubStatus();

            shouldRetryResult = this.ShouldRetryInternalAsync(request, httpStatusCode, httpSubStatusCode, response?.LSN);
            return true;
        }

        private ShouldRetryResult ShouldRetryInternalAsync(DocumentServiceRequest request,
            HttpStatusCode? statusCode,
            SubStatusCodes? subStatusCode,
            long? responseLSN)
        {
            ISessionToken requestSessionToken = request?.RequestContext?.SessionToken;

            if (statusCode.HasValue && statusCode.Value == HttpStatusCode.NotFound
                && subStatusCode.HasValue && subStatusCode.Value == SubStatusCodes.ReadSessionNotAvailable)
            {

                int remainingTimeInMilliSeconds = this.waitTimeInMilliSeconds - Convert.ToInt32(this.durationTimer.Elapsed.TotalMilliseconds);

                if (remainingTimeInMilliSeconds <= 0)
                {
                    this.durationTimer.Stop();

                    DefaultTrace.TraceInformation("SessionTokenMismatchRetryPolicy not retrying because it has exceeded the time limit. Retry count = {0} request-session-token = {1} response-session-token = {2}", 
                        this.retryCount,
                        requestSessionToken == null ? "<empty>" : requestSessionToken.ConvertToString(),
                        responseLSN.HasValue? responseLSN : "<empty>");

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
                if (this.sessionRetryOptions != null && this.sessionRetryOptions.RemoteRegionPreferred &&
                    this.retryCount >= (this.sessionRetryOptions.MaxInRegionRetryCount - 1))
                {
                    TimeSpan elapsed = DateTimeOffset.Now - this.startTime;
                    TimeSpan remainingMinRetryTimeInLocalRegion = TimeSpan.FromMilliseconds(this.sessionRetryOptions.MinInRegionRetryTime.TotalMilliseconds - elapsed.TotalMilliseconds);

                    if (remainingMinRetryTimeInLocalRegion.CompareTo(backoffTime) > 0)
                    {
                        backoffTime = remainingMinRetryTimeInLocalRegion;
                    }
                }

                DefaultTrace.TraceInformation("SessionTokenMismatchRetryPolicy will retry. Retry count = {0}. Backoff time = {1} ms request-session-token = {2} response-session-token = {3}", 
                    this.retryCount, 
                    backoffTime.TotalMilliseconds,
                    requestSessionToken == null ? "<empty>" : requestSessionToken.ConvertToString(),
                    responseLSN.HasValue ? responseLSN : "<empty>");

                return ShouldRetryResult.RetryAfter(backoffTime);
            }

            this.durationTimer.Stop();

            return ShouldRetryResult.NoRetry();
        }

        private bool shouldRetryLocally()
        {
            // If no options, allow retry (legacy behavior)
            if (this.sessionRetryOptions == null)
            {
                return true;
            }

            // If remote region is not preferred, use legacy retry logic
            if (!this.sessionRetryOptions.RemoteRegionPreferred)
            {
                return true;
            }

            // If retries are disabled, do not retry
            if (this.sessionRetryOptions.MaxInRegionRetryCount <= 0)
            {
                return false;
            }

            // SessionTokenMismatchRetryPolicy is invoked after 1 attempt on a region
            // sessionTokenMismatchRetryAttempts increments only after shouldRetry triggers
            // another attempt on the same region
            // hence to curb the retry attempts on a region,
            // compare sessionTokenMismatchRetryAttempts with max retry attempts allowed on the region - 1
            return this.retryCount <= (this.sessionRetryOptions.MaxInRegionRetryCount - 1);
        }
    }
}
