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

        private static InsertOperationMetricsCollector insertOperationMetricsCollector;
        private static readonly object insertOperationMetricsCollectorLock = new object();

        private static QueryOperationMetricsCollector queryOperationMetricsCollector;
        private static readonly object queryOperationMetricsCollectorLock = new object();

        private static ReadOperationMetricsCollector readOperationMetricsCollector;
        private static readonly object readOperationMetricsCollectorLock = new object();

        private static readonly Meter insertOperationMeter = new("CosmosBenchmarkInsertOperationMeter");

        private static readonly Meter queryOperationMeter = new("CosmosBenchmarkQueryOperationMeter");

        private static readonly Meter readOperationMeter = new ("CosmosBenchmarkReadOperationMeter");

        public static InsertOperationMetricsCollector InsertOperationMetricsCollector
        {
            get
            {
                if (insertOperationMetricsCollector is null)
                {
                    lock (insertOperationMetricsCollectorLock)
                    {
                        insertOperationMetricsCollector ??= new InsertOperationMetricsCollector(insertOperationMeter);
                    }
                }

                return insertOperationMetricsCollector;
            }
        }

        public static QueryOperationMetricsCollector QueryOperationMetricsCollector
        {
            get
            {
                if (queryOperationMetricsCollector is null)
                {
                    lock (queryOperationMetricsCollectorLock)
                    {
                        queryOperationMetricsCollector ??= new QueryOperationMetricsCollector(queryOperationMeter);
                    }
                }

                return queryOperationMetricsCollector;
            }
        }

        public static ReadOperationMetricsCollector ReadOperationMetricsCollector
        {
            get
            {
                if (readOperationMetricsCollector is null)
                {
                    lock (readOperationMetricsCollectorLock)
                    {
                        readOperationMetricsCollector ??= new ReadOperationMetricsCollector(readOperationMeter);
                    }
                }

                return readOperationMetricsCollector;
            }
        }

        private static MetricCollectionWindow GetCurrentMetricCollectionWindow(BenchmarkConfig config)
        {
            if (metricCollectionWindow is null || !metricCollectionWindow.IsValid)
            {
                lock (metricCollectionWindowLock)
                {
                    metricCollectionWindow ??= new MetricCollectionWindow(config);
                }
            }

            return metricCollectionWindow;
        }

        public static IMetricsCollector GetMetricsCollector(IBenchmarkOperation benchmarkOperation, MeterProvider meterProvider, BenchmarkConfig config)
        {
            MetricCollectionWindow metricCollectionWindow = GetCurrentMetricCollectionWindow(config);

            // Reset metricCollectionWindow and flush.
            if (!metricCollectionWindow.IsValid)
            {
                meterProvider.ForceFlush();
                metricCollectionWindow.Reset(config);
            }

            Type benchmarkOperationType = benchmarkOperation.GetType();
            if (typeof(InsertBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return InsertOperationMetricsCollector;
            }
            else if (typeof(QueryBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return QueryOperationMetricsCollector;
            }
            else if (typeof(ReadBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return ReadOperationMetricsCollector;
            }
            else
            {
                throw new NotSupportedException($"The type {nameof(benchmarkOperationType)} is not supported for collecting metrics.");
            }
        }
    }
}
