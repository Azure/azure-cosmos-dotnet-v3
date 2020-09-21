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
    public
#else
    internal
#endif
    sealed class CosmosNull : CosmosElement, IEquatable<CosmosNull>, IComparable<CosmosNull>
    {
        private const uint Hash = 448207988;

        private static readonly CosmosNull Singleton = new CosmosNull();

        private CosmosNull()
            : base()
        {
        }

        public override void Accept(ICosmosElementVisitor cosmosElementVisitor) => cosmosElementVisitor.Visit(this);

        public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor) => cosmosElementVisitor.Visit(this);

        public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input) => cosmosElementVisitor.Visit(this, input);

        public override bool Equals(CosmosElement cosmosElement) => cosmosElement is CosmosNull cosmosNull && this.Equals(cosmosNull);

        public bool Equals(CosmosNull cosmosNull) => true;

        public static CosmosNull Create() => CosmosNull.Singleton;

        public override int GetHashCode() => (int)Hash;

        public override void WriteTo(IJsonWriter jsonWriter) => jsonWriter.WriteNullValue();

        public static new CosmosNull CreateFromBuffer(ReadOnlyMemory<byte> buffer) => CosmosElement.CreateFromBuffer<CosmosNull>(buffer);

        public static new CosmosNull Parse(string json) => CosmosElement.Parse<CosmosNull>(json);

        public static bool TryCreateFromBuffer(
            ReadOnlyMemory<byte> buffer,
            out CosmosNull cosmosNull) => CosmosElement.TryCreateFromBuffer<CosmosNull>(buffer, out cosmosNull);

        public static bool TryParse(
            string json,
            out CosmosNull cosmosNull) => CosmosElement.TryParse<CosmosNull>(json, out cosmosNull);

        public int CompareTo(CosmosNull other) => 0;

        public static new class Monadic
        {
            public static TryCatch<CosmosNull> CreateFromBuffer(ReadOnlyMemory<byte> buffer) => CosmosElement.Monadic.CreateFromBuffer<CosmosNull>(buffer);

            public static TryCatch<CosmosNull> Parse(string json) => CosmosElement.Monadic.Parse<CosmosNull>(json);
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
