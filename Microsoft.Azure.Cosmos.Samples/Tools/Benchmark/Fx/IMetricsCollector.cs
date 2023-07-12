//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    public interface IMetricsCollector
    {
        void RecordLatencyAndRps(double milliseconds);

        void CollectMetricsOnSuccess();

        void CollectMetricsOnFailure();
    }
}