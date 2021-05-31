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
        public ConnectionMode ConnectionMode { get; }
        [JsonProperty(PropertyName = "globalDatabaseAccountName")]
        public string GlobalDatabaseAccountName { get; set;  }
        [JsonProperty(PropertyName = "applicationRegion")]
        public string ApplicationRegion { get; set; }
        [JsonProperty(PropertyName = "hostEnvInfo")]
        public string HostEnvInfo { get; set; }
        [JsonProperty(PropertyName = "acceleratedNetworking")]
        public bool? AcceleratedNetworking { get; }
        [JsonProperty(PropertyName = "systemInfo")]
        public List<MetricInfo> SystemInfo { get; set; }
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
                                   ConnectionMode connectionMode,
                                   bool? acceleratedNetworking)
        {
            this.ClientId = clientId;
            this.ProcessId = processId;
            this.UserAgent = userAgent;
            this.ConnectionMode = connectionMode;
            this.AcceleratedNetworking = acceleratedNetworking;
            this.SystemInfo = new List<MetricInfo>();
            this.CacheRefreshInfoMap = new ConcurrentDictionary<ReportPayload, LongConcurrentHistogram>();
            this.OperationInfoMap = new ConcurrentDictionary<ReportPayload, LongConcurrentHistogram>();
        }

        private ICollection<ReportPayload> FillMetricInformation(IDictionary<ReportPayload, LongConcurrentHistogram> metrics)
        {
            foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in metrics)
            {
                ReportPayload payload = entry.Key;
                LongConcurrentHistogram histogram = entry.Value;

                payload.MetricInfo.SetAggregators((LongConcurrentHistogram)histogram.Copy());
            }

            return metrics.Keys;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (System.Reflection.PropertyInfo property in this.GetType().GetProperties())
            {
                sb.Append(property.Name);
                sb.Append(": ");
                if (property.GetIndexParameters().Length > 0)
                {
                    sb.Append("Indexed Property cannot be used");
                }
                else
                {
                    sb.Append(property.GetValue(this, null));
                }

                sb.Append(System.Environment.NewLine);
            }

            return sb.ToString();
        }
    }
}
