//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;

    internal class MetricCollectionWindow
    {
        public DateTime Started { get; private set; }
        public DateTime ValidTill { get; private set; }

        public MetricCollectionWindow(BenchmarkConfig config)
        {
            this.Reset(config);
        }

        public bool IsValid => DateTime.Now > this.ValidTill;

        internal void Reset(BenchmarkConfig config)
        {
            this.Started = DateTime.Now;
            this.ValidTill = this.Started.AddSeconds(config.MetricsReportingIntervalInSec);
        }
    }
}
