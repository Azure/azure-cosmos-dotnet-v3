//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Constants = Documents.Constants;

    internal sealed class PartitionedQueryExecutionInfo
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

        // Change to the below after Direct package upgrade
        // [JsonProperty(Constants.Properties.HybridSearchQueryInfo)]
        [JsonProperty("hybridSearchQueryInfo")]
        public HybridSearchQueryInfo HybridSearchQueryInfo
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
                partitionedQueryExecutionInfo = default;
                return false;
            }
        }
    }
}
