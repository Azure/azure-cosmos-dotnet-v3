namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;

    internal sealed class LazyCosmosArray : CosmosArray, ILazyCosmosElement
    {
        private readonly IJsonNavigator jsonNavigator;
        private readonly IJsonNavigatorNode jsonNavigatorNode;

        public LazyCosmosArray(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
        {
            if (jsonNavigator == null)
            {
                throw new ArgumentNullException($"{nameof(jsonNavigator)} must not be null.");
            }

            if (jsonNavigatorNode == null)
            {
                throw new ArgumentNullException($"{nameof(jsonNavigatorNode)} must not be null.");
            }

            JsonNodeType type = jsonNavigator.GetNodeType(jsonNavigatorNode);
            if (type != JsonNodeType.Array)
            {
                throw new ArgumentException($"{nameof(jsonNavigatorNode)} must not be a {JsonNodeType.Array} node. Got {type} instead.");
            }

            this.jsonNavigator = jsonNavigator;
            this.jsonNavigatorNode = jsonNavigatorNode;
        }

        public override CosmosElement this[int index]
        {
            get
            {
                IJsonNavigatorNode arrayItemNode = this.jsonNavigator.GetArrayItemAt(this.jsonNavigatorNode, index);
                return LazyCosmosElementFactory.CreateTokenFromNavigatorAndNode(this.jsonNavigator, arrayItemNode);
            }
        }

        public override int Count => this.jsonNavigator.GetArrayItemCount(this.jsonNavigatorNode);

        public override IEnumerator<CosmosElement> GetEnumerator() => this
            .jsonNavigator
            .GetArrayItems(this.jsonNavigatorNode)
            .Select((arrayItem) => LazyCosmosElementFactory.CreateTokenFromNavigatorAndNode(this.jsonNavigator, arrayItem))
            .GetEnumerator();

        public override string ToString()
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            this.WriteToWriter(jsonWriter);
            return Encoding.UTF8.GetString(jsonWriter.GetResult());
        }

        public void WriteToWriter(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)} must not be null");
            }

            jsonWriter.WriteJsonNode(this.jsonNavigator, this.jsonNavigatorNode);
        }
    }
}
