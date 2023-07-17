//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics.Metrics;
    using OpenTelemetry.Metrics;

    /// <summary>
    /// Represents the metrics collector provider.
    /// </summary>
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

        /// <summary>
        /// The instance of <see cref="CosmosBenchmark.InsertOperationMetricsCollector"/>.
        /// </summary>
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

        /// <summary>
        /// The instance of <see cref="CosmosBenchmark.QueryOperationMetricsCollector"/>.
        /// </summary>
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

        /// <summary>
        /// The instance of <see cref="CosmosBenchmark.ReadOperationMetricsCollector"/>.
        /// </summary>
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

        /// <summary>
        /// Gets the current metrics collection window.
        /// </summary>
        /// <param name="config">The instance of <see cref="BenchmarkConfig"/>.</param>
        /// <returns>Current <see cref="MetricCollectionWindow"/></returns>
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

        /// <summary>
        /// Gets the metric collector.
        /// </summary>
        /// <param name="benchmarkOperation">Benchmark operation.</param>
        /// <param name="meterProvider">Meter provider.</param>
        /// <param name="config">Benchmark configuration.</param>
        /// <returns>Metrics collector.</returns>
        /// <exception cref="NotSupportedException">Thrown if provided benchmark operation is not covered supported to collect metrics.</exception>
        public static IMetricsCollector GetMetricsCollector(IBenchmarkOperation benchmarkOperation, MeterProvider meterProvider, BenchmarkConfig config)
        {
            MetricCollectionWindow metricCollectionWindow = GetCurrentMetricCollectionWindow(config);

            // Reset metricCollectionWindow and flush.
            if (!metricCollectionWindow.IsValid)
            {
                meterProvider.ForceFlush();
                metricCollectionWindow.Reset(config);
            }

            return benchmarkOperation.OperationType switch
            {
                BenchmarkOperationType.Insert => InsertOperationMetricsCollector,
                BenchmarkOperationType.Query => QueryOperationMetricsCollector,
                BenchmarkOperationType.Read => ReadOperationMetricsCollector,
                _ => throw new NotSupportedException($"The type of {nameof(benchmarkOperation)} is not supported for collecting metrics."),
            };
        }
    }
}
