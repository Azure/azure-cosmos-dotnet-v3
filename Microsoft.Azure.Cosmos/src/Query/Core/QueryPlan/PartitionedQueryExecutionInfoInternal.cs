//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Constants = Documents.Constants;
    using PartitionKeyInternal = Documents.Routing.PartitionKeyInternal;

    // Note: We also return this to client when query execution is disallowed by Gateway
    internal sealed class PartitionedQueryExecutionInfoInternal
    {
        [JsonProperty(Constants.Properties.QueryInfo)]
        public QueryInfo QueryInfo
        {
            get;
            set;
        }

        [JsonProperty(Constants.Properties.QueryRanges)]
        public List<Documents.Routing.Range<PartitionKeyInternal>> QueryRanges
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
    }
}
