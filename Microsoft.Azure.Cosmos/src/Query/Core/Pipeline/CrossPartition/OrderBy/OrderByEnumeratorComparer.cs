// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;

    /// <summary>
    /// For cross partition order by queries we serve documents from the partition
    /// that has the next document in the sort order of the query.
    /// If there is a tie, then we break the tie by picking the leftmost partition.
    /// </summary>
    internal sealed class OrderByEnumeratorComparer : IComparer<OrderByQueryPartitionRangePageAsyncEnumerator>
    {
        /// <summary>
        /// The sort orders for the query (1 for each order by in the query).
        /// Until composite indexing is released this will just be an array of length 1.
        /// </summary>
        private readonly IReadOnlyList<SortOrder> sortOrders;

        /// <summary>
        /// Initializes a new instance of the OrderByConsumeComparer class.
        /// </summary>
        /// <param name="sortOrders">The sort orders for the query.</param>
        public OrderByEnumeratorComparer(IReadOnlyList<SortOrder> sortOrders)
        {
            if (sortOrders == null)
            {
                throw new ArgumentNullException("Sort Orders array can not be null for an order by comparer.");
            }

            if (sortOrders.Count == 0)
            {
                throw new ArgumentException("Sort Orders array can not be empty for an order by comparerer.");
            }

            this.sortOrders = new List<SortOrder>(sortOrders);
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
                return string.CompareOrdinal(enumerator1.Range.MinInclusive, enumerator2.Range.MinInclusive);
            }

            OrderByQueryResult result1 = new OrderByQueryResult(enumerator1.Current.Result.Enumerator.Current);
            OrderByQueryResult result2 = new OrderByQueryResult(enumerator2.Current.Result.Enumerator.Current);

            // First compare the documents based on the sort order of the query.
            int cmp = this.CompareOrderByItems(result1.OrderByItems, result2.OrderByItems);
            if (cmp != 0)
            {
                // If there is no tie just return that.
                return cmp;
            }

            // If there is a tie, then break the tie by picking the one from the left most partition.
            return string.CompareOrdinal(enumerator1.Range.MinInclusive, enumerator2.Range.MinInclusive);

        }

        /// <summary>
        /// Takes the items relevant to the sort and return an integer defining the relationship.
        /// </summary>
        /// <param name="items1">The items relevant to the sort from the first partition.</param>
        /// <param name="items2">The items relevant to the sort from the second partition.</param>
        /// <returns>The sort relationship.</returns>
        /// <example>
        /// Suppose the query was "SELECT * FROM c ORDER BY c.name asc, c.age desc",
        /// then items1 could be ["Brandon", 22] and items2 could be ["Felix", 28]
        /// Then we would first compare "Brandon" to "Felix" and say that "Brandon" comes first in an ascending lex order (we don't even have to look at age).
        /// If items1 was ["Brandon", 22] and items2 was ["Brandon", 23] then we would say have to look at the age to break the tie and in this case 23 comes first in a descending order.
        /// Some examples of composite order by: http://www.dofactory.com/sql/order-by
        /// </example>
        public int CompareOrderByItems(IReadOnlyList<OrderByItem> items1, IReadOnlyList<OrderByItem> items2)
        {
            if (object.ReferenceEquals(items1, items2))
            {
                return 0;
            }

            Debug.Assert(
                items1 != null && items2 != null,
                "Order-by items must be present.");

            Debug.Assert(
                items1.Count == items2.Count,
                "OrderByResult instances should have the same number of order-by items.");

            Debug.Assert(
                items1.Count > 0,
                "OrderByResult instances should have at least 1 order-by item.");

            Debug.Assert(
                this.sortOrders.Count == items1.Count,
                "SortOrders must match size of order-by items.");

            for (int i = 0; i < this.sortOrders.Count; ++i)
            {
                int cmp = ItemComparer.Instance.Compare(
                    items1[i].Item,
                    items2[i].Item);

                if (cmp != 0)
                {
                    return this.sortOrders[i] != SortOrder.Descending ? cmp : -cmp;
                }
            }

            return 0;
        }
    }
}
