//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System.Collections.Generic;
    using PartitionKeyRange = Documents.PartitionKeyRange;

    /// <summary>
    /// For parallel queries we drain from left partition to right,
    /// then by rid order within those partitions.
    /// </summary>
    internal sealed class ParallelItemProducerTreeComparer : IComparer<ItemProducerTree>
    {
        /// <summary>
        /// Compares two document producer trees in a parallel context and returns their comparison.
        /// </summary>
        /// <param name="documentProducerTree1">The first document producer tree.</param>
        /// <param name="documentProducerTree2">The second document producer tree.</param>
        /// <returns>
        /// A negative number if the first comes before the second.
        /// Zero if the two document producer trees are interchangeable.
        /// A positive number if the second comes before the first.
        /// </returns>
        public int Compare(
            ItemProducerTree documentProducerTree1,
            ItemProducerTree documentProducerTree2)
        {
            if (object.ReferenceEquals(documentProducerTree1, documentProducerTree2))
            {
                return 0;
            }

            if (documentProducerTree1.HasMoreResults && !documentProducerTree2.HasMoreResults)
            {
                return -1;
            }

            if (!documentProducerTree1.HasMoreResults && documentProducerTree2.HasMoreResults)
            {
                return 1;
            }

            // Either both don't have results or both do.
            PartitionKeyRange partitionKeyRange1 = documentProducerTree1.PartitionKeyRange;
            PartitionKeyRange partitionKeyRange2 = documentProducerTree2.PartitionKeyRange;
            return string.CompareOrdinal(
                partitionKeyRange1.MinInclusive,
                partitionKeyRange2.MinInclusive);
        }
    }
}