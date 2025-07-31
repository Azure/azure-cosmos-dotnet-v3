//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text.Json.Serialization;

    public sealed class FeedResource_PartitionKeyRange : PlainResource
    {
        // [JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName("PartitionKeyRanges")]
        public Collection<PartitionKeyRange> PartitionKeyRanges { get; set; }

        /// <summary>
        /// Returns a string representation of the feed resource with all property names and values.
        /// </summary>
        /// <returns>A string containing all property names and their values.</returns>
        public new string asString()
        {
            string partitionKeyRangesStr = this.PartitionKeyRanges != null 
                ? $"[{string.Join(", ", this.PartitionKeyRanges.Select(pkr => $"({pkr.asString()})"))}]" 
                : "null";
            return $"{base.asString()}, PartitionKeyRanges={partitionKeyRangesStr}";
        }
    }
}
