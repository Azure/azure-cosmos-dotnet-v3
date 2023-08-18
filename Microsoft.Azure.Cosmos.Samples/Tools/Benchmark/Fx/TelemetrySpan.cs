//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using static CosmosBenchmark.TelemetrySpan;

    internal class TelemetrySpan : ITelemetrySpan
    {
        private static double[] latencyHistogram;
        private static int latencyIndex = -1;

        internal static bool IncludePercentile = false;

        private Stopwatch stopwatch;
        private Func<OperationResult> lazyOperationResult;
        private Action<TimeSpan> recordFailedOpLatencyAction;
        private Action<TimeSpan> recordSuccessOpLatencyAction;
        private bool disableTelemetry;
        private bool isFailed = false;
        private BenchmarkConfig benchmarkConfig;

        public static ITelemetrySpan StartNew(
            BenchmarkConfig benchmarkConfig,
            Func<OperationResult> lazyOperationResult,
            bool disableTelemetry,
            Action<TimeSpan> recordSuccessOpLatencyAction,
            Action<TimeSpan> recordFailedOpLatencyAction)
        {
            if (disableTelemetry || !TelemetrySpan.IncludePercentile)
            {
                return NoOpDisposable.Instance;
            }

            return new TelemetrySpan
            {
                benchmarkConfig = benchmarkConfig,
                stopwatch = Stopwatch.StartNew(),
                lazyOperationResult = lazyOperationResult,
                recordSuccessOpLatencyAction = recordSuccessOpLatencyAction,
                recordFailedOpLatencyAction = recordFailedOpLatencyAction,
                disableTelemetry = disableTelemetry
            };
        }

        public void MarkFailed() { this.isFailed = true; }

        public void Dispose()
        {
            this.stopwatch.Stop();
            if (!this.disableTelemetry)
            {
                OperationResult operationResult = this.lazyOperationResult();

                if (TelemetrySpan.IncludePercentile)
                {
                    RecordLatency(this.stopwatch.Elapsed.TotalMilliseconds);

                    if(this.isFailed)
                    {
                        this.recordSuccessOpLatencyAction?.Invoke(this.stopwatch.Elapsed);
                    }
                    else
                    {
                        this.recordSuccessOpLatencyAction?.Invoke(this.stopwatch.Elapsed);

                    }
                }

                BenchmarkLatencyEventSource.Instance.LatencyDiagnostics(
                    operationResult.DatabseName,
                    operationResult.ContainerName,
                    (int)this.stopwatch.ElapsedMilliseconds,
                    operationResult.LazyDiagnostics, 
                    this.benchmarkConfig.DiagnosticLatencyThresholdInMs);
                
            }
        }

        private static void RecordLatency(double elapsedMilliseoncds)
        {
            int index = Interlocked.Increment(ref latencyIndex);
            latencyHistogram[index] = elapsedMilliseoncds;
        }

        internal static void ResetLatencyHistogram(int totalNumberOfIterations)
        {
            TelemetrySpan.latencyHistogram = new double[totalNumberOfIterations];
            latencyIndex = -1;
        }

        internal static double? GetLatencyPercentile(int percentile)
        {
            if (TelemetrySpan.latencyHistogram == null)
            {
                return null;
            }

            return MathNet.Numerics.Statistics.Statistics.Percentile(latencyHistogram.Take(latencyIndex + 1), percentile);
        }

        internal static double? GetLatencyQuantile(double quantile)
        {
            if (TelemetrySpan.latencyHistogram == null)
            {
                return null;
            }

            return MathNet.Numerics.Statistics.Statistics.Quantile(latencyHistogram.Take(latencyIndex + 1), quantile);
        }

        private class NoOpDisposable : ITelemetrySpan
        {
            public static readonly NoOpDisposable Instance = new NoOpDisposable();

            public void Dispose()
            {
            }

            public void MarkFailed()
            {
            }
        }

        public interface ITelemetrySpan : IDisposable {
            void MarkFailed();
        }
    }
}
