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
    abstract partial class CosmosInt32 : CosmosNumber, IEqutable<CosmosInt32>
    {
        protected CosmosInt32()
            : base(CosmosNumberType.Int32)
        {
        }

        public override Number64 Value => this.GetValue();

        public abstract int GetValue();

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
            if (!(cosmosNumber is CosmosInt32 cosmosInt32))
            {
                return false;
            }

            return this.Equals(cosmosInt32);
        }

        public bool Equals(CosmosInt32 cosmosInt32)
        {
            return this.GetValue() == cosmosInt32.GetValue();
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteInt32Value(this.GetValue());
        }

        public static CosmosInt32 Create(
            IJsonNavigator jsonNavigator,
            IJsonNavigatorNode jsonNavigatorNode)
        {
            return new LazyCosmosInt32(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosInt32 Create(int number)
        {
            return new EagerCosmosInt32(number);
        }
    }
}
