namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine
{
    using System;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class CosmosElementToJsonType : ICosmosElementVisitor<JsonType>
    {
        public static readonly CosmosElementToJsonType Singleton = new CosmosElementToJsonType();

        private CosmosElementToJsonType()
        {
        }

        public JsonType Visit(CosmosArray cosmosArray) => JsonType.Array;

        public JsonType Visit(CosmosBinary cosmosBinary) => throw new NotSupportedException("Binary is not a json type.");

        public JsonType Visit(CosmosBoolean cosmosBoolean) => JsonType.Boolean;

        public JsonType Visit(CosmosGuid cosmosGuid) => throw new NotSupportedException("Guid is not a json type.");

        public JsonType Visit(CosmosNull cosmosNull) => JsonType.Null;

        public JsonType Visit(CosmosNumber cosmosNumber) => JsonType.Number;

        public JsonType Visit(CosmosObject cosmosObject) => JsonType.Object;

        public JsonType Visit(CosmosString cosmosString) => JsonType.String;
    }
}
