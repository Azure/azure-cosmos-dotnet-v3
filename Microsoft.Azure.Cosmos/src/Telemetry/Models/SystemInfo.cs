//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using HdrHistogram;
    using Newtonsoft.Json;

    [Serializable]
    internal sealed class SystemInfo
    {
        [JsonProperty(PropertyName = "resource")]
        internal string Resource => "HostMachine";

        [JsonProperty(PropertyName = "metricInfo")]
        internal MetricInfo MetricInfo { get; set; }

        [JsonConstructor]
        public SystemInfo(MetricInfo metricInfo)
        {
            this.MetricInfo = metricInfo;
        }

        internal SystemInfo(string metricsName,
           string unitName,
           int count)
        {
            this.MetricInfo = new MetricInfo(metricsName, unitName, count);
        }
        
        internal SystemInfo(string metricsName, 
            string unitName, 
            LongConcurrentHistogram histogram,
            double adjustment = 1)
        {
            this.MetricInfo = new MetricInfo(metricsName, unitName, histogram, adjustment);
        }

        internal void RecordValue(long value)
        {
            this.MetricInfo.Histogram.RecordValue(value);
        }

    }
}
