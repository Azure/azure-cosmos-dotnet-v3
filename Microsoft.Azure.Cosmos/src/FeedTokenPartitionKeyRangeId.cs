//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// FeedToken implementation to be used when representing a PartitionKeyRangeId.
    /// </summary>
    [Serializable]
    internal sealed class FeedTokenPartitionKeyRangeId : FeedToken
    {
        private readonly string partitionKeyRangeId;
        public FeedTokenPartitionKeyRangeId(string partitionKeyRangeId)
        {
            if (string.IsNullOrEmpty(partitionKeyRangeId)) throw new ArgumentNullException(nameof(partitionKeyRangeId));
            this.partitionKeyRangeId = partitionKeyRangeId;
        }

        public override string ToString() => this.partitionKeyRangeId;
    }
}