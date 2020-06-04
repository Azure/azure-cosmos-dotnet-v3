//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

// This is auto-generated code. Modify: CosmosNumberCodeGenerator.tt: 178

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
    abstract partial class CosmosFloat64 : CosmosNumber, IEquatable<CosmosFloat64>
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

            public override double GetValue()
            {
                return this.lazyNumber.Value;
            }
        }
    }
}
