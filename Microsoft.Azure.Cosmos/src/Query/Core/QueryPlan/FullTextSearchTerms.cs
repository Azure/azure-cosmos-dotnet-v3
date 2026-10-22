//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System.Collections.Generic;
    using Newtonsoft.Json;

    internal sealed class FullTextSearchTerms
    {
        [JsonProperty("path")]
        public List<string> Path
        {
            get;
            set;
        }

        [JsonProperty("terms")]
        public List<FullTextSearchTerm> Terms
        {
            get;
            set;
        }
    }
}
