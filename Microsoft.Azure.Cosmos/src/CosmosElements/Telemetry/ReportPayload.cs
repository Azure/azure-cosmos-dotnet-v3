//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using HdrHistogram;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    [Serializable]
    internal class ReportPayload
    {
        [JsonProperty(PropertyName = "regionsContacted")]
        public string RegionsContacted { get; }
        [JsonProperty(PropertyName = "greaterThan1Kb")]
        public bool? GreaterThan1Kb { get; }

        [JsonProperty(PropertyName = "databaseName")]
        public string DatabaseName { get; }
        [JsonProperty(PropertyName = "containerName")]
        public string ContainerName { get; }

        [JsonIgnore]
        public OperationType? Operation { get; }
        [JsonProperty(PropertyName = "operation")]
        public string OperationValue => this.Operation?.ToOperationTypeString();

        [JsonIgnore]
        public ResourceType? Resource { get; }
        [JsonProperty(PropertyName = "resource")]
        public string Recourcevalue => this.Resource?.ToResourceTypeString();

        [JsonIgnore]
        public Microsoft.Azure.Cosmos.ConsistencyLevel? Consistency { get; }
        [JsonProperty(PropertyName = "consistency")]
        public string ConsistencyValue => this.Consistency.ToString();

        [JsonProperty(PropertyName = "statusCode")]
        public int? StatusCode { get; }
        [JsonProperty(PropertyName = "responseSizeInBytes")]
        public int? ResponseSizeInBytes { get; }
        [JsonProperty(PropertyName = "metricInfo")]
        public MetricInfo MetricInfo { get; }

        public ReportPayload(string metricInfoName, string unitName)
        {
            this.MetricInfo = new MetricInfo(metricInfoName, unitName);
        }

        public ReportPayload(string regionsContacted, 
            int? responseSizeInBytes, 
            Cosmos.ConsistencyLevel? consistency, 
            string databaseName, 
            string containerName, 
            OperationType? operation, 
            ResourceType? resource, 
            int? statusCode, string metricInfoName, string unitName)
        {
            this.RegionsContacted = regionsContacted;
            this.ResponseSizeInBytes = responseSizeInBytes;
            if (responseSizeInBytes != 0)
            {
                this.GreaterThan1Kb = responseSizeInBytes > ClientTelemetryOptions.OneKbToBytes;
            }
            this.Consistency = consistency;
            this.DatabaseName = databaseName;
            this.ContainerName = containerName;
            this.Operation = operation;
            this.Resource = resource;
            this.StatusCode = statusCode;
            this.MetricInfo = new MetricInfo(metricInfoName, unitName);
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = (hash * 23) ^ (this.RegionsContacted == null ? 0 : this.RegionsContacted.GetHashCode());
            hash = (hash * 23) ^ (this.GreaterThan1Kb == null ? 0 : this.GreaterThan1Kb.GetHashCode());
            hash = (hash * 23) ^ (this.Consistency == null ? 0 : this.Consistency.GetHashCode());
            hash = (hash * 23) ^ (this.DatabaseName == null ? 0 : this.DatabaseName.GetHashCode());
            hash = (hash * 23) ^ (this.ContainerName == null ? 0 : this.ContainerName.GetHashCode());
            hash = (hash * 23) ^ (this.Operation == null ? 0 : this.Operation.GetHashCode());
            hash = (hash * 23) ^ (this.Resource == null ? 0 : this.Resource.GetHashCode());
            hash = (hash * 23) ^ (this.StatusCode == null ? 0 : this.StatusCode.GetHashCode());
            hash = (hash * 23) ^ (this.MetricInfo == null ? 0 : this.MetricInfo.MetricsName == null ? 0 :
                this.MetricInfo.MetricsName.GetHashCode());
            return hash;
        }

        public override bool Equals(object obj)
        {
            bool isequal = obj is ReportPayload payload &&
                   this.RegionsContacted != null && payload.RegionsContacted != null && this.RegionsContacted.Equals(payload.RegionsContacted) &&
                   this.GreaterThan1Kb != null && payload.GreaterThan1Kb != null && this.GreaterThan1Kb.Equals(payload.GreaterThan1Kb) &&
                   this.Consistency != null && payload.Consistency != null && this.Consistency.Equals(payload.Consistency) &&
                   this.DatabaseName != null && payload.DatabaseName != null && this.DatabaseName.Equals(payload.DatabaseName) &&
                   this.ContainerName != null && payload.ContainerName != null && this.ContainerName.Equals(payload.ContainerName) &&
                   this.Operation != null && payload.Operation != null && this.Operation.Equals(payload.Operation) &&
                   this.Resource != null && payload.Resource != null && this.Resource.Equals(payload.Resource) &&
                   this.StatusCode != null && payload.StatusCode != null && this.StatusCode.Equals(payload.StatusCode) &&
                   this.MetricInfo != null && this.MetricInfo.MetricsName != null && payload.MetricInfo != null && payload.MetricInfo.MetricsName != null && this.MetricInfo.MetricsName.Equals(payload.MetricInfo.MetricsName);

            return isequal;
        }

        internal ReportPayload SetAggregators(LongConcurrentHistogram histogram)
        {
            this.MetricInfo.SetAggregators(histogram);
            return this;
        }
    }
}
