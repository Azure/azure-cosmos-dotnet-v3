//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Json;
    using Newtonsoft.Json;
    using JsonWriter = Json.JsonWriter;

    internal static class FeedResponseBinder
    {
        private static JsonSerializer _serializer = new JsonSerializer();
        /// <summary>
        /// DEVNOTE: Need to refactor to use CosmosJsonSerializer
        /// </summary>
        public static FeedResponse<T> Convert<T>(FeedResponse<CosmosElement> dynamicFeed)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);

            jsonWriter.WriteArrayStart();

            foreach (CosmosElement cosmosElement in dynamicFeed)
            {
                cosmosElement.WriteTo(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();

            List<T> typedResults = JsonConvert.DeserializeObject<List<T>>(
                Encoding.UTF8.GetString(jsonWriter.GetResult()));

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
            CosmosSerializationOptions cosmosSerializationOptions)
        {
            IJsonWriter jsonWriter;
            if (cosmosSerializationOptions != null)
            {
                jsonWriter = cosmosSerializationOptions.CreateCustomWriterCallback();
            }
            else
            {
                jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            }

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
                dynamicFeed.Count,
                dynamicFeed.ResponseContinuation,
                dynamicFeed.QueryMetrics);
        }
    }
}
