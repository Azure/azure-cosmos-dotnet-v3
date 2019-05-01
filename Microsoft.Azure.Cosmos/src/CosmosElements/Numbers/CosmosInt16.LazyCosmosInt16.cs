//-----------------------------------------------------------------------
// <copyright file="CosmosInt16.LazyCosmosInt16.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosInt16 : CosmosNumber
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
}
