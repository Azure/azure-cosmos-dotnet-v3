//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using Microsoft.ApplicationInsights;

    internal static class MetricsCollectorProvider
    {
        private static MetricCollectionWindow metricCollectionWindow;

        private static readonly object metricCollectionWindowLock = new object();

        private static MetricCollectionWindow CurrentMetricCollectionWindow
        {
            get
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
        }

        private static bool CheckMetricCollectionInvalid(MetricCollectionWindow metricCollectionWindow)
        {
            return metricCollectionWindow is null || DateTime.Now > metricCollectionWindow.DateTimeCreated.AddSeconds(5);
        }

        public static IMetricsCollector GetMetricsCollector(IBenchmarkOperation benchmarkOperation, TelemetryClient telemetryClient)
        {
            Type benchmarkOperationType = benchmarkOperation.GetType();
            if (typeof(InsertBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return CurrentMetricCollectionWindow.GetInsertOperationMetricsCollector(telemetryClient);
            }
            else if (typeof(QueryBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return CurrentMetricCollectionWindow.GetQueryOperationMetricsCollector(telemetryClient);
            }
            else if (typeof(ReadBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return CurrentMetricCollectionWindow.GetReadOperationMetricsCollector(telemetryClient);
            }
            else
            {
                throw new NotSupportedException($"The type {nameof(benchmarkOperationType)} is not supported for collecting metrics.");
            }
        }
    }
}
