// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System.Collections.Generic;

    /// <summary>
    /// For cross partition order by queries we serve documents from the partition
    /// that has the next document in the sort order of the query.
    /// If there is a tie, then we break the tie by picking the leftmost partition.
    /// </summary>
    internal sealed class OrderByEnumeratorComparer : IComparer<OrderByQueryPartitionRangePageAsyncEnumerator>
    {
        private readonly OrderByQueryResultComparer comparer;

        /// <summary>
        /// Initializes a new instance of the OrderByConsumeComparer class.
        /// </summary>
        /// <param name="sortOrders">The sort orders for the query.</param>
        public OrderByEnumeratorComparer(IReadOnlyList<SortOrder> sortOrders)
        {
            this.comparer = new OrderByQueryResultComparer(sortOrders);
        }

        /// <summary>
        /// Compares two document producer trees and returns an integer with the relation of which has the document that comes first in the sort order.
        /// </summary>
        /// <param name="enumerator1">The first document producer tree.</param>
        /// <param name="enumerator2">The second document producer tree.</param>
        /// <returns>
        /// Less than zero if the document in the first document producer comes first.
        /// Zero if the documents are equivalent.
        /// Greater than zero if the document in the second document producer comes first.
        /// </returns>
        public int Compare(
            OrderByQueryPartitionRangePageAsyncEnumerator enumerator1,
            OrderByQueryPartitionRangePageAsyncEnumerator enumerator2)
        {
            if (object.ReferenceEquals(enumerator1, enumerator2))
            {
                return 0;
            }

            if (enumerator1.Current.Failed && !enumerator2.Current.Failed)
            {
                return -1;
            }

            if (!enumerator1.Current.Failed && enumerator2.Current.Failed)
            {
                return 1;
            }

            if (enumerator1.Current.Failed && enumerator2.Current.Failed)
            {
                return string.CompareOrdinal(((FeedRangeEpk)enumerator1.FeedRangeState.FeedRange).Range.Min, ((FeedRangeEpk)enumerator2.FeedRangeState.FeedRange).Range.Min);
            }

            OrderByQueryResult result1 = new OrderByQueryResult(enumerator1.Current.Result.Enumerator.Current);
            OrderByQueryResult result2 = new OrderByQueryResult(enumerator2.Current.Result.Enumerator.Current);

            // First compare the documents based on the sort order of the query.
            int cmp = this.comparer.Compare(result1, result2);
            if (cmp != 0)
            {
                // If there is no tie just return that.
                return cmp;
            }

            // If there is a tie, then break the tie by picking the one from the left most partition.
            return string.CompareOrdinal(((FeedRangeEpk)enumerator1.FeedRangeState.FeedRange).Range.Min, ((FeedRangeEpk)enumerator2.FeedRangeState.FeedRange).Range.Min);
        }
    }
}
