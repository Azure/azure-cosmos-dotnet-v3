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

        public JsonType Visit(CosmosArray cosmosArray)
        {
            return JsonType.Array;
        }

        public JsonType Visit(CosmosBinary cosmosBinary)
        {
            throw new NotSupportedException("Binary is not a json type.");
        }

        public JsonType Visit(CosmosBoolean cosmosBoolean)
        {
            return JsonType.Boolean;
        }

        public JsonType Visit(CosmosGuid cosmosGuid)
        {
            throw new NotSupportedException("Guid is not a json type.");
        }

        public JsonType Visit(CosmosNull cosmosNull)
        {
            return JsonType.Null;
        }

        public JsonType Visit(CosmosNumber cosmosNumber)
        {
            return JsonType.Number;
        }

        public JsonType Visit(CosmosObject cosmosObject)
        {
            return JsonType.Object;
        }

        public JsonType Visit(CosmosString cosmosString)
        {
            return JsonType.String;
        }

        public JsonType Visit(CosmosUndefined cosmosUndefined)
        {
            return JsonType.Undefined;
        }
    }
}