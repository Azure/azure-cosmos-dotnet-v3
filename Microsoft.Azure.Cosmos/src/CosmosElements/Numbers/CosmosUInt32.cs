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
    abstract partial class CosmosUInt32 : CosmosNumber, IEquatable<CosmosUInt32>, IComparable<CosmosUInt32>
    {
        protected CosmosUInt32()
            : base(CosmosNumberType.UInt32)
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract uint GetValue();

        public override void Accept(ICosmosNumberVisitor cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TResult Accept<TResult>(ICosmosNumberVisitor<TResult> cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TOutput Accept<TArg, TOutput>(ICosmosNumberVisitor<TArg, TOutput> cosmosNumberVisitor, TArg input) => cosmosNumberVisitor.Visit(this, input);

        public override bool Equals(CosmosNumber cosmosNumber) => cosmosNumber is CosmosUInt32 cosmosUInt32 && this.Equals(cosmosUInt32);

        public bool Equals(CosmosUInt32 cosmosUInt32) => this.GetValue() == cosmosUInt32.GetValue();

        public override int GetHashCode() => (int)MurmurHash3.Hash32(this.GetValue(), 3771427877);

        public int CompareTo(CosmosUInt32 cosmosUInt32) => this.GetValue().CompareTo(cosmosUInt32.GetValue());

        public override void WriteTo(IJsonWriter jsonWriter) => jsonWriter.WriteUInt32Value(this.GetValue());

        public static CosmosUInt32 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode) => new LazyCosmosUInt32(jsonNavigator, jsonNavigatorNode);

        public static CosmosUInt32 Create(uint number) => new EagerCosmosUInt32(number);
    }
}
