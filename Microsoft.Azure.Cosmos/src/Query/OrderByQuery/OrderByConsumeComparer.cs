//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.ParallelQuery
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

    internal sealed class OrderByConsumeComparer : IComparer<DocumentProducer<OrderByQueryResult>>
    {
        /// <summary>
        /// This flag used to determine whether we should support mixed type order by.
        /// For testing purposes we might turn it on to test mixed type order by on index v2.
        /// </summary>
        [ThreadStatic]
        public static bool AllowMixedTypeOrderByTestFlag;

        private readonly IReadOnlyList<SortOrder> sortOrders;

        public OrderByConsumeComparer(SortOrder[] sortOrders)
        {
            if (sortOrders == null)
            {
                throw new ArgumentNullException($"{nameof(sortOrders)} must not be null");
            }

            this.sortOrders = new List<SortOrder>(sortOrders);
        }

        public int Compare(DocumentProducer<OrderByQueryResult> producer1, DocumentProducer<OrderByQueryResult> producer2)
        {
            OrderByQueryResult result1 = producer1.Current;
            OrderByQueryResult result2 = producer2.Current;

            if (object.ReferenceEquals(result1, result2))
            {
                return 0;
            }

            if (result1 == null)
            {
                return -1;
            }

            if (result2 == null)
            {
                return 1;
            }

            int cmp = this.CompareOrderByItems(result1.OrderByItems, result2.OrderByItems);
            if (cmp != 0)
            {
                return cmp;
            }

            return string.CompareOrdinal(producer1.TargetRange.MinInclusive, producer2.TargetRange.MinInclusive);
        }

        public int CompareOrderByItems(QueryItem[] items1, QueryItem[] items2)
        {
            if (object.ReferenceEquals(items1, items2))
            {
                return 0;
            }

            Debug.Assert(
                items1 != null && items2 != null,
                "Order-by items must be present.");

            Debug.Assert(
                items1.Length == items2.Length,
                "OrderByResult instances should have the same number of order-by items.");

            Debug.Assert(
                items1.Length > 0,
                "OrderByResult instances should have at least 1 order-by item.");

            Debug.Assert(
                this.sortOrders.Count == items1.Length,
                "SortOrders must match size of order-by items.");

            if (AllowMixedTypeOrderByTestFlag)
            {
                this.CheckTypeMatching(items1, items2);
            }

            for (int i = 0; i < this.sortOrders.Count; ++i)
            {
                int cmp = ItemComparer.Instance.Compare(
                    items1[i].GetItem(),
                    items2[i].GetItem());

                if (cmp != 0)
                {
                    return this.sortOrders[i] != SortOrder.Descending ? cmp : -cmp;
                }
            }

            return 0;
        }

        private void CheckTypeMatching(QueryItem[] items1, QueryItem[] items2)
        {
            for (int i = 0; i < items1.Length; ++i)
            {
                ItemType item1Type = ItemTypeHelper.GetItemType(items1[i].GetItem());
                ItemType item2Type = ItemTypeHelper.GetItemType(items2[i].GetItem());

                if(item1Type != item2Type)
                {
                    throw new NotSupportedException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            RMResources.UnsupportedCrossPartitionOrderByQueryOnMixedTypes,
                            item1Type,
                            item2Type,
                            items1[i].GetItem()));
                }
            }
        }
    }
}
