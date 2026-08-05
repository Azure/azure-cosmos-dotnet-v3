//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// Snapshot of .NET runtime / process metrics captured for a single window. These occupy the
    /// dashboard slots that the Rust workload uses for <c>tokio_*</c> gauges.
    /// </summary>
    internal readonly struct RuntimeMetricsSnapshot
    {
        public RuntimeMetricsSnapshot(
            long gcGen0Count,
            long gcGen1Count,
            long gcGen2Count,
            long gcHeapBytes,
            int threadPoolThreadCount,
            long threadPoolQueueLength,
            double cpuPercent,
            long memoryBytes)
        {
            this.GcGen0Count = gcGen0Count;
            this.GcGen1Count = gcGen1Count;
            this.GcGen2Count = gcGen2Count;
            this.GcHeapBytes = gcHeapBytes;
            this.ThreadPoolThreadCount = threadPoolThreadCount;
            this.ThreadPoolQueueLength = threadPoolQueueLength;
            this.CpuPercent = cpuPercent;
            this.MemoryBytes = memoryBytes;
        }

        /// <summary>Gen0 collections that occurred during the window (delta).</summary>
        public long GcGen0Count { get; }

        /// <summary>Gen1 collections that occurred during the window (delta).</summary>
        public long GcGen1Count { get; }

        /// <summary>Gen2 collections that occurred during the window (delta).</summary>
        public long GcGen2Count { get; }

        public long GcHeapBytes { get; }

        public int ThreadPoolThreadCount { get; }

        public long ThreadPoolQueueLength { get; }

        public double CpuPercent { get; }

        public long MemoryBytes { get; }
    }

    /// <summary>
    /// Captures per-window .NET runtime metrics. GC collection counts and CPU usage are reported as
    /// deltas relative to the previous capture so the dashboard plots per-interval activity.
    /// </summary>
    internal sealed class RuntimeMetricsCollector
    {
        private readonly Process process = Process.GetCurrentProcess();

        private int prevGen0;
        private int prevGen1;
        private int prevGen2;
        private TimeSpan prevCpuTime;
        private DateTime prevTimestampUtc;

        public RuntimeMetricsCollector()
        {
            this.prevGen0 = GC.CollectionCount(0);
            this.prevGen1 = GC.CollectionCount(1);
            this.prevGen2 = GC.CollectionCount(2);
            this.prevCpuTime = this.process.TotalProcessorTime;
            this.prevTimestampUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Captures the current runtime metrics, computing GC and CPU values relative to the
        /// previous capture, then advances the baseline for the next window.
        /// </summary>
        public RuntimeMetricsSnapshot Capture()
        {
            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);

            DateTime nowUtc = DateTime.UtcNow;
            this.process.Refresh();
            TimeSpan cpuTime = this.process.TotalProcessorTime;

            double elapsedMs = (nowUtc - this.prevTimestampUtc).TotalMilliseconds;
            double cpuPercent = 0;
            if (elapsedMs > 0)
            {
                double cpuMs = (cpuTime - this.prevCpuTime).TotalMilliseconds;
                cpuPercent = (cpuMs / (elapsedMs * Environment.ProcessorCount)) * 100.0;
                if (cpuPercent < 0)
                {
                    cpuPercent = 0;
                }
            }

            RuntimeMetricsSnapshot snapshot = new RuntimeMetricsSnapshot(
                gcGen0Count: gen0 - this.prevGen0,
                gcGen1Count: gen1 - this.prevGen1,
                gcGen2Count: gen2 - this.prevGen2,
                gcHeapBytes: GC.GetTotalMemory(forceFullCollection: false),
                threadPoolThreadCount: ThreadPool.ThreadCount,
                threadPoolQueueLength: ThreadPool.PendingWorkItemCount,
                cpuPercent: Math.Round(cpuPercent, 2),
                memoryBytes: this.process.WorkingSet64);

            this.prevGen0 = gen0;
            this.prevGen1 = gen1;
            this.prevGen2 = gen2;
            this.prevCpuTime = cpuTime;
            this.prevTimestampUtc = nowUtc;

            return snapshot;
        }
    }
}
