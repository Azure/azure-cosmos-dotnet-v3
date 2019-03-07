namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using Microsoft.Azure.Cosmos.Json;
    using System;

    /// <summary>
    /// Base abstract class for JSON readers.
    /// The reader defines methods that allow for reading a JSON encoded value as a stream of tokens.
    /// The tokens are traversed in the same order as they appear in the JSON document.
    /// </summary>
    internal static class LazyCosmosElementFactory
    {
        public static CosmosElement CreateFromBuffer(byte[] buffer)
        {
            IJsonNavigator jsonNavigator = JsonNavigator.Create(buffer);
            IJsonNavigatorNode jsonNavigatorNode = jsonNavigator.GetRootNode();

            return LazyCosmosElementFactory.CreateTokenFromNavigatorAndNode(jsonNavigator, jsonNavigatorNode);
        }

        public static CosmosElement CreateTokenFromNavigatorAndNode(IJsonNavigator jsonNavigator, IJsonNavigatorNode jsonNavigatorNode)
        {
            JsonNodeType jsonNodeType = jsonNavigator.GetNodeType(jsonNavigatorNode);
            CosmosElement item;
            switch (jsonNodeType)
            {
                case JsonNodeType.Null:
                    item = LazyCosmosNull.Singleton;
                    break;

                case JsonNodeType.False:
                    item = CosmosBoolean.False;
                    break;

                case JsonNodeType.True:
                    item = CosmosBoolean.True;
                    break;

                case JsonNodeType.Number:
                    item = new LazyCosmosNumber(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.FieldName:
                case JsonNodeType.String:
                    item = new LazyCosmosString(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Array:
                    item = new LazyCosmosArray(jsonNavigator, jsonNavigatorNode);
                    break;

                case JsonNodeType.Object:
                    item = new LazyCosmosObject(jsonNavigator, jsonNavigatorNode);
                    break;

                default:
                    throw new ArgumentException($"Unknown {nameof(JsonNodeType)}: {jsonNodeType}");
            }

            return item;
        }
    }
}
