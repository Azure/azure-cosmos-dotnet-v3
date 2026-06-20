// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Buffers;
    using System.Text.Json;

    internal static class JsonFeedStreamHelper
    {
        private const string MissingDocumentsArrayMessage = "The JSON payload must be a Cosmos DB feed response object containing a Documents array.";
        private const string DocumentsMustBeArrayMessage = "The Documents property must be a JSON array.";

        internal static ProcessResult ProcessChunk(
            ReadOnlySpan<byte> dataSegment,
            bool finalBlock,
            ref JsonReaderState readerState,
            ref JsonArrayTraversalState traversalState,
            JsonSegmentWriter writeSegment)
        {
            Utf8JsonReader reader = new (dataSegment, finalBlock, readerState);
            int lastConsumed = 0;

            while (reader.Read())
            {
                int tokenStart = checked((int)reader.TokenStartIndex);
                int tokenEnd = checked((int)reader.BytesConsumed);

                if (tokenStart > lastConsumed)
                {
                    ReadOnlySpan<byte> preTokenSegment = dataSegment.Slice(lastConsumed, tokenStart - lastConsumed);
                    writeSegment(preTokenSegment, traversalState.DocumentOpen);
                }

                JsonTokenType tokenType = reader.TokenType;

                bool documentCompleted = UpdateTraversalState(ref traversalState, tokenType, reader.CurrentDepth, ref reader);

                ReadOnlySpan<byte> tokenSegment = dataSegment.Slice(tokenStart, tokenEnd - tokenStart);
                writeSegment(tokenSegment, traversalState.DocumentOpen);

                if (documentCompleted)
                {
                    traversalState.DocumentOpen = false;
                    readerState = reader.CurrentState;
                    return new ProcessResult(checked((int)reader.BytesConsumed), objectCompleted: true);
                }

                lastConsumed = tokenEnd;
            }

            readerState = reader.CurrentState;

            return new ProcessResult(checked((int)reader.BytesConsumed), objectCompleted: false);
        }

        internal static byte[] HandleLeftOver(byte[] buffer, int dataLength, int leftOver, int bytesConsumed, int maxBufferSize)
        {
            // Grow only when the buffer is genuinely full of unconsumed data (leftOver == buffer.Length).
            // Using leftOver == dataLength instead would grow whenever the reader consumed nothing this
            // round, which under short reads happens with free buffer space still available and doubles the
            // buffer every iteration until the cap throws on otherwise-valid input.
            if (leftOver == buffer.Length)
            {
                int newSize = checked(buffer.Length * 2);
                if (maxBufferSize > 0 && newSize > maxBufferSize)
                {
                    throw new InvalidOperationException($"JSON document or token does not fit within the maximum buffer size of {maxBufferSize} bytes");
                }

                byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
                buffer.AsSpan(0, dataLength).CopyTo(newBuffer);
                ArrayPool<byte>.Shared.Return(buffer, clearArray: true);

                return newBuffer;
            }

            if (leftOver != 0)
            {
                buffer.AsSpan(bytesConsumed, leftOver).CopyTo(buffer);
            }

            return buffer;
        }

        private static bool UpdateTraversalState(ref JsonArrayTraversalState traversalState, JsonTokenType tokenType, int readerDepth, ref Utf8JsonReader reader)
        {
            if (traversalState.EnvelopeDepth < 0 && tokenType != JsonTokenType.StartObject)
            {
                throw new InvalidOperationException("Expected JSON payload to start with '{'.");
            }

            switch (tokenType)
            {
                case JsonTokenType.StartObject:
                    if (traversalState.EnvelopeDepth < 0)
                    {
                        traversalState.EnvelopeDepth = readerDepth;
                        break;
                    }

                    if (traversalState.PendingDocumentsArray)
                    {
                        throw new InvalidOperationException(DocumentsMustBeArrayMessage);
                    }

                    if (traversalState.DocumentsArrayDepth >= 0 && readerDepth == traversalState.DocumentsArrayDepth + 1)
                    {
                        if (traversalState.DocumentOpen)
                        {
                            throw new InvalidOperationException("Unexpected nested document start at the documents array level.");
                        }

                        traversalState.DocumentOpen = true;
                    }

                    break;

                case JsonTokenType.PropertyName:
                    bool isDocumentsProperty =
                        traversalState.EnvelopeDepth >= 0 &&
                        readerDepth == traversalState.EnvelopeDepth + 1 &&
                        reader.ValueTextEquals(Constants.DocumentsResourcePropertyName);

                    if (!isDocumentsProperty)
                    {
                        traversalState.PendingDocumentsArray = false;
                        break;
                    }

                    traversalState.DocumentsArraySeen = false;
                    traversalState.DocumentsArrayDepth = -1;
                    traversalState.DocumentOpen = false;
                    traversalState.PendingDocumentsArray = true;
                    break;

                case JsonTokenType.StartArray:
                    if (traversalState.PendingDocumentsArray)
                    {
                        traversalState.DocumentsArrayDepth = readerDepth;
                        traversalState.PendingDocumentsArray = false;
                    }

                    break;

                case JsonTokenType.EndArray:
                    if (traversalState.DocumentsArrayDepth >= 0 && readerDepth == traversalState.DocumentsArrayDepth)
                    {
                        traversalState.DocumentsArrayDepth = -1;
                        traversalState.DocumentsArraySeen = true;
                    }

                    break;

                case JsonTokenType.EndObject:
                    if (traversalState.DocumentOpen && traversalState.DocumentsArrayDepth >= 0 && readerDepth == traversalState.DocumentsArrayDepth + 1)
                    {
                        return true;
                    }

                    if (readerDepth == traversalState.EnvelopeDepth && !traversalState.DocumentsArraySeen)
                    {
                        throw new InvalidOperationException(MissingDocumentsArrayMessage);
                    }

                    break;

                default:
                    if (traversalState.PendingDocumentsArray)
                    {
                        throw new InvalidOperationException(DocumentsMustBeArrayMessage);
                    }

                    break;
            }

            return false;
        }
    }

    internal delegate void JsonSegmentWriter(ReadOnlySpan<byte> segment, bool insideDocument);

    internal readonly struct ProcessResult
    {
        internal ProcessResult(int bytesConsumed, bool objectCompleted)
        {
            this.BytesConsumed = bytesConsumed;
            this.ObjectCompleted = objectCompleted;
        }

        internal int BytesConsumed { get; }

        internal bool ObjectCompleted { get; }
    }

    internal struct JsonArrayTraversalState
    {
        internal int EnvelopeDepth;
        internal int DocumentsArrayDepth;
        internal bool DocumentsArraySeen;
        internal bool PendingDocumentsArray;
        internal bool DocumentOpen;

        internal static JsonArrayTraversalState CreateInitial()
        {
            return new JsonArrayTraversalState
            {
                EnvelopeDepth = -1,
                DocumentsArrayDepth = -1,
            };
        }
    }
}
