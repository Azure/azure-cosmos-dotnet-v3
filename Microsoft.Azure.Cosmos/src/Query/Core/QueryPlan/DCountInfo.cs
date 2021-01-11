//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using Newtonsoft.Json;

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class DCountInfo
    {
        [JsonProperty("dCountAlias")]
        public string DCountAlias
        {
            get;
            set;
        }

        public bool IsValueAggregate => string.IsNullOrEmpty(this.DCountAlias);
    }
}