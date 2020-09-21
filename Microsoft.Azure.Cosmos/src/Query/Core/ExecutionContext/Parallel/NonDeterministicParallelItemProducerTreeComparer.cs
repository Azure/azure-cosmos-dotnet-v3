//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;

    /// <summary>
    /// Implementation of <see cref="IComparer{ItemProducerTree}"/> that returns documents from the partition that has the most documents buffered first.
    /// </summary>
    internal sealed class NonDeterministicParallelItemProducerTreeComparer : IComparer<ItemProducerTree>
    {
        public static readonly NonDeterministicParallelItemProducerTreeComparer Singleton = new NonDeterministicParallelItemProducerTreeComparer();

        private NonDeterministicParallelItemProducerTreeComparer()
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
            if (documentProducerTree1 == null)
            {
                throw new ArgumentNullException(nameof(documentProducerTree1));
            }

            if (documentProducerTree2 == null)
            {
                throw new ArgumentNullException(nameof(documentProducerTree2));
            }

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

            return documentProducerTree2.BufferedItemCount.CompareTo(documentProducerTree1.BufferedItemCount);
        }
    }
}