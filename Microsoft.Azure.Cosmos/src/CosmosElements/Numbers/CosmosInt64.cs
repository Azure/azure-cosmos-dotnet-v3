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
            : base(CosmosNumberType.Int64)
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract long GetValue();

        public override void Accept(ICosmosNumberVisitor cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TResult Accept<TResult>(ICosmosNumberVisitor<TResult> cosmosNumberVisitor) => cosmosNumberVisitor.Visit(this);

        public override TOutput Accept<TArg, TOutput>(ICosmosNumberVisitor<TArg, TOutput> cosmosNumberVisitor, TArg input) => cosmosNumberVisitor.Visit(this, input);

        public override bool Equals(CosmosNumber cosmosNumber)
        {
            if (!(cosmosNumber is CosmosInt64 cosmosInt64))
            {
                return false;
            }

            return this.Equals(cosmosInt64);
        }

        public bool Equals(CosmosInt64 cosmosInt64)
        {
            return this.GetValue() == cosmosInt64.GetValue();
        }

        public override int GetHashCode()
        {
            uint hash = 2562566505;
            hash = MurmurHash3.Hash32(this.GetValue(), hash);

            return (int)hash;
        }

        public int CompareTo(CosmosInt64 cosmosInt64)
        {
            return this.GetValue().CompareTo(cosmosInt64.GetValue());
        }

        public override void WriteTo(IJsonWriter jsonWriter) => jsonWriter.WriteInt64Value(this.GetValue());

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
