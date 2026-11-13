//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using Newtonsoft.Json;

    internal sealed class FullTextSearchTerm
    {
        [JsonProperty("term")]
        public string Term
        {
            get;
            set;
        }

        [JsonProperty("distance", NullValueHandling = NullValueHandling.Ignore)]
        public int? Distance
        {
            get;
            set;
        }
    }
}
