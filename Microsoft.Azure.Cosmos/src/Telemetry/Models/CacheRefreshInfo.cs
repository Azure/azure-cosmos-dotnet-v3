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

        internal CacheRefreshInfo()
        {
        }

        [JsonConstructor]
        internal CacheRefreshInfo(string regionsContacted,
            long? responseSizeInBytes,
            string consistency,
            string databaseName,
            string containerName,
            OperationType? operation,
            ResourceType? resource,
            int? statusCode,
            int subStatusCode,
            string cacheRefreshSource)
            : base(
                  regionsContacted: regionsContacted,
                  responseSizeInBytes: responseSizeInBytes,
                  consistency: consistency,
                  databaseName: databaseName,
                  containerName: containerName,
                  operation: operation,
                  resource: resource,
                  statusCode: statusCode,
                  subStatusCode: subStatusCode)
        {
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
            bool isequal = base.Equals(obj) && obj is CacheRefreshInfo payload &&
                   ((this.CacheRefreshSource == null && payload.CacheRefreshSource == null) || 
                        (this.CacheRefreshSource != null && payload.CacheRefreshSource != null && 
                            this.CacheRefreshSource.Equals(payload.CacheRefreshSource)));

            return isequal;
        }
    }
}
