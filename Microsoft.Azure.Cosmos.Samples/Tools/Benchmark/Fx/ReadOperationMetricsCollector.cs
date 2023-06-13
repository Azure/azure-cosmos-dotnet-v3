//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using App.Metrics;
    using App.Metrics.Timer;

    internal class ReadOperationMetricsCollector : MetricsCollector
    {
        public ReadOperationMetricsCollector(MetricsContext metricsContext, IMetrics metrics) : base(metricsContext, metrics)
        {
        }

        public override TimerContext GetTimer()
        {
            return this.metrics.Measure.Timer.Time(this.metricsContext.ReadLatencyTimer);
        }

        public override void CollectMetricsOnSuccess()
        {
            this.metrics.Measure.Counter.Increment(this.metricsContext.ReadSuccessMeter);
        }

        public override void CollectMetricsOnFailure()
        {
            this.metrics.Measure.Counter.Increment(this.metricsContext.ReadFailureMeter);
        }
    }
}
