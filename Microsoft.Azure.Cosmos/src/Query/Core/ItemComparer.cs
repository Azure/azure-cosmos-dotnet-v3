//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
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

            int cmp = CompareTypes(element1, element2);
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
                        if (number1.NumberType == CosmosNumberType.Number64)
                        {
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
                        }
                        else if (number1.IsFloatingPoint)
                        {
                            double double1 = number1.AsFloatingPoint().Value;
                            double double2 = number2.AsFloatingPoint().Value;
                            cmp = Comparer<double>.Default.Compare(double1, double2);
                        }
                        else
                        {
                            long integer1 = number1.AsInteger().Value;
                            long integer2 = number2.AsInteger().Value;
                            cmp = Comparer<long>.Default.Compare(integer1, integer2);
                        }

                        break;

                    case CosmosElementType.String:
                        CosmosString string1 = element1 as CosmosString;
                        CosmosString string2 = element2 as CosmosString;
                        cmp = string.CompareOrdinal(
                            string1.Value,
                            string2.Value);
                        break;

                    case CosmosElementType.Guid:
                        CosmosGuid guid1 = element1 as CosmosGuid;
                        CosmosGuid guid2 = element2 as CosmosGuid;
                        cmp = guid1.Value.CompareTo(guid2.Value);
                        break;

                    case CosmosElementType.Binary:
                        CosmosBinary binary1 = element1 as CosmosBinary;
                        CosmosBinary binary2 = element2 as CosmosBinary;
                        cmp = ItemComparer.CompareTo(binary1, binary2);
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

        private static int CompareTypes(CosmosElement cosmosElement1, CosmosElement cosmosElement2)
        {
            int order1 = TypeToOrder(cosmosElement1);
            int order2 = TypeToOrder(cosmosElement2);

            return order1 - order2;
        }

        private static int TypeToOrder(CosmosElement cosmosElement)
        {
            int order;
            switch (cosmosElement.Type)
            {
                case CosmosElementType.Null:
                    order = 0;
                    break;
                case CosmosElementType.Boolean:
                    order = 1;
                    break;
                case CosmosElementType.Number:
                    {
                        CosmosNumber number = (CosmosNumber)cosmosElement;
                        switch (number.NumberType)
                        {
                            case CosmosNumberType.Number64:
                                order = 2;
                                break;
                            case CosmosNumberType.Float32:
                                order = 6;
                                break;
                            case CosmosNumberType.Float64:
                                order = 7;
                                break;
                            case CosmosNumberType.Int16:
                                order = 8;
                                break;
                            case CosmosNumberType.Int32:
                                order = 9;
                                break;
                            case CosmosNumberType.Int64:
                                order = 10;
                                break;
                            case CosmosNumberType.Int8:
                                order = 11;
                                break;
                            case CosmosNumberType.UInt32:
                                order = 12;
                                break;
                            default:
                                throw new InvalidEnumArgumentException("Unknown number type", (int)number.NumberType, typeof(CosmosNumberType));
                        }
                    }

                    break;
                case CosmosElementType.String:
                    order = 3;
                    break;
                case CosmosElementType.Guid:
                    order = 5;
                    break;
                case CosmosElementType.Binary:
                    order = 4;
                    break;
                default:
                    throw new ArgumentException($"Unknown: {nameof(CosmosElementType)}: {cosmosElement.Type}");
            }

            return order;
        }

        public static int CompareTo(CosmosBinary left, CosmosBinary right)
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int minLength = Math.Min(left.Value.Count, right.Value.Count);
            for (int i = 0; i < minLength; i++)
            {
                int cmp = left.Value[i].CompareTo(right.Value[i]);
                if (cmp != 0)
                {
                    return cmp;
                }
            }

            return left.Value.Count.CompareTo(right.Value.Count);
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
