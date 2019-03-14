//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Json;

    internal static class FeedResponseBinder
    {
        /// <summary>
        /// DEVNOTE: Need to refactor to use CosmosJsonSerializer
        /// </summary>
        public static FeedResponse<T> Convert<T>(
            FeedResponse<CosmosElement> dynamicFeed,
            ContentSerializationFormat contentSerializationFormat,
            Newtonsoft.Json.JsonSerializerSettings serializerSettings)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(ContentToJsonSerializationFormat(contentSerializationFormat));

            jsonWriter.WriteArrayStart();

            foreach (CosmosElement cosmosElement in dynamicFeed)
            {
                cosmosElement.WriteTo(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();

            List<T> typedResults;
            using (MemoryStream memoryStream = new MemoryStream(jsonWriter.GetResult()))
            {
                using (JsonCosmosDBReader reader = new JsonCosmosDBReader(memoryStream))
                {
                    Newtonsoft.Json.JsonSerializer serializer = Newtonsoft.Json.JsonSerializer.Create(serializerSettings);
                    typedResults = serializer.Deserialize<List<T>>(reader);
                }
            }

            return new FeedResponse<T>(
                typedResults,
                typedResults.Count,
                dynamicFeed.Headers,
                dynamicFeed.UseETagAsContinuation,
                dynamicFeed.QueryMetrics,
                dynamicFeed.RequestStatistics,
                dynamicFeed.DisallowContinuationTokenMessage,
                dynamicFeed.ResponseLengthBytes);
        }

        public static CosmosQueryResponse ConvertToCosmosQueryResponse(
            FeedResponse<CosmosElement> dynamicFeed,
            ContentSerializationFormat contentSerializationFormat,
            Newtonsoft.Json.JsonSerializerSettings serializerSettings)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(ContentToJsonSerializationFormat(contentSerializationFormat));

            jsonWriter.WriteArrayStart();

            foreach (CosmosElement cosmosElement in dynamicFeed)
            {
                cosmosElement.WriteTo(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();

            MemoryStream memoryStream = new MemoryStream(jsonWriter.GetResult());
            return new CosmosQueryResponse(
                dynamicFeed.Headers,
                memoryStream,
                dynamicFeed.ResponseContinuation);
        }

        private static Microsoft.Azure.Cosmos.Json.JsonSerializationFormat ContentToJsonSerializationFormat(
            ContentSerializationFormat contentSerializationFormat)
        {
            switch (contentSerializationFormat)
            {
                case ContentSerializationFormat.JsonText:
                    return JsonSerializationFormat.Text;
                case ContentSerializationFormat.CosmosBinary:
                    return JsonSerializationFormat.Binary;
                default:
                    throw new ArgumentException($"Unknown {nameof(ContentSerializationFormat)} : {contentSerializationFormat}");
            }
        }
    }
}
