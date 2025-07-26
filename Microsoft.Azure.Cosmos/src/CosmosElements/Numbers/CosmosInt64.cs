//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 45

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
#nullable enable

    using System;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosInt64 : CosmosNumber, IEquatable<CosmosInt64>, IComparable<CosmosInt64>
    {
        protected CosmosInt64()
            : base()
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract long GetValue();

        public override void Accept(ICosmosNumberVisitor cosmosNumberVisitor)
        {
            cosmosNumberVisitor.Visit(this);
        }

        public override TResult Accept<TResult>(ICosmosNumberVisitor<TResult> cosmosNumberVisitor)
        {
            return cosmosNumberVisitor.Visit(this);
        }

        public override TOutput Accept<TArg, TOutput>(ICosmosNumberVisitor<TArg, TOutput> cosmosNumberVisitor, TArg input)
        {
            return cosmosNumberVisitor.Visit(this, input);
        }

        public override bool Equals(CosmosElement? cosmosNumber) => cosmosNumber is CosmosInt64 cosmosInt64 && this.Equals(cosmosInt64);

        public bool Equals(CosmosInt64? cosmosInt64)
        {
            return cosmosInt64 is not null && this.Value == cosmosInt64.Value;
        }

        public override int GetHashCode()
        {
            return (int)(HashSeed ^ (uint)this.Value.GetHashCode());
        }

        public int CompareTo(CosmosInt64? cosmosInt64)
        {
            return cosmosInt64 is null ? 1 : this.Value.CompareTo(cosmosInt64.Value);
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            jsonWriter.WriteInt64Value(this.GetValue());
        }

        public static CosmosInt64 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosInt64(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosInt64 Create(long number)
        {
            return new EagerCosmosInt64(number);
        }
    }
}
