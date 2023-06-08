//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using App.Metrics;
    using App.Metrics.Counter;
    using App.Metrics.Timer;

    // TODO: Temporary solution. Remove after adding DI.
    public class BenchmarkConfigProvider
    {
        public static BenchmarkConfig CurrentBenchmarkConfig { get; set; }
    }
}