//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using Newtonsoft.Json;

    internal sealed class EmbeddingParameter
    {
        [JsonProperty("path")]
        public string[] Path
        {
            get;
            set;
        }

        [JsonProperty("needle")]
        public string Needle
        {
            get;
            set;
        }
    }
}
