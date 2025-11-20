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
        private const string DocumentsMustBeObjectsMessage = "Documents array items must be JSON objects.";

        internal static ProcessResult ProcessChunk(
            ReadOnlySpan<byte> dataSegment,
            bool finalBlock,
            ref JsonReaderState readerState,
            ref JsonArrayTraversalState traversalState,
            JsonSegmentWriter writeEnvelopeSegment,
            JsonSegmentWriter writeObjectSegment)
        {
            Utf8JsonReader reader = new (dataSegment, finalBlock, readerState);
            int lastConsumed = 0;

            while (reader.Read())
            {
                int tokenStart = checked((int)reader.TokenStartIndex);
                int tokenEnd = checked((int)reader.BytesConsumed);

                if (tokenStart > lastConsumed)
                {
                    WriteSegment(
                        traversalState.DocumentOpen,
                        writeEnvelopeSegment,
                        writeObjectSegment,
                        Slice(dataSegment, lastConsumed, tokenStart));
                }

                JsonTokenType tokenType = reader.TokenType;

                if (!traversalState.EnvelopeValidated)
                {
                    if (tokenType != JsonTokenType.StartObject)
                    {
                        throw new InvalidOperationException("Expected JSON payload to start with '{'.");
                    }

                    traversalState.EnvelopeValidated = true;
                }

                bool documentCompleted = UpdateTraversalState(ref traversalState, tokenType, reader.CurrentDepth, ref reader);

                WriteSegment(
                    traversalState.DocumentOpen,
                    writeEnvelopeSegment,
                    writeObjectSegment,
                    Slice(dataSegment, tokenStart, tokenEnd));

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

        internal static byte[] HandleLeftOver(byte[] buffer, int dataLength, int leftOver, int bytesConsumed, int maxBufferSize = 0)
        {
            if (leftOver == dataLength)
            {
                int newSize = checked(buffer.Length * 2);
                if (maxBufferSize > 0 && newSize > maxBufferSize)
                {
                    throw new InvalidOperationException($"JSON token exceeds maximum buffer size of {maxBufferSize} bytes");
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

        private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> span, int start, int end)
        {
#if NET8_0_OR_GREATER
            return span[start..end];
#else
            return span.Slice(start, end - start);
#endif
        }

        private static void WriteSegment(
            bool capturingObject,
            JsonSegmentWriter writeEnvelopeSegment,
            JsonSegmentWriter writeObjectSegment,
            ReadOnlySpan<byte> segment)
        {
            if (segment.IsEmpty)
            {
                return;
            }

            if (capturingObject)
            {
                if (writeObjectSegment == null)
                {
                    throw new InvalidOperationException("Object segment writer must be provided when capturing documents.");
                }

                writeObjectSegment(segment);
                return;
            }

            writeEnvelopeSegment?.Invoke(segment);
        }

        private static bool UpdateTraversalState(ref JsonArrayTraversalState traversalState, JsonTokenType tokenType, int readerDepth, ref Utf8JsonReader reader)
        {
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

                    if (traversalState.DocumentsArraySeen || traversalState.DocumentsArrayDepth >= 0)
                    {
                        throw new InvalidOperationException("Multiple Documents arrays are not supported in the feed payload.");
                    }

                    traversalState.PendingDocumentsArray = true;
                    break;

                case JsonTokenType.StartArray:
                    if (traversalState.PendingDocumentsArray)
                    {
                        traversalState.DocumentsArrayDepth = readerDepth;
                        traversalState.PendingDocumentsArray = false;
                        break;
                    }

                    EnsureEnvelope(traversalState);
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
                    EnsureEnvelope(traversalState);

                    if (traversalState.PendingDocumentsArray)
                    {
                        throw new InvalidOperationException(DocumentsMustBeArrayMessage);
                    }

                    if (traversalState.DocumentsArrayDepth >= 0 && readerDepth == traversalState.DocumentsArrayDepth + 1)
                    {
                        throw new InvalidOperationException(DocumentsMustBeObjectsMessage);
                    }

                    break;
            }

            return false;
        }

        private static void EnsureEnvelope(in JsonArrayTraversalState traversalState)
        {
            if (traversalState.EnvelopeDepth < 0)
            {
                throw new InvalidOperationException(MissingDocumentsArrayMessage);
            }
        }
    }

    internal delegate void JsonSegmentWriter(ReadOnlySpan<byte> segment);

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
        internal bool EnvelopeValidated;

        internal static JsonArrayTraversalState CreateInitial()
        {
            return new JsonArrayTraversalState
            {
                EnvelopeDepth = -1,
                DocumentsArrayDepth = -1,
                DocumentsArraySeen = false,
                PendingDocumentsArray = false,
                DocumentOpen = false,
                EnvelopeValidated = false,
            };
        }
    }
}
