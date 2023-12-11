//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using System.Text.Json.Serialization;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Telemetry;
 
    [Serializable]
    internal class OperationInfo
    {
        internal OperationInfo()
        {
        }

        [JsonPropertyName("regionsContacted")]
        public string RegionsContacted { get; set; }

        [JsonPropertyName("greaterThan1Kb")]
        public bool? GreaterThan1Kb { get; set; }

        [JsonPropertyName("databaseName")]
        public string DatabaseName { get; set; }

        [JsonPropertyName("containerName")]
        public string ContainerName { get; set; }

        [JsonPropertyName("operation")]
        public string Operation { get; set; }

        [JsonPropertyName("resource")]
        public string Resource { get; set; }

        [JsonPropertyName("consistency")]
        public string Consistency { get; set; }

        [JsonPropertyName("statusCode")]
        public int? StatusCode { get; set; }

        [JsonPropertyName("subStatusCode")]
        public int SubStatusCode { get; set; }

        [JsonPropertyName("metricInfo")]
        public MetricInfo MetricInfo { get; set; }

        public OperationInfo(string regionsContacted,
            bool? greaterThan1Kb,
            string databaseName,
            string containerName,
            string operation,
            string resource,
            string consistency,
            int? statusCode,
            int subStatusCode,
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
            this.SubStatusCode = subStatusCode;
            this.MetricInfo = metricInfo;
        }

        internal OperationInfo Copy()
        {
            return new OperationInfo(this.RegionsContacted,
                                    this.GreaterThan1Kb,
                                    this.DatabaseName,
                                    this.ContainerName,
                                    this.Operation,
                                    this.Resource,
                                    this.Consistency,
                                    this.StatusCode,
                                    this.SubStatusCode,
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
            hash = (hash * 7) ^ (this.SubStatusCode.GetHashCode());
            return hash;
        }

        public override bool Equals(object obj)
        {
            bool isequal = obj is OperationInfo payload &&
                   ((this.RegionsContacted == null && payload.RegionsContacted == null) || (this.RegionsContacted != null && payload.RegionsContacted != null && this.RegionsContacted.Equals(payload.RegionsContacted))) &&
                   ((this.GreaterThan1Kb == null && payload.GreaterThan1Kb == null) || (this.GreaterThan1Kb != null && payload.GreaterThan1Kb != null && this.GreaterThan1Kb.Equals(payload.GreaterThan1Kb))) &&
                   ((this.Consistency == null && payload.Consistency == null) || (this.Consistency != null && payload.Consistency != null && this.Consistency.Equals(payload.Consistency))) &&
                   ((this.DatabaseName == null && payload.DatabaseName == null) || (this.DatabaseName != null && payload.DatabaseName != null && this.DatabaseName.Equals(payload.DatabaseName))) &&
                   ((this.ContainerName == null && payload.ContainerName == null) || (this.ContainerName != null && payload.ContainerName != null && this.ContainerName.Equals(payload.ContainerName))) &&
                   ((this.Operation == null && payload.Operation == null) || (this.Operation != null && payload.Operation != null && this.Operation.Equals(payload.Operation))) &&
                   ((this.Resource == null && payload.Resource == null) || (this.Resource != null && payload.Resource != null && this.Resource.Equals(payload.Resource))) &&
                   ((this.StatusCode == null && payload.StatusCode == null) || (this.StatusCode != null && payload.StatusCode != null && this.StatusCode.Equals(payload.StatusCode))) &&
                   this.SubStatusCode.Equals(payload.SubStatusCode);

            return isequal;
        }

        internal void SetAggregators(LongConcurrentHistogram histogram, double adjustment = 1)
        {
            this.MetricInfo.SetAggregators(histogram, adjustment);
        }
    }
}
