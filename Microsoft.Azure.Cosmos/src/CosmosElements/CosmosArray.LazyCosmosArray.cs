//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    using System;
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
            private readonly Lazy<Lazy<CosmosElement>[]> lazyCosmosElementArray;

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

                this.lazyCosmosElementArray = new Lazy<Lazy<CosmosElement>[]>(() =>
                {
                    Lazy<CosmosElement>[] lazyArray = new Lazy<CosmosElement>[this.jsonNavigator.GetArrayItemCount(this.jsonNavigatorNode)];
                    int index = 0;
                    // Using foreach instead of indexer, since the navigator doesn't support random seeks efficiently.
                    foreach (IJsonNavigatorNode arrayItem in this.jsonNavigator.GetArrayItems(this.jsonNavigatorNode))
                    {
                        lazyArray[index] = new Lazy<CosmosElement>(() => CosmosElement.Dispatch(this.jsonNavigator, arrayItem));
                        index++;
                    }

                    return lazyArray;
                });
            }

            public override int Count => this.lazyCosmosElementArray.Value.Length;

            public override CosmosElement this[int index] => this.lazyCosmosElementArray.Value[index].Value;

            public override IEnumerator<CosmosElement> GetEnumerator()
            {
                return this.lazyCosmosElementArray.Value.Select(lazyItem => lazyItem.Value).GetEnumerator();
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                this.jsonNavigator.WriteTo(this.jsonNavigatorNode, jsonWriter);
            }

            public override IJsonReader CreateReader()
            {
                return this.jsonNavigator.CreateReader(this.jsonNavigatorNode);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
