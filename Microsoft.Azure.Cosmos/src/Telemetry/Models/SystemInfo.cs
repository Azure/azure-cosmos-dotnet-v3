//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using System.Text.Json.Serialization;
    using HdrHistogram;

    [Serializable]
    internal sealed class SystemInfo
    {
        public SystemInfo()
        {
        }

        [JsonPropertyName("resource")]
        public string Resource => "HostMachine";

        [JsonPropertyName("metricInfo")]
        public MetricInfo MetricInfo { get; set; }

        internal SystemInfo(string metricsName, string unitName)
        {
            this.MetricInfo = new MetricInfo(metricsName, unitName);
        }

        internal SystemInfo(string metricsName, string unitName, int count)
        {
            this.MetricInfo = new MetricInfo(metricsName, unitName, count: count);
        }

        public SystemInfo(MetricInfo metricInfo)
        {
            this.MetricInfo = metricInfo;
        }

        internal void SetAggregators(LongConcurrentHistogram histogram, double adjustment = 1)
        {
            this.MetricInfo.SetAggregators(histogram, adjustment);
        }

    }
}
