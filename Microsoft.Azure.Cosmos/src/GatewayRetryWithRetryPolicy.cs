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

    /// <summary>
    /// Client-side retry policy for HTTP 449 (<see cref="StatusCodes.RetryWith"/>) responses on the
    /// gateway / thin-client path. The gateway used to orchestrate 449 retries server-side, which made
    /// the retry behavior inconsistent between Gateway V1 (HTTP gateway) and Gateway V2 (thin-client).
    /// This policy makes the SDK the single client-side authority for 449 retries so the behavior is
    /// consistent across both modes (the server-side retry is suppressed via
    /// <c>x-ms-noretry-449</c>, set in <see cref="GatewayStoreModel.ApplyGatewayRetryWithHeaders"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The backoff schedule starts at
    /// <see cref="InitialBackoffTimeInMilliseconds"/>, doubles on each retry up to
    /// <see cref="MaximumBackoffTimeInMilliseconds"/>, adds a small random salt to avoid synchronized
    /// retries across clients, and stops once the overall budget (<c>waitTimeInSeconds</c>) is exhausted.
    /// </para>
    /// </remarks>
    internal sealed class GatewayRetryWithRetryPolicy : IRetryPolicy
    {
        private const int DefaultWaitTimeInSeconds = 30;
        private const int MaximumBackoffTimeInMilliseconds = 1000;
        private const int InitialBackoffTimeInMilliseconds = 10;
        private const int BackoffMultiplier = 2;
        private const int RandomSaltInMilliseconds = 5;

        private readonly int waitTimeInSeconds;
        private readonly Func<TimeSpan> getElapsedTime;
        private readonly Random random = new Random();

        private ValueStopwatch durationTimer = new ValueStopwatch();
        private int currentBackoffMilliseconds = GatewayRetryWithRetryPolicy.InitialBackoffTimeInMilliseconds;

        public GatewayRetryWithRetryPolicy(int waitTimeInSeconds = GatewayRetryWithRetryPolicy.DefaultWaitTimeInSeconds)
        {
            this.waitTimeInSeconds = waitTimeInSeconds;
            this.durationTimer.Start();
            this.getElapsedTime = () => this.durationTimer.Elapsed;
        }

        /// <summary>
        /// Test-only constructor that allows injecting the elapsed time so the retry budget can be
        /// validated deterministically without relying on wall-clock progression.
        /// </summary>
        internal GatewayRetryWithRetryPolicy(int waitTimeInSeconds, Func<TimeSpan> getElapsedTime)
        {
            this.waitTimeInSeconds = waitTimeInSeconds;
            this.durationTimer.Start();
            this.getElapsedTime = getElapsedTime ?? throw new ArgumentNullException(nameof(getElapsedTime));
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (!GatewayRetryWithRetryPolicy.IsRetryWithException(exception))
            {
                // Not a 449 (RetryWith) response - have the caller propagate the original exception.
                this.durationTimer.Stop();
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }

            long remainingMilliseconds = (this.waitTimeInSeconds * 1000L) - (long)this.getElapsedTime().TotalMilliseconds;
            if (remainingMilliseconds <= 0)
            {
                // 449 is a commonly expected, self-healing response, so the budget exhaustion is logged
                // as a warning (not an error) to keep logs quiet.
                DefaultTrace.TraceWarning("Received RetryWith (449) response after exhausting the client retry budget. Will fail the request.");
                this.durationTimer.Stop();
                return Task.FromResult(ShouldRetryResult.NoRetry());
            }

            long backoffMilliseconds = Math.Min(
                Math.Min(
                    this.currentBackoffMilliseconds + this.random.Next(GatewayRetryWithRetryPolicy.RandomSaltInMilliseconds),
                    remainingMilliseconds),
                GatewayRetryWithRetryPolicy.MaximumBackoffTimeInMilliseconds);

            this.currentBackoffMilliseconds = Math.Max(
                GatewayRetryWithRetryPolicy.InitialBackoffTimeInMilliseconds,
                Math.Min(
                    GatewayRetryWithRetryPolicy.MaximumBackoffTimeInMilliseconds,
                    this.currentBackoffMilliseconds * GatewayRetryWithRetryPolicy.BackoffMultiplier));

            DefaultTrace.TraceInformation("Received RetryWith (449) response, will retry after {0} ms.", backoffMilliseconds);

            return Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.FromMilliseconds(backoffMilliseconds)));
        }

        public Task<ShouldRetryResult> ShouldRetryAsync(
            ResponseMessage cosmosResponseMessage,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private static bool IsRetryWithException(Exception exception)
        {
            return exception is DocumentClientException documentClientException
                && documentClientException.StatusCode.HasValue
                && (int)documentClientException.StatusCode.Value == (int)StatusCodes.RetryWith;
        }
    }
}
