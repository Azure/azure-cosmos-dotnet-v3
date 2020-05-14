//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Serializer;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class ReadFeedResponse<T> : FeedResponse<T>
    {
        private static readonly byte StartArray = Encoding.UTF8.GetBytes("[")[0];
        private static readonly byte EndArray = Encoding.UTF8.GetBytes("]")[0];

        protected ReadFeedResponse(
            HttpStatusCode httpStatusCode,
            CosmosArray cosmosArray,
            CosmosSerializerCore serializerCore,
            Headers responseMessageHeaders,
            CosmosDiagnostics diagnostics)
        {
            this.Count = cosmosArray != null ? cosmosArray.Count : 0;
            this.Headers = responseMessageHeaders;
            this.StatusCode = httpStatusCode;
            this.Diagnostics = diagnostics;
            this.Resource = CosmosElementSerializer.GetResources<T>(
                cosmosArray: cosmosArray,
                serializerCore: serializerCore);
        }

        protected ReadFeedResponse(
            HttpStatusCode httpStatusCode,
            List<T> items,
            Headers responseMessageHeaders,
            CosmosDiagnostics diagnostics)
        {
            this.Count = items != null ? items.Count : 0;
            this.Headers = responseMessageHeaders;
            this.StatusCode = httpStatusCode;
            this.Diagnostics = diagnostics;
            this.Resource = items;
        }

        public override int Count { get; }

        public override string ContinuationToken => this.Headers?.ContinuationToken;

        public override Headers Headers { get; }

        public override IEnumerable<T> Resource { get; }

        public override HttpStatusCode StatusCode { get; }

        public override CosmosDiagnostics Diagnostics { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        internal static ReadFeedResponse<TInput> CreateResponse<TInput>(
            ResponseMessage responseMessage,
            CosmosSerializerCore serializerCore,
            Documents.ResourceType resourceType,
            bool simpleSerializer = false)
        {
            using (responseMessage)
            {
                // ReadFeed can return 304 on some scenarios (Change Feed for example)
                if (responseMessage.StatusCode != HttpStatusCode.NotModified)
                {
                    responseMessage.EnsureSuccessStatusCode();
                }

                if (!simpleSerializer)
                {
                    CosmosArray cosmosArray = null;
                    if (responseMessage.Content != null)
                    {
                        cosmosArray = CosmosElementSerializer.ToCosmosElements(
                            responseMessage.Content,
                            resourceType,
                            null);
                    }

                    ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                        httpStatusCode: responseMessage.StatusCode,
                        cosmosArray: cosmosArray,
                        serializerCore: serializerCore,
                        responseMessageHeaders: responseMessage.Headers,
                        diagnostics: responseMessage.Diagnostics);

                    return readFeedResponse;
                }

                List<TInput> items = null;
                if (responseMessage.Content != null)
                {
                    MemoryStream memoryStream = (MemoryStream)responseMessage.Content;
                    ReadOnlyMemory<byte> content;
                    if (memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
                    {
                        content = buffer;
                    }
                    else
                    {
                        content = memoryStream.ToArray();
                    }

                    int start = GetArrayStartPosition(content);
                    int end = GetArrayEndPosition(content);

                    ReadOnlyMemory<byte> spanwithOnlyArray = content.Slice(start, end - start + 1);
                    if (!MemoryMarshal.TryGetArray(spanwithOnlyArray, out ArraySegment<byte> resultAsArray))
                    {
                        resultAsArray = new ArraySegment<byte>(spanwithOnlyArray.ToArray());
                    }

                    MemoryStream arrayOnlyStream = new MemoryStream(resultAsArray.Array, resultAsArray.Offset, resultAsArray.Count);
                    arrayOnlyStream.Position = 0;
                    items = serializerCore.FromStream<List<TInput>>(arrayOnlyStream);
                }

                return new ReadFeedResponse<TInput>(
                        httpStatusCode: responseMessage.StatusCode,
                        items: items,
                        responseMessageHeaders: responseMessage.Headers,
                        diagnostics: responseMessage.Diagnostics);
            }
        }

        private static int GetArrayStartPosition(ReadOnlyMemory<byte> memoryByte)
        {
            ReadOnlySpan<byte> span = memoryByte.Span;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == StartArray)
                {
                    return i;
                }
            }

            throw new ArgumentException("ArrayStart not found");
        }

        private static int GetArrayEndPosition(ReadOnlyMemory<byte> memoryByte)
        {
            ReadOnlySpan<byte> span = memoryByte.Span;
            for (int i = span.Length - 1; i < span.Length; i--)
            {
                if (span[i] == EndArray)
                {
                    return i;
                }
            }

            throw new ArgumentException("ArrayEnd not found");
        }
    }
}