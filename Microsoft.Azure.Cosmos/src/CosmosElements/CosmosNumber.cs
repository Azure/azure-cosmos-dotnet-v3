// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class CosmosNumber : CosmosElement
    {
        protected CosmosNumber(CosmosNumberType cosmosNumberType)
            : base(CosmosElementType.Number)
        {
            this.NumberType = cosmosNumberType;
        }

        public CosmosNumberType NumberType { get; }

        public abstract Number64 Value { get; }

        public abstract void Accept(ICosmosNumberVisitor cosmosNumberVisitor);

        public abstract TOutput Accept<TArg, TOutput>(ICosmosNumberVisitor<TArg, TOutput> cosmosNumberVisitor, TArg input);

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

        public static new CosmosNumber CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosNumber>(buffer);
        }

        public static new CosmosNumber Parse(string json)
        {
            return CosmosElement.Parse<CosmosNumber>(json);
        }

        public static bool TryCreateFromBuffer(ReadOnlyMemory<byte> buffer, out CosmosNumber cosmosNumber)
        {
            return CosmosElement.TryCreateFromBuffer<CosmosNumber>(buffer, out cosmosNumber);
        }

        public static bool TryParse(string json, out CosmosNumber cosmosNumber)
        {
            return CosmosElement.TryParse<CosmosNumber>(json, out cosmosNumber);
        }

        public static new class Monadic
        {
            public static TryCatch<CosmosNumber> CreateFromBuffer(ReadOnlyMemory<byte> buffer)
            {
                return CosmosElement.Monadic.CreateFromBuffer<CosmosNumber>(buffer);
            }

            public static TryCatch<CosmosNumber> Parse(string json)
            {
                return CosmosElement.Monadic.Parse<CosmosNumber>(json);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}