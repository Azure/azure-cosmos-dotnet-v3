//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosArray : CosmosElement, IReadOnlyList<CosmosElement>, IEquatable<CosmosArray>, IComparable<CosmosArray>
    {
        public static readonly CosmosArray Empty = new EagerCosmosArray(Enumerable.Empty<CosmosElement>());

        private const uint HashSeed = 2533142560;

        protected CosmosArray()
            : base()
        {
        }

        public abstract int Count { get; }

        public abstract CosmosElement this[int index] { get; }

        public override void Accept(ICosmosElementVisitor cosmosElementVisitor) => cosmosElementVisitor.Visit(this);

        public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor) => cosmosElementVisitor.Visit(this);

        public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input) => cosmosElementVisitor.Visit(this, input);

        public override bool Equals(CosmosElement cosmosElement) => cosmosElement is CosmosArray cosmosArray && this.Equals(cosmosArray);

        public bool Equals(CosmosArray cosmosArray)
        {
            if (this.Count != cosmosArray.Count)
            {
                return false;
            }

            IEnumerable<(CosmosElement, CosmosElement)> itemPairs = this.Zip(cosmosArray, (first, second) => (first, second));
            foreach ((CosmosElement thisItem, CosmosElement otherItem) in itemPairs)
            {
                if (thisItem != otherItem)
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            uint hash = HashSeed;

            // Incorporate all the array items into the hash.
            for (int index = 0; index < this.Count; index++)
            {
                CosmosElement arrayItem = this[index];
                hash = MurmurHash3.Hash32(arrayItem.GetHashCode(), hash);
            }

            return (int)hash;
        }

        public int CompareTo(CosmosArray cosmosArray)
        {
            UInt128 hash1 = DistinctHash.GetHash(this);
            UInt128 hash2 = DistinctHash.GetHash(cosmosArray);
            return hash1.CompareTo(hash2);
        }

        public static CosmosArray Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode) => new LazyCosmosArray(jsonNavigator, jsonNavigatorNode);

        public static CosmosArray Create(IEnumerable<CosmosElement> cosmosElements) => new EagerCosmosArray(cosmosElements);

        public static CosmosArray Create(params CosmosElement[] cosmosElements) => new EagerCosmosArray(cosmosElements);

        public static CosmosArray Create() => CosmosArray.Empty;

        public abstract IEnumerator<CosmosElement> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public static new CosmosArray CreateFromBuffer(ReadOnlyMemory<byte> buffer) => CosmosElement.CreateFromBuffer<CosmosArray>(buffer);

        public static new CosmosArray Parse(string json) => CosmosElement.Parse<CosmosArray>(json);

        public static bool TryCreateFromBuffer(
            ReadOnlyMemory<byte> buffer,
            out CosmosArray cosmosArray) => CosmosElement.TryCreateFromBuffer<CosmosArray>(buffer, out cosmosArray);

        public static bool TryParse(string json, out CosmosArray cosmosArray) => CosmosElement.TryParse<CosmosArray>(json, out cosmosArray);

        public static new class Monadic
        {
            public static TryCatch<CosmosArray> CreateFromBuffer(ReadOnlyMemory<byte> buffer) => CosmosElement.Monadic.CreateFromBuffer<CosmosArray>(buffer);

            public static TryCatch<CosmosArray> Parse(string json) => CosmosElement.Monadic.Parse<CosmosArray>(json);
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
