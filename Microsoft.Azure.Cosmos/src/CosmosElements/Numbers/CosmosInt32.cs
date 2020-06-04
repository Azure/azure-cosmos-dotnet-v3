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
    abstract partial class CosmosInt32 : CosmosNumber, IEquatable<CosmosInt32>, IComparable<CosmosInt32>
    {
        protected CosmosInt32()
            : base(CosmosNumberType.Int32)
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract int GetValue();

        public override void Accept(ICosmosNumberVisitor cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TResult Accept<TResult>(ICosmosNumberVisitor<TResult> cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TOutput Accept<TArg, TOutput>(ICosmosNumberVisitor<TArg, TOutput> cosmosNumberVisitor, TArg input) => cosmosNumberVisitor.Visit(this, input);

        public override bool Equals(CosmosNumber cosmosNumber) => cosmosNumber is CosmosInt32 cosmosInt32 && this.Equals(cosmosInt32);

        public bool Equals(CosmosInt32 cosmosInt32) => this.GetValue() == cosmosInt32.GetValue();

        public override int GetHashCode() => (int)MurmurHash3.Hash32(this.GetValue(), 1791401667);

        public int CompareTo(CosmosInt32 cosmosInt32) => this.GetValue().CompareTo(cosmosInt32.GetValue());

        public override void WriteTo(IJsonWriter jsonWriter) => jsonWriter.WriteInt32Value(this.GetValue());

        public static CosmosInt32 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode) => new LazyCosmosInt32(jsonNavigator, jsonNavigatorNode);

        public static CosmosInt32 Create(int number) => new EagerCosmosInt32(number);
    }
}
