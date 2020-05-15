//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Serializer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;
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
        /// <param name="cosmosSerializationOptions">The custom serialization options. This allows custom serialization types like BSON, JSON, or other formats</param>
        /// <returns>Returns a memory stream of cosmos elements. By default the memory stream will contain JSON.</returns>
        internal static CosmosArray ToCosmosElements(
            MemoryStream memoryStream,
            ResourceType resourceType,
            CosmosSerializationFormatOptions cosmosSerializationOptions = null)
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
            ReadOnlyMemory<byte> content;
            if (memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                content = buffer;
            }
            else
            {
                content = memoryStream.ToArray();
            }

            IJsonNavigator jsonNavigator;

            // Use the users custom navigator
            if (cosmosSerializationOptions != null)
            {
                jsonNavigator = cosmosSerializationOptions.CreateCustomNavigatorCallback(content);
                if (jsonNavigator == null)
                {
                    throw new InvalidOperationException("The CosmosSerializationOptions did not return a JSON navigator.");
                }
            }
            else
            {
                jsonNavigator = JsonNavigator.Create(content);
            }

            string resourceName = CosmosElementSerializer.GetRootNodeName(resourceType);

            CosmosArray documents;
            if ((jsonNavigator.SerializationFormat == JsonSerializationFormat.Binary) && jsonNavigator.TryGetObjectProperty(
                jsonNavigator.GetRootNode(),
                "stringDictionary",
                out ObjectProperty stringDictionaryProperty))
            {
                // Payload is string dictionary encode so we have to decode using the string dictionary.
                IJsonNavigatorNode stringDictionaryNode = stringDictionaryProperty.ValueNode;
                JsonStringDictionary jsonStringDictionary = JsonStringDictionary.CreateFromStringArray(
                    jsonNavigator
                        .GetArrayItems(stringDictionaryNode)
                        .Select(item => jsonNavigator.GetStringValue(item))
                        .ToList());

                if (!jsonNavigator.TryGetObjectProperty(
                    jsonNavigator.GetRootNode(),
                    resourceName,
                    out ObjectProperty resourceProperty))
                {
                    throw new InvalidOperationException($"Response Body Contract was violated. QueryResponse did not have property: {resourceName}");
                }

                IJsonNavigatorNode resources = resourceProperty.ValueNode;

                if (!jsonNavigator.TryGetBufferedBinaryValue(resources, out ReadOnlyMemory<byte> resourceBinary))
                {
                    resourceBinary = jsonNavigator.GetBinaryValue(resources);
                }

                IJsonNavigator navigatorWithStringDictionary = JsonNavigator.Create(resourceBinary, jsonStringDictionary);

                if (!(CosmosElement.Dispatch(
                    navigatorWithStringDictionary,
                    navigatorWithStringDictionary.GetRootNode()) is CosmosArray cosmosArray))
                {
                    throw new InvalidOperationException($"QueryResponse did not have an array of : {resourceName}");
                }

                documents = cosmosArray;
            }
            else
            {
                // Payload is not string dictionary encoded so we can just do for the documents as is.
                if (!jsonNavigator.TryGetObjectProperty(
                    jsonNavigator.GetRootNode(),
                    resourceName,
                    out ObjectProperty objectProperty))
                {
                    throw new InvalidOperationException($"Response Body Contract was violated. QueryResponse did not have property: {resourceName}");
                }

                if (!(CosmosElement.Dispatch(
                    jsonNavigator,
                    objectProperty.ValueNode) is CosmosArray cosmosArray))
                {
                    throw new InvalidOperationException($"QueryResponse did not have an array of : {resourceName}");
                }

                documents = cosmosArray;
            }

            return documents;
        }

        /// <summary>
        /// Converts a list of CosmosElements into a memory stream.
        /// </summary>
        /// <param name="containerRid">Container Rid</param>
        /// <param name="cosmosElements">The cosmos elements</param>
        /// <param name="resourceType">The resource type</param>
        /// <param name="cosmosSerializationOptions">The custom serialization options. This allows custom serialization types like BSON, JSON, or other formats</param>
        /// <returns>Returns a memory stream of cosmos elements. By default the memory stream will contain JSON.</returns>
        internal static MemoryStream ToStream(
            string containerRid,
            IEnumerable<CosmosElement> cosmosElements,
            ResourceType resourceType,
            CosmosSerializationFormatOptions cosmosSerializationOptions = null)
        {
            IJsonWriter jsonWriter;
            if (cosmosSerializationOptions != null)
            {
                jsonWriter = cosmosSerializationOptions.CreateCustomWriterCallback();
            }
            else
            {
                jsonWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();
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
            jsonWriter.WriteNumber64Value(count);

            jsonWriter.WriteObjectEnd();

            return GetMemoryStreamFromJsonWriter(jsonWriter);
        }

        internal static IReadOnlyList<T> GetResources<T>(
            IReadOnlyList<CosmosElement> cosmosArray,
            CosmosSerializerCore serializerCore)
        {
            if (cosmosArray == null)
            {
                return new List<T>();
            }

            if (typeof(CosmosElement).IsAssignableFrom(typeof(T)))
            {
                return cosmosArray.Cast<T>().ToList();
            }

            return CosmosElementSerializer.GetResourcesHelper<T>(
                cosmosArray,
                serializerCore);
        }

        internal static T[] GetResourcesHelper<T>(
            IReadOnlyList<CosmosElement> cosmosElements,
            CosmosSerializerCore serializerCore,
            CosmosSerializationFormatOptions cosmosSerializationOptions = null)
        {
            using (MemoryStream memoryStream = ElementsToMemoryStream(
                cosmosElements,
                cosmosSerializationOptions))
            {
                return serializerCore.FromFeedStream<T>(memoryStream);
            }
        }

        /// <summary>
        /// Converts a list of CosmosElements into a memory stream.
        /// </summary>
        /// <param name="cosmosElements">The cosmos elements</param>
        /// <param name="cosmosSerializationOptions">The custom serialization options. This allows custom serialization types like BSON, JSON, or other formats</param>
        /// <returns>Returns a memory stream of cosmos elements. By default the memory stream will contain JSON.</returns>
        internal static MemoryStream ElementsToMemoryStream(
            IReadOnlyList<CosmosElement> cosmosElements,
            CosmosSerializationFormatOptions cosmosSerializationOptions = null)
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

            foreach (CosmosElement element in cosmosElements)
            {
                element.WriteTo(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();

            return GetMemoryStreamFromJsonWriter(jsonWriter);
        }

        private static MemoryStream GetMemoryStreamFromJsonWriter(IJsonWriter jsonWriter)
        {
            ReadOnlyMemory<byte> result = jsonWriter.GetResult();
            if (!MemoryMarshal.TryGetArray(result, out ArraySegment<byte> resultAsArray))
            {
                resultAsArray = new ArraySegment<byte>(result.ToArray());
            }

            return new MemoryStream(
                buffer: resultAsArray.Array,
                index: resultAsArray.Offset,
                count: resultAsArray.Count,
                writable: false,
                publiclyVisible: true);
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
}
