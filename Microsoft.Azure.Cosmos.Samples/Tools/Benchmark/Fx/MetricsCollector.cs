//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.DataContracts;

    internal abstract class MetricsCollector : IMetricsCollector
    {
        private readonly List<double> operationLatenciesInMs = new List<double>();

        protected readonly TelemetryClient telemetryClient;

        protected int successCounter;

        protected int failureCounter;

        public MetricsCollector(TelemetryClient telemetryClient)
        {
            this.telemetryClient = telemetryClient;
            this.successCounter = 0;
            this.failureCounter = 0;
        }

        public void CollectMetricsOnSuccess()
        {
            MetricTelemetry metricTelemetry = new MetricTelemetry(this.SuccessOperationMetricName, ++this.successCounter);
            this.telemetryClient.TrackMetric(metricTelemetry);
        }

        public void CollectMetricsOnFailure()
        {
            MetricTelemetry metricTelemetry = new MetricTelemetry(this.FailureOperationMetricName, ++this.failureCounter);
            this.telemetryClient.TrackMetric(metricTelemetry);
        }

        public void RecordLatency(double milliseconds)
        {
            this.operationLatenciesInMs.Add(milliseconds);
            this.TelemetryPercentiles();
        }

        protected abstract string AverageRpsMetricName { get; }

        protected abstract string LatencyInMsMetricName { get; }

        protected abstract string FailureOperationMetricName { get; }

        protected abstract string SuccessOperationMetricName { get; }

        internal double GetLatencyPercentile(int percentile)
        {
            return MathNet.Numerics.Statistics.Statistics.Percentile(this.operationLatenciesInMs, percentile);
        }

        internal double GetLatencyQuantile(double quantile)
        {
            return MathNet.Numerics.Statistics.Statistics.Quantile(this.operationLatenciesInMs, quantile);
        }

        private void TelemetryPercentiles()
        {
            if (this.operationLatenciesInMs.Count > 10)
            {
                double top10PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.1 * this.operationLatenciesInMs.Count)).Average(), 0);
                MetricTelemetry metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top10Percent", top10PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top20PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.2 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top20Percent", top20PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top30PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.3 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top30Percent", top30PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top40PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.4 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top40Percent", top40PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top50PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.5 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top50Percent", top50PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top60PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.6 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top60Percent", top60PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top70PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.7 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top70Percent", top70PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top80PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.8 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top80Percent", top80PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top90PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.9 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top90Percent", top90PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top95PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.95 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top95Percent", top95PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top99PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.99 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top99Percent", top99PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top999PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.999 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top99.9Percent", top999PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top9999PercentAverageRps = Math.Round(this.operationLatenciesInMs.Take((int)(0.9999 * this.operationLatenciesInMs.Count)).Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName + "-Top99.99Percent", top9999PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double averageRps = Math.Round(this.operationLatenciesInMs.Average(), 0);
                metricTelemetry = new MetricTelemetry(this.AverageRpsMetricName, top9999PercentAverageRps);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top50PercentLatencyInMs = this.GetLatencyPercentile(50);
                metricTelemetry = new MetricTelemetry(this.LatencyInMsMetricName + "-P50", top50PercentLatencyInMs);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top75PercentLatencyInMs = this.GetLatencyPercentile(75);
                metricTelemetry = new MetricTelemetry(this.LatencyInMsMetricName + "-P75", top75PercentLatencyInMs);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top90PercentLatencyInMs = this.GetLatencyPercentile(90);
                metricTelemetry = new MetricTelemetry(this.LatencyInMsMetricName + "-P90", top90PercentLatencyInMs);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top95PercentLatencyInMs = this.GetLatencyPercentile(95);
                metricTelemetry = new MetricTelemetry(this.LatencyInMsMetricName + "-P95", top95PercentLatencyInMs);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top98PercentLatencyInMs = this.GetLatencyPercentile(98);
                metricTelemetry = new MetricTelemetry(this.LatencyInMsMetricName + "-P98", top95PercentLatencyInMs);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top99PercentLatencyInMs = this.GetLatencyPercentile(99);
                metricTelemetry = new MetricTelemetry(this.LatencyInMsMetricName + "-P99", top99PercentLatencyInMs);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top999PercentLatencyInMs = this.GetLatencyQuantile(0.999);
                metricTelemetry = new MetricTelemetry(this.LatencyInMsMetricName + "-Q999", top999PercentLatencyInMs);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double top9999PercentLatencyInMs = this.GetLatencyQuantile(0.9999);
                metricTelemetry = new MetricTelemetry(this.LatencyInMsMetricName + "-Q9999", top9999PercentLatencyInMs);
                this.telemetryClient.TrackMetric(metricTelemetry);

                double maxLatencyInMs = this.GetLatencyPercentile(100);
                metricTelemetry = new MetricTelemetry(this.LatencyInMsMetricName + "-P100", maxLatencyInMs);
                this.telemetryClient.TrackMetric(metricTelemetry);
            }
        }
    }
}
