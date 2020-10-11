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

        /// <summary>
        /// Whether or not the backend has the reverseRid feature enabled.
        /// </summary>
        [JsonProperty("reverseRidEnabled")]
        public bool ReverseRidEnabled { get; }

        /// <summary>
        /// Indicates the direction of the index scan.
        /// </summary>
        [JsonProperty("reverseIndexScan")]
        public bool ReverseIndexScan { get; }
    }
}
