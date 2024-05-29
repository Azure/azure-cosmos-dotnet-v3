//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;

    internal class ReadFeedResponse<T> : FeedResponse<T>
    {
        internal ReadFeedResponse(
         HttpStatusCode httpStatusCode,
         IEnumerable<T> resources,
         int resourceCount,
         Headers responseMessageHeaders,
         CosmosDiagnostics diagnostics,
         RequestMessage requestMessage)
        {
            this.Count = resourceCount;
            this.Headers = responseMessageHeaders;
            this.StatusCode = httpStatusCode;
            this.Diagnostics = diagnostics;
            this.Resource = resources;
            this.RequestMessage = requestMessage;   
        }

        public override int Count { get; }

        public override string ContinuationToken => this.Headers?.ContinuationToken;

        public override Headers Headers { get; }

        public override IEnumerable<T> Resource { get; }

        public override HttpStatusCode StatusCode { get; }

        public override CosmosDiagnostics Diagnostics { get; }

        public override string IndexMetrics { get; }

        internal override RequestMessage RequestMessage { get; }

        public override IEnumerator<T> GetEnumerator()
        {
            return this.Resource.GetEnumerator();
        }

        internal static ReadFeedResponse<TInput> CreateResponse<TInput>(
            ResponseMessage responseMessage,
            CosmosSerializerCore serializerCore)
        {
            using (responseMessage)
            {
                // ReadFeed can return 304 on Change Feed responses
                if (responseMessage.StatusCode != HttpStatusCode.NotModified)
                {
                    responseMessage.EnsureSuccessStatusCode();
                }

                if (responseMessage.Content != null)
                {
                    _ = RewriteStreamAsTextAsync(responseMessage);
                }

                IReadOnlyCollection<TInput> resources = CosmosFeedResponseSerializer.FromFeedResponseStream<TInput>(
                        serializerCore,
                        responseMessage.Content);

                ReadFeedResponse<TInput> readFeedResponse = new ReadFeedResponse<TInput>(
                    httpStatusCode: responseMessage.StatusCode,
                    resources: resources,
                    resourceCount: resources.Count,
                    responseMessageHeaders: responseMessage.Headers,
                    diagnostics: responseMessage.Diagnostics,
                    requestMessage: responseMessage.RequestMessage);

                return readFeedResponse;
            }
        }

        public static Task RewriteStreamAsTextAsync(ResponseMessage responseMessage)
        {
            // Rewrite the payload to be in the specified format.
            // If it's already in the correct format, then the following will be a memcpy.
            MemoryStream memoryStream;
            if (responseMessage.Content is MemoryStream responseContentAsMemoryStream)
            {
                memoryStream = responseContentAsMemoryStream;
            }
            else
            {
                memoryStream = new MemoryStream();
                responseMessage.Content.CopyTo(memoryStream);
            }

            ReadOnlyMemory<byte> buffer;
            if (memoryStream.TryGetBuffer(out ArraySegment<byte> segment))
            {
                buffer = segment.Array.AsMemory().Slice(start: segment.Offset, length: segment.Count);
            }
            else
            {
                buffer = memoryStream.ToArray();
            }

            IJsonNavigator jsonNavigator = JsonNavigator.Create(buffer);
            if (jsonNavigator.SerializationFormat == JsonSerializationFormat.Text)
            {
                return Task.CompletedTask;
            }

            IJsonWriter jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);

            jsonNavigator.WriteNode(jsonNavigator.GetRootNode(), jsonWriter);

            ReadOnlyMemory<byte> result = jsonWriter.GetResult();
            MemoryStream rewrittenMemoryStream;
            if (MemoryMarshal.TryGetArray(result, out ArraySegment<byte> rewrittenSegment))
            {
                rewrittenMemoryStream = new MemoryStream(rewrittenSegment.Array, index: rewrittenSegment.Offset, count: rewrittenSegment.Count, writable: false, publiclyVisible: true);
            }
            else
            {
                byte[] toArray = result.ToArray();
                rewrittenMemoryStream = new MemoryStream(toArray, index: 0, count: toArray.Length, writable: false, publiclyVisible: true);
            }

            responseMessage.Content = rewrittenMemoryStream;
            return Task.CompletedTask;
        }
    } 
}