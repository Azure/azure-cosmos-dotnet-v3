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
    public class MetricsCollectorProvider : IMetricsCollectorProvider
    {
        private MetricCollectionWindow metricCollectionWindow;
        private readonly object metricCollectionWindowLock = new object();

        private InsertOperationMetricsCollector insertOperationMetricsCollector;
        private readonly object insertOperationMetricsCollectorLock = new object();

        private QueryOperationMetricsCollector queryOperationMetricsCollector;
        private readonly object queryOperationMetricsCollectorLock = new object();

        private ReadOperationMetricsCollector readOperationMetricsCollector;
        private readonly object readOperationMetricsCollectorLock = new object();

        private readonly Meter insertOperationMeter = new("CosmosBenchmarkInsertOperationMeter");

        private readonly Meter queryOperationMeter = new("CosmosBenchmarkQueryOperationMeter");

        private readonly Meter readOperationMeter = new ("CosmosBenchmarkReadOperationMeter");

        private readonly MeterProvider meterProvider;

        private readonly BenchmarkConfig config;

        public MetricsCollectorProvider(MeterProvider meterProvider, BenchmarkConfig config)
        {
            this.meterProvider = meterProvider;
            this.config = config;
        }

        /// <summary>
        /// The instance of <see cref="CosmosBenchmark.InsertOperationMetricsCollector"/>.
        /// </summary>
        public InsertOperationMetricsCollector InsertOperationMetricsCollector
        {
            get
            {
                if (this.insertOperationMetricsCollector is null)
                {
                    lock (this.insertOperationMetricsCollectorLock)
                    {
                        this.insertOperationMetricsCollector ??= new InsertOperationMetricsCollector(this.insertOperationMeter);
                    }
                }

                return this.insertOperationMetricsCollector;
            }
        }

        /// <summary>
        /// The instance of <see cref="CosmosBenchmark.QueryOperationMetricsCollector"/>.
        /// </summary>
        public QueryOperationMetricsCollector QueryOperationMetricsCollector
        {
            get
            {
                if (this.queryOperationMetricsCollector is null)
                {
                    lock (this.queryOperationMetricsCollectorLock)
                    {
                        this.queryOperationMetricsCollector ??= new QueryOperationMetricsCollector(this.queryOperationMeter);
                    }
                }

                return this.queryOperationMetricsCollector;
            }
        }

        /// <summary>
        /// The instance of <see cref="CosmosBenchmark.ReadOperationMetricsCollector"/>.
        /// </summary>
        public ReadOperationMetricsCollector ReadOperationMetricsCollector
        {
            get
            {
                if (this.readOperationMetricsCollector is null)
                {
                    lock (this.readOperationMetricsCollectorLock)
                    {
                        this.readOperationMetricsCollector ??= new ReadOperationMetricsCollector(this.readOperationMeter);
                    }
                }

                return this.readOperationMetricsCollector;
            }
        }

        /// <summary>
        /// Gets the current metrics collection window.
        /// </summary>
        /// <param name="config">The instance of <see cref="BenchmarkConfig"/>.</param>
        /// <returns>Current <see cref="MetricCollectionWindow"/></returns>
        private MetricCollectionWindow GetCurrentMetricCollectionWindow(BenchmarkConfig config)
        {
            if (this.metricCollectionWindow is null || !this.metricCollectionWindow.IsValid)
            {
                lock (this.metricCollectionWindowLock)
                {
                    this.metricCollectionWindow ??= new MetricCollectionWindow(config);
                }
            }

            return this.metricCollectionWindow;
        }

        /// <summary>
        /// Gets the metric collector.
        /// </summary>
        /// <param name="benchmarkOperation">Benchmark operation.</param>
        /// <returns>Metrics collector.</returns>
        /// <exception cref="NotSupportedException">Thown if provided benchmark operation is not covered supported to collect metrics.</exception>
        public IMetricsCollector GetMetricsCollector(IBenchmarkOperation benchmarkOperation)
        {
            MetricCollectionWindow metricCollectionWindow = this.GetCurrentMetricCollectionWindow(this.config);

            // Reset metricCollectionWindow and flush.
            if (!metricCollectionWindow.IsValid)
            {
                this.meterProvider.ForceFlush();
                metricCollectionWindow.Reset(this.config);
            }

            Type benchmarkOperationType = benchmarkOperation.GetType();
            if (typeof(InsertBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return this.InsertOperationMetricsCollector;
            }
            else if (typeof(QueryBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return this.QueryOperationMetricsCollector;
            }
            else if (typeof(ReadBenchmarkOperation).IsAssignableFrom(benchmarkOperationType))
            {
                return this.ReadOperationMetricsCollector;
            }
            else
            {
                throw new NotSupportedException($"The type {nameof(benchmarkOperationType)} is not supported for collecting metrics.");
            }
        }
    }
}
