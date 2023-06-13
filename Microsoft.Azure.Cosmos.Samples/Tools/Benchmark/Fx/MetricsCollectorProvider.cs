//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics.Metrics;
    using App.Metrics;
    using OpenTelemetry.Metrics;

    internal static class MetricsCollectorProvider
    {
        private static readonly Meter meter = new("CosmosBenchmarkMeter");

        private static Counter<long> cosmosBenchmarkCounter;

        private static Counter<long> GetCosmosBenchmarkCounter()
        {
            if (cosmosBenchmarkCounter == null)
            {
                cosmosBenchmarkCounter = meter.CreateCounter<long>("ReadCounter");
            }

            return cosmosBenchmarkCounter;
        }

        public static IMetricsCollector GetMetricsCollector(IBenchmarkOperation benchmarkOperation, MetricsContext metricsContext, IMetrics metrics)
        {
            Type benchmarkOperationType = benchmarkOperation.GetType();
            if (typeof(InsertBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return new InsertOperationMetricsCollector(metricsContext, metrics, GetCosmosBenchmarkCounter());
            }
            else if (typeof(QueryBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return new QueryOperationMetricsCollector(metricsContext, metrics, GetCosmosBenchmarkCounter());
            }
            else if (typeof(ReadBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return new ReadOperationMetricsCollector(metricsContext, metrics, GetCosmosBenchmarkCounter());
            }
            else
            {
                throw new NotSupportedException($"The type {nameof(benchmarkOperationType)} is not supported for collecting metrics.");
            }
        }
    }
}
