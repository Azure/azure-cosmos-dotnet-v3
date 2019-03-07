namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;

    internal class LazyCosmosObject : CosmosObject
    {
        private readonly IJsonNavigator jsonNavigator;
        private readonly IJsonNavigatorNode jsonNavigatorNode;

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
                throw new ArgumentException($"{nameof(jsonNavigatorNode)} must not be a {JsonNodeType.Object} node. Got {type} instead.");
            }

            this.jsonNavigator = jsonNavigator;
            this.jsonNavigatorNode = jsonNavigatorNode;
        }

        public override CosmosElement this[string key]
        {
            get
            {
                if (!this.TryGetValue(key, out CosmosElement value))
                {
                    value = null;
                }

                return value;
            }
        }

        public override IEnumerable<string> Keys => this
            .jsonNavigator
            .GetObjectProperties(this.jsonNavigatorNode)
            .Select((objectProperty) => this.jsonNavigator.GetStringValue(objectProperty.NameNode));

        public override IEnumerable<CosmosElement> Values => this
            .jsonNavigator
            .GetObjectProperties(this.jsonNavigatorNode)
            .Select((objectProperty) => LazyCosmosElementFactory.Create(this.jsonNavigator, objectProperty.ValueNode));

        public override int Count => this
            .jsonNavigator
            .GetObjectPropertyCount(this.jsonNavigatorNode);

        public override bool ContainsKey(string key) => this.jsonNavigator.TryGetObjectProperty(
            this.jsonNavigatorNode,
            key,
            out ObjectProperty objectProperty);

        public override IEnumerator<KeyValuePair<string, CosmosElement>> GetEnumerator() => this
            .jsonNavigator
            .GetObjectProperties(this.jsonNavigatorNode)
            .Select(
                (objectProperty) =>
                new KeyValuePair<string, CosmosElement>(
                    this.jsonNavigator.GetStringValue(objectProperty.NameNode),
                    LazyCosmosElementFactory.Create(this.jsonNavigator, objectProperty.ValueNode)))
            .GetEnumerator();

        public override bool TryGetValue(string key, out CosmosElement value)
        {
            value = default(CosmosElement);
            bool gotValue;
            if (this.jsonNavigator.TryGetObjectProperty(
                this.jsonNavigatorNode,
                key,
                out ObjectProperty objectProperty))
            {
                value = LazyCosmosElementFactory.Create(this.jsonNavigator, objectProperty.ValueNode);
                gotValue = true;
            }
            else
            {
                value = null;
                gotValue = false;
            }

            return gotValue;
        }

        public override void WriteTo(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)} must not be null.");
            }

            jsonWriter.WriteJsonNode(this.jsonNavigator, jsonNavigatorNode);
        }
    }
}