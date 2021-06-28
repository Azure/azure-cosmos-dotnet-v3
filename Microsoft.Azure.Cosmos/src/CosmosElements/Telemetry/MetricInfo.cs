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
    internal sealed class MetricInfo
    {
        internal MetricInfo(string metricsName, string unitName)
        {
            this.MetricsName = metricsName;
            this.UnitName = unitName;
        }

        public MetricInfo(string metricsName, 
            string unitName, 
            double mean, 
            long count, 
            double min, 
            double max, 
            IReadOnlyDictionary<double, double> percentiles)
            : this(metricsName, unitName)
        {
            this.Mean = mean;
            this.Count = count;
            this.Min = min;
            this.Max = max;
            this.Percentiles = percentiles;
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
        internal IReadOnlyDictionary<Double, Double> Percentiles { get; set; }

        /// <summary>
        /// It will set the current object with the aggregated values from the given histogram
        /// </summary>
        /// <param name="histogram"></param>
        /// <param name="adjustment"></param>
        /// <returns>MetricInfo</returns>
        internal MetricInfo SetAggregators(LongConcurrentHistogram histogram, int adjustment = 1)
        {
            if (histogram != null)
            {
                this.Count = histogram.TotalCount;
                this.Max = histogram.GetMaxValue() / adjustment;
                this.Min = histogram.GetMinValue() / adjustment;
                this.Mean = histogram.GetMean() / adjustment;
                IReadOnlyDictionary<Double, Double> percentile = new Dictionary<Double, Double>
                {
                    { ClientTelemetryOptions.Percentile50,  histogram.GetValueAtPercentile(ClientTelemetryOptions.Percentile50) },
                    { ClientTelemetryOptions.Percentile90,  histogram.GetValueAtPercentile(ClientTelemetryOptions.Percentile90) },
                    { ClientTelemetryOptions.Percentile95,  histogram.GetValueAtPercentile(ClientTelemetryOptions.Percentile95) },
                    { ClientTelemetryOptions.Percentile99,  histogram.GetValueAtPercentile(ClientTelemetryOptions.Percentile99) },
                    { ClientTelemetryOptions.Percentile999, histogram.GetValueAtPercentile(ClientTelemetryOptions.Percentile999) }
                };
                this.Percentiles = percentile;
            }
            return this;
        }
    }
}
