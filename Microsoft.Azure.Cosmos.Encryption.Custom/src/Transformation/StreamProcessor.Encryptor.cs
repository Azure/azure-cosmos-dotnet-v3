// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom; // For PooledBufferWriter

    internal partial class StreamProcessor
    {
        private readonly byte[] encryptionPropertiesNameBytes = Encoding.UTF8.GetBytes(Constants.EncryptedInfo);

        internal async Task EncryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CancellationToken cancellationToken)
        {
            List<string> pathsEncrypted = new ();

            using ArrayPoolManager arrayPoolManager = new ();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionOptions.DataEncryptionKeyId, encryptionOptions.EncryptionAlgorithm, cancellationToken);

            bool compressionEnabled = encryptionOptions.CompressionOptions.Algorithm != CompressionOptions.CompressionAlgorithm.None;

            BrotliCompressor compressor = encryptionOptions.CompressionOptions.Algorithm == CompressionOptions.CompressionAlgorithm.Brotli
                ? new BrotliCompressor(encryptionOptions.CompressionOptions.CompressionLevel) : null;

            HashSet<string> pathsToEncrypt = encryptionOptions.PathsToEncrypt as HashSet<string> ?? new (encryptionOptions.PathsToEncrypt, StringComparer.Ordinal);

            Dictionary<string, int> compressedPaths = new ();

            await using Utf8JsonWriter writer = new (outputStream);

            using PooledBufferWriter<byte> stagingBuffer = new (InitialBufferSize);

            JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

            bool inputCompleted = false; // Reached physical end of input stream
            bool isFinalBlock = false;   // Passed to Utf8JsonReader (input completed OR no more data expected)

            Utf8JsonWriter encryptionPayloadWriter = null;
            string encryptPropertyName = null;
            PooledBufferWriter<byte> payloadBuffer = null;

            while (!inputCompleted || stagingBuffer.Count > 0)
            {
                // If input not yet exhausted, attempt to read more.
                if (!inputCompleted)
                {
                    int read = await inputStream.ReadAsync(stagingBuffer.GetMemory(1), cancellationToken);
                    if (read > 0)
                    {
                        stagingBuffer.Advance(read);
                    }
                    else
                    {
                        inputCompleted = true; // No further bytes will arrive.
                    }
                }

                int dataSize = stagingBuffer.Count;
                isFinalBlock = inputCompleted; // When input is exhausted, remaining buffer is the final block.

                if (dataSize == 0)
                {
                    // Nothing left to process; exit.
                    break;
                }

                long bytesConsumed = TransformEncryptBuffer(stagingBuffer.WrittenSpan[..dataSize]);
                int consumed = (int)bytesConsumed;
                if (consumed > 0)
                {
                    stagingBuffer.ConsumePrefix(consumed);
                }
                else if (inputCompleted)
                {
                    // No progress and no more data will arrive -> truncated / invalid JSON.
                    throw new InvalidOperationException("Incomplete or truncated JSON input.");
                }
            }

            await inputStream.DisposeAsync();

            EncryptionProperties encryptionProperties = new (
                encryptionFormatVersion: compressionEnabled ? 4 : 3,
                encryptionOptions.EncryptionAlgorithm,
                encryptionOptions.DataEncryptionKeyId,
                encryptedData: null,
                pathsEncrypted,
                encryptionOptions.CompressionOptions.Algorithm,
                compressedPaths);

            writer.WritePropertyName(this.encryptionPropertiesNameBytes);
            JsonSerializer.Serialize(writer, encryptionProperties);
            writer.WriteEndObject();

            await writer.FlushAsync(cancellationToken);
            outputStream.Position = 0;

            long TransformEncryptBuffer(ReadOnlySpan<byte> buffer)
            {
                Utf8JsonReader reader = new (buffer, isFinalBlock, state);

                while (reader.Read())
                {
                    Utf8JsonWriter currentWriter = encryptionPayloadWriter ?? writer;

                    JsonTokenType tokenType = reader.TokenType;

                    switch (tokenType)
                    {
                        case JsonTokenType.None: // Unreachable after first Read()
                            break;
                        case JsonTokenType.StartObject:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                payloadBuffer = new PooledBufferWriter<byte>();
                                encryptionPayloadWriter = new Utf8JsonWriter(payloadBuffer);
                                encryptionPayloadWriter.WriteStartObject();
                            }
                            else
                            {
                                currentWriter.WriteStartObject();
                            }

                            break;
                        case JsonTokenType.EndObject:
                            if (reader.CurrentDepth == 0)
                            {
                                continue;
                            }

                            currentWriter.WriteEndObject();
                            if (reader.CurrentDepth == 1 && encryptionPayloadWriter != null)
                            {
                                currentWriter.Flush();
                                byte[] bytes = payloadBuffer.GetInternalArray();
                                int length = payloadBuffer.Count;
                                ReadOnlySpan<byte> encryptedBytes = TransformEncryptPayload(bytes, length, TypeMarker.Object);
                                writer.WriteBase64StringValue(encryptedBytes);

                                encryptPropertyName = null;
#pragma warning disable VSTHRD103 // Call async methods when in an async method - this method cannot be async, Utf8JsonReader is ref struct
                                encryptionPayloadWriter.Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                                encryptionPayloadWriter = null;
                                payloadBuffer.Dispose();
                                payloadBuffer = null;
                            }

                            break;
                        case JsonTokenType.StartArray:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                payloadBuffer = new PooledBufferWriter<byte>();
                                encryptionPayloadWriter = new Utf8JsonWriter(payloadBuffer);
                                encryptionPayloadWriter.WriteStartArray();
                            }
                            else
                            {
                                currentWriter.WriteStartArray();
                            }

                            break;
                        case JsonTokenType.EndArray:
                            currentWriter.WriteEndArray();
                            if (reader.CurrentDepth == 1 && encryptionPayloadWriter != null)
                            {
                                currentWriter.Flush();
                                byte[] bytes = payloadBuffer.GetInternalArray();
                                int length = payloadBuffer.Count;
                                ReadOnlySpan<byte> encryptedBytes = TransformEncryptPayload(bytes, length, TypeMarker.Array);
                                writer.WriteBase64StringValue(encryptedBytes);

                                encryptPropertyName = null;
#pragma warning disable VSTHRD103 // Call async methods when in an async method - this method cannot be async, Utf8JsonReader is ref struct
                                encryptionPayloadWriter.Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                                encryptionPayloadWriter = null;
                                payloadBuffer.Dispose();
                                payloadBuffer = null;
                            }

                            break;
                        case JsonTokenType.PropertyName:
                            string propertyName = "/" + reader.GetString();
                            if (pathsToEncrypt.Contains(propertyName))
                            {
                                encryptPropertyName = propertyName;
                            }

                            currentWriter.WritePropertyName(reader.ValueSpan);
                            break;
                        case JsonTokenType.Comment: // Skipped via reader options
                            currentWriter.WriteCommentValue(reader.ValueSpan);
                            break;
                        case JsonTokenType.String:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                byte[] bytes = arrayPoolManager.Rent(reader.ValueSpan.Length);
                                int length = reader.CopyString(bytes);
                                ReadOnlySpan<byte> encryptedBytes = TransformEncryptPayload(bytes, length, TypeMarker.String);
                                currentWriter.WriteBase64StringValue(encryptedBytes);
                                encryptPropertyName = null;
                            }
                            else
                            {
                                currentWriter.WriteStringValue(reader.ValueSpan);
                            }

                            break;
                        case JsonTokenType.Number:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                (TypeMarker typeMarker, byte[] bytes, int length) = SerializeNumber(reader.ValueSpan, arrayPoolManager);
                                ReadOnlySpan<byte> encryptedBytes = TransformEncryptPayload(bytes, length, typeMarker);
                                currentWriter.WriteBase64StringValue(encryptedBytes);
                                encryptPropertyName = null;
                            }
                            else
                            {
                                currentWriter.WriteRawValue(reader.ValueSpan, true);
                            }

                            break;
                        case JsonTokenType.True:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                (byte[] bytes, int length) = Serialize(true, arrayPoolManager);
                                ReadOnlySpan<byte> encryptedBytes = TransformEncryptPayload(bytes, length, TypeMarker.Boolean);
                                currentWriter.WriteBase64StringValue(encryptedBytes);
                                encryptPropertyName = null;
                            }
                            else
                            {
                                currentWriter.WriteBooleanValue(true);
                            }

                            break;
                        case JsonTokenType.False:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                (byte[] bytes, int length) = Serialize(false, arrayPoolManager);
                                ReadOnlySpan<byte> encryptedBytes = TransformEncryptPayload(bytes, length, TypeMarker.Boolean);
                                currentWriter.WriteBase64StringValue(encryptedBytes);
                                encryptPropertyName = null;
                            }
                            else
                            {
                                currentWriter.WriteBooleanValue(false);
                            }

                            break;
                        case JsonTokenType.Null:
                            currentWriter.WriteNullValue();
                            encryptPropertyName = null;
                            break;
                    }
                }

                state = reader.CurrentState;
                return reader.BytesConsumed;
            }

            ReadOnlySpan<byte> TransformEncryptPayload(byte[] payload, int payloadSize, TypeMarker typeMarker)
            {
                byte[] processedBytes = payload;
                int processedBytesLength = payloadSize;

                if (compressor != null && payloadSize >= encryptionOptions.CompressionOptions.MinimalCompressedLength)
                {
                    byte[] compressedBytes = arrayPoolManager.Rent(BrotliCompressor.GetMaxCompressedSize(payloadSize));
                    processedBytesLength = compressor.Compress(compressedPaths, encryptPropertyName, processedBytes, payloadSize, compressedBytes);
                    processedBytes = compressedBytes;
                }

                (byte[] encryptedBytes, int encryptedBytesCount) = this.Encryptor.Encrypt(encryptionKey, typeMarker, processedBytes, processedBytesLength, arrayPoolManager);

                pathsEncrypted.Add(encryptPropertyName);
                return encryptedBytes.AsSpan(0, encryptedBytesCount);
            }
        }

        private static (byte[] buffer, int length) Serialize(bool value, ArrayPoolManager arrayPoolManager)
        {
            int byteCount = StreamProcessor.SqlBoolSerializer.GetSerializedMaxByteCount();
            byte[] buffer = arrayPoolManager.Rent(byteCount);
            int length = StreamProcessor.SqlBoolSerializer.Serialize(value, buffer);

            return (buffer, length);
        }

        private static (TypeMarker typeMarker, byte[] buffer, int length) SerializeNumber(ReadOnlySpan<byte> utf8Bytes, ArrayPoolManager arrayPoolManager)
        {
            if (System.Buffers.Text.Utf8Parser.TryParse(utf8Bytes, out long longValue, out int consumedLong) && consumedLong == utf8Bytes.Length)
            {
                return Serialize(longValue, arrayPoolManager);
            }

            if (System.Buffers.Text.Utf8Parser.TryParse(utf8Bytes, out double doubleValue, out int consumedDouble) && consumedDouble == utf8Bytes.Length)
            {
                // Reject non-finite numbers to keep JSON contract compatibility
                if (double.IsFinite(doubleValue))
                {
                    return Serialize(doubleValue, arrayPoolManager);
                }
            }

            throw new InvalidOperationException("Unsupported Number type");
        }

        private static (TypeMarker typeMarker, byte[] buffer, int length) Serialize(long value, ArrayPoolManager arrayPoolManager)
        {
            int byteCount = StreamProcessor.SqlLongSerializer.GetSerializedMaxByteCount();
            byte[] buffer = arrayPoolManager.Rent(byteCount);
            int length = StreamProcessor.SqlLongSerializer.Serialize(value, buffer);

            return (TypeMarker.Long, buffer, length);
        }

        private static (TypeMarker typeMarker, byte[] buffer, int length) Serialize(double value, ArrayPoolManager arrayPoolManager)
        {
            int byteCount = StreamProcessor.SqlDoubleSerializer.GetSerializedMaxByteCount();
            byte[] buffer = arrayPoolManager.Rent(byteCount);
            int length = StreamProcessor.SqlDoubleSerializer.Serialize(value, buffer);

            return (TypeMarker.Double, buffer, length);
        }
    }
}
#endif