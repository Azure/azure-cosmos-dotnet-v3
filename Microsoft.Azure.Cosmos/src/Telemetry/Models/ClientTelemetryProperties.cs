//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [Serializable]
    internal sealed class ClientTelemetryProperties
    {
        [JsonProperty(PropertyName = "timeStamp")]
        internal string DateTimeUtc { get; set; }

        [JsonProperty(PropertyName = "clientId")]
        internal string ClientId { get; }

        [JsonProperty(PropertyName = "machineId")]
        internal string MachineId { get; set; }

        [JsonProperty(PropertyName = "processId")]
        internal string ProcessId { get; }

        [JsonProperty(PropertyName = "userAgent")]
        internal string UserAgent { get; }

        [JsonProperty(PropertyName = "connectionMode")]
        internal string ConnectionMode { get; }

        [JsonProperty(PropertyName = "globalDatabaseAccountName")]
        internal string GlobalDatabaseAccountName { get; set; }

        [JsonProperty(PropertyName = "applicationRegion")]
        internal string ApplicationRegion { get; set; }

        [JsonProperty(PropertyName = "hostEnvInfo")]
        internal string HostEnvInfo { get; set; }

        [JsonProperty(PropertyName = "acceleratedNetworking")]
        internal bool? AcceleratedNetworking { get; set; }

        /// <summary>
        /// Preferred Region set by the client
        /// </summary>
        [JsonProperty(PropertyName = "preferredRegions")]
        internal IReadOnlyList<string> PreferredRegions { get; set; }

        [JsonProperty(PropertyName = "aggregationIntervalInSec")]
        internal int AggregationIntervalInSec { get; set; }

        [JsonProperty(PropertyName = "systemInfo")]
        internal List<SystemInfo> SystemInfo { get; set; }

        [JsonProperty(PropertyName = "cacheRefreshInfo")]
        internal List<CacheRefreshInfo> CacheRefreshInfo { get; set; }

        [JsonProperty(PropertyName = "operationInfo")]
        internal List<OperationInfo> OperationInfo { get; set; }

        [JsonProperty(PropertyName = "requestInfo")]
        internal List<RequestInfo> RequestInfo { get; set; }

        [JsonIgnore]
        internal bool IsDirectConnectionMode { get; }

        internal ClientTelemetryProperties(string clientId,
                                   string processId,
                                   string userAgent,
                                   ConnectionMode connectionMode,
                                   IReadOnlyList<string> preferredRegions,
                                   int aggregationIntervalInSec)
        {
            this.ClientId = clientId;
            this.ProcessId = processId;
            this.UserAgent = userAgent;
            this.ConnectionMode = connectionMode.ToString().ToUpperInvariant();
            this.IsDirectConnectionMode = connectionMode == Cosmos.ConnectionMode.Direct;
            this.SystemInfo = new List<SystemInfo>();
            this.PreferredRegions = preferredRegions;
            this.AggregationIntervalInSec = aggregationIntervalInSec;
        }

        /// <summary>
        /// Needed by Serializer to deserialize the json
        /// </summary>
        [JsonConstructor]
        public ClientTelemetryProperties(string dateTimeUtc,
            string clientId,
            string processId,
            string userAgent,
            string connectionMode,
            string globalDatabaseAccountName,
            string applicationRegion,
            string hostEnvInfo,
            bool? acceleratedNetworking,
            IReadOnlyList<string> preferredRegions,
            List<SystemInfo> systemInfo,
            List<CacheRefreshInfo> cacheRefreshInfo,
            List<OperationInfo> operationInfo,
            List<RequestInfo> requestInfo,
            string machineId)
        {
            this.DateTimeUtc = dateTimeUtc;
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
            this.RequestInfo = requestInfo;
            this.PreferredRegions = preferredRegions;
            this.MachineId = machineId;
        }

        public void Write(JsonWriter writer)
        {
            writer.WritePropertyName("timeStamp");
            writer.WriteValue(this.DateTimeUtc);

            writer.WritePropertyName("clientId");
            writer.WriteValue(this.ClientId);

            writer.WritePropertyName("machineId");
            writer.WriteValue(this.MachineId);

            writer.WritePropertyName("processId");
            writer.WriteValue(this.ProcessId);

            writer.WritePropertyName("userAgent");
            writer.WriteValue(this.UserAgent);

            writer.WritePropertyName("connectionMode");
            writer.WriteValue(this.ConnectionMode);

            writer.WritePropertyName("globalDatabaseAccountName");
            writer.WriteValue(this.GlobalDatabaseAccountName);

            writer.WritePropertyName("applicationRegion");
            if (this.ApplicationRegion != null)
            {
                writer.WriteValue(this.ApplicationRegion);
            }
            else
            {
                writer.WriteNull();
            }

            writer.WritePropertyName("hostEnvInfo");
            writer.WriteValue(this.HostEnvInfo);

            writer.WritePropertyName("acceleratedNetworking");
            if (this.AcceleratedNetworking.HasValue)
            {
                writer.WriteValue(this.AcceleratedNetworking.Value);
            }
            else
            {
                writer.WriteNull();
            }

            writer.WritePropertyName("preferredRegions");

            if (this.PreferredRegions != null)
            {
                writer.WriteStartArray();
                foreach (string region in this.PreferredRegions)
                {
                    writer.WriteValue(region);
                }
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNull();
            }

            writer.WritePropertyName("aggregationIntervalInSec");
            writer.WriteValue(this.AggregationIntervalInSec);

            if (this.SystemInfo != null && this.SystemInfo.Count > 0)
            {
                writer.WritePropertyName("systemInfo");
                writer.WriteRawValue(JsonConvert.SerializeObject(this.SystemInfo));
            }
        }
    }
}
