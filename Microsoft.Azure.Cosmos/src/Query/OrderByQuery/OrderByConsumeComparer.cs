//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#define SUPPORT_V1_COLLECTIONS
namespace Microsoft.Azure.Cosmos.Query.ParallelQuery
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

    internal sealed class OrderByConsumeComparer : IComparer<DocumentProducer<OrderByQueryResult>>
    {
        private readonly SortOrder[] sortOrders;
#if SUPPORT_V1_COLLECTIONS
        private ItemType[] orderByItemTypes;
#endif

        public OrderByConsumeComparer(SortOrder[] sortOrders)
        {
            this.sortOrders = sortOrders;
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
                this.sortOrders.Length == items1.Length,
                "SortOrders must match size of order-by items.");

#if SUPPORT_V1_COLLECTIONS
            this.CheckTypeMatching(items1, items2);
#endif

            for (int i = 0; i < this.sortOrders.Length; ++i)
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

#if SUPPORT_V1_COLLECTIONS
        private void CheckTypeMatching(QueryItem[] items1, QueryItem[] items2)
        {
            if (this.orderByItemTypes == null)
            {
                lock (this)
                {
                    if (this.orderByItemTypes == null)
                    {
                        this.orderByItemTypes = new ItemType[items1.Length];
                        for (int i = 0; i < items1.Length; ++i)
                        {
                            this.orderByItemTypes[i] = ItemTypeHelper.GetItemType(items1[i].GetItem());
                        }
                    }
                }
            }

            for (int i = 0; i < items1.Length; ++i)
            {
                if (this.orderByItemTypes[i] != ItemTypeHelper.GetItemType(items1[i].GetItem()))
                {
                    throw new NotSupportedException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            RMResources.UnsupportedCrossPartitionOrderByQueryOnMixedTypes,
                            orderByItemTypes[i],
                            ItemTypeHelper.GetItemType(items1[i].GetItem()),
                            items1[i].GetItem()));
                }

                if (this.orderByItemTypes[i] != ItemTypeHelper.GetItemType(items2[i].GetItem()))
                {
                    throw new NotSupportedException(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            RMResources.UnsupportedCrossPartitionOrderByQueryOnMixedTypes,
                            orderByItemTypes[i],
                            ItemTypeHelper.GetItemType(items2[i].GetItem()),
                            items2[i].GetItem()));
                }
            }
        }
#endif
    }
}
