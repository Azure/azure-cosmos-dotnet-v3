//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.CosmosElements.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Documents;

    internal class ReportPayload
    {
        public string RegionsContacted { get; }
        public Boolean GreaterThan1Kb { get; }
        public Microsoft.Azure.Cosmos.ConsistencyLevel Consistency { get; }
        public string DatabaseName { get; }
        public string ContainerName { get; }
        public OperationType Operation { get; }
        public ResourceType Resource { get; }
        public int StatusCode { get; }
        public int ResponseSizeInBytes { get; }
        public MetricInfo MetricInfo { get; }

        public ReportPayload(string regionsContacted, 
            int responseSizeInBytes, 
            Cosmos.ConsistencyLevel consistency, 
            string databaseName, 
            string containerName, 
            OperationType operation, 
            ResourceType resource, 
            int statusCode, string metricInfoName, string unitName)
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
            int hash = 0;
            hash = (hash * 397) ^ (this.RegionsContacted == null ? 0 : this.RegionsContacted.GetHashCode());
            hash = (hash * 397) ^ (this.GreaterThan1Kb.GetHashCode());
            hash = (hash * 397) ^ (this.Consistency.GetHashCode());
            hash = (hash * 397) ^ (this.DatabaseName == null ? 0 : this.DatabaseName.GetHashCode());
            hash = (hash * 397) ^ (this.ContainerName == null ? 0 : this.ContainerName.GetHashCode());
            hash = (hash * 397) ^ (this.Operation.GetHashCode());
            hash = (hash * 397) ^ (this.Resource.GetHashCode());
            hash = (hash * 397) ^ (this.StatusCode.GetHashCode());
            hash = (hash * 397) ^ (this.MetricInfo == null ? 0 : this.MetricInfo.MetricsName == null ? 0 :
                this.MetricInfo.MetricsName.GetHashCode());
            return hash;
        }

        public override bool Equals(object obj)
        {
            bool isequal = obj is ReportPayload payload &&
                   this.RegionsContacted != null && payload.RegionsContacted != null && this.RegionsContacted.Equals(payload.RegionsContacted) &&
                   this.GreaterThan1Kb.Equals(payload.GreaterThan1Kb) &&
                   this.Consistency.GetTypeCode().Equals(payload.Consistency.GetTypeCode()) &&
                   this.DatabaseName != null && payload.DatabaseName != null && this.DatabaseName.Equals(payload.DatabaseName) &&
                   this.ContainerName != null && payload.ContainerName != null && this.ContainerName.Equals(payload.ContainerName) &&
                   this.Operation.GetTypeCode().Equals(payload.Operation.GetTypeCode()) &&
                   this.Resource.GetTypeCode().Equals(payload.Resource.GetTypeCode()) &&
                   this.StatusCode.GetTypeCode().Equals(payload.StatusCode.GetTypeCode()) &&
                   this.MetricInfo != null && this.MetricInfo.MetricsName != null && payload.MetricInfo != null && payload.MetricInfo.MetricsName != null && this.MetricInfo.MetricsName.Equals(payload.MetricInfo.MetricsName);

            return isequal;
        }
    }
}
