//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Internal;

    internal static class ItemTypeHelper
    {
        public static ItemType GetItemType(object value)
        {
            if(value is Undefined)
            {
                return ItemType.NoValue;
            }

            if (value == null)
            {
                return ItemType.Null;
            }

            if (value is bool)
            {
                return ItemType.Bool;
            }

            if (value is string)
            {
                return ItemType.String;
            }

            if (IsNumeric(value))
            {
                return ItemType.Number;
            }

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,"Unrecognized type {0}", value.GetType().ToString()));
        }

        public static bool IsPrimitive(object value)
        {
            return (value == null || value is bool || value is string || IsNumeric(value));
        }

        public static bool IsNumeric(object value)
        {
            return value is sbyte
                    || value is byte
                    || value is short
                    || value is ushort
                    || value is int
                    || value is uint
                    || value is long
                    || value is ulong
                    || value is float
                    || value is double
                    || value is decimal;
        }
    }
}