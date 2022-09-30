//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    [Serializable]
    internal sealed class CacheRefreshInfo : OperationInfo
    {
        [JsonProperty(PropertyName = "cacheRefreshSource")]
        internal string CacheRefreshSource { get; }

        internal CacheRefreshInfo(string metricsName, string unitName)
            : base(metricsName, unitName)
        {
        }

        internal CacheRefreshInfo(string regionsContacted,
            long? responseSizeInBytes,
            string consistency,
            string databaseName,
            string containerName,
            OperationType? operation,
            ResourceType? resource,
            int? statusCode,
            string subStatusCode,
            string cacheRefreshSource)
        {
            this.RegionsContacted = regionsContacted;
            if (responseSizeInBytes != null)
            {
                this.GreaterThan1Kb = responseSizeInBytes > ClientTelemetryOptions.OneKbToBytes;
            }
            this.Consistency = consistency;
            this.DatabaseName = databaseName;
            this.ContainerName = containerName;
            this.Operation = operation?.ToOperationTypeString();
            this.Resource = resource?.ToResourceTypeString();
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.CacheRefreshSource = cacheRefreshSource;
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
            hash = (hash * 7) ^ (this.SubStatusCode == null ? 0 : this.SubStatusCode.GetHashCode());
            hash = (hash * 7) ^ (this.CacheRefreshSource == null ? 0 : this.CacheRefreshSource.GetHashCode());
            return hash;
        }

        public override bool Equals(object obj)
        {
            bool isequal = obj is CacheRefreshInfo payload &&
                   (this.RegionsContacted == null && payload.RegionsContacted == null || this.RegionsContacted != null && payload.RegionsContacted != null && this.RegionsContacted.Equals(payload.RegionsContacted)) &&
                   (this.GreaterThan1Kb == null && payload.GreaterThan1Kb == null || this.GreaterThan1Kb != null && payload.GreaterThan1Kb != null && this.GreaterThan1Kb.Equals(payload.GreaterThan1Kb)) &&
                   (this.Consistency == null && payload.Consistency == null || this.Consistency != null && payload.Consistency != null && this.Consistency.Equals(payload.Consistency)) &&
                   (this.DatabaseName == null && payload.DatabaseName == null || this.DatabaseName != null && payload.DatabaseName != null && this.DatabaseName.Equals(payload.DatabaseName)) &&
                   (this.ContainerName == null && payload.ContainerName == null || this.ContainerName != null && payload.ContainerName != null && this.ContainerName.Equals(payload.ContainerName)) &&
                   (this.Operation == null && payload.Operation == null || this.Operation != null && payload.Operation != null && this.Operation.Equals(payload.Operation)) &&
                   (this.Resource == null && payload.Resource == null || this.Resource != null && payload.Resource != null && this.Resource.Equals(payload.Resource)) &&
                   (this.StatusCode == null && payload.StatusCode == null || this.StatusCode != null && payload.StatusCode != null && this.StatusCode.Equals(payload.StatusCode)) &&
                   (this.CacheRefreshSource == null && payload.CacheRefreshSource == null || this.CacheRefreshSource != null && payload.CacheRefreshSource != null && this.CacheRefreshSource.Equals(payload.CacheRefreshSource)) &&
                   (this.SubStatusCode == null && payload.SubStatusCode == null || this.SubStatusCode != null && payload.SubStatusCode != null && this.SubStatusCode.Equals(payload.SubStatusCode));

            return isequal;
        }
    }
}
