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
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
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
