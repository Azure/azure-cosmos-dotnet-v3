//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    [Serializable]
    internal sealed class CacheRefreshInfo : OperationInfo
    {
        internal CacheRefreshInfo()
        {
        }

        [JsonPropertyName("cacheRefreshSource")]
        public string CacheRefreshSource { get; }

        public CacheRefreshInfo(string regionsContacted,
            bool? greaterThan1Kb,
            string consistency,
            string databaseName,
            string containerName,
            string operation,
            string resource,
            int? statusCode,
            int subStatusCode,
            string cacheRefreshSource,
            MetricInfo metricInfo = null)
        {
            this.RegionsContacted = regionsContacted;
            this.GreaterThan1Kb = greaterThan1Kb;
            this.Consistency = consistency;
            this.DatabaseName = databaseName;
            this.ContainerName = containerName;
            this.Operation = operation;
            this.Resource = resource;
            this.StatusCode = statusCode;
            this.SubStatusCode = subStatusCode;
            this.MetricInfo = metricInfo;
            this.CacheRefreshSource = cacheRefreshSource;
        }

        public override int GetHashCode()
        {
            int hash = base.GetHashCode();
            hash = (hash * 7) ^ (this.CacheRefreshSource == null ? 0 : this.CacheRefreshSource.GetHashCode());
            return hash;
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj) && 
                obj is CacheRefreshInfo payload &&
                String.CompareOrdinal(this.CacheRefreshSource, payload.CacheRefreshSource) == 0;
        }
    }
}
