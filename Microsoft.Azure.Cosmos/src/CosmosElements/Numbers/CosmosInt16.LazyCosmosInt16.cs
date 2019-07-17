//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable SA1601 // Partial elements should be documented
    public abstract partial class CosmosInt16 : CosmosNumber
#else
    internal abstract partial class CosmosInt16 : CosmosNumber
#endif
    {
        private sealed class LazyCosmosInt16 : CosmosInt16
        {
            private readonly Lazy<short> lazyNumber;

            public LazyCosmosInt16(
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
                if (type != JsonNodeType.Int16)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Int16} node. Got {type} instead.");
                }

                this.lazyNumber = new Lazy<short>(() => jsonNavigator.GetInt16Value(jsonNavigatorNode));
            }

            protected override short GetValue()
            {
                return this.lazyNumber.Value;
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#endif
}
