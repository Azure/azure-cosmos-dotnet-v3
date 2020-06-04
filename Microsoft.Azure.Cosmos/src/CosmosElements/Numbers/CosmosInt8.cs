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
    abstract partial class CosmosInt8 : CosmosNumber, IEquatable<CosmosInt8>, IComparable<CosmosInt8>
    {
        protected CosmosInt8()
            : base(CosmosNumberType.Int8)
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract sbyte GetValue();

        public override void Accept(ICosmosNumberVisitor cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TResult Accept<TResult>(ICosmosNumberVisitor<TResult> cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TOutput Accept<TArg, TOutput>(ICosmosNumberVisitor<TArg, TOutput> cosmosNumberVisitor, TArg input) => cosmosNumberVisitor.Visit(this, input);

        public override bool Equals(CosmosNumber cosmosNumber) => cosmosNumber is CosmosInt8 cosmosInt8 && this.Equals(cosmosInt8);

        public bool Equals(CosmosInt8 cosmosInt8) => this.GetValue() == cosmosInt8.GetValue();

        public override int GetHashCode() => (int)MurmurHash3.Hash32(this.GetValue(), 1301790982);

        public int CompareTo(CosmosInt8 cosmosInt8) => this.GetValue().CompareTo(cosmosInt8.GetValue());

        public override void WriteTo(IJsonWriter jsonWriter) => jsonWriter.WriteInt8Value(this.GetValue());

        public static CosmosInt8 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode) => new LazyCosmosInt8(jsonNavigator, jsonNavigatorNode);

        public static CosmosInt8 Create(sbyte number) => new EagerCosmosInt8(number);
    }
}
