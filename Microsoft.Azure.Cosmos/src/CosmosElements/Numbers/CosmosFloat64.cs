//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 45

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
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
    abstract partial class CosmosFloat64 : CosmosNumber, IEquatable<CosmosFloat64>
    {
        protected CosmosFloat64()
            : base(CosmosNumberType.Float64)
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract double GetValue();

        public override void Accept(ICosmosNumberVisitor cosmosNumberVisitor)
        {
            if (cosmosNumberVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosNumberVisitor));
            }

            cosmosNumberVisitor.Visit(this);
        }

        public override TOutput Accept<TArg, TOutput>(ICosmosNumberVisitor<TArg, TOutput> cosmosNumberVisitor, TArg input)
        {
            if (cosmosNumberVisitor == null)
            {
                throw new ArgumentNullException(nameof(cosmosNumberVisitor));
            }

            return cosmosNumberVisitor.Visit(this, input);
        }

        public override bool Equals(CosmosNumber cosmosNumber)
        {
            if (!(cosmosNumber is CosmosFloat64 cosmosFloat64))
            {
                return false;
            }

            return this.Equals(cosmosFloat64);
        }

        public override int GetHashCode()
        {
            uint hash = 470975939;
            hash = MurmurHash3.Hash32(this.GetValue(), hash);

            return (int)hash;
        }

        public bool Equals(CosmosFloat64 cosmosFloat64)
        {
            return this.GetValue() == cosmosFloat64.GetValue();
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteFloat64Value(this.GetValue());
        }

        public static CosmosFloat64 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosFloat64(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosFloat64 Create(double number)
        {
            return new EagerCosmosFloat64(number);
        }
    }
}
