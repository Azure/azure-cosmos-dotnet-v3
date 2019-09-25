//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Documents;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    static class CosmosElementSerializer
    {
        /// <summary>
        /// Converts a list of CosmosElements into a memory stream.
        /// </summary>
        /// <param name="memoryStream">The memory stream response from Azure Cosmos</param>
        /// <param name="resourceType">The resource type</param>
        /// <param name="createJsonNavigator">The custom jsonNavigator. This allows custom serialization types like BSON, JSON, or other formats</param>
        /// <returns>Returns a memory stream of cosmos elements. By default the memory stream will contain JSON.</returns>
        internal static CosmosArray ToCosmosElements(
            MemoryStream memoryStream,
            ResourceType resourceType,
            CosmosSerializationFormatOptions.CreateCustomNavigator createJsonNavigator)
        {
            if (!memoryStream.CanRead)
            {
                throw new InvalidDataException("Stream can not be read");
            }

            // Execute the callback an each element of the page
            // For example just could get a response like this
            // {
            //    "_rid": "qHVdAImeKAQ=",
            //    "Documents": [{
            //        "id": "03230",
            //        "_rid": "qHVdAImeKAQBAAAAAAAAAA==",
            //        "_self": "dbs\/qHVdAA==\/colls\/qHVdAImeKAQ=\/docs\/qHVdAImeKAQBAAAAAAAAAA==\/",
            //        "_etag": "\"410000b0-0000-0000-0000-597916b00000\"",
            //        "_attachments": "attachments\/",
            //        "_ts": 1501107886
            //    }],
            //    "_count": 1
            // }
            // And you should execute the callback on each document in "Documents".

            long responseLengthBytes = memoryStream.Length;
            byte[] content = memoryStream.ToArray();

            IJsonNavigator jsonNavigator = null;
            // Use the users custom navigator
            if (createJsonNavigator == null)
            {
                jsonNavigator = createJsonNavigator(content);
            }
            else
            {
                jsonNavigator = JsonNavigator.Create(content);
            }

            string resourceName = CosmosElementSerializer.GetRootNodeName(resourceType);

            if (!jsonNavigator.TryGetObjectProperty(
                jsonNavigator.GetRootNode(),
                resourceName,
                out ObjectProperty objectProperty))
            {
                throw new InvalidOperationException($"Response Body Contract was violated. QueryResponse did not have property: {resourceName}");
            }

            IJsonNavigatorNode cosmosElements = objectProperty.ValueNode;
            if (!(CosmosElement.Dispatch(
                jsonNavigator,
                cosmosElements) is CosmosArray cosmosArray))
            {
                throw new InvalidOperationException($"QueryResponse did not have an array of : {resourceName}");
            }

            return cosmosArray;
        }

        /// <summary>
        /// Converts a list of CosmosElements into a memory stream.
        /// </summary>
        /// <param name="containerRid">Container Rid</param>
        /// <param name="cosmosElements">The cosmos elements</param>
        /// <param name="resourceType">The resource type</param>
        /// <param name="jsonWriter">The custom jsonWriter. This allows custom serialization types like BSON, JSON, or other formats</param>
        /// <returns>Returns a memory stream of cosmos elements. By default the memory stream will contain JSON.</returns>
        internal static Stream ToStream(
            string containerRid,
            IEnumerable<CosmosElement> cosmosElements,
            ResourceType resourceType,
            IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            }

            // The stream contract should return the same contract as read feed.
            // {
            //    "_rid": "qHVdAImeKAQ=",
            //    "Documents": [{
            //        "id": "03230",
            //        "_rid": "qHVdAImeKAQBAAAAAAAAAA==",
            //        "_self": "dbs\/qHVdAA==\/colls\/qHVdAImeKAQ=\/docs\/qHVdAImeKAQBAAAAAAAAAA==\/",
            //        "_etag": "\"410000b0-0000-0000-0000-597916b00000\"",
            //        "_attachments": "attachments\/",
            //        "_ts": 1501107886
            //    }],
            //    "_count": 1
            // }

            jsonWriter.WriteObjectStart();

            // Write the rid field and value
            jsonWriter.WriteFieldName("_rid");
            jsonWriter.WriteStringValue(containerRid);

            // Write the array of elements
            string rootName = CosmosElementSerializer.GetRootNodeName(resourceType);
            jsonWriter.WriteFieldName(rootName);

            int count = 0;
            jsonWriter.WriteArrayStart();
            foreach (CosmosElement element in cosmosElements)
            {
                count++;
                element.WriteTo(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();

            // Write the count field and value
            jsonWriter.WriteFieldName("_count");
            jsonWriter.WriteNumberValue(count);

            jsonWriter.WriteObjectEnd();

            return new MemoryStream(jsonWriter.GetResult());
        }

        /// <summary>
        /// Converts a list of CosmosElements into a list of objects.
        /// </summary>
        /// <param name="containerRid">Container Rid</param>
        /// <param name="cosmosElements">The cosmos elements</param>
        /// <param name="resourceType">The resource type</param>
        /// <param name="jsonSerializer">The JSON </param>
        /// <param name="jsonWriter">The custom jsonWriter. This allows custom serialization types like BSON, JSON, or other formats</param>
        /// <returns>Returns a list of deserialized objects</returns>
        internal static IEnumerable<T> Deserialize<T>(
            string containerRid,
            IEnumerable<CosmosElement> cosmosElements,
            ResourceType resourceType,
            CosmosSerializer jsonSerializer,
            IJsonWriter jsonWriter = null)
        {
            if (!cosmosElements.Any())
            {
                return Enumerable.Empty<T>();
            }

            Stream stream = CosmosElementSerializer.ToStream(
                containerRid,
                cosmosElements,
                resourceType,
                jsonWriter);

            IEnumerable<T> typedResults = jsonSerializer.FromStream<CosmosFeedResponseUtil<T>>(stream).Data;

            return typedResults;
        }

        private static string GetRootNodeName(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case Documents.ResourceType.Collection:
                    return "DocumentCollections";
                default:
                    return resourceType.ToResourceTypeString() + "s";
            }
        }
    }
#if INTERNAL
#pragma warning restore SA1600 // Elements should be documented
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
#endif
}
