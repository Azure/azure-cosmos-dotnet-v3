//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Constants = Documents.Constants;
    using PartitionKeyInternal = Documents.Routing.PartitionKeyInternal;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    // Note: We also return this to client when query execution is disallowed by Gateway
    sealed class PartitionedQueryExecutionInfoInternal
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
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
