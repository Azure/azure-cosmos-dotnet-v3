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
        private DateTime ValidTill { get; set; }

        private int MetricsReportingIntervalInSec { get; set; }

        /// <summary>
        /// Creates the instance of <see cref="MetricCollectionWindow"/>.
        /// </summary>
        /// <param name="config">Cosmos Benchmark configuration.</param>
        public MetricCollectionWindow(int metricsReportingIntervalInSec)
        {
            this.MetricsReportingIntervalInSec = metricsReportingIntervalInSec;
            this.Reset();
        }

        /// <summary>
        /// Indicates whether the current window is valid.
        /// </summary>
        public bool IsValid => DateTime.UtcNow > this.ValidTill;

        /// <summary>
        /// Resets the started timestamp and valid till timespan.
        /// </summary>
        /// <param name="config"></param>
        public void Reset()
        {
            this.ValidTill = DateTime.UtcNow.AddSeconds(this.MetricsReportingIntervalInSec);
        }
    }
}
