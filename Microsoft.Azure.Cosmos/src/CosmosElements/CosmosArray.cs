//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Monads;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosArray : CosmosElement, IReadOnlyList<CosmosElement>
    {
        protected CosmosArray()
            : base(CosmosElementType.Array)
        {
        }

        public abstract int Count { get; }

        public abstract CosmosElement this[int index] { get; }

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

        public static CosmosArray Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosArray(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosArray Create(IEnumerable<CosmosElement> cosmosElements)
        {
            return new EagerCosmosArray(cosmosElements);
        }

        public abstract IEnumerator<CosmosElement> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public static new CosmosArray CreateFromBuffer(ReadOnlyMemory<byte> buffer)
        {
            return CosmosElement.CreateFromBuffer<CosmosArray>(buffer);
        }

        public static new CosmosArray Parse(string json)
        {
            return CosmosElement.Parse<CosmosArray>(json);
        }

        public static bool TryCreateFromBuffer(ReadOnlyMemory<byte> buffer, out CosmosArray cosmosArray)
        {
            return CosmosElement.TryCreateFromBuffer<CosmosArray>(buffer, out cosmosArray);
        }

        public static bool TryParse(string json, out CosmosArray cosmosArray)
        {
            return CosmosElement.TryParse<CosmosArray>(json, out cosmosArray);
        }

        public static new class Monadic
        {
            public static TryCatch<CosmosArray> CreateFromBuffer(ReadOnlyMemory<byte> buffer)
            {
                return CosmosElement.Monadic.CreateFromBuffer<CosmosArray>(buffer);
            }

            public static TryCatch<CosmosArray> Parse(string json)
            {
                return CosmosElement.Monadic.Parse<CosmosArray>(json);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
