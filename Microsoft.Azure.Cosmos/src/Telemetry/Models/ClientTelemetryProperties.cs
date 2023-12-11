//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [Serializable]
    internal sealed class ClientTelemetryProperties
    {
        [JsonPropertyName("timeStamp")]
        public string DateTimeUtc { get; set; }

        [JsonPropertyName("clientId")]
        public string ClientId { get; }

        [JsonPropertyName("machineId")]
        public string MachineId { get; set; }

        [JsonPropertyName("processId")]
        public string ProcessId { get; }

        [JsonPropertyName("userAgent")]
        public string UserAgent { get; }

        [JsonPropertyName("connectionMode")]
        public string ConnectionMode { get; }

        [JsonPropertyName("globalDatabaseAccountName")]
        public string GlobalDatabaseAccountName { get; set; }

        [JsonPropertyName("applicationRegion")]
        public string ApplicationRegion { get; set; }

        [JsonPropertyName("hostEnvInfo")]
        public string HostEnvInfo { get; set; }

        [JsonPropertyName("acceleratedNetworking")]
        public bool? AcceleratedNetworking { get; set; }

        /// <summary>
        /// Preferred Region set by the client
        /// </summary>
        [JsonPropertyName("preferredRegions")]
        public IReadOnlyList<string> PreferredRegions { get; set; }

        [JsonPropertyName("aggregationIntervalInSec")]
        public int AggregationIntervalInSec { get; set; }

        [JsonPropertyName("systemInfo")]
        public List<SystemInfo> SystemInfo { get; set; }

        [JsonPropertyName("cacheRefreshInfo")]
        public List<CacheRefreshInfo> CacheRefreshInfo { get; set; }

        [JsonPropertyName("operationInfo")]
        public List<OperationInfo> OperationInfo { get; set; }

        [JsonPropertyName("requestInfo")]
        public List<RequestInfo> RequestInfo { get; set; }

        [JsonIgnore]
        public bool IsDirectConnectionMode { get; }

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

        internal void Write(Utf8JsonWriter writer)
        {
            writer.WriteString("timeStamp", this.DateTimeUtc);
            writer.WriteString("clientId", this.ClientId);
            writer.WriteString("machineId", this.MachineId);
            writer.WriteString("processId", this.ProcessId);
            writer.WriteString("userAgent", this.UserAgent);
            writer.WriteString("connectionMode", this.ConnectionMode);
            writer.WriteString("globalDatabaseAccountName", this.GlobalDatabaseAccountName);
            if (this.ApplicationRegion != null)
            {
                writer.WriteString("applicationRegion", this.ApplicationRegion);
            }
            else
            {
                writer.WriteNull("applicationRegion");
            }

            writer.WriteString("hostEnvInfo", this.HostEnvInfo);
            if (this.AcceleratedNetworking.HasValue)
            {
                writer.WriteBoolean("acceleratedNetworking", this.AcceleratedNetworking.Value);
            }
            else
            {
                writer.WriteNull("acceleratedNetworking");
            }

            writer.WritePropertyName("preferredRegions");

            if (this.PreferredRegions != null)
            {
                writer.WriteStartArray();
                foreach (string region in this.PreferredRegions)
                {
                    writer.WriteStringValue(region);
                }
                writer.WriteEndArray();
            }
            else
            {
                writer.WriteNull("preferredRegions");
            }

            writer.WriteNumber("aggregationIntervalInSec", this.AggregationIntervalInSec);

            if (this.SystemInfo != null && this.SystemInfo.Count > 0)
            {
                writer.WritePropertyName("systemInfo");
                string sysInfo = JsonSerializer.Serialize(this.SystemInfo);
                writer.WriteRawValue(sysInfo);
            }
        }
    }
}
