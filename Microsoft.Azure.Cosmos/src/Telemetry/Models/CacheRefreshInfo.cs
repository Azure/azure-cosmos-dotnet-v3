//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using HdrHistogram;
    using Newtonsoft.Json;

    [Serializable]
    internal class CacheRefreshInfo : OperationInfoKey
    {
        [JsonProperty(PropertyName = "metricInfo")]
        internal MetricInfo MetricInfo { get; set; }
        
        internal CacheRefreshInfo(OperationInfoKey infoKey, string metricsName,
            string unitName,
            LongConcurrentHistogram histogram,
            double adjustment = 1)
        {
            this.RegionsContacted = infoKey.RegionsContacted;
            this.GreaterThan1Kb = infoKey.GreaterThan1Kb;
            this.Consistency = infoKey.Consistency;
            this.DatabaseName = infoKey.DatabaseName;
            this.ContainerName = infoKey.ContainerName;
            this.Operation = infoKey.Operation;
            this.Resource = infoKey.Resource;
            this.StatusCode = infoKey.StatusCode;
            this.SubStatusCode = infoKey.SubStatusCode;
            this.CacheRefreshSource = infoKey.CacheRefreshSource;
            
            this.MetricInfo = new MetricInfo(metricsName, unitName, histogram, adjustment);
        }

        [JsonConstructor]
        public CacheRefreshInfo(MetricInfo metricInfo)
        {
            this.MetricInfo = metricInfo;
        }

        internal void RecordValue(long value)
        {
            this.MetricInfo.Histogram.RecordValue(value);
        }
    }
}
