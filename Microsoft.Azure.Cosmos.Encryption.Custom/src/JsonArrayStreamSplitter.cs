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
    using System.Threading.Tasks;
    using Microsoft.IO;

    internal static class JsonArrayStreamSplitter
    {
        private const int DefaultBufferSize = 8192;
        private const int MaxBufferSize = 64 * 1024 * 1024;
        private const int InitialStreamCapacity = 4;

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
            int currentBufferSize = buffer.Length;
            try
            {
                StreamReadState state = new ();

                while (true)
                {
                    if (state.TotalBytesInBuffer == currentBufferSize)
                    {
                        buffer = GrowBuffer(buffer, state.TotalBytesInBuffer, ref currentBufferSize);
                    }

                    int bytesRead = await ReadMoreDataAsync(
                        jsonArrayStream,
                        buffer,
                        state.TotalBytesInBuffer,
                        cancellationToken).ConfigureAwait(false);
                    state.TotalBytesInBuffer += bytesRead;

                    if (state.TotalBytesInBuffer == 0 && bytesRead == 0)
                    {
                        yield break;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    bool isFinalBlock = bytesRead == 0;

                    while (true)
                    {
                        JsonReaderResult result;
                        try
                        {
                            result = ProcessBuffer(buffer, state.TotalBytesInBuffer, isFinalBlock, ref state);
                        }
                        catch (JsonException) when (!isFinalBlock)
                        {
                            if (state.TotalBytesInBuffer == currentBufferSize)
                            {
                                buffer = GrowBuffer(buffer, state.TotalBytesInBuffer, ref currentBufferSize);
                            }

                            break;
                        }

                        List<MemoryStream> extractedStreams = state.ExtractedStreams;
                        for (int i = 0; i < result.ExtractedStreamCount; i++)
                        {
                            yield return extractedStreams[i];
                        }

                        extractedStreams.Clear();

                        if (result.BytesConsumed > 0)
                        {
                            state.TotalBytesInBuffer = ManageBuffer(buffer, result.BytesConsumed, state.TotalBytesInBuffer);

                            if (state.PendingObjectStart >= 0)
                            {
                                state.PendingObjectStart = Math.Max(0, state.PendingObjectStart - (int)result.BytesConsumed);
                            }

                            if (state.PendingObjectStream != null)
                            {
                                state.PendingObjectNextCopyIndex = Math.Max(0, state.PendingObjectNextCopyIndex - (int)result.BytesConsumed);
                            }
                        }

                        if (result.EndOfArray && state.TotalBytesInBuffer == 0 && isFinalBlock)
                        {
                            yield break;
                        }

                        if (state.TotalBytesInBuffer == 0)
                        {
                            break;
                        }

                        if (result.BytesConsumed == 0)
                        {
                            break;
                        }
                    }

                    if (isFinalBlock && state.TotalBytesInBuffer == 0)
                    {
                        break;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer, true);
            }
        }

        private static async Task<int> ReadMoreDataAsync(
            Stream stream,
            byte[] buffer,
            int currentDataLength,
            CancellationToken cancellationToken)
        {
            int remainingSpace = buffer.Length - currentDataLength;
            if (remainingSpace <= 0)
            {
                return 0;
            }

            return await stream.ReadAsync(buffer, currentDataLength, remainingSpace, cancellationToken).ConfigureAwait(false);
        }

        private static JsonReaderResult ProcessBuffer(byte[] buffer, int bufferLength, bool isFinalBlock, ref StreamReadState state)
        {
            List<MemoryStream> extractedStreams = state.ExtractedStreams ??= new List<MemoryStream>(InitialStreamCapacity);
            extractedStreams.Clear();
            state.ResponseCompleted = false;

            ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(buffer, 0, bufferLength);
            Utf8JsonReader reader = new (data, isFinalBlock, state.ReaderState);

            if (state.IsFirstBuffer)
            {
                if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new InvalidOperationException("Expected JSON payload to start with '{'.");
                }

                state.IsFirstBuffer = false;
            }

            while (reader.Read())
            {
                TokenProcessingOutcome outcome = reader.TokenType switch
                {
                    JsonTokenType.PropertyName => HandlePropertyName(ref state, ref reader),
                    JsonTokenType.StartArray => HandleStartArray(ref state, ref reader),
                    JsonTokenType.StartObject => HandleStartObject(ref state, ref reader),
                    JsonTokenType.EndObject => HandleEndObject(buffer, bufferLength, ref state, ref reader, extractedStreams),
                    JsonTokenType.EndArray => HandleEndArray(ref state, ref reader),
                    _ => HandleDefault(ref state),
                };

                if (outcome == TokenProcessingOutcome.SkipFlushAndAdvance)
                {
                    continue;
                }

                FlushPendingObjectBytes(buffer, ref state, reader.BytesConsumed, bufferLength);

                if (state.ResponseCompleted)
                {
                    break;
                }
            }

            state.ReaderState = reader.CurrentState;
            FlushPendingObjectBytes(buffer, ref state, reader.BytesConsumed, bufferLength);

            return new JsonReaderResult
            {
                ExtractedStreamCount = extractedStreams.Count,
                EndOfArray = state.ResponseCompleted,
                BytesConsumed = reader.BytesConsumed,
            };
        }

        private static void FlushPendingObjectBytes(byte[] buffer, ref StreamReadState state, long bytesConsumed, int bufferLength)
        {
            if (state.PendingObjectStream == null)
            {
                return;
            }

            int upperBound = (int)Math.Min(bytesConsumed, bufferLength);
            if (upperBound <= state.PendingObjectNextCopyIndex)
            {
                return;
            }

            int length = upperBound - state.PendingObjectNextCopyIndex;
            state.PendingObjectStream.Write(buffer.AsSpan(state.PendingObjectNextCopyIndex, length));
            state.PendingObjectNextCopyIndex = upperBound;
        }

        private static int ManageBuffer(byte[] buffer, long bytesConsumed, int totalBytesInBuffer)
        {
            int unconsumedBytes = (int)(totalBytesInBuffer - bytesConsumed);

            if (unconsumedBytes > 0)
            {
                Buffer.BlockCopy(buffer, (int)bytesConsumed, buffer, 0, unconsumedBytes);

                return unconsumedBytes;
            }

            return 0;
        }

        private static byte[] GrowBuffer(byte[] currentBuffer, int dataLength, ref int currentBufferSize)
        {
            int newSize = Math.Min(currentBufferSize * 2, MaxBufferSize);
            if (newSize <= currentBufferSize)
            {
                throw new InvalidOperationException($"JSON token exceeds maximum buffer size of {MaxBufferSize} bytes");
            }

            byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
            Buffer.BlockCopy(currentBuffer, 0, newBuffer, 0, dataLength);
            ArrayPool<byte>.Shared.Return(currentBuffer, true);

            currentBufferSize = newSize;

            return newBuffer;
        }

        private struct StreamReadState
        {
            public int TotalBytesInBuffer;
            public JsonReaderState ReaderState;
            public bool IsFirstBuffer;
            public bool ResponseCompleted;
            public int PendingObjectStart;
            public int PendingObjectDepth;
            public List<MemoryStream> ExtractedStreams;
            public RecyclableMemoryStream PendingObjectStream;
            public int PendingObjectNextCopyIndex;
            public bool WaitingForDocumentsArray;
            public bool InDocumentsArray;
            public bool DocumentsArrayClosed;
            public int PayloadArrayDepth;

            public StreamReadState()
            {
                this.TotalBytesInBuffer = 0;
                this.ReaderState = default;
                this.IsFirstBuffer = true;
                this.ResponseCompleted = false;
                this.PendingObjectStart = -1;
                this.PendingObjectDepth = -1;
                this.ExtractedStreams = null;
                this.PendingObjectStream = null;
                this.PendingObjectNextCopyIndex = 0;
                this.WaitingForDocumentsArray = false;
                this.InDocumentsArray = false;
                this.DocumentsArrayClosed = false;
                this.PayloadArrayDepth = -1;
            }
        }

        private struct JsonReaderResult
        {
            public int ExtractedStreamCount;
            public bool EndOfArray;
            public long BytesConsumed;
        }

        private static TokenProcessingOutcome HandlePropertyName(ref StreamReadState state, ref Utf8JsonReader reader)
        {
            if (!state.InDocumentsArray && !state.DocumentsArrayClosed)
            {
                string propertyName = reader.GetString();
                state.WaitingForDocumentsArray = string.Equals(propertyName, Constants.DocumentsResourcePropertyName, StringComparison.Ordinal);
            }

            return TokenProcessingOutcome.Continue;
        }

        private static TokenProcessingOutcome HandleStartArray(ref StreamReadState state, ref Utf8JsonReader reader)
        {
            if (!state.InDocumentsArray)
            {
                if (state.WaitingForDocumentsArray)
                {
                    state.InDocumentsArray = true;
                    state.PayloadArrayDepth = reader.CurrentDepth;
                }
                else
                {
                    state.WaitingForDocumentsArray = false;
                    return TokenProcessingOutcome.SkipFlushAndAdvance;
                }
            }

            state.WaitingForDocumentsArray = false;

            return TokenProcessingOutcome.Continue;
        }

        private static TokenProcessingOutcome HandleStartObject(ref StreamReadState state, ref Utf8JsonReader reader)
        {
            if (state.InDocumentsArray && state.PendingObjectStart < 0)
            {
                state.PendingObjectStart = (int)reader.TokenStartIndex;
                state.PendingObjectDepth = reader.CurrentDepth;
                state.PendingObjectStream = RecyclableMemoryStreamManager.GetStream("JsonArrayStreamSplitter");
                state.PendingObjectNextCopyIndex = state.PendingObjectStart;
            }

            state.WaitingForDocumentsArray = false;

            return TokenProcessingOutcome.Continue;
        }

        private static TokenProcessingOutcome HandleEndObject(
            byte[] buffer,
            int bufferLength,
            ref StreamReadState state,
            ref Utf8JsonReader reader,
            List<MemoryStream> extractedStreams)
        {
            if (state.PendingObjectStart >= 0 && state.PendingObjectDepth == reader.CurrentDepth)
            {
                FlushPendingObjectBytes(buffer, ref state, reader.BytesConsumed, bufferLength);

                if (state.PendingObjectStream != null)
                {
                    state.PendingObjectStream.Position = 0;
                    extractedStreams.Add(state.PendingObjectStream);
                    state.PendingObjectStream = null;
                }

                state.PendingObjectStart = -1;
                state.PendingObjectDepth = -1;
                state.PendingObjectNextCopyIndex = 0;
            }

            if (reader.CurrentDepth == 0 && state.DocumentsArrayClosed)
            {
                state.ResponseCompleted = true;
            }

            state.WaitingForDocumentsArray = false;
            return TokenProcessingOutcome.Continue;
        }

        private static TokenProcessingOutcome HandleEndArray(ref StreamReadState state, ref Utf8JsonReader reader)
        {
            if (state.InDocumentsArray && reader.CurrentDepth == state.PayloadArrayDepth)
            {
                state.InDocumentsArray = false;
                state.DocumentsArrayClosed = true;
                state.WaitingForDocumentsArray = false;
            }
            else
            {
                state.WaitingForDocumentsArray = false;
            }

            return TokenProcessingOutcome.Continue;
        }

        private static TokenProcessingOutcome HandleDefault(ref StreamReadState state)
        {
            state.WaitingForDocumentsArray = false;

            return TokenProcessingOutcome.Continue;
        }

        private enum TokenProcessingOutcome
        {
            Continue,
            SkipFlushAndAdvance,
        }
    }
}
