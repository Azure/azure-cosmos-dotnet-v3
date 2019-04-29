//-----------------------------------------------------------------------
// <copyright file="CosmosFloat32.LazyCosmosFloat32.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosFloat32 : CosmosNumber
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

            protected override float GetValue()
            {
                return this.lazyNumber.Value;
            }
        }
    }
}
