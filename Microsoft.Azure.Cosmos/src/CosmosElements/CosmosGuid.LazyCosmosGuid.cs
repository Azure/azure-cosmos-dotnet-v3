//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    using System;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosGuid : CosmosElement, IEquatable<CosmosGuid>, IComparable<CosmosGuid>
    {
        private sealed class LazyCosmosGuid : CosmosGuid
        {
            private readonly Lazy<Guid> lazyGuid;

            public LazyCosmosGuid(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
            {
                JsonNodeType type = jsonNavigator.GetNodeType(jsonNavigatorNode);
                if (type != JsonNodeType.Guid)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Guid} node. Got {type} instead.");
                }

                this.lazyGuid = new Lazy<Guid>(() => jsonNavigator.GetGuidValue(jsonNavigatorNode));
            }

            public override Guid Value => this.lazyGuid.Value;
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
