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

    internal static class JsonArrayStreamSplitter
    {
        private const int DefaultBufferSize = 4096;
        private const int MaxBufferSize = 64 * 1024 * 1024;

        /// <summary>
        /// Splits a JSON array stream into separate objects, returning each as a Stream.
        /// </summary>
        /// <param name="jsonArrayStream">The input stream containing a Cosmos DB response object with a Documents array.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An enumerable of Stream objects, each containing a single JSON object.</returns>
        /// <remarks>
        /// <para>
        /// Callers MUST dispose every yielded <see cref="Stream"/> instance. Each yielded stream is a
        /// <see cref="PooledMemoryStream"/> that holds a buffer rented from <see cref="System.Buffers.ArrayPool{T}"/>.
        /// </para>
        /// <para>
        /// <strong>Security-relevant:</strong> downstream code typically decrypts each yielded stream's
        /// contents in place. If the consumer abandons iteration without disposing yielded streams,
        /// the rented buffers (which may contain plaintext) are never returned to the pool, never zeroed,
        /// and remain reachable in the heap until GC reclaims them. The CLR does not zero freed memory
        /// before reuse, so plaintext fragments can persist until that memory is overwritten.
        /// </para>
        /// <para>
        /// Always wrap consumption in <c>using</c> / <c>await using</c> blocks, and ensure that any
        /// wrapper objects (such as <c>StreamDecryptableItem</c>) are themselves disposed.
        /// </para>
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods", Justification = "The method returns IAsyncEnumerable.")]
        public static async IAsyncEnumerable<Stream> SplitIntoSubstreamsAsync(
            Stream jsonArrayStream,
            [EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            if (jsonArrayStream == null)
            {
                throw new ArgumentNullException(nameof(jsonArrayStream));
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
            PooledMemoryStream currentDocumentStream = null;

            try
            {
                JsonReaderState readerState = new (new JsonReaderOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                JsonArrayTraversalState traversalState = JsonArrayTraversalState.CreateInitial();

                int leftOver = 0;
                bool isFinalBlock = false;

                JsonSegmentWriter writeSegment = (segment, insideDocument) =>
                    currentDocumentStream = WriteSegment(segment, insideDocument, currentDocumentStream);

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
                        writeSegment);

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
        }

        private static PooledMemoryStream WriteSegment(
            ReadOnlySpan<byte> segment,
            bool insideDocument,
            PooledMemoryStream currentDocumentStream)
        {
            if (!insideDocument)
            {
                return currentDocumentStream;
            }

            currentDocumentStream ??= new PooledMemoryStream();
            currentDocumentStream.Write(segment);
            return currentDocumentStream;
        }
    }
}
