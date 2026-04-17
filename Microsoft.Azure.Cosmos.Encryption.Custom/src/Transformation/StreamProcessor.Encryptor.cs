// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
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

            // Pre-encode the paths-to-encrypt as UTF-8 byte sequences so that we can match
            // against Utf8JsonReader tokens with ValueTextEquals (which correctly handles
            // JSON escape sequences), without allocating a new string per property name.
            // The leading '/' is stripped here since ValueTextEquals compares against the
            // decoded property-name bytes, while the original slash-prefixed path string is
            // preserved for the pathsEncrypted output list.
            (byte[] nameBytes, string fullPath)[] encryptedPathsTable = BuildEncryptedPathsTable(encryptionOptions.PathsToEncrypt);

            using Utf8JsonWriter writer = new (outputStream);

            byte[] buffer = arrayPoolManager.Rent(PooledStreamConfiguration.Current.StreamProcessorBufferSize);

            JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

            int leftOver = 0;

            bool isFinalBlock = false;

            Utf8JsonWriter encryptionPayloadWriter = null;
            string encryptPropertyName = null;
            RentArrayBufferWriter bufferWriter = null;
            bool firstTokenValidated = false;

            try
            {
                while (!isFinalBlock)
                {
                    int dataLength = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                    int dataSize = dataLength + leftOver;
                    isFinalBlock = dataLength == 0;

                    long bytesConsumed = TransformEncryptBuffer(buffer.AsSpan(0, dataSize));

                    leftOver = dataSize - (int)bytesConsumed;

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
            }
            finally
            {
                if (encryptionPayloadWriter != null)
                {
                    await encryptionPayloadWriter.DisposeAsync();
                }

#pragma warning disable VSTHRD103 // Call async methods when in an async method
                bufferWriter?.Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
            }

            EncryptionProperties encryptionProperties = new (
                encryptionFormatVersion: EncryptionFormatVersion.Mde,
                encryptionOptions.EncryptionAlgorithm,
                encryptionOptions.DataEncryptionKeyId,
                encryptedData: null,
                pathsEncrypted);

            writer.WritePropertyName(this.encryptionPropertiesNameBytes);
            JsonSerializer.Serialize(writer, encryptionProperties);
            writer.WriteEndObject();

            writer.Flush();
            outputStream.Position = 0;

            long TransformEncryptBuffer(ReadOnlySpan<byte> buffer)
            {
                Utf8JsonReader reader = new (buffer, isFinalBlock, state);

                while (reader.Read())
                {
                    JsonTokenType tokenType = reader.TokenType;

                    if (!firstTokenValidated)
                    {
                        // The first non-None token must be StartObject for streaming encryption.
                        if (tokenType == JsonTokenType.StartObject)
                        {
                            firstTokenValidated = true;
                        }
                        else if (tokenType == JsonTokenType.Comment || tokenType == JsonTokenType.None)
                        {
                            continue; // skip and keep waiting for first structural token
                        }
                        else
                        {
                            throw new NotSupportedException("Streaming encryption requires a JSON object root. Root arrays or primitive values are not supported.");
                        }
                    }

                    Utf8JsonWriter currentWriter = encryptionPayloadWriter ?? writer;

                    switch (tokenType)
                    {
                        case JsonTokenType.None: // Unreachable after first Read()
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
                                ReadOnlySpan<byte> encryptedBytes = TransformEncryptPayload(bytes, length, TypeMarker.Object);
                                writer.WriteBase64StringValue(encryptedBytes);

                                encryptPropertyName = null;
                                encryptionPayloadWriter = null;
                                bufferWriter?.Dispose();
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
                                ReadOnlySpan<byte> encryptedBytes = TransformEncryptPayload(bytes, length, TypeMarker.Array);
                                writer.WriteBase64StringValue(encryptedBytes);

                                encryptPropertyName = null;
                                encryptionPayloadWriter = null;
                                bufferWriter?.Dispose();
                                bufferWriter = null;
                            }

                            break;
                        case JsonTokenType.PropertyName:
                            string matchedPath = null;
                            for (int i = 0; i < encryptedPathsTable.Length; i++)
                            {
                                if (reader.ValueTextEquals(encryptedPathsTable[i].nameBytes))
                                {
                                    matchedPath = encryptedPathsTable[i].fullPath;
                                    break;
                                }
                            }

                            if (matchedPath != null)
                            {
                                encryptPropertyName = matchedPath;
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

                (byte[] encryptedBytes, int encryptedBytesCount) = this.Encryptor.Encrypt(encryptionKey, typeMarker, processedBytes, processedBytesLength, arrayPoolManager);

                pathsEncrypted.Add(encryptPropertyName);
                return encryptedBytes.AsSpan(0, encryptedBytesCount);
            }
        }

        private static (byte[] nameBytes, string fullPath)[] BuildEncryptedPathsTable(System.Collections.Generic.IEnumerable<string> pathsToEncrypt)
        {
            System.Collections.Generic.List<(byte[] nameBytes, string fullPath)> table = new ();
            foreach (string path in pathsToEncrypt)
            {
                if (string.IsNullOrEmpty(path) || path[0] != '/' || path.Length < 2)
                {
                    // Paths are already validated by EncryptionOptions; skip defensively.
                    continue;
                }

                // Strip the leading '/'. The property name bytes are what the JSON reader
                // token surfaces (without the JSON Pointer prefix). The original slash-
                // prefixed string is preserved for the output pathsEncrypted list so the
                // serialized _ei metadata remains byte-identical to the previous
                // implementation.
                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(path.AsSpan(1).ToString());
                table.Add((nameBytes, path));
            }

            return table.ToArray();
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
            if (System.Buffers.Text.Utf8Parser.TryParse(utf8bytes, out long longValue, out int consumedLong) && consumedLong == utf8bytes.Length)
            {
                return Serialize(longValue, arrayPoolManager);
            }

            if (System.Buffers.Text.Utf8Parser.TryParse(utf8bytes, out double doubleValue, out int consumedDouble) && consumedDouble == utf8bytes.Length)
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