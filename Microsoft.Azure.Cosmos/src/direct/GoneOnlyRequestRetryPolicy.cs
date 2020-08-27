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

    internal class GoneOnlyRequestRetryPolicy<TResponse> : 
        IRequestRetryPolicy<GoneOnlyRequestRetryPolicyContext, DocumentServiceRequest, TResponse> where TResponse : IRetriableResponse
    {
        private const int backoffMultiplier = 2;
        private const int initialBackoffTimeInSeconds = 1;

        private Stopwatch durationTimer = new Stopwatch();

        private readonly TimeSpan retryTimeout;
        private int currentBackoffTimeInSeconds;
        private bool isInRetry;

        public GoneOnlyRequestRetryPolicy(
            TimeSpan retryTimeout)
        {
            this.retryTimeout = retryTimeout;
            this.currentBackoffTimeInSeconds = initialBackoffTimeInSeconds;
            this.isInRetry = false;

            // Initialize context
            this.ExecuteContext.RemainingTimeInMsOnClientRequest = retryTimeout;
            durationTimer.Start();
        }

        public GoneOnlyRequestRetryPolicyContext ExecuteContext { get; } = new GoneOnlyRequestRetryPolicyContext();

        public void OnBeforeSendRequest(DocumentServiceRequest request) {}

        public bool TryHandleResponseSynchronously(DocumentServiceRequest request, TResponse response, Exception exception, out ShouldRetryResult shouldRetryResult)
        {
            if (response?.StatusCode != HttpStatusCode.Gone
                && !(exception is GoneException))
            {
                shouldRetryResult = ShouldRetryResult.NoRetry();
                return true;
            }

            TimeSpan elapsed = this.durationTimer.Elapsed;
            if (elapsed >= this.retryTimeout)
            {
                DefaultTrace.TraceInformation("GoneOnlyRequestRetryPolicy - timeout {0}, elapsed {1}", this.retryTimeout, elapsed);

                this.durationTimer.Stop();
                shouldRetryResult = ShouldRetryResult.NoRetry(new ServiceUnavailableException(exception));
                return true;
            }

            TimeSpan remainingTime = this.retryTimeout - elapsed;

            TimeSpan backoffTime = TimeSpan.Zero;
            if (this.isInRetry)
            {
                backoffTime = TimeSpan.FromSeconds(this.currentBackoffTimeInSeconds);
                this.currentBackoffTimeInSeconds *= 2;

                if (backoffTime > remainingTime)
                {
                    DefaultTrace.TraceInformation("GoneOnlyRequestRetryPolicy - timeout {0}, elapsed {1}, backoffTime {2}", this.retryTimeout, elapsed, backoffTime);

                    this.durationTimer.Stop();
                    shouldRetryResult = ShouldRetryResult.NoRetry(new ServiceUnavailableException(exception));
                    return true;
                }
            }
            else
            {
                this.isInRetry = true;
            }

            DefaultTrace.TraceInformation(
                "GoneOnlyRequestRetryPolicy - timeout {0}, elapsed {1}, backoffTime {2}, remainingTime {3}",
                this.retryTimeout,
                elapsed,
                backoffTime,
                remainingTime);
            shouldRetryResult = ShouldRetryResult.RetryAfter(backoffTime);

            // Update context
            this.ExecuteContext.IsInRetry = this.isInRetry;
            this.ExecuteContext.ForceRefresh = true;
            this.ExecuteContext.RemainingTimeInMsOnClientRequest = remainingTime;

            return true;
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(DocumentServiceRequest request, TResponse response, Exception exception, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    internal sealed class GoneOnlyRequestRetryPolicyContext
    {
        public bool ForceRefresh { get; set; }

        public bool IsInRetry { get; set; }

        public TimeSpan RemainingTimeInMsOnClientRequest { get; set; }
    }
}
