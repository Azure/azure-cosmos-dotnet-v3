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
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// Utility class used to compare all items that we get back from a query.
    /// </summary>
    internal sealed class ItemComparer : IComparer<CosmosElement>
    {
        /// <summary>
        /// Singleton item comparer.
        /// </summary>
        public static readonly ItemComparer Instance = new ItemComparer();

        /// <summary>
        /// The minimum value out of all possible items.
        /// </summary>
        /// <remarks>Note that this isn't a real item.</remarks>
        public static readonly MinValueItem MinValue = MinValueItem.Singleton;

        /// <summary>
        /// The maximum value out of all possible items.
        /// </summary>
        /// <remarks>Note that this isn't a real item.</remarks>
        public static readonly MaxValueItem MaxValue = MaxValueItem.Singleton;

        /// <summary>
        /// Undefined is represented by null in the CosmosElement library.
        /// </summary>
        private static readonly CosmosElement Undefined = null;

        /// <summary>
        /// Compares to objects and returns their partial sort relationship.
        /// </summary>
        /// <param name="element1">The first element to compare.</param>
        /// <param name="element2">The second element to compare.</param>
        /// <returns>
        /// Less than zero if obj1 comes before obj2 in the sort order.
        /// Zero if obj1 and obj2 are interchangeable in the sort order.
        /// Greater than zero if obj2 comes before obj1 in the sort order.
        /// </returns>
        public int Compare(CosmosElement element1, CosmosElement element2)
        {
            if (object.ReferenceEquals(element1, element2))
            {
                return 0;
            }

            if (object.ReferenceEquals(element1, MinValueItem.Singleton))
            {
                return -1;
            }

            if (object.ReferenceEquals(element2, MinValueItem.Singleton))
            {
                return 1;
            }

            if (object.ReferenceEquals(element1, MaxValueItem.Singleton))
            {
                return 1;
            }

            if (object.ReferenceEquals(element2, MaxValueItem.Singleton))
            {
                return -1;
            }

            if (element1 == Undefined)
            {
                return -1;
            }

            if (element2 == Undefined)
            {
                return 1;
            }

            CosmosElementType type1 = element1.Type;
            CosmosElementType type2 = element2.Type;

            int cmp = CompareTypes(type1, type2);
            if (cmp == 0)
            {
                // If they are the same type then you need to break the tie.
                switch (type1)
                {
                    case CosmosElementType.Boolean:
                        cmp = Comparer<bool>.Default.Compare(
                            (element1 as CosmosBoolean).Value,
                            (element2 as CosmosBoolean).Value);
                        break;

                    case CosmosElementType.Null:
                        // All nulls are the same.
                        cmp = 0;
                        break;

                    case CosmosElementType.Number:
                        CosmosNumber number1 = element1 as CosmosNumber;
                        CosmosNumber number2 = element2 as CosmosNumber;

                        double double1;
                        if (number1.IsFloatingPoint)
                        {
                            double1 = number1.AsFloatingPoint().Value;
                        }
                        else
                        {
                            double1 = number1.AsInteger().Value;
                        }

                        double double2;
                        if (number2.IsFloatingPoint)
                        {
                            double2 = number2.AsFloatingPoint().Value;
                        }
                        else
                        {
                            double2 = number2.AsInteger().Value;
                        }

                        cmp = Comparer<double>.Default.Compare(
                            double1,
                            double2);
                        break;

                    case CosmosElementType.String:
                    case CosmosElementType.Int8:
                    case CosmosElementType.Int16:
                    case CosmosElementType.Int32:
                    case CosmosElementType.Int64:
                    case CosmosElementType.UInt32:
                    case CosmosElementType.Float32:
                    case CosmosElementType.Float64:
                        CosmosTypedElement typedElement1 = element1 as CosmosTypedElement;
                        CosmosTypedElement typedElement2 = element2 as CosmosTypedElement;
                        cmp = typedElement1.CompareTo(typedElement2);
                        break;

                    default:
                        throw new ArgumentException($"Unknown: {nameof(CosmosElementType)}: {type1}");
                }
            }

            return cmp;
        }

        public static bool IsMinOrMax(CosmosElement obj)
        {
            return obj == MinValue || obj == MaxValue;
        }

        private static int CompareTypes(CosmosElementType cosmosElementType1, CosmosElementType cosmosElementType2)
        {
            int order1 = TypeToOrder(cosmosElementType1);
            int order2 = TypeToOrder(cosmosElementType2);

            return order1 - order2;
        }

        private static int TypeToOrder(CosmosElementType cosmosElementType)
        {
            int order;
            switch (cosmosElementType)
            {
                case CosmosElementType.Null:
                    order = 0;
                    break;
                case CosmosElementType.Boolean:
                    order = 1;
                    break;
                case CosmosElementType.Number:
                    order = 2;
                    break;
                case CosmosElementType.String:
                    order = 3;
                    break;
                case CosmosElementType.Int8:
                    order = 4;
                    break;
                case CosmosElementType.Int16:
                    order = 5;
                    break;
                case CosmosElementType.Int32:
                    order = 6;
                    break;
                case CosmosElementType.Int64:
                    order = 7;
                    break;
                case CosmosElementType.UInt32:
                    order = 8;
                    break;
                case CosmosElementType.Float32:
                    order = 9;
                    break;
                case CosmosElementType.Float64:
                    order = 10;
                    break;
                default:
                    throw new ArgumentException($"Unknown: {nameof(CosmosElementType)}: {cosmosElementType}");
            }

            return order;
        }

        /// <summary>
        /// Represents the minimum value item.
        /// </summary>
        public sealed class MinValueItem : CosmosElement
        {
            public static readonly MinValueItem Singleton = new MinValueItem();

            private MinValueItem()
                : base(default(CosmosElementType))
            {
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Represent the maximum value item.
        /// </summary>
        public sealed class MaxValueItem : CosmosElement
        {
            public static readonly MaxValueItem Singleton = new MaxValueItem();

            private MaxValueItem()
                : base(default(CosmosElementType))
            {
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                throw new NotImplementedException();
            }
        }
    }
}
