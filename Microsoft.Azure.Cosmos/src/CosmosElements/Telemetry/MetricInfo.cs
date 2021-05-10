//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class MetricInfo
    {
        public MetricInfo(string metricsName, string unitName)
        {
            this.MetricsName = metricsName;
            this.UnitName = unitName;
        }
        internal String MetricsName { get; set; }
        internal String UnitName { get; set; }
        internal double Mean { get; set; }
        internal long Count { get; set; }
        internal double Min { get; set; }
        internal double Max { get; set; }
        internal IDictionary<Double, Double> Percentiles { get; set; }

        public override string ToString()
        {
            return base.ToString() + " : " +
                this.MetricsName + " : " +
                this.UnitName + " : " +
                this.Mean + " : " +
                this.Count + " : " +
                this.Min + " : " +
                this.Max + " : " +
                this.Percentiles;
        }
    }
}
