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
    using System.Text;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Serializer;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class ReadFeedResponse<T> : FeedResponse<T>
    {
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

                List<TInput> items = new List<TInput>();
                if (responseMessage.Content != null)
                {
                    using (JsonDocument jDoc = JsonDocument.Parse(responseMessage.Content))
                    {
                        foreach (JsonProperty property in jDoc.RootElement.EnumerateObject())
                        {
                            if (property.Value.ValueKind == JsonValueKind.Array)
                            {
                                byte[] rented = ArrayPool<byte>.Shared.Rent((int)responseMessage.Content.Length);
                                int written = 0;
                                try
                                {
                                    using (MemoryStream memory = new MemoryStream(rented, 0, rented.Length, true, true))
                                    {
                                        using (Utf8JsonWriter jsonWriter = new Utf8JsonWriter(memory))
                                        {
                                            property.Value.WriteTo(jsonWriter);
                                        }

                                        written = (int)memory.Position;
                                        memory.Position = 0;
                                        items = serializerCore.FromStream<List<TInput>>(memory);
                                    }
                                }
                                finally
                                {
                                    rented.AsSpan(0, written).Clear();
                                    ArrayPool<byte>.Shared.Return(rented);
                                }

                                break;
                            }
                        }
                    }
                }

                return new ReadFeedResponse<TInput>(
                        httpStatusCode: responseMessage.StatusCode,
                        items: items,
                        responseMessageHeaders: responseMessage.Headers,
                        diagnostics: responseMessage.Diagnostics);
            }
        }
    }
}