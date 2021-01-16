// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.IO;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Documents;

    internal static class RestFeedResponseParser
    {
        public static CosmosArray ParseRestFeedResponse(
            Stream stream, 
            ContentSerializationFormatOptions jsonSerializationFormatOptions)
        {
            return ParseRestFeedResponse(stream, ResourceType.Document, jsonSerializationFormatOptions);
        }

        public static CosmosArray ParseRestFeedResponse(
            Stream stream, 
            ResourceType resourceType,
            ContentSerializationFormatOptions jsonSerializationFormatOptions)
        {
            // Parse out the document from the REST response like this:
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
            // You want to create a CosmosElement for each document in "Documents".

            ReadOnlyMemory<byte> content = StreamToBytes(stream);
            IJsonNavigator jsonNavigator = CreateNavigatorFromContent(content, jsonSerializationFormatOptions);
            string arrayKeyName = ResourceTypeToArrayKeyName(resourceType);
            return GetResourceArrayFromNavigator(jsonNavigator, arrayKeyName);
        }

        private static ReadOnlyMemory<byte> StreamToBytes(Stream stream)
        {
            if (!(stream is MemoryStream memoryStream))
            {
                memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
            }

            if (!memoryStream.CanRead)
            {
                throw new InvalidDataException("Stream can not be read");
            }

            ReadOnlyMemory<byte> content = memoryStream.TryGetBuffer(out ArraySegment<byte> buffer) ? buffer : (ReadOnlyMemory<byte>)memoryStream.ToArray();
            return content;
        }

        private static IJsonNavigator CreateNavigatorFromContent(ReadOnlyMemory<byte> content, ContentSerializationFormatOptions jsonSerializationFormatOptions)
        {
            IJsonNavigator jsonNavigator;
            if (jsonSerializationFormatOptions != null)
            {
                if (jsonSerializationFormatOptions is ContentSerializationFormatOptions.CustomJsonSerializationFormatOptions customOptions)
                {
                    jsonNavigator = customOptions.createNavigator(content);
                    if (jsonNavigator == null)
                    {
                        throw new InvalidOperationException("The CosmosSerializationOptions did not return a JSON navigator.");
                    }
                }
                else if (jsonSerializationFormatOptions is ContentSerializationFormatOptions.NativelySupportedJsonSerializationFormatOptions)
                {
                    jsonNavigator = JsonNavigator.Create(content);
                }
                else
                {
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(ContentSerializationFormatOptions)} type: {jsonSerializationFormatOptions.GetType()}");
                }
            }
            else
            {
                jsonNavigator = JsonNavigator.Create(content);
            }

            return jsonNavigator;
        }

        private static string ResourceTypeToArrayKeyName(ResourceType resourceType)
        {
            return resourceType switch
            {
                ResourceType.Database => "Databases",
                ResourceType.Collection => "DocumentCollections",
                ResourceType.Document => "Documents",
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(resourceType)}: {resourceType}"),
            };
        }
        
        private static CosmosArray GetResourceArrayFromNavigator(
            IJsonNavigator jsonNavigator, 
            string arrayKeyName)
        {
            if (!jsonNavigator.TryGetObjectProperty(
                jsonNavigator.GetRootNode(),
                arrayKeyName,
                out ObjectProperty objectProperty))
            {
                throw new InvalidOperationException($"Response Body Contract was violated. FeedResponse did not have property: {arrayKeyName}");
            }

            if (!(CosmosElement.Dispatch(
                jsonNavigator,
                objectProperty.ValueNode) is CosmosArray cosmosArray))
            {
                throw new InvalidOperationException($"FeedResponse did not have an array of : {arrayKeyName}");
            }

            return cosmosArray;
        }
    }
}
