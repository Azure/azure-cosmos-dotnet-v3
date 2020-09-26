//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;

    internal struct TelemetrySpan : IDisposable
    {
        private static double[] latencyHistogram;
        private static int latencyIndex = -1;

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
                    RecordLatency(this.stopwatch.Elapsed.TotalMilliseconds);
                }

                BenchmarkLatencyEventSource.Instance.LatencyDiagnostics(
                    operationResult.DatabseName,
                    operationResult.ContainerName,
                    (int)this.stopwatch.ElapsedMilliseconds,
                    operationResult.lazyDiagnostics);
            }
        }

        private static void RecordLatency(double elapsedMilliseoncds)
        {
            int index = Interlocked.Increment(ref latencyIndex);
            latencyHistogram[index] = elapsedMilliseoncds;
        }

        internal static void ResetLatencyHistogram(int totalNumberOfIterations)
        {
            latencyHistogram = new double[totalNumberOfIterations];
            latencyIndex = -1;
        }

        internal static double? GetLatencyPercentile(int percentile)
        {
            if (latencyHistogram == null)
            {
                return null;
            }

            return MathNet.Numerics.Statistics.Statistics.Percentile(latencyHistogram.Take(latencyIndex + 1), percentile);
        }
    }
}
