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
        public static Dictionary<string, List<Documents.PartitionKeyRange>> partitionKeyRanges = new Dictionary<string, List<Documents.PartitionKeyRange>>();

        public List<Documents.PartitionKeyRange> GetPartitionKeyRanges(string secondaryIndex, string secondaryIndexValue)
        {
            return partitionKeyRanges[secondaryIndexValue];
        }
    }
}
