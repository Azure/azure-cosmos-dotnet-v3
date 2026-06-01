//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    internal sealed class WebExceptionRetryPolicy : IRetryPolicy
    {
        // total wait time in seconds to retry. should be max of primary reconfigrations/replication wait duration etc
        private const int waitTimeInSeconds = 30;
        private const int initialBackoffSeconds = 1;
        private const int backoffMultiplier = 2;

        private ValueStopwatch durationTimer = new ValueStopwatch();
        private int attemptCount = 1;

        // Don't penalise first retry with delay.
        private int currentBackoffSeconds = WebExceptionRetryPolicy.initialBackoffSeconds;

        public WebExceptionRetryPolicy()
        {
            durationTimer.Start();
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            // Honor the operation-level CancellationToken before scheduling any further retry.
            // Without this check, a fresh ShouldRetryResult.RetryAfter(...) (exponential backoff up
            // to ~16s) can be issued even after the caller's token has been cancelled, causing the
            // operation to overrun the caller's deadline.
            if (cancellationToken.IsCancellationRequested)
            {
                this.durationTimer.Stop();
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }

            TimeSpan backoffTime = TimeSpan.FromSeconds(0);

            if (!WebExceptionUtility.IsWebExceptionRetriable(exception))
            {
                // Have caller propagate original exception.
                this.durationTimer.Stop();
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }

            // Don't penalise first retry with delay.
            if (attemptCount++ > 1)
            {
                int remainingSeconds = WebExceptionRetryPolicy.waitTimeInSeconds - this.durationTimer.Elapsed.Seconds;
                if (remainingSeconds <= 0)
                {
                    this.durationTimer.Stop();
                    return Task.FromResult(ShouldRetryResult.NoRetry());
                }

                backoffTime = TimeSpan.FromSeconds(Math.Min(this.currentBackoffSeconds, remainingSeconds));
                this.currentBackoffSeconds *= WebExceptionRetryPolicy.backoffMultiplier;
            }

            DefaultTrace.TraceWarning("Received retriable web exception, will retry, {0}", exception.Message);

            return Task.FromResult(ShouldRetryResult.RetryAfter(backoffTime));
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}