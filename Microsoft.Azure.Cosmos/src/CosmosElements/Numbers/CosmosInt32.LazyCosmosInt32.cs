//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosInt32 : CosmosNumber
#else
    internal abstract partial class CosmosInt32 : CosmosNumber
#endif
    {
        private sealed class LazyCosmosInt32 : CosmosInt32
        {
            private readonly Lazy<int> lazyNumber;

            public LazyCosmosInt32(
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
                if (type != JsonNodeType.Int32)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Int32} node. Got {type} instead.");
                }

                this.lazyNumber = new Lazy<int>(() => jsonNavigator.GetInt32Value(jsonNavigatorNode));
            }

            protected override int GetValue()
            {
                return this.lazyNumber.Value;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}
