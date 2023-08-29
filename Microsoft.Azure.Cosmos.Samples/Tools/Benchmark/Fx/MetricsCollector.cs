//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
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
        public MetricsCollector(BenchmarkOperationType operationType)
        {
            this.meter = new Meter($"CosmosBenchmark{operationType}OperationMeter");
            this.rpsMetricNameHistogram = this.meter.CreateHistogram<double>($"{operationType}OperationRpsHistogram");
            this.operationLatencyHistogram = this.meter.CreateHistogram<double>($"{operationType}OperationLatencyInMsHistogram");

            this.rpsFailedMetricNameHistogram = this.meter.CreateHistogram<double>($"{operationType}FailedOperationRpsHistogram");
            this.operationFailedLatencyHistogram = this.meter.CreateHistogram<double>($"{operationType}FailedOperationLatencyInMsHistogram");

            this.successOperationCounter = this.meter.CreateCounter<long>($"{operationType}OperationSuccess");
            this.failureOperationCounter = this.meter.CreateCounter<long>($"{operationType}OperationFailure");

            this.latencyInMsMetricNameGauge = this.meter.CreateObservableGauge($"{operationType}OperationLatencyInMs",
                () => new Measurement<double>(this.latencyInMs));

            this.rpsNameGauge = this.meter.CreateObservableGauge($"{operationType}OperationRps",
                () => new Measurement<double>(this.rps));

            this.latencyInMsFailedMetricNameGauge = this.meter.CreateObservableGauge($"{operationType}FailedOperationLatencyInMs",
                () => new Measurement<double>(this.latencyInMs));

            this.rpsFailedNameGauge = this.meter.CreateObservableGauge($"{operationType}FailedOperationRps",
                () => new Measurement<double>(this.rps));
        }

        internal static IEnumerable<string> GetBenchmarkMeterNames()
        {
            foreach (BenchmarkOperationType entry in Enum.GetValues<BenchmarkOperationType>())
            {
                yield return $"CosmosBenchmark{entry}OperationMeter";
            }
        }

        /// <summary>
        /// Successful operation with latency
        /// </summary>
        public void OnOperationSuccess(TimeSpan operationLatency)
        {
            this.successOperationCounter.Add(1);
            this.RecordSuccessOpLatencyAndRps(operationLatency);
        }

        /// <summary>
        /// Failed operation with latency
        /// </summary>
        public void OnOperationFailure(TimeSpan operationLatency)
        {
            this.failureOperationCounter.Add(1);
            this.RecordFailedOpLatencyAndRps(operationLatency);
        }

        /// <summary>
        /// Records success operation latency in milliseconds.
        /// </summary>
        /// <param name="timeSpan">The number of milliseconds to record.</param>
        public void RecordSuccessOpLatencyAndRps(
            TimeSpan timeSpan)
        {
            this.rps = timeSpan.TotalMilliseconds != 0 ? 1000 / timeSpan.TotalMilliseconds : 0;
            this.latencyInMs = timeSpan.TotalMilliseconds;
            this.rpsMetricNameHistogram.Record(this.rps);
            this.operationLatencyHistogram.Record(this.latencyInMs);
        }

        /// <summary>
        /// Records failed operation latency in milliseconds.
        /// </summary>
        /// <param name="timeSpan">The number of milliseconds to record.</param>
        public void RecordFailedOpLatencyAndRps(
            TimeSpan timeSpan)
        {
            this.rpsFailed = timeSpan.TotalMilliseconds != 0 ? 1000 / timeSpan.TotalMilliseconds : 0;
            this.latencyFailedInMs = timeSpan.TotalMilliseconds;
            this.rpsFailedMetricNameHistogram.Record(this.rpsFailed);
            this.operationFailedLatencyHistogram.Record(this.latencyFailedInMs);
        }
    }
}
