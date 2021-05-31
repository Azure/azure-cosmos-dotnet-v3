//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Util;
    using Newtonsoft.Json;
    
    [Serializable]
    internal class MetricInfo
    {
        public MetricInfo(string metricsName, string unitName)
        {
            this.MetricsName = metricsName;
            this.UnitName = unitName;
        }
        [JsonProperty(PropertyName = "metricsName")]
        internal String MetricsName { get; }
        [JsonProperty(PropertyName = "unitName")]
        internal String UnitName { get; }
        [JsonProperty(PropertyName = "mean")]
        internal double Mean { get; set; }
        [JsonProperty(PropertyName = "count")]
        internal long Count { get; set; }
        [JsonProperty(PropertyName = "min")]
        internal double Min { get; set; }
        [JsonProperty(PropertyName = "max")]
        internal double Max { get; set; }
        [JsonProperty(PropertyName = "percentiles")]
        internal IDictionary<Double, Double> Percentiles { get; set; }
        
        internal MetricInfo SetAggregators(LongConcurrentHistogram histogram)
        {
            this.Count = histogram.TotalCount;
            this.Max = histogram.GetMaxValue();
            this.Min = histogram.GetMinValue();
            this.Mean = histogram.GetMean();
            IDictionary<Double, Double> percentile = new Dictionary<Double, Double>
                {
                    { ClientTelemetryOptions.Percentile50,  histogram.GetValueAtPercentile(ClientTelemetryOptions.Percentile50) },
                    { ClientTelemetryOptions.Percentile90,  histogram.GetValueAtPercentile(ClientTelemetryOptions.Percentile90) },
                    { ClientTelemetryOptions.Percentile95,  histogram.GetValueAtPercentile(ClientTelemetryOptions.Percentile95) },
                    { ClientTelemetryOptions.Percentile99,  histogram.GetValueAtPercentile(ClientTelemetryOptions.Percentile99) },
                    { ClientTelemetryOptions.Percentile999, histogram.GetValueAtPercentile(ClientTelemetryOptions.Percentile999) }
                };
            this.Percentiles = percentile;

            return this;
        }

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
