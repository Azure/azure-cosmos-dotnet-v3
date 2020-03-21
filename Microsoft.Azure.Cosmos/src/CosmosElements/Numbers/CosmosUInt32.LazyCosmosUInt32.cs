//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
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
    abstract partial class CosmosUInt32 : CosmosNumber
    {
        private sealed class LazyCosmosUInt32 : CosmosUInt32
        {
            private readonly Lazy<uint> lazyNumber;

            public LazyCosmosUInt32(
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
                if (type != JsonNodeType.UInt32)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.UInt32} node. Got {type} instead.");
                }

                this.lazyNumber = new Lazy<uint>(() => jsonNavigator.GetUInt32Value(jsonNavigatorNode));
            }

            public override uint GetValue()
            {
                return this.lazyNumber.Value;
            }
        }
    }
}
