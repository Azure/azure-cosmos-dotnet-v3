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

            using Utf8JsonWriter writer = new (outputStream);

            byte[] buffer = arrayPoolManager.Rent(InitialBufferSize);

            JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

            int leftOver = 0;

            bool isFinalBlock = false;

            Utf8JsonWriter encryptionPayloadWriter = null;
            string encryptPropertyName = null;
            RentArrayBufferWriter bufferWriter = null;

            while (!isFinalBlock)
            {
                int dataLength = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                int dataSize = dataLength + leftOver;
                isFinalBlock = dataSize == 0;
                long bytesConsumed = 0;

                bytesConsumed = this.TransformEncryptBuffer(
                    buffer.AsSpan(0, dataSize),
                    isFinalBlock,
                    writer,
                    ref encryptionPayloadWriter,
                    ref bufferWriter,
                    ref state,
                    ref encryptPropertyName,
                    pathsToEncrypt,
                    pathsEncrypted,
                    compressedPaths,
                    compressor,
                    arrayPoolManager,
                    encryptionKey,
                    encryptionOptions);

                leftOver = dataSize - (int)bytesConsumed;

                // we need to scale out buffer
                if (leftOver == dataSize)
                {
                    byte[] newBuffer = arrayPoolManager.Rent(buffer.Length * 2);
                    buffer.AsSpan().CopyTo(newBuffer);
                    buffer = newBuffer;
                }
                else if (leftOver != 0)
                {
                    buffer.AsSpan(dataSize - leftOver, leftOver).CopyTo(buffer);
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

            writer.Flush();
            outputStream.Position = 0;
        }

        private long TransformEncryptBuffer(
            ReadOnlySpan<byte> buffer,
            bool isFinalBlock,
            Utf8JsonWriter writer,
            ref Utf8JsonWriter encryptionPayloadWriter,
            ref RentArrayBufferWriter bufferWriter,
            ref JsonReaderState state,
            ref string encryptPropertyName,
            HashSet<string> pathsToEncrypt,
            List<string> pathsEncrypted,
            Dictionary<string, int> compressedPaths,
            BrotliCompressor compressor,
            ArrayPoolManager arrayPoolManager,
            DataEncryptionKey encryptionKey,
            EncryptionOptions encryptionOptions)
        {
            Utf8JsonReader reader = new (buffer, isFinalBlock, state);

            while (reader.Read())
            {
                Utf8JsonWriter currentWriter = encryptionPayloadWriter ?? writer;

                JsonTokenType tokenType = reader.TokenType;

                switch (tokenType)
                {
                    case JsonTokenType.None:
                        break;
                    case JsonTokenType.StartObject:
                        if (encryptPropertyName != null && encryptionPayloadWriter == null)
                        {
                            bufferWriter = new RentArrayBufferWriter();
                            encryptionPayloadWriter = new Utf8JsonWriter(bufferWriter);
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
                            (byte[] bytes, int length) = bufferWriter.WrittenBuffer;
                            ReadOnlySpan<byte> encryptedBytes = this.TransformEncryptPayload(bytes, length, TypeMarker.Object, encryptPropertyName, encryptionOptions, encryptionKey, pathsEncrypted, compressedPaths, compressor, arrayPoolManager);
                            writer.WriteBase64StringValue(encryptedBytes);

                            encryptPropertyName = null;
                            encryptionPayloadWriter.Dispose();
                            encryptionPayloadWriter = null;
                            bufferWriter.Dispose();
                            bufferWriter = null;
                        }

                        break;
                    case JsonTokenType.StartArray:
                        if (encryptPropertyName != null && encryptionPayloadWriter == null)
                        {
                            bufferWriter = new RentArrayBufferWriter();
                            encryptionPayloadWriter = new Utf8JsonWriter(bufferWriter);
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
                            (byte[] bytes, int length) = bufferWriter.WrittenBuffer;
                            ReadOnlySpan<byte> encryptedBytes = this.TransformEncryptPayload(bytes, length, TypeMarker.Array, encryptPropertyName, encryptionOptions, encryptionKey, pathsEncrypted, compressedPaths, compressor, arrayPoolManager);
                            writer.WriteBase64StringValue(encryptedBytes);

                            encryptPropertyName = null;
                            encryptionPayloadWriter.Dispose();
                            encryptionPayloadWriter = null;
                            bufferWriter.Dispose();
                            bufferWriter = null;
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
                    case JsonTokenType.Comment:
                        currentWriter.WriteCommentValue(reader.ValueSpan);
                        break;
                    case JsonTokenType.String:
                        if (encryptPropertyName != null && encryptionPayloadWriter == null)
                        {
                            byte[] bytes = arrayPoolManager.Rent(reader.ValueSpan.Length);
                            int length = reader.CopyString(bytes);
                            ReadOnlySpan<byte> encryptedBytes = this.TransformEncryptPayload(bytes, length, TypeMarker.String, encryptPropertyName, encryptionOptions, encryptionKey, pathsEncrypted, compressedPaths, compressor, arrayPoolManager);
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
                            ReadOnlySpan<byte> encryptedBytes = this.TransformEncryptPayload(bytes, length, typeMarker, encryptPropertyName, encryptionOptions, encryptionKey, pathsEncrypted, compressedPaths, compressor, arrayPoolManager);
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
                            ReadOnlySpan<byte> encryptedBytes = this.TransformEncryptPayload(bytes, length, TypeMarker.Boolean, encryptPropertyName, encryptionOptions, encryptionKey, pathsEncrypted, compressedPaths, compressor, arrayPoolManager);
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
                            ReadOnlySpan<byte> encryptedBytes = this.TransformEncryptPayload(bytes, length, TypeMarker.Boolean, encryptPropertyName, encryptionOptions, encryptionKey, pathsEncrypted, compressedPaths, compressor, arrayPoolManager);
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
                        break;
                }
            }

            state = reader.CurrentState;
            return reader.BytesConsumed;
        }

        private static (byte[] buffer, int length) Serialize(bool value, ArrayPoolManager arrayPoolManager)
        {
            int byteCount = StreamProcessor.SqlBoolSerializer.GetSerializedMaxByteCount();
            byte[] buffer = arrayPoolManager.Rent(byteCount);
            int length = StreamProcessor.SqlBoolSerializer.Serialize(value, buffer);

            return (buffer, length);
        }

        private static (TypeMarker typeMarker, byte[] buffer, int length) SerializeNumber(ReadOnlySpan<byte> utf8bytes, ArrayPoolManager arrayPoolManager)
        {
            if (long.TryParse(utf8bytes, out long longValue))
            {
                return Serialize(longValue, arrayPoolManager);
            }
            else if (double.TryParse(utf8bytes, out double doubleValue))
            {
                return Serialize(doubleValue, arrayPoolManager);
            }
            else
            {
                throw new InvalidOperationException("Unsupported Number type");
            }
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

        private ReadOnlySpan<byte> TransformEncryptPayload(byte[] payload, int payloadSize, TypeMarker typeMarker, string encryptName, EncryptionOptions options, DataEncryptionKey encryptionKey, List<string> pathsEncrypted, Dictionary<string, int> pathsCompressed, BrotliCompressor compressor, ArrayPoolManager arrayPoolManager)
        {
            byte[] processedBytes = payload;
            int processedBytesLength = payloadSize;

            if (compressor != null && payloadSize >= options.CompressionOptions.MinimalCompressedLength)
            {
                byte[] compressedBytes = arrayPoolManager.Rent(BrotliCompressor.GetMaxCompressedSize(payloadSize));
                processedBytesLength = compressor.Compress(pathsCompressed, encryptName, processedBytes, payloadSize, compressedBytes);
                processedBytes = compressedBytes;
            }

            (byte[] encryptedBytes, int encryptedBytesCount) = this.Encryptor.Encrypt(encryptionKey, typeMarker, processedBytes, processedBytesLength, arrayPoolManager);

            pathsEncrypted.Add(encryptName);
            return encryptedBytes.AsSpan(0, encryptedBytesCount);
        }
    }
}
#endif