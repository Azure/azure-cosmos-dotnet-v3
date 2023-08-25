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
            private readonly Lazy<List<CosmosElement>> lazyCosmosElementArray;
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;

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

                this.lazyCosmosElementArray = new Lazy<List<CosmosElement>>(() =>
                {
                    List<CosmosElement> elements = new List<CosmosElement>(jsonNavigator.GetArrayItemCount(jsonNavigatorNode));
                    // Using foreach instead of indexer, since the navigator doesn't support random seeks efficiently.
                    foreach (IJsonNavigatorNode arrayItem in jsonNavigator.GetArrayItems(jsonNavigatorNode))
                    {
                        elements.Add(CosmosElement.Dispatch(jsonNavigator, arrayItem));
                    }

                    return elements;
                });
            }

            public override int Count => this.lazyCosmosElementArray.Value.Count;

            public override CosmosElement this[int index] => this.lazyCosmosElementArray.Value[index];

            public override Enumerator GetEnumerator() => new Enumerator(this.lazyCosmosElementArray.Value.GetEnumerator());

            public override void WriteTo(IJsonWriter jsonWriter) => this.jsonNavigator.WriteNode(this.jsonNavigatorNode, jsonWriter);

            public override IJsonReader CreateReader() => this.jsonNavigator.CreateReader(this.jsonNavigatorNode);
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}