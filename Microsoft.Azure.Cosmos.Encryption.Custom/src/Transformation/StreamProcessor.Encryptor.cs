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
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            long bytesRead = 0;
            long bytesWritten = 0;
            long propertiesEncrypted = 0;
            long compressedPathsCompressed = 0;
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

            List<string> pathsEncrypted = new ();

            using ArrayPoolManager arrayPoolManager = new ();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionOptions.DataEncryptionKeyId, encryptionOptions.EncryptionAlgorithm, cancellationToken);

            bool compressionEnabled = encryptionOptions.CompressionOptions.Algorithm != CompressionOptions.CompressionAlgorithm.None;

            BrotliCompressor compressor = encryptionOptions.CompressionOptions.Algorithm == CompressionOptions.CompressionAlgorithm.Brotli
                ? new BrotliCompressor(encryptionOptions.CompressionOptions.CompressionLevel) : null;

            // Build a top-level property map for encryption
            Dictionary<string, bool> pathsToEncryptTopLevel = new (StringComparer.Ordinal);
            foreach (string p in encryptionOptions.PathsToEncrypt)
            {
                if (!string.IsNullOrEmpty(p) && p[0] == '/')
                {
                    string name = p.Substring(1);
                    if (!name.Contains('/'))
                    {
                        pathsToEncryptTopLevel[name] = true;
                    }
                }
            }

            Dictionary<string, int> compressedPaths = new ();

            // If output stream is seekable we can compute bytes written via Position delta; otherwise wrap to count writes.
            long initialOutputPos = outputStream.CanSeek ? outputStream.Position : 0;
            Stream countingOutput = outputStream.CanSeek ? outputStream : new CountingStream(outputStream, onWrite: (n) => { bytesWritten += n; });
            using Utf8JsonWriter writer = new (countingOutput, StreamProcessor.JsonWriterOptions);

            byte[] buffer = arrayPoolManager.Rent(this.initialBufferSize);

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
                bytesRead += dataLength;
                isFinalBlock = dataSize == 0;
                long bytesConsumed = 0;

                bytesConsumed = TransformEncryptBuffer(buffer.AsSpan(0, dataSize));

                leftOver = dataSize - (int)bytesConsumed;

                // we need to scale out buffer
                // Guard against end-of-stream: when dataSize == 0, don't resize unnecessarily
                if (dataSize > 0 && leftOver == dataSize)
                {
                    int target = Math.Max(buffer.Length * 2, leftOver + BufferGrowthMinIncrement);
                    int capped = Math.Min(MaxBufferSizeBytes, target);
                    if (buffer.Length >= capped)
                    {
                        throw new InvalidOperationException($"JSON token exceeds maximum supported size of {MaxBufferSizeBytes} bytes.");
                    }

                    byte[] newBuffer = arrayPoolManager.Rent(capped);
                    buffer.AsSpan(0, dataSize).CopyTo(newBuffer);
                    buffer = newBuffer;
                }
                else if (leftOver != 0)
                {
                    buffer.AsSpan(dataSize - leftOver, leftOver).CopyTo(buffer);
                }
            }

            // Do not dispose inputStream here; caller owns streams for consistency with decryptor.
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
            if (outputStream.CanSeek)
            {
                bytesWritten = Math.Max(0, outputStream.Position - initialOutputPos);
                outputStream.Position = 0;
            }

            // finalize diagnostics
            diagnosticsContext?.SetMetric("encrypt.bytesRead", bytesRead);
            diagnosticsContext?.SetMetric("encrypt.bytesWritten", bytesWritten);
            diagnosticsContext?.SetMetric("encrypt.propertiesEncrypted", propertiesEncrypted);
            diagnosticsContext?.SetMetric("encrypt.compressedPathsCompressed", compressedPathsCompressed);
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            long elapsedMs = (long)(elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            diagnosticsContext?.SetMetric("encrypt.elapsedMs", elapsedMs);

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
                                propertiesEncrypted++;

                                encryptPropertyName = null;
#pragma warning disable VSTHRD103 // Call async methods when in an async method - this method cannot be async, Utf8JsonReader is ref struct
                                encryptionPayloadWriter.Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
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
                                ReadOnlySpan<byte> encryptedBytes = TransformEncryptPayload(bytes, length, TypeMarker.Array);
                                writer.WriteBase64StringValue(encryptedBytes);
                                propertiesEncrypted++;

                                encryptPropertyName = null;
#pragma warning disable VSTHRD103 // Call async methods when in an async method - this method cannot be async, Utf8JsonReader is ref struct
                                encryptionPayloadWriter.Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                                encryptionPayloadWriter = null;
                                bufferWriter.Dispose();
                                bufferWriter = null;
                            }

                            break;
                        case JsonTokenType.PropertyName:
                            string propName = reader.GetString();
                            if (propName != null && pathsToEncryptTopLevel.ContainsKey(propName))
                            {
                                encryptPropertyName = "/" + propName;
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

                                // Early return temp string buffer
                                arrayPoolManager.Return(bytes);
                                currentWriter.WriteBase64StringValue(encryptedBytes);
                                encryptPropertyName = null;
                                propertiesEncrypted++;
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

                                // Early return temp number buffer
                                arrayPoolManager.Return(bytes);
                                currentWriter.WriteBase64StringValue(encryptedBytes);
                                encryptPropertyName = null;
                                propertiesEncrypted++;
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
                                propertiesEncrypted++;
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
                                propertiesEncrypted++;
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
                    compressedPathsCompressed++;
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