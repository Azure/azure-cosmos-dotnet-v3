//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
#nullable enable

    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Json;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1601 // Partial elements should be documented
    public
#else
    internal
#endif
    abstract partial class CosmosObject : CosmosElement, IReadOnlyDictionary<string, CosmosElement>, IEquatable<CosmosObject>, IComparable<CosmosObject>
    {
        private class LazyCosmosObject : CosmosObject
        {
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;
            private readonly Lazy<Dictionary<string, CosmosElement>> lazyCache;

            public LazyCosmosObject(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
            {
                JsonNodeType type = jsonNavigator.GetNodeType(jsonNavigatorNode);
                if (type != JsonNodeType.Object)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Object} node. Got {type} instead.");
                }

                this.jsonNavigator = jsonNavigator;
                this.jsonNavigatorNode = jsonNavigatorNode;
                this.lazyCache = new Lazy<Dictionary<string, CosmosElement>>(() =>
                {
                    int propertyCount = this.jsonNavigator.GetObjectPropertyCount(this.jsonNavigatorNode);
                    Dictionary<string, CosmosElement> cache = new Dictionary<string, CosmosElement>(capacity: propertyCount);
                    foreach (ObjectProperty objectProperty in this.jsonNavigator.GetObjectProperties(this.jsonNavigatorNode))
                    {
                        string key = this.jsonNavigator.GetStringValue(objectProperty.NameNode);
                        CosmosElement value = CosmosElement.Dispatch(this.jsonNavigator, objectProperty.ValueNode);
                        cache[key] = value;
                    }

                    return cache;
                });
            }

            public override Dictionary<string, CosmosElement>.KeyCollection Keys => this.lazyCache.Value.Keys;

            public override Dictionary<string, CosmosElement>.ValueCollection Values => this.lazyCache.Value.Values;

            public override int Count => this.lazyCache.Value.Count;

            public override CosmosElement this[string key] => this.lazyCache.Value[key];

            public override bool ContainsKey(string key) => this.lazyCache.Value.ContainsKey(key);

            public override Dictionary<string, CosmosElement>.Enumerator GetEnumerator() => this.lazyCache.Value.GetEnumerator();

            public override bool TryGetValue(string key, out CosmosElement value) => this.lazyCache.Value.TryGetValue(key, out value);

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                this.jsonNavigator.WriteNode(this.jsonNavigatorNode, jsonWriter);
            }

            public override IJsonReader CreateReader()
            {
                return this.jsonNavigator.CreateReader(this.jsonNavigatorNode);
            }
        }
    }
}