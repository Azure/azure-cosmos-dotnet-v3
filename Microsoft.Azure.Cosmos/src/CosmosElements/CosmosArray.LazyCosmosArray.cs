//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosArray : CosmosElement, IReadOnlyList<CosmosElement>, IEquatable<CosmosArray>, IComparable<CosmosArray>
    {
        private sealed class LazyCosmosArray : CosmosArray
        {
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;
            private readonly Dictionary<int, CosmosElement> cachedCosmosElements;
            private readonly Lazy<int> lazyCount;

            public LazyCosmosArray(
                IJsonNavigator jsonNavigator,
                IJsonNavigatorNode jsonNavigatorNode)
            {
                JsonNodeType type = jsonNavigator.GetNodeType(jsonNavigatorNode);
                if (type != JsonNodeType.Array)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be an {JsonNodeType.Array} node. Got {type} instead.");
                }

                this.jsonNavigator = jsonNavigator;
                this.jsonNavigatorNode = jsonNavigatorNode;
                this.cachedCosmosElements = new Dictionary<int, CosmosElement>(capacity: 0);
                this.lazyCount = new Lazy<int>(() => this.jsonNavigator.GetArrayItemCount(this.jsonNavigatorNode));
            }

            public override int Count => this.lazyCount.Value;

            public override CosmosElement this[int index]
            {
                get
                {
                    if (!this.cachedCosmosElements.TryGetValue(index, out CosmosElement cachedValue))
                    {
                        IJsonNavigatorNode jsonNavigatorNode = this.jsonNavigator.GetArrayItemAt(this.jsonNavigatorNode, index);
                        cachedValue = CosmosElement.Dispatch(this.jsonNavigator, jsonNavigatorNode);
                        this.cachedCosmosElements[index] = cachedValue;
                    }

                    return cachedValue;
                }
            }

            public override IEnumerator<CosmosElement> GetEnumerator() => new LazyCosmosArrayEnumerator(this);

            public override void WriteTo(IJsonWriter jsonWriter) => this.jsonNavigator.WriteNode(this.jsonNavigatorNode, jsonWriter);

            public override IJsonReader CreateReader() => this.jsonNavigator.CreateReader(this.jsonNavigatorNode);

            private struct LazyCosmosArrayEnumerator : IEnumerator<CosmosElement>
            {
                private readonly LazyCosmosArray lazyCosmosArray;
                private IEnumerator<IJsonNavigatorNode> nodes;
                private int index;

                public LazyCosmosArrayEnumerator(LazyCosmosArray lazyCosmosArray)
                {
                    this.lazyCosmosArray = lazyCosmosArray;
                    this.Current = default;
                    this.nodes = default;
                    this.index = default;
                    this.Reset();
                }

                public CosmosElement Current { get; private set; }

                object IEnumerator.Current => this.Current;

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (!this.nodes.MoveNext())
                    {
                        this.Current = default;
                        return false;
                    }

                    this.index++;
                    if (!this.lazyCosmosArray.cachedCosmosElements.TryGetValue(this.index, out CosmosElement cachedValue))
                    {
                        cachedValue = CosmosElement.Dispatch(this.lazyCosmosArray.jsonNavigator, this.nodes.Current);
                        this.lazyCosmosArray.cachedCosmosElements[this.index] = cachedValue;
                    }

                    this.Current = cachedValue;
                    return true;
                }

                public void Reset()
                {
                    this.nodes = this.lazyCosmosArray.jsonNavigator.GetArrayItems(this.lazyCosmosArray.jsonNavigatorNode).GetEnumerator();
                    this.index = -1;
                }
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}