//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using System.Collections.Generic;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Util;
    using Newtonsoft.Json;

    [Serializable]
    internal sealed class MetricInfo
    {
        internal MetricInfo(string metricsName,
            string unitName,
            LongConcurrentHistogram histogram = null,
            double adjustment = 1)
        {
            this.MetricsName = metricsName;
            this.UnitName = unitName;
            this.Adjustment = adjustment;
            this.Histogram = histogram;
        }

        internal MetricInfo(string metricsName,
           string unitName, int count)
        {
            this.MetricsName = metricsName;
            this.UnitName = unitName;
            this.count = count;
        }

        [JsonConstructor]
        public MetricInfo(string metricsName,
            string unitName,
            long count,
            double mean,
            double min,
            double max,
            IReadOnlyDictionary<double, double> percentiles)
        {
            this.MetricsName = metricsName;
            this.UnitName = unitName;
            this.Count = count;
            this.Mean = mean;
            this.Min = min;
            this.Max = max;
            this.Percentiles = percentiles;
        }

        [JsonProperty(PropertyName = "metricsName")]
        internal string MetricsName { get; }

        [JsonProperty(PropertyName = "unitName")]
        internal string UnitName { get; }

        private double mean;
        [JsonProperty(PropertyName = "mean")]
        internal double Mean
        {
            get
            {
                if (this.mean > 0)
                {
                    return this.mean;
                }
                return (this.Histogram?.GetMean() ?? 0) / this.Adjustment;
            }

            set => this.mean = value;
        }

        private long count;
        [JsonProperty(PropertyName = "count")]
        internal long Count
        {
            get
            {
                if (this.count > 0)
                {
                    return this.count;
                }
                return this.Histogram?.TotalCount ?? 0;
            }

            set => this.count = value;
        }

        private double min;
        [JsonProperty(PropertyName = "min")]
        internal double Min
        {
            get
            {
                if (this.min > 0)
                {
                    return this.min;
                }
                return (this.Histogram?.GetMinValue() ?? 0) / this.Adjustment;
            }

            set => this.min = value;
        }

        private double max;
        [JsonProperty(PropertyName = "max")]
        internal double Max
        {
            get
            {
                if (this.max > 0)
                {
                    return this.max;
                }
                return (this.Histogram?.GetMaxValue() ?? 0) / this.Adjustment;
            }

            set => this.max = value;
        }

        private IReadOnlyDictionary<double, double> percentiles;
        [JsonProperty(PropertyName = "percentiles")]
        internal IReadOnlyDictionary<double, double> Percentiles
        {
            get
            {
                if (this.percentiles != null)
                {
                    return this.percentiles;
                }
                return new Dictionary<double, double>
                {
                    { ClientTelemetryOptions.Percentile50,  (this.Histogram?.GetValueAtPercentile(ClientTelemetryOptions.Percentile50) ?? 0) / this.Adjustment },
                    { ClientTelemetryOptions.Percentile90,  (this.Histogram?.GetValueAtPercentile(ClientTelemetryOptions.Percentile90) ?? 0) / this.Adjustment },
                    { ClientTelemetryOptions.Percentile95,  (this.Histogram?.GetValueAtPercentile(ClientTelemetryOptions.Percentile95) ?? 0) / this.Adjustment },
                    { ClientTelemetryOptions.Percentile99,  (this.Histogram?.GetValueAtPercentile(ClientTelemetryOptions.Percentile99) ?? 0) / this.Adjustment },
                    { ClientTelemetryOptions.Percentile999, (this.Histogram?.GetValueAtPercentile(ClientTelemetryOptions.Percentile999) ?? 0) / this.Adjustment }
                };
            }

            set => this.percentiles = value;
        }
        
        [JsonIgnore]
        internal LongConcurrentHistogram Histogram { get; }

        [JsonIgnore]
        internal double Adjustment { get; } = 1;
    }
}
