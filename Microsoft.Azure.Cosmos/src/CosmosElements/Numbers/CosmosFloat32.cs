//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 33

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
    abstract partial class CosmosFloat32 : CosmosNumber, IEqutable<CosmosFloat32>
    {
        protected CosmosFloat32()
            : base(CosmosNumberType.Float32)
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract float GetValue();

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
            if (!(cosmosNumber is CosmosFloat32 cosmosFloat32))
            {
                return false;
            }

            return this.Equals(cosmosFloat32);
        }

        public bool Equals(CosmosFloat32 cosmosFloat32)
        {
            return this.GetValue() == cosmosFloat32.GetValue();
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

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
