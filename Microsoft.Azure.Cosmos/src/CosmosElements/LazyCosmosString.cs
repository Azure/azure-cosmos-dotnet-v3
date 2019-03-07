namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;
    using System;

    internal sealed class LazyCosmosString : CosmosString
    {
        private readonly IJsonNavigator jsonNavigator;
        private readonly IJsonNavigatorNode jsonNavigatorNode;
        private readonly Lazy<string> lazyString;

        public LazyCosmosString(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
        {
            LazyCosmosElementUtils.ValidateNavigatorAndNode(
                jsonNavigator,
                jsonNavigatorNode,
                JsonNodeType.String);

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

        public override void WriteToWriter(IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException($"{nameof(jsonWriter)}");
            }

            jsonWriter.WriteJsonNode(this.jsonNavigator, jsonNavigatorNode);
        }
    }
}
