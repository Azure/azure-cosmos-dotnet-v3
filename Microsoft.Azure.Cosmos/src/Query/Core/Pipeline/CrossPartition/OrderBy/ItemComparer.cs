//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Collections.Generic;
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

            return element1.CompareTo(element2);
        }

        public static bool IsMinOrMax(CosmosElement obj)
        {
            return obj == MinValue || obj == MaxValue;
        }

        /// <summary>
        /// Represents the minimum value item.
        /// </summary>
        public sealed class MinValueItem : CosmosElement
        {
            public static readonly MinValueItem Singleton = new MinValueItem();

            private MinValueItem()
                : base()
            {
            }

            public override void Accept(ICosmosElementVisitor cosmosElementVisitor)
            {
                throw new NotImplementedException();
            }

            public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor)
            {
                throw new NotImplementedException();
            }

            public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input)
            {
                throw new NotImplementedException();
            }

            public override bool Equals(CosmosElement cosmosElement)
            {
                return cosmosElement is MinValueItem;
            }

            public override int GetHashCode()
            {
                return 42;
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
                : base()
            {
            }

            public override void Accept(ICosmosElementVisitor cosmosElementVisitor)
            {
                throw new NotImplementedException();
            }

            public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor)
            {
                throw new NotImplementedException();
            }

            public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input)
            {
                throw new NotImplementedException();
            }

            public override bool Equals(CosmosElement cosmosElement)
            {
                return cosmosElement is MaxValueItem;
            }

            public override int GetHashCode()
            {
                return 1337;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                throw new NotImplementedException();
            }
        }
    }
}
