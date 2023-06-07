//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using App.Metrics;

namespace CosmosBenchmark
{
    public interface IMetricsCollector
    {
        void CollectMetricsOnSuccess(MetricsContext metricsContext, IMetrics metrics);

        void CollectMetricsOnFailure(MetricsContext metricsContext, IMetrics metrics);
    }
}