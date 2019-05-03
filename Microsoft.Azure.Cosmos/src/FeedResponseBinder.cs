//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using JsonWriter = Json.JsonWriter;

    internal static class FeedResponseBinder
    {
        //Helper to materialize Any IResourceFeed<T> from IResourceFeed<dynamic> as long as source
        //conversion from dynamic to T.

        //This method is invoked via expression as part of dynamic binding of cast operator.
        public static FeedResponse<T> Convert<T>(FeedResponse<dynamic> dynamicFeed)
        {
            if (typeof(T) == typeof(object))
            {
                return (FeedResponse<T>)(object)dynamicFeed;
            }
            IList<T> result = new List<T>();

            foreach (T item in dynamicFeed)
            {
                result.Add(item);
            }

            return new FeedResponse<T>(
                result,
                dynamicFeed.Count,
                dynamicFeed.Headers,
                dynamicFeed.UseETagAsContinuation,
                dynamicFeed.QueryMetrics,
                dynamicFeed.RequestStatistics,
                responseLengthBytes: dynamicFeed.ResponseLengthBytes);
        }

        /// <summary>
        /// DEVNOTE: Need to refactor to use CosmosJsonSerializer
        /// </summary>
        public static FeedResponse<T> ConvertCosmosElementFeed<T>(
            FeedResponse<CosmosElement> dynamicFeed, 
            ResourceType resourceType,
            CosmosJsonSerializer jsonSerializer)
        {
            if(dynamicFeed.Count == 0)
            {
                return new FeedResponse<T>(
                new List<T>(),
                dynamicFeed.Count,
                dynamicFeed.Headers,
                dynamicFeed.UseETagAsContinuation,
                dynamicFeed.QueryMetrics,
                dynamicFeed.RequestStatistics,
                dynamicFeed.DisallowContinuationTokenMessage,
                dynamicFeed.ResponseLengthBytes);
            }

            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);

            jsonWriter.WriteArrayStart();

            foreach (CosmosElement cosmosElement in dynamicFeed)
            {
                cosmosElement.WriteTo(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();
            MemoryStream stream = new MemoryStream(jsonWriter.GetResult());
            IEnumerable<T> typedResults;

            // If the resource type is an offer and the requested type is either a Offer or OfferV2 or dynamic
            // create a OfferV2 object and cast it to T. This is a temporary fix until offers is moved to v3 API. 
            if (resourceType == ResourceType.Offer &&
                (typeof(T).IsSubclassOf(typeof(Resource)) || typeof(T) == typeof(object)))
            {
                typedResults = jsonSerializer.FromStream<List<OfferV2>>(stream).Cast<T>();
            }
            else
            {
                typedResults = jsonSerializer.FromStream<List<T>>(stream);
            }

            return new FeedResponse<T>(
                typedResults,
                dynamicFeed.Count,
                dynamicFeed.Headers,
                dynamicFeed.UseETagAsContinuation,
                dynamicFeed.QueryMetrics,
                dynamicFeed.RequestStatistics,
                dynamicFeed.DisallowContinuationTokenMessage,
                dynamicFeed.ResponseLengthBytes);
        }


        /// <summary>
        /// DEVNOTE: Need to refactor to use CosmosJsonSerializer
        /// </summary>
        public static FeedResponse<T> ConvertCosmosElementFeed<T>(
            FeedResponse<CosmosElement> dynamicFeed,
            ResourceType resourceType,
            JsonSerializerSettings settings)
        {
            if (dynamicFeed.Count == 0)
            {
                return new FeedResponse<T>(
                new List<T>(),
                dynamicFeed.Count,
                dynamicFeed.Headers,
                dynamicFeed.UseETagAsContinuation,
                dynamicFeed.QueryMetrics,
                dynamicFeed.RequestStatistics,
                dynamicFeed.DisallowContinuationTokenMessage,
                dynamicFeed.ResponseLengthBytes);
            }

            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);

            jsonWriter.WriteArrayStart();

            foreach (CosmosElement cosmosElement in dynamicFeed)
            {
                cosmosElement.WriteTo(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();
            string jsonText = Encoding.UTF8.GetString(jsonWriter.GetResult());
            IEnumerable<T> typedResults;

            // If the resource type is an offer and the requested type is either a Offer or OfferV2 or dynamic
            // create a OfferV2 object and cast it to T. This is a temporary fix until offers is moved to v3 API. 
            if (resourceType == ResourceType.Offer &&
                (typeof(T).IsSubclassOf(typeof(Resource)) || typeof(T) == typeof(object)))
            {
                typedResults = JsonConvert.DeserializeObject<List<OfferV2>>(jsonText, settings).Cast<T>();
            }
            else
            {
                typedResults = JsonConvert.DeserializeObject<List<T>>(jsonText, settings);
            }


            return new FeedResponse<T>(
                typedResults,
                dynamicFeed.Count,
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
                dynamicFeed.InternalResponseContinuation,
                dynamicFeed.DisallowContinuationTokenMessage,
                dynamicFeed.QueryMetrics);
        }
    }
}
