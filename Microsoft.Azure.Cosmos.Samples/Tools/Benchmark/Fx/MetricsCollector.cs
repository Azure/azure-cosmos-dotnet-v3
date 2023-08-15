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
        /// Represents the histogram for failed operation latency.
        /// </summary>
        private readonly Histogram<double> operationFailedLatencyHistogram;

        /// <summary>
        /// Represents the histogram failed operations for records per second metric.
        /// </summary>
        private readonly Histogram<double> rpsFailedMetricNameHistogram;

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
        /// Represents latency in milliseconds metric gauge for failed operations.
        /// </summary>
        /// <remarks>Please do not remove this as it used when collecting metrics..</remarks>
        private readonly ObservableGauge<double> latencyInMsFailedMetricNameGauge;

        /// <summary>
        /// Represents records per second metric gauge for failed operations.
        /// </summary>
        /// <remarks>Please do not remove this as it used when collecting metrics..</remarks>
        private readonly ObservableGauge<double> rpsFailedNameGauge;

        /// <summary>
        /// Latency in milliseconds.
        /// </summary>
        private double latencyInMs;

        /// <summary>
        /// Records per second.
        /// </summary>
        private double rps;

        /// <summary>
        /// Latency in milliseconds.
        /// </summary>
        private double latencyFailedInMs;

        /// <summary>
        /// Records per second.
        /// </summary>
        private double rpsFailed;

        /// <summary>
        /// Initialize new  instance of <see cref="MetricsCollector"/>.
        /// </summary>
        /// <param name="meter">OpenTelemetry meter.</param>
        public MetricsCollector(Meter meter, string prefix)
        {
            this.meter = meter;
            this.rpsMetricNameHistogram = meter.CreateHistogram<double>($"{prefix}OperationRpsHistogram");
            this.operationLatencyHistogram = meter.CreateHistogram<double>($"{prefix}OperationLatencyInMsHistogram");

            this.rpsFailedMetricNameHistogram = meter.CreateHistogram<double>($"{prefix}FailedOperationRpsHistogram");
            this.operationFailedLatencyHistogram = meter.CreateHistogram<double>($"{prefix}FailedOperationLatencyInMsHistogram");

            this.successOperationCounter = meter.CreateCounter<long>($"{prefix}OperationSuccess");
            this.failureOperationCounter = meter.CreateCounter<long>($"{prefix}OperationFailure");
            
            this.latencyInMsMetricNameGauge = this.meter.CreateObservableGauge($"{prefix}OperationLatencyInMs",
                () => new Measurement<double>(this.latencyInMs));

            this.rpsNameGauge = this.meter.CreateObservableGauge($"{prefix}OperationRps",
                () => new Measurement<double>(this.rps));

            this.latencyInMsFailedMetricNameGauge = this.meter.CreateObservableGauge($"{prefix}FailedOperationLatencyInMs",
                () => new Measurement<double>(this.latencyInMs));

            this.rpsFailedNameGauge = this.meter.CreateObservableGauge($"{prefix}FailedOperationRps",
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
        /// Records success operation latency in milliseconds.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to record.</param>
        public void RecordSuccessOpLatencyAndRps(
            TimeSpan timeSpan)
        {
            this.rps = 1000 / timeSpan.Milliseconds;
            this.latencyInMs = timeSpan.Milliseconds;
            this.rpsMetricNameHistogram.Record(this.rps);
            this.operationLatencyHistogram.Record(this.latencyInMs);
        }
        
        /// <summary>
        /// Records failed operation latency in milliseconds.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to record.</param>
        public void RecordFailedOpLatencyAndRps(
            TimeSpan timeSpan)
        {
            this.rpsFailed = 1000 / timeSpan.Milliseconds;
            this.latencyFailedInMs = timeSpan.Milliseconds;
            this.rpsFailedMetricNameHistogram.Record(this.rpsFailed);
            this.operationFailedLatencyHistogram.Record(this.latencyFailedInMs);
        }
    }
}
