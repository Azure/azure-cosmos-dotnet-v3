//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosInt8 : CosmosNumber
    {
        private sealed class LazyCosmosInt8 : CosmosInt8
        {
            private readonly Lazy<sbyte> lazyNumber;

            public LazyCosmosInt8(
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
                if (type != JsonNodeType.Int8)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Int8} node. Got {type} instead.");
                }

                this.lazyNumber = new Lazy<sbyte>(() => jsonNavigator.GetInt8Value(jsonNavigatorNode));
            }

            protected override sbyte GetValue()
            {
                return this.lazyNumber.Value;
            }
        }
    }
}
