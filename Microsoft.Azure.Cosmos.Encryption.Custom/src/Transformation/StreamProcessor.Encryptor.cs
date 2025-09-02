// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
        private static readonly byte[] EncryptionPropertiesNameBytes = Encoding.UTF8.GetBytes(Constants.EncryptedInfo);

        // _ei is emitted manually to avoid allocations from JsonSerializer and DTOs.

        // Helper that writes the encryption metadata object (_ei) using the provided values
        // Emits all required fields and omits compressedEncryptedPaths when null.
        internal static void WriteEncryptionInfo(
            Utf8JsonWriter writer,
            int formatVersion,
            string encryptionAlgorithm,
            string dataEncryptionKeyId,
            IReadOnlyList<string> encryptedPaths,
            CompressionOptions.CompressionAlgorithm compressionAlgorithm,
            IReadOnlyDictionary<string, int> compressedEncryptedPaths,
            byte[] encryptedData)
        {
            // Property name: _ei
            writer.WritePropertyName(EncryptionPropertiesNameBytes);
            writer.WriteStartObject();

            // version, algorithm, key id
            writer.WriteNumber(Constants.EncryptionFormatVersion, formatVersion);
            writer.WriteString(Constants.EncryptionAlgorithm, encryptionAlgorithm);
            writer.WriteString(Constants.EncryptionDekId, dataEncryptionKeyId);

            // encrypted data (base64). Empty array serializes to empty string, matching JsonSerializer behavior
            writer.WriteBase64String(Constants.EncryptedData, encryptedData ?? Array.Empty<byte>());

            // encryptedPaths
            writer.WritePropertyName(Constants.EncryptedPaths);
            writer.WriteStartArray();
            if (encryptedPaths != null)
            {
                foreach (string p in encryptedPaths)
                {
                    writer.WriteStringValue(p);
                }
            }

            writer.WriteEndArray();

            // compression algorithm (enum written as number like JsonSerializer default)
            writer.WriteNumber(Constants.CompressionAlgorithm, (int)compressionAlgorithm);

            // compressedEncryptedPaths (optional)
            if (compressedEncryptedPaths != null)
            {
                writer.WritePropertyName(Constants.CompressedEncryptedPaths);
                writer.WriteStartObject();
                foreach (KeyValuePair<string, int> kvp in compressedEncryptedPaths)
                {
                    writer.WriteNumber(kvp.Key, kvp.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Helper that precomputes candidate top-level encrypted paths for fast, readable matching.
        /// Only paths starting with '/' and without additional '/' (i.e., top-level) are considered.
        /// Keeps a compact bitmask of UTF-8 name byte lengths to quickly rule out impossible matches.
        /// Avoids per-candidate substring allocation by slicing the original path at compare time.
        /// </summary>
        private sealed class CandidatePaths
        {
            private readonly struct Entry
            {
                public readonly string FullPath;      // e.g. "/foo"
                public readonly int NameCharLen;      // FullPath.Length - 1
                public readonly int NameUtf8Len;

                public Entry(string fullPath, int nameUtf8Len)
                {
                    this.FullPath = fullPath;
                    this.NameCharLen = fullPath.Length - 1;
                    this.NameUtf8Len = nameUtf8Len;
                }

                public ReadOnlySpan<char> NameChars => this.FullPath.AsSpan(1, this.NameCharLen);
            }

            private readonly Entry[] topLevel;
            private readonly ulong lengthMask;
            private readonly bool hasLongNames;
            private readonly string includesEmptyFullPath; // null unless "/" present

            private CandidatePaths(Entry[] topLevel, ulong lengthMask, bool hasLongNames, string includesEmptyFullPath)
            {
                this.topLevel = topLevel;
                this.lengthMask = lengthMask;
                this.hasLongNames = hasLongNames;
                this.includesEmptyFullPath = includesEmptyFullPath;
            }

            public static CandidatePaths Build(IEnumerable<string> paths)
            {
                if (paths == null)
                {
                    return new CandidatePaths(Array.Empty<Entry>(), 0UL, false, null);
                }

                List<Entry> list = new List<Entry>();
                ulong mask = 0UL;
                bool hasLong = false;
                string includesEmpty = null;

                // Iterate the provided paths and extract only top-level candidates
                foreach (string p in paths)
                {
                    if (string.IsNullOrEmpty(p) || p[0] != '/')
                    {
                        continue;
                    }

                    bool isEmpty = p.Length <= 1;
                    ReadOnlySpan<char> nameSpan = isEmpty ? ReadOnlySpan<char>.Empty : p.AsSpan(1);
                    if (!isEmpty && nameSpan.IndexOf('/') >= 0)
                    {
                        continue; // only support top-level names here
                    }

                    if (isEmpty)
                    {
                        includesEmpty = p; // track "/" separately; do not add to list
                        mask |= 1UL << 0;
                        continue;
                    }

                    // Compute UTF-8 byte length of the name without leading '/'
                    int nameUtf8Len = Encoding.UTF8.GetByteCount(nameSpan);
                    if ((uint)nameUtf8Len < 64)
                    {
                        mask |= 1UL << nameUtf8Len;
                    }
                    else
                    {
                        hasLong = true;
                    }

                    list.Add(new Entry(p, nameUtf8Len));
                }

                return new CandidatePaths(list.ToArray(), mask, hasLong, includesEmpty);
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private bool IsLengthPossible(int utf8Len)
            {
                return (utf8Len < 64) ? ((this.lengthMask & (1UL << utf8Len)) != 0) : this.hasLongNames;
            }

            public bool TryMatch(ref Utf8JsonReader reader, int propNameUtf8Len, out string matchedFullPath)
            {
                // Handle empty name candidate (path "/")
                if (propNameUtf8Len == 0 && this.includesEmptyFullPath != null)
                {
                    matchedFullPath = this.includesEmptyFullPath;
                    return true;
                }

                if (!this.IsLengthPossible(propNameUtf8Len))
                {
                    matchedFullPath = null;
                    return false;
                }

                // Linear scan over small candidate set; early out on len mismatch.
                foreach (ref readonly Entry e in this.topLevel.AsSpan())
                {
                    if (e.NameUtf8Len != propNameUtf8Len)
                    {
                        continue;
                    }

                    // Allocation-free compare using Utf8JsonReader.ValueTextEquals(ReadOnlySpan<char>)
                    if (reader.ValueTextEquals(e.NameChars))
                    {
                        matchedFullPath = e.FullPath;
                        return true;
                    }
                }

                matchedFullPath = null;
                return false;
            }
        }

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

            // Pre-size pathsEncrypted if we know the candidate count
            int pathsCapacity = 0;
            if (encryptionOptions.PathsToEncrypt is ICollection<string> coll)
            {
                pathsCapacity = coll.Count;
            }

            List<string> pathsEncrypted = pathsCapacity > 0 ? new List<string>(pathsCapacity) : new List<string>();

            using ArrayPoolManager arrayPoolManager = new ArrayPoolManager();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionOptions.DataEncryptionKeyId, encryptionOptions.EncryptionAlgorithm, cancellationToken);

            bool compressionEnabled = encryptionOptions.CompressionOptions.Algorithm != CompressionOptions.CompressionAlgorithm.None;

            BrotliCompressor compressor = encryptionOptions.CompressionOptions.Algorithm == CompressionOptions.CompressionAlgorithm.Brotli
                ? new BrotliCompressor(encryptionOptions.CompressionOptions.CompressionLevel) : null;

            // Pre-compute candidate encrypted paths for fast matching at property names.
            CandidatePaths candidatePaths = CandidatePaths.Build(encryptionOptions.PathsToEncrypt);

            Dictionary<string, int> compressedPaths = null; // allocate lazily on first compressed payload

            // Write directly to the provided output stream; we'll compute bytes written via Utf8JsonWriter.BytesCommitted
            Utf8JsonWriter writer = new Utf8JsonWriter(outputStream, StreamProcessor.JsonWriterOptions);

            byte[] buffer = arrayPoolManager.Rent(this.initialBufferSize);

            JsonReaderState state = new JsonReaderState(StreamProcessor.JsonReaderOptions);

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

            // Reusable pooled buffer and writer for encrypted payload containers
            RentArrayBufferWriter reusablePayloadBuffer = new RentArrayBufferWriter();
            Utf8JsonWriter reusablePayloadWriter = new Utf8JsonWriter(reusablePayloadBuffer);

            // Helper moved to class scope below

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

                // Dispose main writer
#pragma warning disable VSTHRD103 // Call async methods when in an async method - Utf8JsonWriter does not implement IAsyncDisposable
                writer?.Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method

                // Dispose reusable payload writer and its buffer
#pragma warning disable VSTHRD103 // Call async methods when in an async method - Utf8JsonWriter/RentArrayBufferWriter do not implement IAsyncDisposable
                reusablePayloadWriter?.Dispose();
                reusablePayloadBuffer?.Dispose();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
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
                Utf8JsonReader reader = new Utf8JsonReader(buffer, isFinalBlock, state);

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
                                // Start buffering this object as the encrypted payload for the current property (reuse pooled buffer/writer)
                                bufferWriter = reusablePayloadBuffer;
                                encryptionPayloadWriter = reusablePayloadWriter;
                                bufferWriter.Clear();
                                encryptionPayloadWriter.Reset(bufferWriter);
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

                                    // Keep reusable instances for next use
                                    encryptionPayloadWriter = null;
                                    bufferWriter.Clear();
                                    bufferWriter = null;
                                }
                            }
                            else
                            {
                                // Closing an object on the main writer path
                                // If we're closing the root object (depth becomes 0 after this EndObject), append _ei before closing.
                                if (rootIsObject && reader.CurrentDepth == 0)
                                {
                                    int formatVersion = (encryptionOptions.CompressionOptions.Algorithm != CompressionOptions.CompressionAlgorithm.None) ? 4 : 3;

                                    WriteEncryptionInfo(
                                        writer,
                                        formatVersion,
                                        encryptionOptions.EncryptionAlgorithm,
                                        encryptionOptions.DataEncryptionKeyId,
                                        pathsEncrypted,
                                        encryptionOptions.CompressionOptions.Algorithm,
                                        compressedPaths,
                                        Array.Empty<byte>());
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
                                // Start buffering this array as the encrypted payload for the current property (reuse pooled buffer/writer)
                                bufferWriter = reusablePayloadBuffer;
                                encryptionPayloadWriter = reusablePayloadWriter;
                                bufferWriter.Clear();
                                encryptionPayloadWriter.Reset(bufferWriter);
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

                                    // Keep reusable instances for next use
                                    encryptionPayloadWriter = null;
                                    bufferWriter.Clear();
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
                                string matchedFullPath = null;
                                int propNameUtf8Len = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                                if (candidatePaths.TryMatch(ref reader, propNameUtf8Len, out string path))
                                {
                                    matchedFullPath = path;
                                }

                                encryptPropertyName = matchedFullPath; // may be null if not encrypted
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
                                int estimatedLength = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                                EnsureCapacity(ref tmpScratch, Math.Max(estimatedLength, 64), arrayPoolManager);
                                int length = reader.CopyString(tmpScratch);

                                // Encrypt a top-level string primitive using the shared scratch buffer to avoid extra allocations.
                                (byte[] encBytes, int encLength) = TransformEncryptPayload(tmpScratch, length, TypeMarker.String, encryptPropertyName);

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
                                ReadOnlySpan<byte> numberSpan;
                                if (!reader.HasValueSequence)
                                {
                                    numberSpan = reader.ValueSpan;
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

                                    numberSpan = tmpScratch.AsSpan(0, len);
                                }

                                (TypeMarker typeMarker, byte[] bytes, int length) = SerializeNumber(numberSpan, arrayPoolManager);
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
                path ??= activeEncryptedPath ?? encryptPropertyName;

                byte[] processedBytes = payload;
                int processedBytesLength = payloadSize;

                if (compressor != null && payloadSize >= encryptionOptions.CompressionOptions.MinimalCompressedLength && path != null)
                {
                    // Lazily allocate the compressedPaths dictionary on first use
                    if (compressedPaths == null)
                    {
                        int capacity = pathsCapacity > 0 ? Math.Min(pathsCapacity, 8) : 8;
                        compressedPaths = new Dictionary<string, int>(capacity);
                    }

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
