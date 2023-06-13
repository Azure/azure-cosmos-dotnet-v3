//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using App.Metrics.Timer;
    using App.Metrics;

    internal class InsertOperationMetricsCollector : MetricsCollector
    {
        public InsertOperationMetricsCollector(MetricsContext metricsContext, IMetrics metrics) : base(metricsContext, metrics)
        {
        }

        public override TimerContext GetTimer()
        {
            return this.metrics.Measure.Timer.Time(this.metricsContext.InsertLatencyTimer);
        }

        public override void CollectMetricsOnSuccess()
        {
            this.metrics.Measure.Counter.Increment(this.metricsContext.WriteSuccessMeter);
        }

        public override void CollectMetricsOnFailure()
        {
            this.metrics.Measure.Counter.Increment(this.metricsContext.WriteFailureMeter);
        }
    }
}
