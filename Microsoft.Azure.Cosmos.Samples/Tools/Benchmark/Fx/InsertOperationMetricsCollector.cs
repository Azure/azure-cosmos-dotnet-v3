//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using App.Metrics.Timer;
    using App.Metrics;
    using System.Diagnostics.Metrics;

    internal class InsertOperationMetricsCollector : MetricsCollector
    {
        private Counter<long> _counter;

        public InsertOperationMetricsCollector(MetricsContext metricsContext, IMetrics metrics, Counter<long> counter) : base(metricsContext, metrics)
        {
            _counter = counter;
        }

        public override TimerContext GetTimer()
        {
            return this.metrics.Measure.Timer.Time(this.metricsContext.InsertLatencyTimer);
        }

        public override void CollectMetricsOnSuccess()
        {
            this.metrics.Measure.Counter.Increment(this.metricsContext.WriteSuccessMeter);

            _counter.Add(1, new("name", "success"), new("color", "green"));
        }

        public override void CollectMetricsOnFailure()
        {
            this.metrics.Measure.Counter.Increment(this.metricsContext.WriteFailureMeter);

            _counter.Add(1, new("name", "failure"), new("color", "red"));
        }
    }
}
