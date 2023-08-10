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
    internal class MetricsCollectorProvider
    {
        private readonly MetricCollectionWindow metricCollectionWindow;

        private readonly InsertOperationMetricsCollector insertOperationMetricsCollector;

        private readonly QueryOperationMetricsCollector queryOperationMetricsCollector;

        private readonly ReadOperationMetricsCollector readOperationMetricsCollector;

        private readonly Meter insertOperationMeter = new("CosmosBenchmarkInsertOperationMeter");

        private readonly Meter queryOperationMeter = new("CosmosBenchmarkQueryOperationMeter");

        private readonly Meter readOperationMeter = new("CosmosBenchmarkReadOperationMeter");

        public MetricsCollectorProvider(BenchmarkConfig config)
        {
            this.insertOperationMetricsCollector ??= new InsertOperationMetricsCollector(this.insertOperationMeter);
            this.queryOperationMetricsCollector ??= new QueryOperationMetricsCollector(this.queryOperationMeter);
            this.readOperationMetricsCollector ??= new ReadOperationMetricsCollector(this.readOperationMeter);
            this.metricCollectionWindow ??= new MetricCollectionWindow(config);
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
        public IMetricsCollector GetMetricsCollector(IBenchmarkOperation benchmarkOperation, MeterProvider meterProvider, BenchmarkConfig config)
        {
            MetricCollectionWindow metricCollectionWindow = this.metricCollectionWindow;

            // Reset metricCollectionWindow and flush.
            if (!metricCollectionWindow.IsValid)
            {
                meterProvider.ForceFlush();
                metricCollectionWindow.Reset(config);
            }

            return benchmarkOperation.OperationType switch
            {
                BenchmarkOperationType.Insert => this.insertOperationMetricsCollector,
                BenchmarkOperationType.Query => this.queryOperationMetricsCollector,
                BenchmarkOperationType.Read => this.readOperationMetricsCollector,
                _ => throw new NotSupportedException($"The type of {nameof(benchmarkOperation)} is not supported for collecting metrics."),
            };
        }
    }
}
