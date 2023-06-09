//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using App.Metrics;

    internal static class MetricsCollectorProvider
    {
        public static IMetricsCollector GetMetricsCollector(IBenchmarkOperation benchmarkOperation, MetricsContext metricsContext, IMetrics metrics)
        {
            Type benchmarkOperationType = benchmarkOperation.GetType();
            if (typeof(InsertBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return new InsertOperationMetricsCollector(metricsContext, metrics);
            }
            else if (typeof(QueryBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return new QueryOperationMetricsCollector(metricsContext, metrics);
            }
            else if (typeof(ReadBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return new ReadOperationMetricsCollector(metricsContext, metrics);
            }
            else
            {
                throw new NotSupportedException($"The type {nameof(benchmarkOperationType)} is not supported for collecting metrics.");
            }
        }
    }
}
