//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;

    internal sealed class ItemComparer : IComparer
    {
        public static readonly ItemComparer Instance = new ItemComparer();

        private ItemComparer()
        {
        }

        public int Compare(object obj1, object obj2)
        {
            ItemType type1 = ItemTypeHelper.GetItemType(obj1);
            ItemType type2 = ItemTypeHelper.GetItemType(obj2);

            int cmp = type1.CompareTo(type2);

            if (cmp != 0)
            {
                return cmp;
            }

            switch (type1)
            {
                case ItemType.NoValue:
                case ItemType.Null:
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
    }
}
