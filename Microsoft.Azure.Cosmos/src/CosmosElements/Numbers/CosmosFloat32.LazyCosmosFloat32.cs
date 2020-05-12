//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 141

namespace Microsoft.Azure.Cosmos.CosmosElements.Numbers
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosFloat32 : CosmosNumber
    {
        private sealed class LazyCosmosFloat32 : CosmosFloat32
        {
            private readonly Lazy<float> lazyNumber;

            public LazyCosmosFloat32(
                IJsonNavigator jsonNavigator,
                IJsonNavigatorNode jsonNavigatorNode)
            {
                if (jsonNavigator == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonNavigator)}");
                }

                if (jsonNavigatorNode == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonNavigatorNode)}");
                }

                JsonNodeType type = jsonNavigator.GetNodeType(jsonNavigatorNode);
                if (type != JsonNodeType.Float32)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Float32} node. Got {type} instead.");
                }

                this.lazyNumber = new Lazy<float>(() => jsonNavigator.GetFloat32Value(jsonNavigatorNode));
            }

            public override float GetValue()
            {
                return this.lazyNumber.Value;
            }
        }
    }
}
