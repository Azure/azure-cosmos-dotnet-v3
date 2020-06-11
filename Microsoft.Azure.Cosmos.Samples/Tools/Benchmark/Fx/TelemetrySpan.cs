//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics;
    using HdrHistogram;

    internal struct TelemetrySpan : IDisposable
    {
        internal static HistogramBase LatencyHistogram = new IntConcurrentHistogram(1, 10 * 1000, 0);
        internal static bool IncludePercentile = true;

        private Stopwatch stopwatch;
        private Func<OperationResult> lazyOperationResult;
        private bool disableTelemetry;

        public static TelemetrySpan StartNew(
            Func<OperationResult> lazyOperationResult,
            bool disableTelemetry)
        {
            TelemetrySpan span = new TelemetrySpan();
            span.stopwatch = Stopwatch.StartNew();

            span.lazyOperationResult = lazyOperationResult;
            span.disableTelemetry = disableTelemetry;

            return span;
        }

        public void Dispose()
        {
            this.stopwatch.Stop();
            if (!this.disableTelemetry)
            {
                OperationResult operationResult = this.lazyOperationResult();

                if (TelemetrySpan.IncludePercentile)
                {
                    TelemetrySpan.LatencyHistogram.RecordValue(this.stopwatch.ElapsedMilliseconds);
                }

                BenchmarkLatencyEventSource.Instance.LatencyDiagnostics(
                    operationResult.DatabseName,
                    operationResult.ContainerName,
                    (int)this.stopwatch.ElapsedMilliseconds,
                    operationResult.lazyDiagnostics);
            }
        }
    }
}
