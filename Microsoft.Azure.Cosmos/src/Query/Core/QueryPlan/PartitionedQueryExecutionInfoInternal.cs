//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Constants = Documents.Constants;
    using PartitionKeyInternal = Documents.Routing.PartitionKeyInternal;

    // Note: We also return this to client when query execution is disallowed by Gateway
    internal sealed class PartitionedQueryExecutionInfoInternal
    {
        [JsonPropertyName(Constants.Properties.QueryInfo)]
        public QueryInfo QueryInfo
        {
            get;
            set;
        }

        [JsonPropertyName(Constants.Properties.QueryRanges)]
        public List<Documents.Routing.Range<PartitionKeyInternal>> QueryRanges
        {
            get;
            set;
        }

        [JsonPropertyName(Constants.Properties.HybridSearchQueryInfo)]
        public HybridSearchQueryInfo HybridSearchQueryInfo
        {
            get;
            set;
        }
    }
}
