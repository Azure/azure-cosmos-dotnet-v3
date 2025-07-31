//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryPlan
{
    using System.Text.Json.Serialization;

    internal sealed class DCountInfo
    {
        [JsonPropertyName("dCountAlias")]
        public string DCountAlias
        {
            get;
            set;
        }

        public bool IsValueAggregate => string.IsNullOrEmpty(this.DCountAlias);
    }
}