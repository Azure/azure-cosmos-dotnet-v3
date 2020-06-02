//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Concurrent;
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
    abstract partial class CosmosObject : CosmosElement, IReadOnlyDictionary<string, CosmosElement>
    {
        private class LazyCosmosObject : CosmosObject
        {
            private readonly IJsonNavigator jsonNavigator;
            private readonly IJsonNavigatorNode jsonNavigatorNode;
            private readonly ConcurrentDictionary<string, CosmosElement> cachedElements;
            private readonly Lazy<int> lazyCount;

            public LazyCosmosObject(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
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
                if (type != JsonNodeType.Object)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(jsonNavigatorNode)} must be a {JsonNodeType.Object} node. Got {type} instead.");
                }

                this.jsonNavigator = jsonNavigator;
                this.jsonNavigatorNode = jsonNavigatorNode;
                this.cachedElements = new ConcurrentDictionary<string, CosmosElement>();
                this.lazyCount = new Lazy<int>(() => this.jsonNavigator.GetObjectPropertyCount(this.jsonNavigatorNode));
            }

            // This method is not efficient (but that's the expectation of IEnumerable ... A user should call ToList() if they are calling multiple times).
            public override IEnumerable<string> Keys => this
                .jsonNavigator
                .GetObjectProperties(this.jsonNavigatorNode)
                .Select((objectProperty) => this.jsonNavigator.GetStringValue(objectProperty.NameNode));

            // This method is not efficient (but that's the expectation of IEnumerable ... A user should call ToList() if they are calling multiple times).
            public override IEnumerable<CosmosElement> Values => this
                .jsonNavigator
                .GetObjectProperties(this.jsonNavigatorNode)
                .Select((objectProperty) => CosmosElement.Dispatch(this.jsonNavigator, objectProperty.ValueNode));

            public override int Count => this.lazyCount.Value;

            public override CosmosElement this[string key]
            {
                get
                {
                    if (!this.TryGetValue(key, out CosmosElement value))
                    {
                        throw new KeyNotFoundException($"Failed to find key: {key}");
                    }

                    return value;
                }
            }

            public override bool ContainsKey(string key) => this.jsonNavigator.TryGetObjectProperty(
                this.jsonNavigatorNode,
                key,
                out _);

            public override IEnumerator<KeyValuePair<string, CosmosElement>> GetEnumerator() => this
                .jsonNavigator
                .GetObjectProperties(this.jsonNavigatorNode)
                .Select(
                    (objectProperty) =>
                    new KeyValuePair<string, CosmosElement>(
                        this.jsonNavigator.GetStringValue(objectProperty.NameNode),
                        CosmosElement.Dispatch(this.jsonNavigator, objectProperty.ValueNode)))
                .GetEnumerator();

            public override bool TryGetValue(string key, out CosmosElement value)
            {
                if (this.cachedElements.TryGetValue(
                    key,
                    out CosmosElement cosmosElemet))
                {
                    value = cosmosElemet;
                    return true;
                }

                if (this.jsonNavigator.TryGetObjectProperty(
                    this.jsonNavigatorNode,
                    key,
                    out ObjectProperty objectProperty))
                {
                    value = CosmosElement.Dispatch(this.jsonNavigator, objectProperty.ValueNode);
                    this.cachedElements[key] = value;

                    return true;
                }

                value = default;
                return false;
            }

            public override void WriteTo(IJsonWriter jsonWriter)
            {
                if (jsonWriter == null)
                {
                    throw new ArgumentNullException($"{nameof(jsonWriter)}");
                }

                this.jsonNavigator.WriteTo(this.jsonNavigatorNode, jsonWriter);
            }

            public override T Materialize<T>(Newtonsoft.Json.JsonSerializer jsonSerializer = null)
            {
                jsonSerializer ??= DefaultSerializer;
                return this.jsonNavigator.Materialize<T>(jsonSerializer, this.jsonNavigatorNode);
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1601 // Partial elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}