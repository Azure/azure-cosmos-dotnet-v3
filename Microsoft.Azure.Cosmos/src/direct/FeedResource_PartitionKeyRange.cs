//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.Json.Serialization;

    public sealed class FeedResource_PartitionKeyRange : PlainResource
    {
        // [JsonInclude]
        [System.Text.Json.Serialization.JsonPropertyName("PartitionKeyRanges")]
        public Collection<PartitionKeyRange> PartitionKeyRanges { get; set; }
    }
}
