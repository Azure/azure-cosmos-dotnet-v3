//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.CosmosElements.Telemetry;
    using Newtonsoft.Json;
    
    [Serializable]
    internal class ClientTelemetryInfo
    {
        [JsonProperty(PropertyName = "timeStamp")]
        public string TimeStamp { get; set; }
        [JsonProperty(PropertyName = "clientId")]
        public string ClientId { get; }
        [JsonProperty(PropertyName = "processId")]
        public string ProcessId { get; }
        [JsonProperty(PropertyName = "userAgent")]
        public string UserAgent { get; }
        [JsonProperty(PropertyName = "connectionMode")]
        public string ConnectionMode { get; }
        [JsonProperty(PropertyName = "globalDatabaseAccountName")]
        public string GlobalDatabaseAccountName { get; set;  }
        [JsonProperty(PropertyName = "applicationRegion")]
        public string ApplicationRegion { get; set; }
        [JsonProperty(PropertyName = "hostEnvInfo")]
        public string HostEnvInfo { get; set; }
        [JsonProperty(PropertyName = "acceleratedNetworking")]
        public bool? AcceleratedNetworking { get; set; }
        [JsonProperty(PropertyName = "systemInfo")]
        public List<ReportPayload> SystemInfo { get; set; }
        [JsonProperty(PropertyName = "cacheRefreshInfo")]
        public List<ReportPayload> CacheRefreshInfo => new List<ReportPayload>(this.FillMetricInformation(this.CacheRefreshInfoMap));
        [JsonProperty(PropertyName = "operationInfo")]
        public List<ReportPayload> OperationInfo => new List<ReportPayload>(this.FillMetricInformation(this.OperationInfoMap));

        [JsonIgnore]
        public ConcurrentDictionary<ReportPayload, LongConcurrentHistogram> CacheRefreshInfoMap { get; set; }
        [JsonIgnore]
        public ConcurrentDictionary<ReportPayload, LongConcurrentHistogram> OperationInfoMap { get; set; }

        public ClientTelemetryInfo(string clientId,
                                   string processId,
                                   string userAgent,
                                   ConnectionMode connectionMode)
        {
            this.ClientId = clientId;
            this.ProcessId = processId;
            this.UserAgent = userAgent;
            this.ConnectionMode = connectionMode.ToString();
            this.SystemInfo = new List<ReportPayload>();
            this.CacheRefreshInfoMap = new ConcurrentDictionary<ReportPayload, LongConcurrentHistogram>();
            this.OperationInfoMap = new ConcurrentDictionary<ReportPayload, LongConcurrentHistogram>();
        }

        /// <summary>
        /// This function will be called at the time of serialization to calculate the agrregated values.
        /// </summary>
        /// <param name="metrics"></param>
        /// <returns>Collection of ReportPayload</returns>
        private ICollection<ReportPayload> FillMetricInformation(IDictionary<ReportPayload, LongConcurrentHistogram> metrics)
        {
            if (metrics == null)
            {
                return null;
            }

            foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in metrics)
            {
                ReportPayload payload = entry.Key;
                LongConcurrentHistogram histogram = entry.Value;

                payload.SetAggregators((LongConcurrentHistogram)histogram.Copy());
            }

            return metrics.Keys;
        }
    }
}
