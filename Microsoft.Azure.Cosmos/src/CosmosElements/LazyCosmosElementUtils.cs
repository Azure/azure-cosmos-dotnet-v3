namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using Microsoft.Azure.Cosmos.Json;

    internal static class LazyCosmosElementUtils
    {
        public static void ValidateNavigatorAndNode(
            IJsonNavigator jsonNavigator, 
            IJsonNavigatorNode jsonNavigatorNode, 
            JsonNodeType jsonNodeType)
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
            if (type != jsonNodeType)
            {
                throw new ArgumentException($"{nameof(jsonNavigatorNode)} must not be a {jsonNodeType} node. Got {type} instead.");
            }
        }
    }
}
