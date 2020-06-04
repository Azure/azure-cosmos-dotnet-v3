//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
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
    abstract partial class CosmosGuid : CosmosElement, IEquatable<CosmosGuid>, IComparable<CosmosGuid>
    {
        private const uint HashSeed = 527095639;

        protected CosmosGuid()
            : base(CosmosElementType.Guid)
        {
        }

        public abstract Guid Value { get; }

        public override void Accept(ICosmosElementVisitor cosmosElementVisitor)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            return cosmosElementVisitor.Visit(this);
        }
        public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input)
        {
            if (cosmosElementVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosElementVisitor));
            }

            return cosmosElementVisitor.Visit(this, input);
        }

        public override bool Equals(CosmosElement cosmosElement)
        {
            if (!(cosmosElement is CosmosGuid cosmosGuid))
            {
                return false;
            }

            return this.Equals(cosmosGuid);
        }

        public bool Equals(CosmosGuid cosmosGuid)
        {
            if (cosmosGuid == null)
            {
                return false;
            }

            return this.Value == cosmosGuid.Value;
        }

        public override int GetHashCode()
        {
            uint hash = HashSeed;
            hash = MurmurHash3.Hash32(this.Value, hash);
            return (int)hash;
        }

        public int CompareTo(CosmosGuid cosmosGuid)
        {
            return this.Value.CompareTo(cosmosGuid.Value);
        }

        public static CosmosGuid Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosGuid(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosGuid Create(Guid value)
        {
            return new EagerCosmosGuid(value);
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteGuidValue(this.Value);
        }

        public static new CosmosGuid CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosGuid>(buffer);
        }

        public static new CosmosGuid Parse(string json)
        {
            return CosmosElement.Parse<CosmosGuid>(json);
        }

        public static bool TryCreateFromBuffer(ReadOnlyMemory<byte> buffer, out CosmosGuid cosmosGuid)
        {
            return CosmosElement.TryCreateFromBuffer<CosmosGuid>(buffer, out cosmosGuid);
        }

        public static bool TryParse(string json, out CosmosGuid cosmosGuid)
        {
            return CosmosElement.TryParse<CosmosGuid>(json, out cosmosGuid);
        }

        public static new class Monadic
        {
            public static TryCatch<CosmosGuid> CreateFromBuffer(ReadOnlyMemory<byte> buffer)
            {
                return CosmosElement.Monadic.CreateFromBuffer<CosmosGuid>(buffer);
            }

            public static TryCatch<CosmosGuid> Parse(string json)
            {
                return CosmosElement.Monadic.Parse<CosmosGuid>(json);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
