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
    internal sealed class ClientTelemetryInfo
    {
        [JsonProperty(PropertyName = "timeStamp")]
        internal string TimeStamp { get; set; }
        [JsonProperty(PropertyName = "clientId")]
        private string ClientId { get; }
        [JsonProperty(PropertyName = "processId")]
        private string ProcessId { get; }
        [JsonProperty(PropertyName = "userAgent")]
        private string UserAgent { get; }
        [JsonProperty(PropertyName = "connectionMode")]
        private string ConnectionMode { get; }
        [JsonProperty(PropertyName = "globalDatabaseAccountName")]
        internal string GlobalDatabaseAccountName { get; set;  }
        [JsonProperty(PropertyName = "applicationRegion")]
        internal string ApplicationRegion { get; set; }
        [JsonProperty(PropertyName = "hostEnvInfo")]
        internal string HostEnvInfo { get; set; }
        [JsonProperty(PropertyName = "acceleratedNetworking")]
        internal bool? AcceleratedNetworking { private get; set; }
        [JsonProperty(PropertyName = "systemInfo")]
        internal List<ReportPayload> SystemInfo { get; set; }

        [JsonProperty(PropertyName = "cacheRefreshInfo")]
        internal List<ReportPayload> CacheRefreshInfo { get; set; }

        [JsonProperty(PropertyName = "operationInfo")]
        internal List<ReportPayload> OperationInfo
        {
            get => new List<ReportPayload>(this.GetWithAggregation(this.OperationInfoMap));
            set => this.OperationInfoMap = this.SetOperationMapFromList(value);
        }
        
        [JsonIgnore]
        internal ConcurrentDictionary<ReportPayload, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> OperationInfoMap { get; set; }

        internal ClientTelemetryInfo(string clientId,
                                   string processId,
                                   string userAgent,
                                   ConnectionMode connectionMode)
        {
            this.ClientId = clientId;
            this.ProcessId = processId;
            this.UserAgent = userAgent;
            this.ConnectionMode = connectionMode.ToString();
            this.SystemInfo = new List<ReportPayload>();
            this.OperationInfoMap = new ConcurrentDictionary<ReportPayload, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>();
        }

        public ClientTelemetryInfo(string timeStamp, 
            string clientId, 
            string processId, 
            string userAgent, 
            string connectionMode, 
            string globalDatabaseAccountName, 
            string applicationRegion, 
            string hostEnvInfo, 
            bool? acceleratedNetworking, 
            List<ReportPayload> systemInfo, 
            List<ReportPayload> cacheRefreshInfo,
            List<ReportPayload> operationInfo)
        {
            this.TimeStamp = timeStamp;
            this.ClientId = clientId;
            this.ProcessId = processId;
            this.UserAgent = userAgent;
            this.ConnectionMode = connectionMode;
            this.GlobalDatabaseAccountName = globalDatabaseAccountName;
            this.ApplicationRegion = applicationRegion;
            this.HostEnvInfo = hostEnvInfo;
            this.AcceleratedNetworking = acceleratedNetworking;
            this.SystemInfo = systemInfo;
            this.CacheRefreshInfo = cacheRefreshInfo;
            this.OperationInfo = operationInfo;
        }

        /// <summary>
        /// This function will be called at the time of serialization to calculate the agrregated values.
        /// </summary>
        /// <param name="metrics"></param>
        /// <returns>Collection of ReportPayload</returns>
        private ICollection<ReportPayload> GetWithAggregation(IDictionary<ReportPayload, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> metrics)
        {
            List<ReportPayload> payloadWithMetricInformation = new List<ReportPayload>();
            foreach (KeyValuePair<ReportPayload, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> entry in metrics)
            {
                ReportPayload payloadForLatency = entry.Key;
                payloadForLatency.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestLatencyName, ClientTelemetryOptions.RequestLatencyUnit)
                    .SetAggregators(entry.Value.latency);
                payloadWithMetricInformation.Add(payloadForLatency);

                ReportPayload payloadForRequestCharge = payloadForLatency.Copy();
                payloadForRequestCharge.MetricInfo = new MetricInfo(ClientTelemetryOptions.RequestChargeName, ClientTelemetryOptions.RequestChargeUnit)
                    .SetAggregators(entry.Value.requestcharge);
                payloadWithMetricInformation.Add(payloadForRequestCharge);
            }

            return payloadWithMetricInformation;
        }

        /// <summary>
        /// Required by Tests while DeSerializing the Json
        /// </summary>
        /// <param name="payloadList"></param>
        /// <returns>Return Map with payload as keys and null tuple as values</returns>
        private ConcurrentDictionary<ReportPayload, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> SetOperationMapFromList(List<ReportPayload> payloadList)
        {
            ConcurrentDictionary<ReportPayload, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)> operationInfoMap = new ConcurrentDictionary<ReportPayload, (LongConcurrentHistogram latency, LongConcurrentHistogram requestcharge)>();
            if (payloadList != null)
            {
                foreach (ReportPayload payload in payloadList)
                {
                    operationInfoMap.TryAdd(payload, (null, null));
                }
            }
            return operationInfoMap;
        }

        internal void Clear()
        {
            this.OperationInfoMap.Clear();
            this.SystemInfo.Clear();
        }
    }
}
