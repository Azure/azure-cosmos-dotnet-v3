// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// Temporarily disable strict StyleCop rules for this preview-only implementation to unblock iteration.
#pragma warning disable SA1513 // Closing brace should be followed by blank line
#pragma warning disable SA1510 // 'else' statement should not be preceded by a blank line
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
#pragma warning disable SA1512 // Single-line comments should not be followed by blank line
#pragma warning disable SA1137 // Elements should have the same indentation
#pragma warning disable SA1505 // An opening brace should not be followed by a blank line
#pragma warning disable SA1507 // Code should not contain multiple blank lines in a row
#pragma warning disable SA1508 // A closing brace should not be preceded by a blank line
#pragma warning disable SA1028 // Code should not contain trailing whitespace

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
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

            // Precompute top-level names and canonical full paths to avoid per-hit string concat and reduce lookups
            HashSet<string> encryptedFullPaths = new (encryptionOptions.PathsToEncrypt ?? Array.Empty<string>(), StringComparer.Ordinal);
            List<byte[]> topLevelNameUtf8 = new (encryptedFullPaths.Count);
            List<string> topLevelFullPaths = new (encryptedFullPaths.Count);
            foreach (string p in encryptedFullPaths)
            {
                if (string.IsNullOrEmpty(p) || p[0] != '/')
                {
                    continue;
                }

                string name = p.Length > 1 ? p.Substring(1) : string.Empty;
                if (name.IndexOf('/') >= 0)
                {
                    continue; // only support top-level names here
                }

                topLevelNameUtf8.Add(Encoding.UTF8.GetBytes(name));
                topLevelFullPaths.Add(p); // reuse canonical provided full path
            }

            Dictionary<string, int> compressedPaths = new ();

            // Write directly to the provided output stream; we'll compute bytes written via Utf8JsonWriter.BytesCommitted
            using Utf8JsonWriter writer = new (outputStream, StreamProcessor.JsonWriterOptions);

            byte[] buffer = arrayPoolManager.Rent(this.initialBufferSize);

            JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

            int leftOver = 0;

            bool isFinalBlock = false;

            Utf8JsonWriter encryptionPayloadWriter = null;
            string encryptPropertyName = null;
            // Track the path of the currently-active encrypted container (object/array)
            // This guards against any accidental mutation of encryptPropertyName while the container is open
            string activeEncryptedPath = null;
            // Track nesting depth within the buffered encrypted container to know exactly when it closes
            // 0 means no active encrypted container. When we start buffering a container we set this to 1,
            // and increment/decrement on nested Start*/End* inside it. When it returns to 0, we flush.
            int encryptedContainerDepth = 0;
            // Track whether the JSON root is an object so we can append _ei correctly
            bool rootIsObject = false;
            RentArrayBufferWriter bufferWriter = null;
            // Reusable pooled scratch buffer used for multi-segment strings/numbers and property names
            byte[] tmpScratch = null;

            // Local helper to ensure pooled buffer capacity with minimal churn
            static void EnsureCapacity(ref byte[] scratch, int needed, ArrayPoolManager pool)
            {
                if (scratch == null || scratch.Length < needed)
                {
                    byte[] newBuf = pool.Rent(needed);
                    if (scratch != null)
                    {
                        pool.Return(scratch);
                    }
                    scratch = newBuf;
                }
            }

            try
            {
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

                        byte[] oldBuffer = buffer;
                        byte[] newBuffer = arrayPoolManager.Rent(capped);
                        oldBuffer.AsSpan(0, dataSize).CopyTo(newBuffer);
                        buffer = newBuffer;
                        arrayPoolManager.Return(oldBuffer);
                    }
                    else if (leftOver != 0)
                    {
                        buffer.AsSpan(dataSize - leftOver, leftOver).CopyTo(buffer);
                    }
                }

                // Do not dispose inputStream here; caller owns streams for consistency with decryptor.
                writer.Flush();
                bytesWritten = writer.BytesCommitted;
                if (outputStream.CanSeek)
                {
                    outputStream.Position = 0;
                }
            }
            finally
            {
                // Return pooled buffers
                arrayPoolManager.Return(buffer);
                if (tmpScratch != null)
                {
                    arrayPoolManager.Return(tmpScratch);
                }
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
                            // If this is the root start object, mark it so we can append _ei later
                            if (reader.CurrentDepth == 0)
                            {
                                rootIsObject = true;
                            }
                            if (encryptionPayloadWriter != null)
                            {
                                // Inside a buffered encrypted container
                                encryptionPayloadWriter.WriteStartObject();
                                encryptedContainerDepth++;
                            }
                            else if (encryptPropertyName != null)
                            {
                                // Start buffering this object as the encrypted payload for the current property
                                bufferWriter = new RentArrayBufferWriter();
                                encryptionPayloadWriter = new Utf8JsonWriter(bufferWriter);
                                encryptionPayloadWriter.WriteStartObject();
                                activeEncryptedPath = encryptPropertyName;
                                encryptedContainerDepth = 1; // start of the buffered container
                            }
                            else
                            {
                                // Regular object being written to the main writer
                                writer.WriteStartObject();
                            }

                            break;
                        case JsonTokenType.EndObject:
                            if (encryptionPayloadWriter != null)
                            {
                                // Closing an object inside the buffered encrypted container
                                encryptionPayloadWriter.WriteEndObject();
                                encryptedContainerDepth--;
                                if (encryptedContainerDepth == 0)
                                {
                                    encryptionPayloadWriter.Flush();
                                    (byte[] bytes, int length) = bufferWriter.WrittenBuffer;
                                    string pathForPayload = activeEncryptedPath ?? encryptPropertyName;
                                    (byte[] encBytes, int encLength) = TransformEncryptPayload(bytes, length, TypeMarker.Object, pathForPayload);
                                    writer.WriteBase64StringValue(encBytes.AsSpan(0, encLength));
                                    arrayPoolManager.Return(encBytes);
                                    propertiesEncrypted++;

                                    encryptPropertyName = null;
                                    activeEncryptedPath = null;

#pragma warning disable VSTHRD103 // Call async methods when in an async method - this method cannot be async, Utf8JsonReader is ref struct
                                    encryptionPayloadWriter.Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                                    encryptionPayloadWriter = null;
                                    bufferWriter.Dispose();
                                    bufferWriter = null;
                                }
                            }
                            else
                            {
                                // Closing an object on the main writer path
                                // If we're closing the root object (depth becomes 0 after this EndObject), append _ei before closing.
                                if (rootIsObject && reader.CurrentDepth == 0)
                                {
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
                                }

                                writer.WriteEndObject();
                            }

                            break;
                        case JsonTokenType.StartArray:
                            if (encryptionPayloadWriter != null)
                            {
                                // Inside a buffered encrypted container
                                encryptionPayloadWriter.WriteStartArray();
                                encryptedContainerDepth++;
                            }
                            else if (encryptPropertyName != null)
                            {
                                // Start buffering this array as the encrypted payload for the current property
                                bufferWriter = new RentArrayBufferWriter();
                                encryptionPayloadWriter = new Utf8JsonWriter(bufferWriter);
                                encryptionPayloadWriter.WriteStartArray();
                                activeEncryptedPath = encryptPropertyName;
                                encryptedContainerDepth = 1; // start of the buffered array
                            }
                            else
                            {
                                writer.WriteStartArray();
                            }

                            break;
                        case JsonTokenType.EndArray:
                            if (encryptionPayloadWriter != null)
                            {
                                encryptionPayloadWriter.WriteEndArray();
                                encryptedContainerDepth--;
                                if (encryptedContainerDepth == 0)
                                {
                                    encryptionPayloadWriter.Flush();
                                    (byte[] bytes, int length) = bufferWriter.WrittenBuffer;
                                    string pathForPayload = activeEncryptedPath ?? encryptPropertyName;
                                    (byte[] encBytes, int encLength) = TransformEncryptPayload(bytes, length, TypeMarker.Array, pathForPayload);
                                    writer.WriteBase64StringValue(encBytes.AsSpan(0, encLength));
                                    arrayPoolManager.Return(encBytes);
                                    propertiesEncrypted++;

                                    encryptPropertyName = null;
                                    activeEncryptedPath = null;

#pragma warning disable VSTHRD103 // Call async methods when in an async method - this method cannot be async, Utf8JsonReader is ref struct
                                    encryptionPayloadWriter.Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                                    encryptionPayloadWriter = null;
                                    bufferWriter.Dispose();
                                    bufferWriter = null;
                                }
                            }
                            else
                            {
                                writer.WriteEndArray();
                            }

                            break;
                        case JsonTokenType.PropertyName:
                            // Maintain the current encrypted path while writing nested properties.
                            // Only resolve/reset encryptPropertyName for top-level properties (depth == 1).
                            if (reader.CurrentDepth == 1)
                            {
                                if (topLevelNameUtf8.Count != 0)
                                {
                                    string matchedFullPath = null;
                                    for (int i = 0; i < topLevelNameUtf8.Count; i++)
                                    {
                                        if (reader.ValueTextEquals(topLevelNameUtf8[i]))
                                        {
                                            matchedFullPath = topLevelFullPaths[i];
                                            break;
                                        }
                                    }

                                    encryptPropertyName = matchedFullPath; // may be null if not encrypted
                                }
                                else
                                {
                                    encryptPropertyName = null;
                                }
                            }
                            // For nested properties (depth > 1), do not modify encryptPropertyName so we keep the outer encrypted path.

                            if (!reader.HasValueSequence)
                            {
                                currentWriter.WritePropertyName(reader.ValueSpan);
                            }
                            else
                            {
                                int estimatedLength = (int)reader.ValueSequence.Length;
                                EnsureCapacity(ref tmpScratch, Math.Max(estimatedLength, 64), arrayPoolManager);
                                int copied = reader.CopyString(tmpScratch);
                                currentWriter.WritePropertyName(tmpScratch.AsSpan(0, copied));
                            }
                            break;
                        case JsonTokenType.Comment: // Skipped via reader options
                            currentWriter.WriteCommentValue(reader.ValueSpan);
                            break;
                        case JsonTokenType.String:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                byte[] bytes = arrayPoolManager.Rent(reader.ValueSpan.Length);
                                int length = reader.CopyString(bytes);
                                // At this point we encrypt a top-level primitive; encryptPropertyName must be set.
                                (byte[] encBytes, int encLength) = TransformEncryptPayload(bytes, length, TypeMarker.String, encryptPropertyName);

                                // Early return temp string buffer
                                arrayPoolManager.Return(bytes);
                                currentWriter.WriteBase64StringValue(encBytes.AsSpan(0, encLength));
                                arrayPoolManager.Return(encBytes);
                                encryptPropertyName = null;
                                propertiesEncrypted++;
                            }
                            else
                            {
                                if (!reader.HasValueSequence && !reader.ValueIsEscaped)
                                {
                                    currentWriter.WriteStringValue(reader.ValueSpan);
                                }
                                else
                                {
                                    int estimatedLength = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                                    EnsureCapacity(ref tmpScratch, Math.Max(estimatedLength, 64), arrayPoolManager);
                                    int copied = reader.CopyString(tmpScratch);
                                    currentWriter.WriteStringValue(tmpScratch.AsSpan(0, copied));
                                }
                            }

                            break;
                        case JsonTokenType.Number:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                (TypeMarker typeMarker, byte[] bytes, int length) = SerializeNumber(reader.ValueSpan, arrayPoolManager);
                                (byte[] encBytes, int encLength) = TransformEncryptPayload(bytes, length, typeMarker, encryptPropertyName);

                                // Early return temp number buffer
                                arrayPoolManager.Return(bytes);
                                currentWriter.WriteBase64StringValue(encBytes.AsSpan(0, encLength));
                                arrayPoolManager.Return(encBytes);
                                encryptPropertyName = null;
                                propertiesEncrypted++;
                            }
                            else
                            {
                                if (!reader.HasValueSequence)
                                {
                                    currentWriter.WriteRawValue(reader.ValueSpan, true);
                                }
                                else
                                {
                                    int len = (int)reader.ValueSequence.Length;
                                    EnsureCapacity(ref tmpScratch, Math.Max(len, 32), arrayPoolManager);
                                    int offset = 0;
                                    foreach (ReadOnlyMemory<byte> segment in reader.ValueSequence)
                                    {
                                        segment.Span.CopyTo(tmpScratch.AsSpan(offset));
                                        offset += segment.Length;
                                    }
                                    currentWriter.WriteRawValue(tmpScratch.AsSpan(0, len), true);
                                }
                            }

                            break;
                        case JsonTokenType.True:
                            if (encryptPropertyName != null && encryptionPayloadWriter == null)
                            {
                                (byte[] bytes, int length) = Serialize(true, arrayPoolManager);
                                (byte[] encBytes, int encLength) = TransformEncryptPayload(bytes, length, TypeMarker.Boolean, encryptPropertyName);

                                // Return the serialized boolean input buffer promptly
                                arrayPoolManager.Return(bytes);
                                currentWriter.WriteBase64StringValue(encBytes.AsSpan(0, encLength));
                                arrayPoolManager.Return(encBytes);
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
                                (byte[] encBytes, int encLength) = TransformEncryptPayload(bytes, length, TypeMarker.Boolean, encryptPropertyName);

                                // Return the serialized boolean input buffer promptly
                                arrayPoolManager.Return(bytes);
                                currentWriter.WriteBase64StringValue(encBytes.AsSpan(0, encLength));
                                arrayPoolManager.Return(encBytes);
                                encryptPropertyName = null;
                                propertiesEncrypted++;
                            }
                            else
                            {
                                currentWriter.WriteBooleanValue(false);
                            }

                            break;
                        case JsonTokenType.Null:
                            // If we're inside an encrypted container, the null must be part of the payload.
                            if (encryptPropertyName != null && encryptionPayloadWriter != null)
                            {
                                currentWriter.WriteNullValue();
                                // keep encryptPropertyName until the container closes
                            }
                            else
                            {
                                currentWriter.WriteNullValue();
                                encryptPropertyName = null;
                            }
                            break;
                    }
                }

                state = reader.CurrentState;
                return reader.BytesConsumed;
            }

        (byte[] buffer, int length) TransformEncryptPayload(byte[] payload, int payloadSize, TypeMarker typeMarker, string path)
            {
                // Defensive: ensure we always have a non-null path for metadata/tracking
                if (path == null)
                {
                    path = activeEncryptedPath ?? encryptPropertyName;
                }
                byte[] processedBytes = payload;
                int processedBytesLength = payloadSize;

                if (compressor != null && payloadSize >= encryptionOptions.CompressionOptions.MinimalCompressedLength && path != null)
                {
                    byte[] compressedBytes = arrayPoolManager.Rent(BrotliCompressor.GetMaxCompressedSize(payloadSize));
                    // Use the explicit path for this payload (container or primitive). If path is null, skip compression and recording.
                    processedBytesLength = compressor.Compress(compressedPaths, path, processedBytes, payloadSize, compressedBytes);
                    processedBytes = compressedBytes;
                    compressedPathsCompressed++;
                }

                (byte[] encryptedBytes, int encryptedBytesCount) = this.Encryptor.Encrypt(encryptionKey, typeMarker, processedBytes, processedBytesLength, arrayPoolManager);

                // If we created a temporary compressed buffer, return it now
                if (!ReferenceEquals(processedBytes, payload) && compressor != null)
                {
                    arrayPoolManager.Return(processedBytes);
                }

                if (path != null)
                {
                    pathsEncrypted.Add(path);
                }
                return (encryptedBytes, encryptedBytesCount);
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

// Restore disabled StyleCop rules
#pragma warning restore SA1028
#pragma warning restore SA1508
#pragma warning restore SA1507
#pragma warning restore SA1505
#pragma warning restore SA1137
#pragma warning restore SA1515
#pragma warning restore SA1510
#pragma warning restore SA1513