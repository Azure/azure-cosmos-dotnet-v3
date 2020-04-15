// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Json.Interop;

    /// <summary>
    /// This feed iterator composes another feed iterator and converts all the responses to the user's deseried serialization format (or text by default),
    /// so that a user's deserializer can use it.
    /// </summary>
    internal sealed class FeedIteratorInternalSerializationConverter : FeedIteratorInternal
    {
        private readonly FeedIteratorInternal feedIterator;
        private readonly CosmosSerializationFormatOptions cosmosSerializationFormatOptions;

        public FeedIteratorInternalSerializationConverter(
            FeedIteratorInternal feedIterator,
            CosmosSerializationFormatOptions cosmosSerializationFormatOptions = null)
        {
            this.feedIterator = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));
            this.cosmosSerializationFormatOptions = cosmosSerializationFormatOptions;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override CosmosElement GetCosmosElementContinuationToken() => this.feedIterator.GetCosmosElementContinuationToken();

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);

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
                await responseMessage.Content.CopyToAsync(memoryStream);
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

            IJsonReader jsonReader = JsonReader.Create(buffer);
            IJsonWriter jsonWriter;
            if (this.cosmosSerializationFormatOptions != null)
            {
                jsonWriter = this.cosmosSerializationFormatOptions.CreateCustomWriterCallback();
            }
            else
            {
                jsonWriter = NewtonsoftToCosmosDBWriter.CreateTextWriter();
            }

            jsonWriter.WriteAll(jsonReader);

            ReadOnlyMemory<byte> result = jsonWriter.GetResult();
            MemoryStream rewrittenMemoryStream;
            if (MemoryMarshal.TryGetArray(result, out ArraySegment<byte> rewrittenSegment))
            {
                rewrittenMemoryStream = new MemoryStream(rewrittenSegment.Array, index: rewrittenSegment.Offset, count: rewrittenSegment.Count);
            }
            else
            {
                rewrittenMemoryStream = new MemoryStream(result.ToArray());
            }

            responseMessage.Content = rewrittenMemoryStream;
            return responseMessage;
        }
    }
}
