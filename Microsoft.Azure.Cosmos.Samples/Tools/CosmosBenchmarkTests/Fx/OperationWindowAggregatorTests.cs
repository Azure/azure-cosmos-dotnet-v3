//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark.Fx.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using CosmosBenchmark;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests the per-window rollup logic that turns concurrently-recorded latency / RU / error
    /// samples into a dashboard-schema snapshot. The benchmark runs many concurrent executors that
    /// write into the same aggregator, so the window snapshot/reset must neither lose in-flight
    /// samples nor double-count them across the boundary.
    /// </summary>
    [TestClass]
    public class OperationWindowAggregatorTests
    {
        [TestMethod]
        public void SnapshotAndReset_RollsUpCountErrorsPercentilesAndRu()
        {
            OperationWindowAggregator aggregator = new OperationWindowAggregator();

            // Ten successful operations with latencies 10..100 ms, each charging 5 RU.
            for (int i = 1; i <= 10; i++)
            {
                aggregator.RecordSuccess(latencyMs: i * 10, ruCharge: 5);
            }

            // Two failures (one with a known status code / message).
            aggregator.RecordFailure(latencyMs: 5, ruCharge: 0, statusCode: 429, errorMessage: "Throttled");
            aggregator.RecordFailure(latencyMs: 5, ruCharge: 0, statusCode: 0, errorMessage: null);

            OperationWindowSnapshot snapshot = aggregator.SnapshotAndReset(windowSeconds: 10);

            Assert.AreEqual(10, snapshot.Count, "All ten successful ops should be counted.");
            Assert.AreEqual(2, snapshot.Errors, "Both failures should be counted.");
            Assert.AreEqual(55.0, snapshot.MeanMs, 0.0001, "Mean of 10..100 is 55.");
            Assert.IsTrue(snapshot.P50Ms >= 10 && snapshot.P50Ms <= 100, "p50 within sample range.");
            Assert.IsTrue(snapshot.P99Ms >= snapshot.P90Ms, "p99 >= p90.");
            Assert.IsTrue(snapshot.P90Ms >= snapshot.P50Ms, "p90 >= p50.");

            // Total RU = 10 * 5 = 50 over a 10s window => 5 RU/s.
            Assert.AreEqual(5.0, snapshot.RuPerSec, 0.0001);

            // Last observed (non-zero) error status/message should be preserved.
            Assert.AreEqual(429, snapshot.ErrorStatusCode);
            Assert.AreEqual("Throttled", snapshot.ErrorMessage);
        }

        [TestMethod]
        public void SnapshotAndReset_ResetsBetweenWindows()
        {
            OperationWindowAggregator aggregator = new OperationWindowAggregator();
            aggregator.RecordSuccess(latencyMs: 42, ruCharge: 1);

            OperationWindowSnapshot first = aggregator.SnapshotAndReset(windowSeconds: 1);
            Assert.AreEqual(1, first.Count);

            // The second window starts empty.
            OperationWindowSnapshot second = aggregator.SnapshotAndReset(windowSeconds: 1);
            Assert.AreEqual(0, second.Count);
            Assert.AreEqual(0, second.Errors);
        }

        [TestMethod]
        public void ConcurrentRecording_AcrossWindowBoundaries_NoLossNoDoubleCount()
        {
            OperationWindowAggregator aggregator = new OperationWindowAggregator();

            const int writerThreads = 16;
            const int successPerThread = 5000;
            const int failurePerThread = 500;

            long totalCount = 0;
            long totalErrors = 0;
            ConcurrentBag<OperationWindowSnapshot> snapshots = new ConcurrentBag<OperationWindowSnapshot>();

            using CancellationTokenSource cts = new CancellationTokenSource();

            // A snapshot thread that repeatedly rolls up windows while writers are active. This is
            // the concurrency boundary that must neither drop nor double-count in-flight samples.
            Task snapshotTask = Task.Run(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    OperationWindowSnapshot snap = aggregator.SnapshotAndReset(windowSeconds: 1);
                    Interlocked.Add(ref totalCount, snap.Count);
                    Interlocked.Add(ref totalErrors, snap.Errors);
                    Thread.SpinWait(50);
                }
            });

            Task[] writers = new Task[writerThreads];
            for (int t = 0; t < writerThreads; t++)
            {
                writers[t] = Task.Run(() =>
                {
                    for (int i = 0; i < successPerThread; i++)
                    {
                        aggregator.RecordSuccess(latencyMs: 1, ruCharge: 1);
                    }

                    for (int i = 0; i < failurePerThread; i++)
                    {
                        aggregator.RecordFailure(latencyMs: 1, ruCharge: 0, statusCode: 500, errorMessage: "boom");
                    }
                });
            }

            Task.WaitAll(writers);

            // Stop the concurrent snapshotter, then drain the final (post-join) window so every
            // recorded sample is accounted for exactly once.
            cts.Cancel();
            snapshotTask.Wait();

            OperationWindowSnapshot finalSnapshot = aggregator.SnapshotAndReset(windowSeconds: 1);
            totalCount += finalSnapshot.Count;
            totalErrors += finalSnapshot.Errors;

            long expectedSuccess = (long)writerThreads * successPerThread;
            long expectedErrors = (long)writerThreads * failurePerThread;

            Assert.AreEqual(expectedSuccess, totalCount,
                "Every successful sample must be counted exactly once across all windows (no loss, no double-count).");
            Assert.AreEqual(expectedErrors, totalErrors,
                "Every failed sample must be counted exactly once across all windows (no loss, no double-count).");
        }
    }
}
