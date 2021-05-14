//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    // Retry when we receive the throttling from server.
    internal sealed class ResourceThrottleRetryPolicy : IDocumentClientRetryPolicy
    {
        private const int DefaultMaxWaitTimeInSeconds = 60;
        private const int DefaultRetryInSeconds = 5;
        private readonly uint backoffDelayFactor;
        private readonly int maxAttemptCount;
        private readonly TimeSpan maxWaitTimeInMilliseconds;

        private int currentAttemptCount;
        private TimeSpan cumulativeRetryDelay;

        public ResourceThrottleRetryPolicy(
            int maxAttemptCount,
            int maxWaitTimeInSeconds = DefaultMaxWaitTimeInSeconds,
            uint backoffDelayFactor = 1)
        {
            if (maxWaitTimeInSeconds > int.MaxValue / 1000)
            {
                throw new ArgumentException("maxWaitTimeInSeconds", "maxWaitTimeInSeconds must be less than " + (int.MaxValue / 1000));
            }

            this.maxAttemptCount = maxAttemptCount;
            this.backoffDelayFactor = backoffDelayFactor;
            this.maxWaitTimeInMilliseconds = TimeSpan.FromSeconds(maxWaitTimeInSeconds);
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="exception">Exception that occured when the operation was tried</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is DocumentClientException)
            {
                DocumentClientException dce = (DocumentClientException)exception;
                if (!this.IsValidThrottleStatusCode(dce.StatusCode))
                {
                    DefaultTrace.TraceError(
                        "Operation will NOT be retried. Current attempt {0}, Status Code: {1} ",
                        this.currentAttemptCount,
                        dce.StatusCode);
                    return Task.FromResult(ShouldRetryResult.NoRetry());
                }

                return this.ShouldRetryInternalAsync(dce.RetryAfter);
            }

            DefaultTrace.TraceError(
                    "Operation will NOT be retried. Current attempt {0}, Exception: {1} ",
                    this.currentAttemptCount,
                    this.GetExceptionMessage(exception));
            return Task.FromResult(ShouldRetryResult.NoRetry());
        }

        /// <summary> 
        /// Should the caller retry the operation.
        /// </summary>
        /// <param name="cosmosResponseMessage"><see cref="ResponseMessage"/> in return of the request</param>
        /// <param name="cancellationToken"></param>
        /// <returns>True indicates caller should retry, False otherwise</returns>
        public Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            if (!this.IsValidThrottleStatusCode(cosmosResponseMessage?.StatusCode))
            {
                DefaultTrace.TraceError(
                    "Operation will NOT be retried. Current attempt {0}, Status Code: {1} ",
                    this.currentAttemptCount,
                    cosmosResponseMessage?.StatusCode);
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }

            return this.ShouldRetryInternalAsync(cosmosResponseMessage?.Headers.RetryAfter);
        }

        private Task<ShouldRetryResult> ShouldRetryInternalAsync(TimeSpan? retryAfter)
        {
            TimeSpan retryDelay = TimeSpan.Zero;
            if (this.currentAttemptCount < this.maxAttemptCount &&
                this.CheckIfRetryNeeded(retryAfter, out retryDelay))
            {
                this.currentAttemptCount++;
                DefaultTrace.TraceWarning(
                    "Operation will be retried after {0} milliseconds. Current attempt {1}, Cumulative delay {2}",
                    retryDelay.TotalMilliseconds,
                    this.currentAttemptCount,
                    this.cumulativeRetryDelay);
                return Task.FromResult(ShouldRetryResult.RetryAfter(retryDelay));
            }
            else
            {
                DefaultTrace.TraceError(
                    "Operation will NOT be retried. Current attempt {0} maxAttempts {1} Cumulative delay {2} requested retryAfter {3} maxWaitTime {4}",
                    this.currentAttemptCount, this.maxAttemptCount, this.cumulativeRetryDelay, retryAfter, this.maxWaitTimeInMilliseconds);
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }
        }

        private object GetExceptionMessage(Exception exception)
        {
            if (exception is DocumentClientException dce && dce.StatusCode != null && (int)dce.StatusCode < (int)StatusCodes.InternalServerError)
            {
                // for client related errors, don't print out the whole call stack.
                // simply return the message to prevent CPU overhead on ToString() 
                return exception.Message;
            }

            return exception;
        }

        /// <summary>
        /// Method that is called before a request is sent to allow the retry policy implementation
        /// to modify the state of the request.
        /// </summary>
        /// <param name="request">The request being sent to the service.</param>
        public void OnBeforeSendRequest(DocumentServiceRequest request)
        {
        }

        /// <summary>
        /// Returns True if the given <paramref name="retryAfter"/> is within retriable bounds
        /// </summary>
        /// <param name="retryAfter">Value of x-ms-retry-after-ms header</param>
        /// <param name="retryDelay">retryDelay</param>
        /// <returns>True if the exception is retriable; False otherwise</returns>
        private bool CheckIfRetryNeeded(
            TimeSpan? retryAfter,
            out TimeSpan retryDelay)
        {
            retryDelay = TimeSpan.Zero;

            if (retryAfter.HasValue)
            {
                retryDelay = retryAfter.Value;
            }

            if (this.backoffDelayFactor > 1)
            {
                retryDelay = TimeSpan.FromTicks(retryDelay.Ticks * this.backoffDelayFactor);
            }

            if (retryDelay < this.maxWaitTimeInMilliseconds &&
                this.maxWaitTimeInMilliseconds >= (this.cumulativeRetryDelay = retryDelay.Add(this.cumulativeRetryDelay)))
            {
                if (retryDelay == TimeSpan.Zero)
                {
                    // we should never reach here as BE should turn non-zero of retryDelay
                    DefaultTrace.TraceInformation("Received retryDelay of 0 with Http 429: {0}", retryAfter);
                    retryDelay = TimeSpan.FromSeconds(DefaultRetryInSeconds);
                }

                return true;
            }

            return false;
        }

        private bool IsValidThrottleStatusCode(HttpStatusCode? statusCode)
        {
            return statusCode.HasValue && (int)statusCode.Value == (int)StatusCodes.TooManyRequests;
        }
    }
}
