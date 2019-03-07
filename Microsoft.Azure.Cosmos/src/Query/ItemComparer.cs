//-----------------------------------------------------------------------
// <copyright file="ItemComparer.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

    /// <summary>
    /// Utility class used to compare all items that we get back from a query.
    /// </summary>
    internal sealed class ItemComparer : IComparer
    {
        /// <summary>
        /// Singleton item comparer.
        /// </summary>
        public static readonly ItemComparer Instance = new ItemComparer();

        /// <summary>
        /// The minimum value out of all possible items.
        /// </summary>
        /// <remarks>Note that this isn't a real item.</remarks>
        public static readonly MinValueItem MinValue = new MinValueItem();

        /// <summary>
        /// The maximum value out of all possible items.
        /// </summary>
        /// <remarks>Note that this isn't a real item.</remarks>
        public static readonly MaxValueItem MaxValue = new MaxValueItem();

        /// <summary>
        /// Compares to objects and returns their partial sort relationship.
        /// </summary>
        /// <param name="obj1">The first object to compare.</param>
        /// <param name="obj2">The second object to compare.</param>
        /// <returns>
        /// Less than zero if obj1 comes before obj2 in the sort order.
        /// Zero if obj1 and obj2 are interchangeable in the sort order.
        /// Greater than zero if obj2 comes before obj1 in the sort order.
        /// </returns>
        public int Compare(object obj1, object obj2)
        {
            if (object.ReferenceEquals(obj1, obj2))
            {
                return 0;
            }

            if (obj1 is MinValueItem)
            {
                return -1;
            }

            if (obj2 is MinValueItem)
            {
                return 1;
            }

            if (obj1 is MaxValueItem)
            {
                return 1;
            }

            if (obj2 is MaxValueItem)
            {
                return -1;
            }

            ItemType type1 = ItemTypeHelper.GetItemType(obj1);
            ItemType type2 = ItemTypeHelper.GetItemType(obj2);

            int cmp = type1.CompareTo(type2);
            if (cmp != 0)
            {
                // If one item type comes before another item type, then just return that.
                return cmp;
            }

            // If they are the same type then you need to break the tie.
            switch (type1)
            {
                case ItemType.NoValue:
                case ItemType.Null:
                    // All null and no values are not distinguishable.
                    return 0;
                case ItemType.Bool:
                    return Comparer<bool>.Default.Compare((bool)obj1, (bool)obj2);
                case ItemType.Number:
                    return Comparer<double>.Default.Compare(
                        Convert.ToDouble(obj1, CultureInfo.InvariantCulture),
                        Convert.ToDouble(obj2, CultureInfo.InvariantCulture));
                case ItemType.String:
                    return string.CompareOrdinal((string)obj1, (string)obj2);
                default:
                    string errorMessage = string.Format(CultureInfo.InvariantCulture, "Unexpected type: {0}", type1);
                    Debug.Assert(false, errorMessage);
                    throw new InvalidCastException(errorMessage);
            }
        }

        public static bool IsMinOrMax(object obj)
        {
            return obj == MinValue || obj == MaxValue;
        }

        /// <summary>
        /// Represents the minimum value item.
        /// </summary>
        public sealed class MinValueItem
        {
        }

        /// <summary>
        /// Represent the maximum value item.
        /// </summary>
        public sealed class MaxValueItem
        {
        }
    }
}
