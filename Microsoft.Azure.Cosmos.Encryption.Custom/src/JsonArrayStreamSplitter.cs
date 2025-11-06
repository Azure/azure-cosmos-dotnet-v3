//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Threading;
    using Microsoft.IO;

    internal static class JsonArrayStreamSplitter
    {
        private const int DefaultBufferSize = 8192;
        private const int MaxBufferSize = 64 * 1024 * 1024;

        private static readonly RecyclableMemoryStreamManager RecyclableMemoryStreamManager = new ();

        /// <summary>
        /// Splits a JSON array stream into separate objects, returning each as a MemoryStream.
        /// </summary>
        /// <param name="jsonArrayStream">The input stream containing a Cosmos DB response object with a Documents array.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An enumerable of MemoryStream objects, each containing a single JSON object.</returns>
        /// <remarks>
        /// Callers MUST dispose the returned MemoryStream instances to return them to the pool.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "The method returns IAsyncEnumerable.")]
        public static async IAsyncEnumerable<MemoryStream> SplitIntoSubstreamsAsync(
            Stream jsonArrayStream,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            if (jsonArrayStream == null)
            {
                throw new ArgumentNullException(nameof(jsonArrayStream));
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            RecyclableMemoryStream currentDocumentStream = null;

            try
            {
                JsonReaderState readerState = new (new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                JsonArrayTraversalState traversalState = JsonArrayTraversalState.CreateInitial();

                int leftOver = 0;
                bool isFinalBlock = false;

                while (!isFinalBlock)
                {
                    int bytesRead = await jsonArrayStream.ReadAsync(buffer, leftOver, buffer.Length - leftOver, cancellationToken).ConfigureAwait(false);
                    int dataLength = leftOver + bytesRead;
                    isFinalBlock = bytesRead == 0;

                    cancellationToken.ThrowIfCancellationRequested();

                    ProcessResult result = JsonFeedStreamHelper.ProcessChunk(
                        buffer.AsSpan(0, dataLength),
                        isFinalBlock,
                        ref readerState,
                        ref traversalState,
                        writeEnvelopeSegment: null,
                        writeObjectSegment: WriteObjectSegment);

                    leftOver = dataLength - result.BytesConsumed;
                    buffer = JsonFeedStreamHelper.HandleLeftOver(buffer, dataLength, leftOver, result.BytesConsumed, MaxBufferSize);

                    if (isFinalBlock && leftOver > 0)
                    {
                        isFinalBlock = false;
                    }

                    if (result.ObjectCompleted)
                    {
                        if (currentDocumentStream == null)
                        {
                            throw new InvalidOperationException("Document payload was not captured.");
                        }

                        currentDocumentStream.Position = 0;
                        yield return currentDocumentStream;
                        currentDocumentStream = null;
                    }
                }
            }
            finally
            {
                currentDocumentStream?.Dispose();
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
            }

            void WriteObjectSegment(ReadOnlySpan<byte> segment)
            {
                if (segment.IsEmpty)
                {
                    return;
                }

                currentDocumentStream ??= RecyclableMemoryStreamManager.GetStream("JsonArrayStreamSplitter");
                currentDocumentStream.Write(segment);
            }
        }
    }
}
