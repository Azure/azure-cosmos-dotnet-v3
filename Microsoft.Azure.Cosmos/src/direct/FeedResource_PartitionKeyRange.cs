//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text.Json.Serialization;

    internal sealed class FeedResource_PartitionKeyRange : PlainResource
    {
        [JsonInclude]
        internal Collection<PartitionKeyRange> PartitionKeyRanges { get; set; }
    }
}
