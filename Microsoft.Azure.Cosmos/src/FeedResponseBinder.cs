//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        public static DocumentFeedResponse<T> Convert<T>(DocumentFeedResponse<dynamic> dynamicFeed)
        {
            if (typeof(T) == typeof(object))
            {
                return (DocumentFeedResponse<T>)(object)dynamicFeed;
            }
            IList<T> result = new List<T>();

            foreach (T item in dynamicFeed)
            {
                result.Add(item);
            }

            return new DocumentFeedResponse<T>(
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
        /// Todo: This method can be optimized by not writing the result out to text.
        /// </summary>
        public static DocumentFeedResponse<T> ConvertCosmosElementFeed<T>(
            DocumentFeedResponse<CosmosElement> dynamicFeed,
            ResourceType resourceType,
            JsonSerializerSettings settings)
        {
            if (dynamicFeed.Count == 0)
            {
                return new DocumentFeedResponse<T>(
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

            ReadOnlyMemory<byte> buffer = jsonWriter.GetResult();
            string jsonText = Utf8StringHelpers.ToString(buffer);

            IEnumerable<T> typedResults;

            // If the resource type is an offer and the requested type is either a Offer or OfferV2 or dynamic
            // create a OfferV2 object and cast it to T. This is a temporary fix until offers is moved to v3 API. 
            if (resourceType == ResourceType.Offer &&
                (typeof(T).IsSubclassOf(typeof(Documents.Resource)) || typeof(T) == typeof(object)))
            {
                typedResults = JsonConvert.DeserializeObject<List<OfferV2>>(jsonText, settings).Cast<T>();
            }
            else
            {
                typedResults = JsonConvert.DeserializeObject<List<T>>(jsonText, settings);
            }

            return new DocumentFeedResponse<T>(
                typedResults,
                dynamicFeed.Count,
                dynamicFeed.Headers,
                dynamicFeed.UseETagAsContinuation,
                dynamicFeed.QueryMetrics,
                dynamicFeed.RequestStatistics,
                dynamicFeed.DisallowContinuationTokenMessage,
                dynamicFeed.ResponseLengthBytes);
        }
    }
}
