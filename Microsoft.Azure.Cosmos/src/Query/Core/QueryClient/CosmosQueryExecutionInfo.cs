// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.QueryClient
{
    using Newtonsoft.Json;

    internal sealed class CosmosQueryExecutionInfo
    {
        [JsonConstructor]
        public CosmosQueryExecutionInfo(bool reverseRidEnabled, bool reverseIndexScan)
        {
            this.ReverseRidEnabled = reverseRidEnabled;
            this.ReverseIndexScan = reverseIndexScan;
        }

        [JsonProperty("reverseRidEnabled")]
        public bool ReverseRidEnabled { get; }

        [JsonProperty("reverseIndexScan")]
        public bool ReverseIndexScan { get; }
    }
}
