//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Diagnostics.Tracing;

    [EventSource(Name = "Azure.Cosmos.Benchmark")]
    internal class BenchmarkLatencyEventSource : EventSource
    {
        public static BenchmarkLatencyEventSource Instance = new BenchmarkLatencyEventSource();

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
    }
}
