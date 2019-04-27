//-----------------------------------------------------------------------
// <copyright file="CosmosFloat64.LazyCosmosFloat64.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal abstract partial class CosmosFloat64 : CosmosNumber
    {
        private sealed class LazyCosmosFloat64 : CosmosFloat64
        {
            private readonly Lazy<double> lazyNumber;

            public LazyCosmosFloat64(
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
                if (type != JsonNodeType.Float64)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Float64} node. Got {type} instead.");
                }

                this.lazyNumber = new Lazy<double>(() => jsonNavigator.GetFloat64Value(jsonNavigatorNode));
            }

            protected override double GetValue()
            {
                return this.lazyNumber.Value;
            }
        }
    }
}
