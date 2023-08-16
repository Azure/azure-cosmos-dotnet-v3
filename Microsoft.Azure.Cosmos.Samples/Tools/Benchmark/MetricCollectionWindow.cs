//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace CosmosBenchmark
{
    using System;

    /// <summary>
    /// Represents the metric collection window (time span while accumulating and granulating the data)
    /// </summary>
    internal class MetricCollectionWindow
    {
        /// <summary>
        /// The timestamp when window span is started.
        /// </summary>
        public DateTime Started { get; private set; }
        
        /// <summary>
        /// The timestamp until which the current window span is not elapsed.
        /// </summary>
        public DateTime ValidTill { get; private set; }

        /// <summary>
        /// Creates the instance of <see cref="MetricCollectionWindow"/>.
        /// </summary>
        /// <param name="config">Cosmos Benchmark configuration.</param>
        public MetricCollectionWindow(BenchmarkConfig config)
        {
            this.Reset(config);
        }

        /// <summary>
        /// Indicates whether the current window is valid.
        /// </summary>
        public bool IsValid => DateTime.UtcNow > this.ValidTill;

        /// <summary>
        /// Resets the started timestamp and valid till timespan.
        /// </summary>
        /// <param name="config"></param>
        public void Reset(BenchmarkConfig config)
        {
            this.Started = DateTime.UtcNow;
            this.ValidTill = this.Started.AddSeconds(config.MetricsReportingIntervalInSec);
        }
    }
}
