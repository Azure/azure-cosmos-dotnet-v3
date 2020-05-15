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
        /// <returns>A read only list of the serialized items</returns>
        internal static IReadOnlyList<T> FromFeedResponseStream<T>(
            CosmosSerializerCore serializerCore,
            Stream streamWithServiceEnvelope)
        {
            if (streamWithServiceEnvelope == null)
            {
                return new List<T>();
            }

            using (streamWithServiceEnvelope)
            using (MemoryStream stream = GetStreamWithoutServiceEnvelope(
                            streamWithServiceEnvelope))
            {
                return serializerCore.FromFeedStream<T>(stream);
            }
        }

        /// <summary>
        /// The service returns feed responses in an envelope. This removes the envelope
        /// so it only returns the array of items.
        /// </summary>
        /// <param name="streamWithServiceEnvelope">A stream with the service envelope like: { "ContainerRid":"Test", "Documents":[{ "id":"MyItem"}], "count":1}</param>
        /// <returns>A stream containing only the array of items</returns>
        internal static MemoryStream GetStreamWithoutServiceEnvelope(
            Stream streamWithServiceEnvelope)
        {
            using (streamWithServiceEnvelope)
            {
                ReadOnlyMemory<byte> content;
                if (!(streamWithServiceEnvelope is MemoryStream memoryStreamWithEnvelope))
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

                int start = content.Span.IndexOf(ArrayStart);
                int end = GetArrayEndPosition(content);

                ReadOnlyMemory<byte> spanwithOnlyArray = content.Slice(
                    start: start,
                    length: end - start + 1);

                if (!MemoryMarshal.TryGetArray(spanwithOnlyArray, out ArraySegment<byte> resultAsArray))
                {
                    resultAsArray = new ArraySegment<byte>(spanwithOnlyArray.ToArray());
                }

                MemoryStream arrayOnlyStream = new MemoryStream(resultAsArray.Array, resultAsArray.Offset, resultAsArray.Count, writable: false, publiclyVisible: true);
                return arrayOnlyStream;
            }
        }

        private static int GetArrayEndPosition(
            ReadOnlyMemory<byte> memoryByte)
        {
            ReadOnlySpan<byte> span = memoryByte.Span;
            for (int i = span.Length - 1; i < span.Length; i--)
            {
                if (span[i] == ArrayEnd)
                {
                    return i;
                }
            }

            string response = Encoding.UTF8.GetString(span);
            throw new InvalidDataException($"Could not find the end of the json array in the stream: {response}");
        }
    }
}
