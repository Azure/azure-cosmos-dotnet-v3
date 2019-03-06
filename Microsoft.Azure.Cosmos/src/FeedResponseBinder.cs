//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

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

            IList<T> typedResults = new List<T>(dynamicFeed.Count);
            if (dynamicFeed.Count > 0 && IsCosmosElement(dynamicFeed.First().GetType()))
            {
                // We know all the items are LazyCosmosElements
                foreach (CosmosElement cosmosElement in dynamicFeed)
                {
                    // For now we will just to string the whole thing and have newtonsoft do the deserializaiton
                    // TODO: in the future we should deserialize using the LazyCosmosElement.
                    // this is temporary. Once we finished stream api we will get rid of this typed api and have the stream call into that.
                    T typedValue;
                    switch (cosmosElement.Type)
                    {
                        case CosmosElementType.String:
                            typedValue = JToken.FromObject((cosmosElement as CosmosString).Value)
                                .ToObject<T>();
                            break;
                        case CosmosElementType.Number:
                            typedValue = JToken.FromObject((cosmosElement as CosmosNumber).GetValueAsDouble())
                               .ToObject<T>();
                            break;
                        case CosmosElementType.Object:
                            typedValue = JsonConvert.DeserializeObject<T>((cosmosElement as LazyCosmosObject).ToString());
                            break;
                        case CosmosElementType.Array:
                            typedValue = JsonConvert.DeserializeObject<T>((cosmosElement as LazyCosmosArray).ToString());
                            break;
                        case CosmosElementType.Boolean:
                            typedValue = JToken.FromObject((cosmosElement as CosmosBoolean).Value)
                               .ToObject<T>();
                            break;
                        case CosmosElementType.Null:
                            typedValue = JValue.CreateNull().ToObject<T>();
                            break;
                        default:
                            throw new ArgumentException($"Unexpected {nameof(CosmosElementType)}: {cosmosElement.Type}");
                    }

                    typedResults.Add(typedValue);
                }
            }
            else
            {
                foreach (dynamic item in dynamicFeed)
                {
                    typedResults.Add(JsonConvert.DeserializeObject<T>(JToken.FromObject(item).ToString()));
                }
            }

            return new FeedResponse<T>(
                typedResults,
                dynamicFeed.Count,
                dynamicFeed.Headers,
                dynamicFeed.UseETagAsContinuation,
                dynamicFeed.QueryMetrics,
                dynamicFeed.RequestStatistics,
                responseLengthBytes: dynamicFeed.ResponseLengthBytes);
        }

        public static IQueryable<T> AsQueryable<T>(FeedResponse<dynamic> dynamicFeed)
        {
            FeedResponse<T> response = FeedResponseBinder.Convert<T>(dynamicFeed);
            return response.AsQueryable<T>();
        }

        private static bool IsCosmosElement(Type type)
        {
            return (
                (type == typeof(CosmosElement))
                || type.BaseType == typeof(CosmosTrue)
                || type.BaseType == typeof(CosmosFalse)
                || type.BaseType == typeof(CosmosArray)
                || type.BaseType == typeof(CosmosObject)
                || type.BaseType == typeof(CosmosString)
                || type.BaseType == typeof(CosmosNumber)
                || type.BaseType == typeof(CosmosBoolean)
                || type.BaseType == typeof(CosmosNull));
        }
    }
}
