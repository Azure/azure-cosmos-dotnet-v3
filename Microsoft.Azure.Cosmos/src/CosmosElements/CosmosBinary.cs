//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    using System;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosBinary : CosmosElement, IEquatable<CosmosBinary>, IComparable<CosmosBinary>
    {
        private const uint HashSeed = 1577818695;

        protected CosmosBinary()
            : base(CosmosElementType.Binary)
        {
        }

        public abstract ReadOnlyMemory<byte> Value { get; }

        public override void Accept(ICosmosElementVisitor cosmosElementVisitor) => cosmosElementVisitor.Visit(this);

        public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor) => cosmosElementVisitor.Visit(this);

        public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input) => cosmosElementVisitor.Visit(this, input);

        public override bool Equals(CosmosElement cosmosElement) => cosmosElement is CosmosBinary cosmosBinary && this.Equals(cosmosBinary);

        public bool Equals(CosmosBinary cosmosBinary) => this.Value.Span.SequenceEqual(cosmosBinary.Value.Span);

        public override int GetHashCode()
        {
            uint hash = HashSeed;
            hash = MurmurHash3.Hash32(this.Value.Span, hash);
            return (int)hash;
        }

        public int CompareTo(CosmosBinary cosmosBinary) => this.Value.Span.SequenceCompareTo(cosmosBinary.Value.Span);

        public static CosmosBinary Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode) => new LazyCosmosBinary(jsonNavigator, jsonNavigatorNode);

        public static CosmosBinary Create(ReadOnlyMemory<byte> value) => new EagerCosmosBinary(value);

        public static new CosmosBinary CreateFromBuffer(ReadOnlyMemory<byte> buffer) => CosmosElement.CreateFromBuffer<CosmosBinary>(buffer);

        public static new CosmosBinary Parse(string json) => CosmosElement.Parse<CosmosBinary>(json);

        public static bool TryCreateFromBuffer(
            ReadOnlyMemory<byte> buffer,
            out CosmosBinary cosmosBinary) => CosmosElement.TryCreateFromBuffer<CosmosBinary>(buffer, out cosmosBinary);

        public static bool TryParse(
            string json, out
            CosmosBinary cosmosBinary) => CosmosElement.TryParse<CosmosBinary>(json, out cosmosBinary);

        public static new class Monadic
        {
            public static TryCatch<CosmosBinary> CreateFromBuffer(ReadOnlyMemory<byte> buffer) => CosmosElement.Monadic.CreateFromBuffer<CosmosBinary>(buffer);

            public static TryCatch<CosmosBinary> Parse(string json) => CosmosElement.Monadic.Parse<CosmosBinary>(json);
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
