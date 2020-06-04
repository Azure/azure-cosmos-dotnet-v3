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
    abstract partial class CosmosFloat64 : CosmosNumber, IEquatable<CosmosFloat64>, IComparable<CosmosFloat64>
    {
        protected CosmosFloat64()
            : base(CosmosNumberType.Float64)
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract double GetValue();

        public override void Accept(ICosmosNumberVisitor cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TResult Accept<TResult>(ICosmosNumberVisitor<TResult> cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TOutput Accept<TArg, TOutput>(ICosmosNumberVisitor<TArg, TOutput> cosmosNumberVisitor, TArg input) => cosmosNumberVisitor.Visit(this, input);

        public override bool Equals(CosmosNumber cosmosNumber) => cosmosNumber is CosmosFloat64 cosmosFloat64 && this.Equals(cosmosFloat64);

        public bool Equals(CosmosFloat64 cosmosFloat64) => this.GetValue() == cosmosFloat64.GetValue();

        public override int GetHashCode() => (int)MurmurHash3.Hash32(this.GetValue(), 470975939);

        public int CompareTo(CosmosFloat64 cosmosFloat64) => this.GetValue().CompareTo(cosmosFloat64.GetValue());

        public override void WriteTo(IJsonWriter jsonWriter) => jsonWriter.WriteFloat64Value(this.GetValue());

        public static CosmosFloat64 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode) => new LazyCosmosFloat64(jsonNavigator, jsonNavigatorNode);

        public static CosmosFloat64 Create(double number) => new EagerCosmosFloat64(number);
    }
}
