//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.ParallelQuery
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using RMResources = Documents.RMResources;

    /// <summary>
    /// For cross partition order by queries we serve documents from the partition
    /// that has the next document in the sort order of the query.
    /// If there is a tie, then we break the tie by picking the leftmost partition.
    /// </summary>
    internal sealed class OrderByConsumeComparer : IComparer<ItemProducerTree>
    {
        /// <summary>
        /// This flag used to determine whether we should support mixed type order by.
        /// For testing purposes we might turn it on to test mixed type order by on index v2.
        /// </summary>
        public static bool AllowMixedTypeOrderByTestFlag = false;

        /// <summary>
        /// The sort orders for the query (1 for each order by in the query).
        /// Until composite indexing is released this will just be an array of length 1.
        /// </summary>
        private readonly IReadOnlyList<SortOrder> sortOrders;

        /// <summary>
        /// Initializes a new instance of the OrderByConsumeComparer class.
        /// </summary>
        /// <param name="sortOrders">The sort orders for the query.</param>
        public OrderByConsumeComparer(SortOrder[] sortOrders)
        {
            if (sortOrders == null)
            {
                throw new ArgumentNullException("Sort Orders array can not be null for an order by comparer.");
            }

            if (sortOrders.Length == 0)
            {
                throw new ArgumentException("Sort Orders array can not be empty for an order by comparerer.");
            }

            this.sortOrders = new List<SortOrder>(sortOrders);
        }

        /// <summary>
        /// Compares two document producer trees and returns an integer with the relation of which has the document that comes first in the sort order.
        /// </summary>
        /// <param name="producer1">The first document producer tree.</param>
        /// <param name="producer2">The second document producer tree.</param>
        /// <returns>
        /// Less than zero if the document in the first document producer comes first.
        /// Zero if the documents are equivalent.
        /// Greater than zero if the document in the second document producer comes first.
        /// </returns>
        public int Compare(ItemProducerTree producer1, ItemProducerTree producer2)
        {
            if (object.ReferenceEquals(producer1, producer2))
            {
                return 0;
            }

            if (producer1.HasMoreResults && !producer2.HasMoreResults)
            {
                return -1;
            }

            if (!producer1.HasMoreResults && producer2.HasMoreResults)
            {
                return 1;
            }

            if (!producer1.HasMoreResults && !producer2.HasMoreResults)
            {
                return string.CompareOrdinal(producer1.PartitionKeyRange.MinInclusive, producer2.PartitionKeyRange.MinInclusive);
            }

            OrderByQueryResult result1 = new OrderByQueryResult(producer1.Current);
            OrderByQueryResult result2 = new OrderByQueryResult(producer2.Current);

            // First compare the documents based on the sort order of the query.
            int cmp = this.CompareOrderByItems(result1.OrderByItems, result2.OrderByItems);
            if (cmp != 0)
            {
                // If there is no tie just return that.
                return cmp;
            }

            // If there is a tie, then break the tie by picking the one from the left most partition.
            return string.CompareOrdinal(producer1.PartitionKeyRange.MinInclusive, producer2.PartitionKeyRange.MinInclusive);

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
        public int CompareOrderByItems(IList<OrderByItem> items1, IList<OrderByItem> items2)
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

            if (!AllowMixedTypeOrderByTestFlag)
            {
                this.CheckTypeMatching(items1, items2);
            }

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

        /// <summary>
        /// With index V1 collections we have the check the types of the items since it is impossible to support mixed typed order by for V1 collections.
        /// The reason for this is, since V1 does not order types.
        /// The only constraint is that all the numbers will be sorted with respect to each other and same for the strings, but strings and numbers might get interleaved.
        /// Take the following example:
        /// Partition 1: "A", 1, "B", 2
        /// Partition 2: 42, "Z", 0x5F3759DF
        /// Step 1: Compare "A" to 42 and WLOG 42 comes first
        /// Step 2: Compare "A" to "Z" and "A" comes first
        /// Step 3: Compare "Z" to 1 and WLOG 1 comes first
        /// Whoops: We have 42, "A", 1 and 1 should come before 42.
        /// </summary>
        /// <param name="items1">The items relevant to the sort for the first partition.</param>
        /// <param name="items2">The items relevant to the sort for the second partition.</param>
        private void CheckTypeMatching(IList<OrderByItem> items1, IList<OrderByItem> items2)
        {
            for (int i = 0; i < items1.Count; ++i)
            {
                CosmosElementType itemType1 = items1[i].Item.Type;
                CosmosElementType itemType2 = items2[i].Item.Type;

                if (itemType1 != itemType2)
                {
                    throw new NotSupportedException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            RMResources.UnsupportedCrossPartitionOrderByQueryOnMixedTypes,
                            itemType1,
                            itemType2,
                            items1[i]));
                }
            }
        }
    }
}
