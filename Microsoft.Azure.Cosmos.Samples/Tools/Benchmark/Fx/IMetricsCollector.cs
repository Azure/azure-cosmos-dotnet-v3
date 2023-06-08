//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using App.Metrics.Timer;

    public interface IMetricsCollector
    {
        TimerContext GetTimer();

        void CollectMetricsOnSuccess();

        void CollectMetricsOnFailure();
    }
}