//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Core.Utf8;
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
    abstract partial class CosmosString : CosmosElement, IEquatable<CosmosString>, IComparable<CosmosString>
    {
        private const uint HashSeed = 3163568842;

        protected CosmosString()
            : base(CosmosElementType.String)
        {
        }

        public abstract string Value { get; }

        public abstract bool TryGetBufferedValue(out Utf8Memory bufferedUtf8Value);

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
            if (!(cosmosElement is CosmosString cosmosString))
            {
                return false;
            }

            return this.Equals(cosmosString);
        }

        public bool Equals(CosmosString cosmosString)
        {
            return this.Value == cosmosString.Value;
        }

        public override int GetHashCode()
        {
            uint hash = HashSeed;
            hash = MurmurHash3.Hash32(this.Value, hash);

            return (int)hash;
        }

        public int CompareTo(CosmosString cosmosString)
        {
            return string.CompareOrdinal(this.Value, cosmosString.Value);
        }

        public static CosmosString Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            if (jsonNavigator == null)
            {
                throw new ArgumentNullException(nameof(jsonNavigator));
            }

            if (jsonNavigatorNode == null)
            {
                throw new ArgumentNullException(nameof(jsonNavigatorNode));
            }

            return new LazyCosmosString(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosString Create(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Length == 0)
            {
                return EagerCosmosString.Empty;
            }

            return new EagerCosmosString(value);
        }

        public static new CosmosString CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosString>(buffer);
        }

        public static new CosmosString Parse(string json)
        {
            return CosmosElement.Parse<CosmosString>(json);
        }

        public static bool TryCreateFromBuffer(ReadOnlyMemory<byte> buffer, out CosmosString cosmosString)
        {
            return CosmosElement.TryCreateFromBuffer<CosmosString>(buffer, out cosmosString);
        }

        public static bool TryParse(string json, out CosmosString cosmosString)
        {
            return CosmosElement.TryParse<CosmosString>(json, out cosmosString);
        }

        public static new class Monadic
        {
            public static TryCatch<CosmosString> CreateFromBuffer(ReadOnlyMemory<byte> buffer)
            {
                return CosmosElement.Monadic.CreateFromBuffer<CosmosString>(buffer);
            }

            public static TryCatch<CosmosString> Parse(string json)
            {
                return CosmosElement.Monadic.Parse<CosmosString>(json);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
