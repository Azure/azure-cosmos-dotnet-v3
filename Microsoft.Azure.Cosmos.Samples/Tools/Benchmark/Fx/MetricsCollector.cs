//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;

    internal abstract class MetricsCollector : IMetricsCollector
    {
        private readonly Histogram<double> operationLatencyHistogram;

        private readonly List<double> latencies;

        private readonly Histogram<double> rpsMetricNameHistogram;

        private readonly Counter<long> successOperationCounter;

        private readonly Counter<long> failureOperationCounter;

        public MetricsCollector(Meter meter)
        {
            this.latencies = new List<double>();
            this.rpsMetricNameHistogram = meter.CreateHistogram<double>(this.RpsMetricName);
            this.operationLatencyHistogram = meter.CreateHistogram<double>(this.LatencyInMsMetricName);
            this.successOperationCounter = meter.CreateCounter<long>(this.SuccessOperationMetricName);
            this.failureOperationCounter = meter.CreateCounter<long>(this.FailureOperationMetricName);
        }

        public void CollectMetricsOnSuccess()
        {
            this.successOperationCounter.Add(1);
        }

        public void CollectMetricsOnFailure()
        {
            this.failureOperationCounter.Add(1);
        }

        public void RecordLatencyAndRps(double milliseconds)
        {
            double rps = 1000 / milliseconds;
            this.rpsMetricNameHistogram.Record(rps);
            this.operationLatencyHistogram.Record(milliseconds);
        }

        protected abstract string RpsMetricName { get; }

        protected abstract string LatencyInMsMetricName { get; }

        protected abstract string FailureOperationMetricName { get; }

        protected abstract string SuccessOperationMetricName { get; }
    }
}
