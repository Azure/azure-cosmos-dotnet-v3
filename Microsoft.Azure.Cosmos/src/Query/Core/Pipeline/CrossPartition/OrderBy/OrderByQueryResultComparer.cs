// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System.Collections.Generic;
    using System.Diagnostics;

    internal sealed class OrderByQueryResultComparer : IComparer<OrderByQueryResult>
    {
        private readonly IReadOnlyList<SortOrder> sortOrders;

        public OrderByQueryResultComparer(IReadOnlyList<SortOrder> sortOrders)
        {
            if (sortOrders == null)
            {
                throw new System.ArgumentNullException("Sort Orders array can not be null for an order by comparer.");
            }

            if (sortOrders.Count == 0)
            {
                throw new System.ArgumentException("Sort Orders array can not be empty for an order by comparer.");
            }

            this.sortOrders = sortOrders;
        }

        public int Compare(OrderByQueryResult x, OrderByQueryResult y)
        {
            return this.CompareOrderByItems(x.OrderByItems, y.OrderByItems);
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
        private int CompareOrderByItems(IReadOnlyList<OrderByItem> items1, IReadOnlyList<OrderByItem> items2)
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
