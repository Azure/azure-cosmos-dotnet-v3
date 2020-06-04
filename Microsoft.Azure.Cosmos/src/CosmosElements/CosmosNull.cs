//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Runtime.CompilerServices;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class CosmosNull : CosmosElement, IEquatable<CosmosNull>
    {
        private const uint Hash = 448207988;

        private static readonly CosmosNull Singleton = new CosmosNull();

        private CosmosNull()
            : base(CosmosElementType.Null)
        {
        }

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
            if (!(cosmosElement is CosmosNull cosmosNull))
            {
                return false;
            }

            return this.Equals(cosmosNull);
        }

        public bool Equals(CosmosNull cosmosNull) => true;

        public static CosmosNull Create()
        {
            return CosmosNull.Singleton;
        }

        public override int GetHashCode()
        {
            return (int)Hash;
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteNullValue();
        }

        public static new CosmosNull CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosNull>(buffer);
        }

        public static new CosmosNull Parse(string json)
        {
            return CosmosElement.Parse<CosmosNull>(json);
        }

        public static bool TryCreateFromBuffer(ReadOnlyMemory<byte> buffer, out CosmosNull cosmosNull)
        {
            return CosmosElement.TryCreateFromBuffer<CosmosNull>(buffer, out cosmosNull);
        }

        public static bool TryParse(string json, out CosmosNull cosmosNull)
        {
            return CosmosElement.TryParse<CosmosNull>(json, out cosmosNull);
        }

        public static new class Monadic
        {
            public static TryCatch<CosmosNull> CreateFromBuffer(ReadOnlyMemory<byte> buffer)
            {
                return CosmosElement.Monadic.CreateFromBuffer<CosmosNull>(buffer);
            }

            public static TryCatch<CosmosNull> Parse(string json)
            {
                return CosmosElement.Monadic.Parse<CosmosNull>(json);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
