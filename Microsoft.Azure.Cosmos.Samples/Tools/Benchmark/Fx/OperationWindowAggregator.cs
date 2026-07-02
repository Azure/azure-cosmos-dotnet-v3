//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;

    /// <summary>
    /// Thread-safe per-operation accumulator that rolls up latency / RU / error samples
    /// for a single metric collection window and produces an <see cref="OperationWindowSnapshot"/>
    /// at the window boundary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ParallelExecutionStrategy"/> runs many concurrent executors that all record
    /// into the same aggregator. The window boundary (<see cref="SnapshotAndReset(double)"/>)
    /// must neither lose in-flight samples nor double-count them.
    /// </para>
    /// <para>
    /// Correctness is achieved with a <see cref="ReaderWriterLockSlim"/>: recording takes a
    /// (shared) read lock and appends to a thread-safe <see cref="ConcurrentQueue{T}"/> /
    /// interlocked counters, while the snapshot takes the (exclusive) write lock and atomically
    /// swaps the window state. Because the write lock waits for every in-flight read lock to
    /// release, no <c>Record*</c> call can be mid-flight while the state is swapped, so every
    /// recorded sample lands in exactly one window.
    /// </para>
    /// </remarks>
    public sealed class OperationWindowAggregator
    {
        private readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private WindowState current = new WindowState();

        /// <summary>
        /// Records a successful operation.
        /// </summary>
        /// <param name="latencyMs">Operation latency in milliseconds.</param>
        /// <param name="ruCharge">Request units charged by the operation.</param>
        public void RecordSuccess(double latencyMs, double ruCharge)
        {
            this.rwLock.EnterReadLock();
            try
            {
                WindowState state = this.current;
                state.SuccessLatencies.Enqueue(latencyMs);
                InterlockedAddDouble(ref state.SuccessRuTotal, ruCharge);
            }
            finally
            {
                this.rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Records a failed operation.
        /// </summary>
        /// <param name="latencyMs">Operation latency in milliseconds.</param>
        /// <param name="ruCharge">Request units charged by the operation (often 0 for failures).</param>
        /// <param name="statusCode">The error status code, if known.</param>
        /// <param name="errorMessage">The error message, if known.</param>
        public void RecordFailure(double latencyMs, double ruCharge, int statusCode, string errorMessage)
        {
            this.rwLock.EnterReadLock();
            try
            {
                WindowState state = this.current;
                Interlocked.Increment(ref state.ErrorCount);
                InterlockedAddDouble(ref state.ErrorRuTotal, ruCharge);
                if (statusCode != 0)
                {
                    state.LastErrorStatusCode = statusCode;
                }

                if (errorMessage != null)
                {
                    state.LastErrorMessage = errorMessage;
                }
            }
            finally
            {
                this.rwLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Atomically captures the accumulated samples for the window, resets the aggregator for
        /// the next window, and rolls the captured samples up into an <see cref="OperationWindowSnapshot"/>.
        /// </summary>
        /// <param name="windowSeconds">Elapsed length of the window in seconds (used for RU/s).</param>
        public OperationWindowSnapshot SnapshotAndReset(double windowSeconds)
        {
            WindowState captured;
            this.rwLock.EnterWriteLock();
            try
            {
                captured = this.current;
                this.current = new WindowState();
            }
            finally
            {
                this.rwLock.ExitWriteLock();
            }

            // 'captured' is now exclusively owned by this method: no other thread can touch it.
            double[] latencies = captured.SuccessLatencies.ToArray();
            long count = latencies.LongLength;
            long errors = Interlocked.Read(ref captured.ErrorCount);

            double p50 = 0, p90 = 0, p99 = 0, mean = 0;
            if (latencies.Length > 0)
            {
                p50 = MathNet.Numerics.Statistics.Statistics.Percentile(latencies, 50);
                p90 = MathNet.Numerics.Statistics.Statistics.Percentile(latencies, 90);
                p99 = MathNet.Numerics.Statistics.Statistics.Percentile(latencies, 99);
                mean = latencies.Average();
            }

            double totalRu = captured.SuccessRuTotal + captured.ErrorRuTotal;
            double ruPerSec = windowSeconds > 0 ? totalRu / windowSeconds : 0;

            return new OperationWindowSnapshot(
                count: count,
                errors: errors,
                p50Ms: p50,
                p90Ms: p90,
                p99Ms: p99,
                meanMs: mean,
                ruPerSec: ruPerSec,
                errorStatusCode: captured.LastErrorStatusCode,
                errorMessage: captured.LastErrorMessage);
        }

        private static void InterlockedAddDouble(ref double location, double value)
        {
            double initial, computed;
            do
            {
                initial = location;
                computed = initial + value;
            }
            while (initial != Interlocked.CompareExchange(ref location, computed, initial));
        }

        /// <summary>
        /// Mutable state for a single window. Swapped atomically under the write lock.
        /// </summary>
        private sealed class WindowState
        {
            public readonly ConcurrentQueue<double> SuccessLatencies = new ConcurrentQueue<double>();
            public double SuccessRuTotal;
            public double ErrorRuTotal;
            public long ErrorCount;
            public volatile int LastErrorStatusCode;
            public volatile string LastErrorMessage;
        }
    }
}
