//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos;

    internal struct TelemetrySpan : IDisposable
    {
        internal static HistogramBase LatencyHistogram = new IntConcurrentHistogram(1, 10 * 1000, 0);

        private Stopwatch stopwatch;
        private Func<CosmosDiagnostics> funcDiagnostics;
        private string databaseName;
        private string containerName;
        private bool disableTelemetry;

        public static TelemetrySpan StartNew(
            string databaseName,
            string containerName,
            Func<CosmosDiagnostics> funcDiag,
            bool disableTelemetry)
        {
            TelemetrySpan span = new TelemetrySpan();
            span.stopwatch = Stopwatch.StartNew();

            span.databaseName = databaseName;
            span.containerName = containerName;
            span.funcDiagnostics = funcDiag;
            span.disableTelemetry = disableTelemetry;

            return span;
        }

        public void Dispose()
        {
            this.stopwatch.Stop();
            if (!this.disableTelemetry)
            {
                TelemetrySpan.LatencyHistogram.RecordValue(this.stopwatch.ElapsedMilliseconds);
                BenchmarkLatencyEventSource.Instance.LatencyDiagnostics(
                    this.databaseName,
                    this.containerName,
                    (int)this.stopwatch.ElapsedMilliseconds,
                    this.funcDiagnostics()?.ToString());
            }
        }
    }
}
