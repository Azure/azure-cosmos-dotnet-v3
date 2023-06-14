//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    public interface IMetricsCollector
    {
        void RecordLatency(double milliseconds);

        void CollectMetricsOnSuccess();

        void CollectMetricsOnFailure();
    }
}