// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Shared retry primitives (jitter + bounded exponential backoff) for the Distributed Transaction (DTX)
    /// commit pipeline. Used by both <see cref="ClientRetryPolicy"/> (inner loop, envelope failures) and
    /// <see cref="DistributedTransactionCommitter"/> (outer loop, body-bearing semantic failures).
    /// </summary>
    /// <remarks>
    /// Each retry loop owns its own retry budget and base/max delay constants — see the respective files.
    /// </remarks>
    internal static class DistributedTransactionRetryHelpers
    {
        [ThreadStatic]
        private static Random threadJitter;

        /// <summary>
        /// Computes a bounded exponential backoff with ±25% jitter.
        /// Jitter decorrelates fleet-wide clients so they do not retry in lockstep.
        /// </summary>
        /// <param name="attempt">Zero-based attempt number.</param>
        /// <param name="baseDelay">Base delay (multiplied by 2^attempt).</param>
        /// <param name="maxDelay">Cap applied before jitter is multiplied in.</param>
        /// <param name="maxExponent">Exponent cap to prevent overflow on large attempt counts.</param>
        internal static TimeSpan ComputeBackoff(int attempt, TimeSpan baseDelay, TimeSpan maxDelay, int maxExponent)
        {
            int exponent = Math.Min(Math.Max(attempt, 0), maxExponent);
            double delayMs = baseDelay.TotalMilliseconds * (1L << exponent);
            double cappedMs = Math.Min(delayMs, maxDelay.TotalMilliseconds);
            return DistributedTransactionRetryHelpers.ApplyJitter(cappedMs);
        }

        // Applies ±25% multiplicative jitter to a delay value (range [0.75x, 1.25x]).
        private static TimeSpan ApplyJitter(double delayMs)
        {
            double jitterFactor = 0.75 + (DistributedTransactionRetryHelpers.GetThreadJitter().NextDouble() * 0.5);
            return TimeSpan.FromMilliseconds(delayMs * jitterFactor);
        }

        // Thread-local Random avoids lock contention of a shared instance and keeps seeds independent across threads.
        private static Random GetThreadJitter()
        {
            Random local = DistributedTransactionRetryHelpers.threadJitter;
            if (local == null)
            {
                local = new Random(Guid.NewGuid().GetHashCode() ^ Environment.CurrentManagedThreadId);
                DistributedTransactionRetryHelpers.threadJitter = local;
            }

            return local;
        }
    }
}
