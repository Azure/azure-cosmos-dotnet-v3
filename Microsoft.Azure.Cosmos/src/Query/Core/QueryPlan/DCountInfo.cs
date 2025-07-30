//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    public sealed class DCountInfo
    {
        [System.Text.Json.Serialization.JsonPropertyName("dCountAlias")]
        public string DCountAlias
        {
            get;
            set;
        }

        public bool IsValueAggregate => string.IsNullOrEmpty(this.DCountAlias);
    }
}