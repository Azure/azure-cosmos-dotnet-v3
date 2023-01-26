//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Telemetry.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Newtonsoft.Json;

    [Serializable]
    internal sealed class RequestInfo
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("databaseName")]
        public string DatabaseName { get; set; }

        [JsonProperty("containerName")]
        public string ContainerName { get; set; }

        [JsonProperty("operation")]
        public string Operation { get; set; }

        [JsonProperty("resource")]
        public string Resource { get; set; }

        [JsonProperty("statusCode")]
        public int? StatusCode { get; set; }

        [JsonProperty("subStatusCode")]
        public int SubStatusCode { get; set; }

        [JsonProperty("cacheRefreshSource")]
        public string CacheRefreshSource { get; set; }

        [JsonProperty("metricInfo")]
        public List<MetricInfo> Metrics { get; set; } = new List<MetricInfo>();

        public override int GetHashCode()
        {
            int hash = 3;
            hash = (hash * 7) ^ (this.Uri == null ? 0 : this.Uri.GetHashCode());
            hash = (hash * 7) ^ (this.DatabaseName == null ? 0 : this.DatabaseName.GetHashCode());
            hash = (hash * 7) ^ (this.ContainerName == null ? 0 : this.ContainerName.GetHashCode());
            hash = (hash * 7) ^ (this.Operation == null ? 0 : this.Operation.GetHashCode());
            hash = (hash * 7) ^ (this.Resource == null ? 0 : this.Resource.GetHashCode());
            hash = (hash * 7) ^ (this.StatusCode == null ? 0 : this.StatusCode.GetHashCode());
            hash = (hash * 7) ^ (this.SubStatusCode.GetHashCode());
            hash = (hash * 7) ^ (this.CacheRefreshSource == null ? 0 : this.CacheRefreshSource.GetHashCode());
            return hash;
        }
        
        public override bool Equals(object obj)
        {
            bool isequal = obj is RequestInfo payload &&
                   ((this.Uri == null && payload.Uri == null) || (this.Uri != null && payload.Uri != null && this.Uri.Equals(payload.Uri))) &&
                   ((this.DatabaseName == null && payload.DatabaseName == null) || (this.DatabaseName != null && payload.DatabaseName != null && this.DatabaseName.Equals(payload.DatabaseName))) &&
                   ((this.ContainerName == null && payload.ContainerName == null) || (this.ContainerName != null && payload.ContainerName != null && this.ContainerName.Equals(payload.ContainerName))) &&
                   ((this.Operation == null && payload.Operation == null) || (this.Operation != null && payload.Operation != null && this.Operation.Equals(payload.Operation))) &&
                   ((this.Resource == null && payload.Resource == null) || (this.Resource != null && payload.Resource != null && this.Resource.Equals(payload.Resource))) &&
                   ((this.StatusCode == null && payload.StatusCode == null) || (this.StatusCode != null && payload.StatusCode != null && this.StatusCode.Equals(payload.StatusCode))) &&
                   this.SubStatusCode.Equals(payload.SubStatusCode) &&
                   String.CompareOrdinal(this.CacheRefreshSource, payload.CacheRefreshSource) == 0;

            return isequal;
        }

    }
}
