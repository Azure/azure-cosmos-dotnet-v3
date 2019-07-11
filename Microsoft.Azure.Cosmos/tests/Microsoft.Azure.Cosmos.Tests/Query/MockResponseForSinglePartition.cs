//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This represent a single partition and the list of messages
    /// the partition should receive
    /// </summary>
    internal class MockResponseForSinglePartition
    {
        public PartitionKeyRange PartitionKeyRange { get; set; }

        public string StartingContinuationToken { get; set; }

        public MockResponseMessages[] Messages { get; set; }
    }

    /// <summary>
    /// This represent a single query response message. 
    /// 1. Response message contains a list of items
    /// 2. Response message is a split failure
    /// </summary>
    internal class MockResponseMessages
    {
        public MockResponseMessages(params int[] itemsIndexPosition)
        {
            this.ItemsIndexPosition = itemsIndexPosition;
            this.DelayResponse = null;
        }

        public MockResponseMessages(IReadOnlyList<PartitionKeyRange> updatedRangesAfterSplit)
        {
            this.UpdatedRangesAfterSplit = updatedRangesAfterSplit;
            this.DelayResponse = null;
        }

        public TimeSpan? DelayResponse { get; }

        public int[] ItemsIndexPosition { get; }

        public IReadOnlyList<PartitionKeyRange> UpdatedRangesAfterSplit { get; }
    }
}
