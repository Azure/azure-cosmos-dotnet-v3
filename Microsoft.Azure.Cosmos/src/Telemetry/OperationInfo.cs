//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry
{
    using System;
    using HdrHistogram;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    [Serializable]
    internal sealed class OperationInfo
    {
        [JsonProperty(PropertyName = "regionsContacted")]
        private string RegionsContacted { get; }

        [JsonProperty(PropertyName = "greaterThan1Kb")]
        private bool? GreaterThan1Kb { get; }

        [JsonProperty(PropertyName = "databaseName")]
        private string DatabaseName { get; }

        [JsonProperty(PropertyName = "containerName")]
        private string ContainerName { get; }

        [JsonProperty(PropertyName = "operation")]
        internal string Operation { get; }

        [JsonProperty(PropertyName = "resource")]
        internal string Resource { get; }

        [JsonProperty(PropertyName = "consistency")]
        internal string Consistency { get; }

        [JsonProperty(PropertyName = "statusCode")]
        public int? StatusCode { get; }

        [JsonProperty(PropertyName = "responseSizeInBytes")]
        public long? ResponseSizeInBytes { get; }

        [JsonProperty(PropertyName = "metricInfo")]
        internal MetricInfo MetricInfo { get; set; }

        internal OperationInfo(string metricsName, string unitName)
        {
            this.MetricInfo = new MetricInfo(metricsName, unitName);
        }

        internal OperationInfo(string regionsContacted, 
            long? responseSizeInBytes,            
            Cosmos.ConsistencyLevel? consistency, 
            string databaseName, 
            string containerName, 
            OperationType? operation, 
            ResourceType? resource, 
            int? statusCode)
        {
            this.RegionsContacted = regionsContacted;
            this.ResponseSizeInBytes = responseSizeInBytes;
            if (responseSizeInBytes != null)
            {
                this.GreaterThan1Kb = responseSizeInBytes > ClientTelemetryOptions.OneKbToBytes;
            }
            this.Consistency = consistency?.ToString();
            this.DatabaseName = databaseName;
            this.ContainerName = containerName;
            this.Operation = operation?.ToOperationTypeString();
            this.Resource = resource?.ToResourceTypeString();
            this.StatusCode = statusCode;
        }

        public OperationInfo(string regionsContacted, 
            bool? greaterThan1Kb, 
            string databaseName, 
            string containerName, 
            string operation, 
            string resource, 
            string consistency, 
            int? statusCode, 
            long? responseSizeInBytes, 
            MetricInfo metricInfo)
        {
            this.RegionsContacted = regionsContacted;
            this.GreaterThan1Kb = greaterThan1Kb;
            this.DatabaseName = databaseName;
            this.ContainerName = containerName;
            this.Operation = operation;
            this.Resource = resource;
            this.Consistency = consistency;
            this.StatusCode = statusCode;
            this.ResponseSizeInBytes = responseSizeInBytes;
            this.MetricInfo = metricInfo;
        }

        public OperationInfo Copy()
        {
            return new OperationInfo(this.RegionsContacted,
            this.GreaterThan1Kb,
            this.DatabaseName,
            this.ContainerName,
            this.Operation,
            this.Resource,
            this.Consistency,
            this.StatusCode,
            this.ResponseSizeInBytes, 
            null);
        }

        public override int GetHashCode()
        {
            int hash = 3;
            hash = (hash * 7) ^ (this.RegionsContacted == null ? 0 : this.RegionsContacted.GetHashCode());
            hash = (hash * 7) ^ (this.GreaterThan1Kb == null ? 0 : this.GreaterThan1Kb.GetHashCode());
            hash = (hash * 7) ^ (this.Consistency == null ? 0 : this.Consistency.GetHashCode());
            hash = (hash * 7) ^ (this.DatabaseName == null ? 0 : this.DatabaseName.GetHashCode());
            hash = (hash * 7) ^ (this.ContainerName == null ? 0 : this.ContainerName.GetHashCode());
            hash = (hash * 7) ^ (this.Operation == null ? 0 : this.Operation.GetHashCode());
            hash = (hash * 7) ^ (this.Resource == null ? 0 : this.Resource.GetHashCode());
            hash = (hash * 7) ^ (this.StatusCode == null ? 0 : this.StatusCode.GetHashCode());
            return hash;
        }

        public override bool Equals(object obj)
        {
            bool isequal = obj is OperationInfo payload &&
                   this.RegionsContacted != null && payload.RegionsContacted != null && this.RegionsContacted.Equals(payload.RegionsContacted) &&
                   this.GreaterThan1Kb != null && payload.GreaterThan1Kb != null && this.GreaterThan1Kb.Equals(payload.GreaterThan1Kb) &&
                   this.Consistency != null && payload.Consistency != null && this.Consistency.Equals(payload.Consistency) &&
                   this.DatabaseName != null && payload.DatabaseName != null && this.DatabaseName.Equals(payload.DatabaseName) &&
                   this.ContainerName != null && payload.ContainerName != null && this.ContainerName.Equals(payload.ContainerName) &&
                   this.Operation != null && payload.Operation != null && this.Operation.Equals(payload.Operation) &&
                   this.Resource != null && payload.Resource != null && this.Resource.Equals(payload.Resource) &&
                   this.StatusCode != null && payload.StatusCode != null && this.StatusCode.Equals(payload.StatusCode);

            return isequal;
        }

        internal void SetAggregators(LongConcurrentHistogram histogram, double adjustment = 1)
        {
            this.MetricInfo.SetAggregators(histogram, adjustment);
        }
    }
}
