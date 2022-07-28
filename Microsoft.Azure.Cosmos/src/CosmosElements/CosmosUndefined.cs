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
    sealed class CosmosUndefined : CosmosElement, IEquatable<CosmosUndefined>, IComparable<CosmosUndefined>
    {
        public static readonly CosmosUndefined Instance = new CosmosUndefined();

        private CosmosUndefined()
        {
        }

        public override void Accept(ICosmosElementVisitor cosmosElementVisitor)
        {
            cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(ICosmosElementVisitor<TResult> cosmosElementVisitor)
        {
            return cosmosElementVisitor.Visit(this);
        }

        public override TResult Accept<TArg, TResult>(ICosmosElementVisitor<TArg, TResult> cosmosElementVisitor, TArg input)
        {
            return cosmosElementVisitor.Visit(this, input);
        }

        public int CompareTo(CosmosUndefined other)
        {
            return 0;
        }

        public override bool Equals(CosmosElement cosmosElement)
        {
            return cosmosElement is CosmosUndefined;
        }

        public bool Equals(CosmosUndefined other)
        {
            return true;
        }

        public override int GetHashCode()
        {
            return Instance.GetHashCode();
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            // We could be more forgiving here, e.g. just return
            throw new InvalidOperationException("Cannot serialize CosmosUndefined");
        }
    }
}