//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics.Metrics;
    using OpenTelemetry.Metrics;

    internal static class MetricsCollectorProvider
    {
        private static MetricCollectionWindow metricCollectionWindow;

        private static readonly object metricCollectionWindowLock = new object();

        private static readonly Meter insertOperationMeter = new("CosmosBenchmarkInsertOperationMeter");

        private static readonly Meter queryOperationMeter = new("CosmosBenchmarkQueryOperationMeter");

        private static readonly Meter readOperationMeter = new("CosmosBenchmarkReadOperationMeter");

        private static MetricCollectionWindow GetCurrentMetricCollectionWindow(MeterProvider meterProvider)
        {
            if (CheckMetricCollectionInvalid(metricCollectionWindow))
            {
                lock (metricCollectionWindowLock)
                {
                    if (CheckMetricCollectionInvalid(metricCollectionWindow))
                    {
                        metricCollectionWindow = new MetricCollectionWindow();
                    }
                }
            }

            return metricCollectionWindow;
        }

        private static bool CheckMetricCollectionInvalid(MetricCollectionWindow metricCollectionWindow)
        {
            return metricCollectionWindow is null || DateTime.Now > metricCollectionWindow.DateTimeCreated.AddSeconds(5);
        }

        public static IMetricsCollector GetMetricsCollector(IBenchmarkOperation benchmarkOperation, MeterProvider meterProvider)
        {
            Type benchmarkOperationType = benchmarkOperation.GetType();
            if (typeof(InsertBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return GetCurrentMetricCollectionWindow(meterProvider).GetInsertOperationMetricsCollector(insertOperationMeter);
            }
            else if (typeof(QueryBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return GetCurrentMetricCollectionWindow(meterProvider).GetQueryOperationMetricsCollector(queryOperationMeter);
            }
            else if (typeof(ReadBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return GetCurrentMetricCollectionWindow(meterProvider).GetReadOperationMetricsCollector(readOperationMeter);
            }
            else
            {
                throw new NotSupportedException($"The type {nameof(benchmarkOperationType)} is not supported for collecting metrics.");
            }
        }
    }
}
