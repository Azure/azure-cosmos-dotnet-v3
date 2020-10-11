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
    abstract partial class CosmosBinary : CosmosElement, IEquatable<CosmosBinary>, IComparable<CosmosBinary>
    {
        private sealed class LazyCosmosBinary : CosmosBinary
        {
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;
            private readonly Lazy<ReadOnlyMemory<byte>> lazyBytes;

            public LazyCosmosBinary(
                IJsonNavigator jsonNavigator,
                IJsonNavigatorNode jsonNavigatorNode)
            {
                JsonNodeType type = jsonNavigator.GetNodeType(jsonNavigatorNode);
                if (type != JsonNodeType.Binary)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Binary} node. Got {type} instead.");
                }

                this.jsonNavigator = jsonNavigator;
                this.jsonNavigatorNode = jsonNavigatorNode;
                this.lazyBytes = new Lazy<ReadOnlyMemory<byte>>(() =>
                {
                    if (!this.jsonNavigator.TryGetBufferedBinaryValue(
                        this.jsonNavigatorNode,
                        out ReadOnlyMemory<byte> bufferedBinaryValue))
                    {
                        bufferedBinaryValue = this.jsonNavigator.GetBinaryValue(
                            this.jsonNavigatorNode);
                    }

                    return bufferedBinaryValue;
                });
            }

            public override ReadOnlyMemory<byte> Value => this.lazyBytes.Value;

            public override void WriteTo(IJsonWriter jsonWriter) => this.jsonNavigator.WriteNode(this.jsonNavigatorNode, jsonWriter);
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
