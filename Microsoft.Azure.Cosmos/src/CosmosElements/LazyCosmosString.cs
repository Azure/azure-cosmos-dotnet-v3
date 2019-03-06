namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;
    using System;

    internal sealed class LazyCosmosString : CosmosString, ILazyCosmosElement
    {
        private readonly IJsonNavigator jsonNavigator;
        private readonly IJsonNavigatorNode jsonNavigatorNode;
        private readonly Lazy<string> lazyString;

        public LazyCosmosString(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
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
            if (!(type == JsonNodeType.String || type == JsonNodeType.FieldName))
            {
                throw new ArgumentException($"{nameof(jsonNavigatorNode)} must not be a string node. Got {type} instead.");
            }

            this.jsonNavigator = jsonNavigator;
            this.jsonNavigatorNode = jsonNavigatorNode;
            this.lazyString = new Lazy<string>(() => 
            {
                return this.jsonNavigator.GetStringValue(this.jsonNavigatorNode);
            });
        }

        public override string Value
        {
            get
            {
                return this.lazyString.Value;
            }
        }

        public void WriteToWriter(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)} must not be null.");
            }

            jsonWriter.WriteJsonNode(this.jsonNavigator, jsonNavigatorNode);
        }
    }
}
