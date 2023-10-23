//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics.Tracing;
    using Microsoft.Azure.Cosmos;

    [EventSource(Name = "Azure.Cosmos.Benchmark")]
    internal class BenchmarkLatencyEventSource : EventSource
    {
        public static BenchmarkLatencyEventSource Instance = new BenchmarkLatencyEventSource();
        private const int TraceLatencyThreshold = 50;

        private BenchmarkLatencyEventSource()
        {
        }

        [Event(1, Level = EventLevel.Informational)]
        public void LatencyDiagnostics(
            string dbName,
            string containerName,
            int durationInMs,
            string dianostics)
        {
            if (this.IsEnabled())
            {
                this.WriteEvent(1, dbName, containerName, durationInMs, dianostics);
            }
        }

        [NonEvent]
        public void LatencyDiagnostics(
            string dbName,
            string containerName,
            int durationInMs,
            Func<string> lazyDiagnostics,
            int latencyThreshold)
        {
            if (durationInMs > latencyThreshold
                && this.IsEnabled())
            {
                this.WriteEvent(1, dbName, containerName, durationInMs, lazyDiagnostics());
            }
        }

        [Event(2, Level = EventLevel.Informational)]
        public void OnOperationSuccess(int operationType, double durationInMs)
        {
            this.WriteEvent(2, operationType, durationInMs);
        }

        [Event(3, Level = EventLevel.Informational)]
        public void OnOperationFailure(int operationType, double durationInMs)
        {
            this.WriteEvent(3, operationType, durationInMs);
        }
    }
}
