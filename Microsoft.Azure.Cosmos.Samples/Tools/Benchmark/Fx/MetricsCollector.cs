//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using App.Metrics.Timer;
    using App.Metrics;

    internal abstract class MetricsCollector : IMetricsCollector
    {
        protected MetricsContext metricsContext;

        protected IMetrics metrics;

        public MetricsCollector(MetricsContext metricsContext, IMetrics metrics)
        {
            this.metricsContext = metricsContext;
            this.metrics = metrics;
        }

        public abstract TimerContext GetTimer();

        public abstract void CollectMetricsOnFailure();

        public abstract void CollectMetricsOnSuccess();
    }
}
