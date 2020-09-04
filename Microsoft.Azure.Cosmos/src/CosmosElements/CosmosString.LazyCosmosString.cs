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
    abstract partial class CosmosString : CosmosElement, IEquatable<CosmosString>, IComparable<CosmosString>
    {
        private sealed class LazyCosmosString : CosmosString
        {
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;
            private readonly Lazy<string> lazyString;

            public LazyCosmosString(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
            {
                JsonNodeType type = jsonNavigator.GetNodeType(jsonNavigatorNode);
                if (type != JsonNodeType.String)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.String} node. Got {type} instead.");
                }

                this.jsonNavigator = jsonNavigator;
                this.jsonNavigatorNode = jsonNavigatorNode;
                this.lazyString = new Lazy<string>(() => this.jsonNavigator.GetStringValue(this.jsonNavigatorNode));
            }

            public override string Value => this.lazyString.Value;

            public override bool TryGetBufferedValue(out Utf8Memory bufferedValue) => this.jsonNavigator.TryGetBufferedStringValue(
                this.jsonNavigatorNode,
                out bufferedValue);

            public override void WriteTo(IJsonWriter jsonWriter) => this.jsonNavigator.WriteNode(this.jsonNavigatorNode, jsonWriter);
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
