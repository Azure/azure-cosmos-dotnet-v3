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
    abstract partial class CosmosFloat32 : CosmosNumber, IEquatable<CosmosFloat32>, IComparable<CosmosFloat32>
    {
        protected CosmosFloat32()
            : base()
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract float GetValue();

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

        public override bool Equals(CosmosNumber cosmosNumber)
        {
            return cosmosNumber is CosmosFloat32 cosmosFloat32 && this.Equals(cosmosFloat32);
        }

        public bool Equals(CosmosFloat32 cosmosFloat32)
        {
            return this.GetValue() == cosmosFloat32.GetValue();
        }

        public override int GetHashCode()
        {
            return (int)MurmurHash3.Hash32(this.GetValue(), 495253708);
        }

        public int CompareTo(CosmosFloat32 cosmosFloat32)
        {
            return this.GetValue().CompareTo(cosmosFloat32.GetValue());
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            jsonWriter.WriteFloat32Value(this.GetValue());
        }

        public static CosmosFloat32 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosFloat32(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosFloat32 Create(float number)
        {
            return new EagerCosmosFloat32(number);
        }
    }
}
