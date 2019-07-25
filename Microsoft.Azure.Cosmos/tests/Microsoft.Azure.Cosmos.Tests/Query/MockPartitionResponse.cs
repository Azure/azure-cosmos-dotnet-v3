//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// This represent a single partition and the list of messages
    /// the partition should receive
    /// </summary>
    internal class MockPartitionResponse
    {
        public PartitionKeyRange PartitionKeyRange { get; set; }

        /// <summary>
        /// Each int[] represents a single response message from the backend
        /// Each int in the array represents an item in the response message 
        /// The int value represents the index of all sorted items across all the partitions
        /// </summary>
        /// <remarks>
        /// Empty int[] represent an empty page
        /// </remarks>
        public List<int[]> MessagesWithItemIndex { get; set; } = new List<int[]>();

        public MockPartitionResponse[] Split { get; set; }

        public bool HasSplit => this.Split != null;

        public IReadOnlyList<PartitionKeyRange> GetPartitionKeyRangeOfSplit()
        {
            if (this.Split == null || this.Split.Length == 0)
            {
                throw new ArgumentException("No split was configured");
            }

            return this.Split.Select(mockResponses => mockResponses.PartitionKeyRange).ToList().AsReadOnly();
        }

        public int GetTotalItemCount()
        {
            int totalItemCount = 0;
            if(this.MessagesWithItemIndex != null)
            {
                foreach (var message in this.MessagesWithItemIndex)
                {
                    totalItemCount += message.Length;
                }
            }

            if(this.Split != null)
            {
                foreach(var partitionResponse in this.Split)
                {
                    totalItemCount += partitionResponse.GetTotalItemCount();
                }
            }

            return totalItemCount;
        }
    }
}
