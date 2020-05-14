//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;

#nullable enable
    internal static class CosmosFeedResponseSerializer
    {
        private static readonly byte ArrayStart = Encoding.UTF8.GetBytes("[")[0];
        private static readonly byte ArrayEnd = Encoding.UTF8.GetBytes("]")[0];

        /// <summary>
        /// The service returns feed responses in an envelope. This removes the envelope
        /// and serializes all the items into a list
        /// </summary>
        /// <param name="serializerCore">The cosmos serializer</param>
        /// <param name="streamWithServiceEnvelope">A stream with the service envelope like: { "ContainerRid":"Test", "Documents":[{ "id":"MyItem"}], "count":1}</param>
        /// <param name="resourceType">The resource type</param>
        /// <returns>A read only list of the serialized items</returns>
        internal static IReadOnlyList<T> FromFeedResponseStream<T>(
            CosmosSerializerCore serializerCore,
            Stream streamWithServiceEnvelope,
            Documents.ResourceType resourceType)
        {
            if (streamWithServiceEnvelope == null)
            {
                return new List<T>();
            }

            using (MemoryStream stream = GetStreamWithoutServiceEnvelope(
                streamWithServiceEnvelope,
                resourceType))
            {
                return serializerCore.FromStream<List<T>>(stream);
            }
        }

        /// <summary>
        /// The service returns feed responses in an envelope. This removes the envelope
        /// so it only returns the array of items.
        /// </summary>
        /// <param name="streamWithServiceEnvelope">A stream with the service envelope like: { "ContainerRid":"Test", "Documents":[{ "id":"MyItem"}], "count":1}</param>
        /// <param name="resourceType">The resource type</param>
        /// <returns>A stream containing only the array of items</returns>
        internal static MemoryStream GetStreamWithoutServiceEnvelope(
            Stream streamWithServiceEnvelope,
            Documents.ResourceType resourceType)
        {
            ReadOnlyMemory<byte> content;
            MemoryStream? memoryStreamWithEnvelope = streamWithServiceEnvelope as MemoryStream;
            if (memoryStreamWithEnvelope == null)
            {
                memoryStreamWithEnvelope = new MemoryStream();
                streamWithServiceEnvelope.CopyTo(memoryStreamWithEnvelope);
                memoryStreamWithEnvelope.Position = 0;
            }

            if (memoryStreamWithEnvelope.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                content = buffer;
            }
            else
            {
                content = memoryStreamWithEnvelope.ToArray();
            }

            int start = GetArrayStartPosition(content, resourceType);
            int end = GetArrayEndPosition(content, resourceType);

            ReadOnlyMemory<byte> spanwithOnlyArray = content.Slice(start, end - start + 1);
            if (!MemoryMarshal.TryGetArray(spanwithOnlyArray, out ArraySegment<byte> resultAsArray))
            {
                resultAsArray = new ArraySegment<byte>(spanwithOnlyArray.ToArray());
            }

            MemoryStream arrayOnlyStream = new MemoryStream(resultAsArray.Array, resultAsArray.Offset, resultAsArray.Count);
            return arrayOnlyStream;
        }

        private static int GetArrayStartPosition(
            ReadOnlyMemory<byte> memoryByte,
            Documents.ResourceType resourceType)
        {
            ReadOnlySpan<byte> span = memoryByte.Span;

            // This is an optimization for documents to check the
            // default envelope position for the array start
            int defaultDocumentArrayStartPosition = 35;
            if (resourceType == Documents.ResourceType.Document &&
                span[defaultDocumentArrayStartPosition] == ArrayStart)
            {
                return defaultDocumentArrayStartPosition;
            }

            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == ArrayStart)
                {
                    return i;
                }
            }

            throw new ArgumentException("ArrayStart not found");
        }

        private static int GetArrayEndPosition(
            ReadOnlyMemory<byte> memoryByte,
            Documents.ResourceType resourceType)
        {
            ReadOnlySpan<byte> span = memoryByte.Span;

            // This is an optimization for documents to check the
            // default envelope position for the array start
            int defaultDocumentArrayEndPosition = span.Length - 13;
            if (resourceType == Documents.ResourceType.Document &&
                span[defaultDocumentArrayEndPosition] == ArrayStart)
            {
                return defaultDocumentArrayEndPosition;
            }

            for (int i = span.Length - 1; i < span.Length; i--)
            {
                if (span[i] == ArrayEnd)
                {
                    return i;
                }
            }

            throw new ArgumentException("ArrayEnd not found");
        }
    }
}
