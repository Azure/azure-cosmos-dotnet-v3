//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using PartitionKeyRange = Documents.PartitionKeyRange;

    /// <summary>
    /// Implementation of <see cref="IComparer{ItemProducerTree}"/> that returns documents from the left partition first.
    /// The documents within a partition are already sorted in _rid order.
    /// </summary>
    /// <remarks>This comparer gaurentees that the query returns results in a deterministic order meaning that running the same query twice returns results in the same order. This also ensures that continuation token returned to the user is as small as possible, since we finish draining a partition before moving onto another partition. The only draw back is that all partitions can not be drained until all their partitions to the left are fully drained, which reduces the parallelism.
    /// </remarks>
    internal sealed class DeterministicParallelItemProducerTreeComparer : IComparer<ItemProducerTree>
    {
        public static readonly DeterministicParallelItemProducerTreeComparer Singleton = new DeterministicParallelItemProducerTreeComparer();

        private DeterministicParallelItemProducerTreeComparer()
        {
            // Singleton class, so leave the constructor private.
        }

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