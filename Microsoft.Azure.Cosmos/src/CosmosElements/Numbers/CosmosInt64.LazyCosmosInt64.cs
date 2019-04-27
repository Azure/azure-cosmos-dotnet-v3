//-----------------------------------------------------------------------
// <copyright file="CosmosInt64.LazyCosmosInt64.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosInt64 : CosmosNumber
    {
        private sealed class LazyCosmosInt64 : CosmosInt64
        {
            private readonly Lazy<long> lazyNumber;

            public LazyCosmosInt64(
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
                if (type != JsonNodeType.Int64)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Int64} node. Got {type} instead.");
                }

                this.lazyNumber = new Lazy<long>(() => jsonNavigator.GetInt64Value(jsonNavigatorNode));
            }

            protected override long GetValue()
            {
                return this.lazyNumber.Value;
            }
        }
    }
}
