//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Represents the metrics collector.
    /// </summary>
    internal class MetricsCollector : IMetricsCollector
    {
        /// <summary>
        /// Represents the meter to collect metrics.
        /// </summary>
        private readonly Meter meter;

        /// <summary>
        /// Represents the histogram for operation latency.
        /// </summary>
        private readonly Histogram<double> operationLatencyHistogram;

        /// <summary>
        /// Represents the histogram for records per second metric.
        /// </summary>
        private readonly Histogram<double> rpsMetricNameHistogram;

        /// <summary>
        /// Represents the success operation counter.
        /// </summary>
        private readonly Counter<long> successOperationCounter;

        /// <summary>
        /// Represents the failure operation counter.
        /// </summary>
        private readonly Counter<long> failureOperationCounter;

        /// <summary>
        /// Represents latency in milliseconds metric gauge.
        /// </summary>
        /// <remarks>Please do not remove this as it used when collecting metrics..</remarks>
        private readonly ObservableGauge<double> latencyInMsMetricNameGauge;

        /// <summary>
        /// Represents records per second metric gauge.
        /// </summary>
        /// <remarks>Please do not remove this as it used when collecting metrics..</remarks>
        private readonly ObservableGauge<double> rpsNameGauge;

        /// <summary>
        /// Latency in milliseconds.
        /// </summary>
        private double latencyInMs;

        /// <summary>
        /// Records per second.
        /// </summary>
        private double rps;

        /// <summary>
        /// Initialize new  instance of <see cref="MetricsCollector"/>.
        /// </summary>
        /// <param name="meter">OpenTelemetry meter.</param>
        public MetricsCollector(Meter meter, string prefix)
        {
            this.meter = meter;
            this.rpsMetricNameHistogram = meter.CreateHistogram<double>($"{prefix}OperationRpsHistogram");
            this.operationLatencyHistogram = meter.CreateHistogram<double>($"{prefix}OperationLatencyInMsHistogram");
            this.successOperationCounter = meter.CreateCounter<long>($"{prefix}InsertOperationSuccess");
            this.failureOperationCounter = meter.CreateCounter<long>($"{prefix}InsertOperationFailure");
            
            this.latencyInMsMetricNameGauge = this.meter.CreateObservableGauge($"{prefix}InsertOperationLatencyInMs",
                () => new Measurement<double>(this.latencyInMs));

            this.rpsNameGauge = this.meter.CreateObservableGauge($"{prefix}InsertOperationRps",
                () => new Measurement<double>(this.rps));
        }

        /// <summary>
        /// Collects the number of successful operations.
        /// </summary>
        public void CollectMetricsOnSuccess()
        {
            this.successOperationCounter.Add(1);
        }

        /// <summary>
        /// Collects the number of failed operations.
        /// </summary>
        public void CollectMetricsOnFailure()
        {
            this.failureOperationCounter.Add(1);
        }

        /// <summary>
        /// Records latency in milliseconds.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to record.</param>
        public void RecordLatencyAndRps(
            TimeSpan timeSpan)
        {
            this.rps = 1000 / timeSpan.Milliseconds;
            this.latencyInMs = timeSpan.Milliseconds;
            this.rpsMetricNameHistogram.Record(this.rps);
            this.operationLatencyHistogram.Record(this.latencyInMs);
        }
    }
}
