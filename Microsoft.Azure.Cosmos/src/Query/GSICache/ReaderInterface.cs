//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    internal class ReaderInterface : IReaderInterface
    {
        private readonly Dictionary<string, List<Documents.PartitionKeyRange>> partitionKeyRanges = new Dictionary<string, List<Documents.PartitionKeyRange>>();

        public ReaderInterface()
        {
            this.partitionKeyRanges.Add("CreateRandomToDoActivity", new List<Documents.PartitionKeyRange>()
            {
                new Documents.PartitionKeyRange()
                {
                    MinInclusive = "AA",
                    MaxExclusive = "FF",
                    Id = "0"
                }
            });
        }
        
        public List<Documents.PartitionKeyRange> GetPartitionKeyRanges(string secondaryIndex, string secondaryIndexValue)
        {
            return new List<Documents.PartitionKeyRange>();
            //this.partitionKeyRanges[secondaryIndexValue];
        }
    }
}
