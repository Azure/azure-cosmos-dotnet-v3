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
    abstract partial class CosmosInt16 : CosmosNumber, IEquatable<CosmosInt16>, IComparable<CosmosInt16>
    {
        protected CosmosInt16()
            : base()
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract short GetValue();

        public override void Accept(ICosmosNumberVisitor cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TResult Accept<TResult>(ICosmosNumberVisitor<TResult> cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TOutput Accept<TArg, TOutput>(ICosmosNumberVisitor<TArg, TOutput> cosmosNumberVisitor, TArg input) => cosmosNumberVisitor.Visit(this, input);

        public override bool Equals(CosmosNumber cosmosNumber) => cosmosNumber is CosmosInt16 cosmosInt16 && this.Equals(cosmosInt16);

        public bool Equals(CosmosInt16 cosmosInt16) => this.GetValue() == cosmosInt16.GetValue();

        public override int GetHashCode() => (int)MurmurHash3.Hash32(this.GetValue(), 1176550641);

        public int CompareTo(CosmosInt16 cosmosInt16) => this.GetValue().CompareTo(cosmosInt16.GetValue());

        public override void WriteTo(IJsonWriter jsonWriter) => jsonWriter.WriteInt16Value(this.GetValue());

        public static CosmosInt16 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode) => new LazyCosmosInt16(jsonNavigator, jsonNavigatorNode);

        public static CosmosInt16 Create(short number) => new EagerCosmosInt16(number);
    }
}
