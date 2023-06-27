//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Represents the metrics collector.
    /// </summary>
    internal abstract class MetricsCollector : IMetricsCollector
    {
        private readonly Meter meter;

        private readonly Histogram<double> operationLatencyHistogram;

        private readonly Histogram<double> rpsMetricNameHistogram;

        private readonly Counter<long> successOperationCounter;

        private readonly Counter<long> failureOperationCounter;

        private readonly ObservableGauge<double> latencyInMsMetricNameGauge;

        private readonly ObservableGauge<double> rpsNameGauge;

        private double latencyInMs;

        private double rps;

        /// <summary>
        /// Initialize new  instance of <see cref="MetricsCollector"/>.
        /// </summary>
        /// <param name="meter">OpenTelemetry meter.</param>
        public MetricsCollector(Meter meter)
        {
            this.meter = meter;
            this.rpsMetricNameHistogram = meter.CreateHistogram<double>(this.RpsHistogramName);
            this.operationLatencyHistogram = meter.CreateHistogram<double>(this.LatencyInMsHistogramName);
            this.successOperationCounter = meter.CreateCounter<long>(this.SuccessOperationMetricName);
            this.failureOperationCounter = meter.CreateCounter<long>(this.FailureOperationMetricName);
            
            this.latencyInMsMetricNameGauge = this.meter.CreateObservableGauge(this.LatencyInMsMetricName,
                () => new Measurement<double>(this.latencyInMs));

            this.rpsNameGauge = this.meter.CreateObservableGauge(this.RpsMetricName,
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
        /// Records latency in milliseconda.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to record.</param>
        public void RecordLatencyAndRps(double milliseconds)
        {
            this.rps = 1000 / milliseconds;
            this.latencyInMs = milliseconds;
            this.rpsMetricNameHistogram.Record(this.rps);
            this.operationLatencyHistogram.Record(this.latencyInMs);
        }

        protected abstract string RpsHistogramName { get; }

        protected abstract string LatencyInMsHistogramName { get; }

        protected abstract string RpsMetricName { get; }

        protected abstract string LatencyInMsMetricName { get; }

        protected abstract string FailureOperationMetricName { get; }

        protected abstract string SuccessOperationMetricName { get; }
    }
}
