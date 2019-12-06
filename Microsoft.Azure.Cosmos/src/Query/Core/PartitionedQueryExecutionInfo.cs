//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Constants = Documents.Constants;

    // Note: We also return this to client when query execution is disallowed by Gateway
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    sealed class PartitionedQueryExecutionInfo
    {
        public PartitionedQueryExecutionInfo()
        {
            this.Version = Constants.PartitionedQueryExecutionInfo.CurrentVersion;
        }

        [JsonProperty(Constants.Properties.PartitionedQueryExecutionInfoVersion)]
        public int Version
        {
            get;
            private set;
        }

        [JsonProperty(Constants.Properties.QueryInfo)]
        public QueryInfo QueryInfo
        {
            get;
            set;
        }

        [JsonProperty(Constants.Properties.QueryRanges)]
        public List<Documents.Routing.Range<string>> QueryRanges
        {
            get;
            set;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        public static bool TryParse(string serializedQueryPlan, out PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            if (serializedQueryPlan == null)
            {
                throw new ArgumentNullException(nameof(serializedQueryPlan));
            }

            try
            {
                partitionedQueryExecutionInfo = JsonConvert.DeserializeObject<PartitionedQueryExecutionInfo>(serializedQueryPlan);
                return true;
            }
            catch (JsonException)
            {
                partitionedQueryExecutionInfo = default(PartitionedQueryExecutionInfo);
                return false;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
