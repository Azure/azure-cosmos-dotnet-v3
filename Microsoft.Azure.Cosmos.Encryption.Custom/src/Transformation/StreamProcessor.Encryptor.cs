// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    // Streaming JSON encryptor:
    // - Parses with Utf8JsonReader and writes with Utf8JsonWriter (no DOM).
    // - Encrypts values of top-level properties whose paths match configured candidates.
    // - For object/array values under encrypted properties, buffers their JSON, then encrypts the entire payload.
    // - Emits _ei metadata just before closing the root object.
    internal partial class StreamProcessor
    {
        private static readonly byte[] EncryptionPropertiesNameBytes = Encoding.UTF8.GetBytes(Constants.EncryptedInfo);

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
            writer.WritePropertyName(EncryptionPropertiesNameBytes);
            writer.WriteStartObject();

            writer.WriteNumber(Constants.EncryptionFormatVersion, formatVersion);
            writer.WriteString(Constants.EncryptionAlgorithm, encryptionAlgorithm);
            writer.WriteString(Constants.EncryptionDekId, dataEncryptionKeyId);

            if (encryptedData == null)
            {
                writer.WritePropertyName(Constants.EncryptedData);
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteBase64String(Constants.EncryptedData, encryptedData);
            }

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

            writer.WriteNumber(Constants.CompressionAlgorithm, (int)compressionAlgorithm);

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

        internal async Task EncryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(inputStream);
            ArgumentNullException.ThrowIfNull(outputStream);
            ArgumentNullException.ThrowIfNull(encryptor);
            ArgumentNullException.ThrowIfNull(encryptionOptions);
            if (!inputStream.CanRead)
            {
                throw new ArgumentException("Input stream must be readable.", nameof(inputStream));
            }

            if (!outputStream.CanWrite)
            {
                throw new ArgumentException("Output stream must be writable.", nameof(outputStream));
            }

            long bytesRead = 0;
            long bytesWritten = 0;
            long startTimestamp = Stopwatch.GetTimestamp();

            int pathsCapacity = 0;
            if (encryptionOptions.PathsToEncrypt is ICollection<string> coll)
            {
                pathsCapacity = coll.Count;
            }

            using ArrayPoolManager arrayPoolManager = new ArrayPoolManager();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(
                encryptionOptions.DataEncryptionKeyId,
                encryptionOptions.EncryptionAlgorithm,
                cancellationToken).ConfigureAwait(false);

            BrotliCompressor compressor =
                encryptionOptions.CompressionOptions.Algorithm == CompressionOptions.CompressionAlgorithm.Brotli
                    ? new BrotliCompressor(encryptionOptions.CompressionOptions.CompressionLevel)
                    : null;

            CandidatePaths candidatePaths = CandidatePaths.Build(encryptionOptions.PathsToEncrypt);
            int formatVersion = encryptionOptions.CompressionOptions.Algorithm != CompressionOptions.CompressionAlgorithm.None ? 4 : 3;

            using Utf8JsonWriter writer = new Utf8JsonWriter(outputStream, StreamProcessor.JsonWriterOptions);

            // Read buffer
            byte[] buffer = arrayPoolManager.Rent(this.initialBufferSize);
            int leftOver = 0;
            JsonReaderState readerState = new JsonReaderState(StreamProcessor.JsonReaderOptions);

            EncryptionPipeline pipeline = new EncryptionPipeline(
                writer,
                encryptionKey,
                encryptionOptions,
                compressor,
                candidatePaths,
                formatVersion,
                pathsCapacity,
                arrayPoolManager,
                readerState);

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int read = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken).ConfigureAwait(false);
                    int dataSize = read + leftOver;
                    bytesRead += read;

                    bool isFinalBlock = read == 0;
                    long consumed = pipeline.ProcessBufferChunk(buffer.AsSpan(0, dataSize), isFinalBlock);

                    leftOver = dataSize - (int)consumed;

                    if (isFinalBlock)
                    {
                        break;
                    }

                    // If no progress (token larger than buffer), grow buffer with cap.
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

                writer.Flush();
                bytesWritten = writer.BytesCommitted;

                if (outputStream.CanSeek)
                {
                    outputStream.Position = 0;
                }
            }
            finally
            {
                arrayPoolManager.Return(buffer);
                pipeline.Dispose();
            }

            diagnosticsContext?.SetMetric("encrypt.bytesRead", bytesRead);
            diagnosticsContext?.SetMetric("encrypt.bytesWritten", bytesWritten);
            diagnosticsContext?.SetMetric("encrypt.propertiesEncrypted", pipeline.PropertiesEncrypted);
            diagnosticsContext?.SetMetric("encrypt.compressedPathsCompressed", pipeline.CompressedPathsCompressed);

#if NET8_0_OR_GREATER
            long elapsedMs = (long)Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
#else
            long elapsedTicks = Stopwatch.GetTimestamp() - startTimestamp;
            long elapsedMs = (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
#endif
            diagnosticsContext?.SetMetric("encrypt.elapsedMs", elapsedMs);
        }

        private sealed class EncryptionPipeline : IDisposable
        {
            private const int ScratchMinForStrings = 64;
            private const int ScratchMinForNumbers = 32;

            private readonly Utf8JsonWriter writer;
            private readonly MdeEncryptor mdeEncryptor = new MdeEncryptor();
            private readonly DataEncryptionKey encryptionKey;
            private readonly string encryptionAlgorithmName;
            private readonly string dataEncryptionKeyId;
            private readonly CompressionOptions.CompressionAlgorithm compressionAlgorithm;
            private readonly int minimalCompressedLength;
            private readonly BrotliCompressor compressor;
            private readonly CandidatePaths candidatePaths;
            private readonly int formatVersion;
            private readonly ArrayPoolManager pool;

            private readonly List<string> pathsEncrypted;
            private readonly RentArrayBufferWriter payloadBuffer;
            private readonly Utf8JsonWriter payloadWriter;
            private Dictionary<string, int> compressedPaths;

            private byte[] scratch;
            private JsonReaderState readerState;

            // Buffering state
            private Utf8JsonWriter currentPayloadWriter; // null when not buffering encrypted container
            private string pendingEncryptedPath; // set when a top-level property is identified to encrypt
            private string bufferingPath; // path of currently buffered container
            private int bufferedDepth; // >0 when buffering an encrypted container

            internal long PropertiesEncrypted { get; private set; }

            internal long CompressedPathsCompressed { get; private set; }

            internal EncryptionPipeline(
                Utf8JsonWriter writer,
                DataEncryptionKey encryptionKey,
                EncryptionOptions options,
                BrotliCompressor compressor,
                CandidatePaths candidatePaths,
                int formatVersion,
                int pathsCapacity,
                ArrayPoolManager pool,
                JsonReaderState initialReaderState)
            {
                this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
                this.encryptionKey = encryptionKey ?? throw new ArgumentNullException(nameof(encryptionKey));
                this.encryptionAlgorithmName = options?.EncryptionAlgorithm ?? throw new ArgumentNullException(nameof(options));
                this.dataEncryptionKeyId = options.DataEncryptionKeyId;
                this.compressionAlgorithm = options.CompressionOptions.Algorithm;
                this.minimalCompressedLength = options.CompressionOptions.MinimalCompressedLength;
                this.compressor = compressor; // may be null
                this.candidatePaths = candidatePaths ?? throw new ArgumentNullException(nameof(candidatePaths));
                this.formatVersion = formatVersion;
                this.pool = pool ?? throw new ArgumentNullException(nameof(pool));

                this.pathsEncrypted = pathsCapacity > 0 ? new List<string>(pathsCapacity) : new List<string>();
                this.payloadBuffer = new RentArrayBufferWriter();
                this.payloadWriter = new Utf8JsonWriter(this.payloadBuffer);
                this.readerState = initialReaderState;
            }

            public void Dispose()
            {
#pragma warning disable VSTHRD103
                this.payloadWriter?.Dispose();
                this.payloadBuffer?.Dispose();
#pragma warning restore VSTHRD103

                if (this.scratch != null)
                {
                    this.pool.Return(this.scratch);
                    this.scratch = null;
                }
            }

            internal long ProcessBufferChunk(ReadOnlySpan<byte> span, bool isFinalBlock)
            {
                Utf8JsonReader reader = new Utf8JsonReader(span, isFinalBlock, this.readerState);

                while (reader.Read())
                {
                    Utf8JsonWriter targetWriter = this.currentPayloadWriter ?? this.writer;

                    switch (reader.TokenType)
                    {
                        case JsonTokenType.StartObject:
                            if (this.currentPayloadWriter != null)
                            {
                                this.currentPayloadWriter.WriteStartObject();
                                this.bufferedDepth++;
                            }
                            else if (this.pendingEncryptedPath != null)
                            {
                                this.BeginBufferingContainer(isArray: false, this.pendingEncryptedPath);
                            }
                            else
                            {
                                this.writer.WriteStartObject();
                            }

                            break;

                        case JsonTokenType.EndObject:
                            if (this.currentPayloadWriter != null)
                            {
                                this.currentPayloadWriter.WriteEndObject();
                                this.bufferedDepth--;
                                if (this.bufferedDepth == 0)
                                {
                                    this.EndBufferingContainer(TypeMarker.Object);
                                }
                            }
                            else
                            {
                                // Closing root object: append _ei metadata just before closing
                                if (reader.CurrentDepth == 0)
                                {
                                    StreamProcessor.WriteEncryptionInfo(
                                        this.writer,
                                        this.formatVersion,
                                        this.encryptionAlgorithmName,
                                        this.dataEncryptionKeyId,
                                        this.pathsEncrypted,
                                        this.compressionAlgorithm,
                                        this.compressedPaths,
                                        null);
                                }

                                this.writer.WriteEndObject();
                            }

                            break;

                        case JsonTokenType.StartArray:
                            if (this.currentPayloadWriter != null)
                            {
                                this.currentPayloadWriter.WriteStartArray();
                                this.bufferedDepth++;
                            }
                            else if (this.pendingEncryptedPath != null)
                            {
                                this.BeginBufferingContainer(isArray: true, this.pendingEncryptedPath);
                            }
                            else
                            {
                                this.writer.WriteStartArray();
                            }

                            break;

                        case JsonTokenType.EndArray:
                            if (this.currentPayloadWriter != null)
                            {
                                this.currentPayloadWriter.WriteEndArray();
                                this.bufferedDepth--;
                                if (this.bufferedDepth == 0)
                                {
                                    this.EndBufferingContainer(TypeMarker.Array);
                                }
                            }
                            else
                            {
                                this.writer.WriteEndArray();
                            }

                            break;

                        case JsonTokenType.PropertyName:
                            if (reader.CurrentDepth == 1)
                            {
                                int nameLen = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                                this.pendingEncryptedPath = this.candidatePaths.TryMatch(ref reader, nameLen, out string path) ? path : null;
                            }

                            if (!reader.HasValueSequence)
                            {
                                targetWriter.WritePropertyName(reader.ValueSpan);
                            }
                            else
                            {
                                int estimatedLength = (int)reader.ValueSequence.Length;
                                EnsureCapacity(ref this.scratch, Math.Max(estimatedLength, ScratchMinForStrings), this.pool);
                                int copied = reader.CopyString(this.scratch);
                                targetWriter.WritePropertyName(this.scratch.AsSpan(0, copied));
                            }

                            break;

                        case JsonTokenType.String:
                            if (this.pendingEncryptedPath != null && this.currentPayloadWriter == null)
                            {
                                int estimatedLength = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                                EnsureCapacity(ref this.scratch, Math.Max(estimatedLength, ScratchMinForStrings), this.pool);
                                int len = reader.CopyString(this.scratch);

                                this.EncryptAndWritePrimitive(TypeMarker.String, this.scratch, len, this.pendingEncryptedPath);
                                this.pendingEncryptedPath = null;
                                this.PropertiesEncrypted++;
                            }
                            else
                            {
                                if (!reader.HasValueSequence && !reader.ValueIsEscaped)
                                {
                                    targetWriter.WriteStringValue(reader.ValueSpan);
                                }
                                else
                                {
                                    int estimatedLength = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                                    EnsureCapacity(ref this.scratch, Math.Max(estimatedLength, ScratchMinForStrings), this.pool);
                                    int copied = reader.CopyString(this.scratch);
                                    targetWriter.WriteStringValue(this.scratch.AsSpan(0, copied));
                                }
                            }

                            break;

                        case JsonTokenType.Number:
                            if (this.pendingEncryptedPath != null && this.currentPayloadWriter == null)
                            {
                                ReadOnlySpan<byte> numberSpan;
                                if (!reader.HasValueSequence)
                                {
                                    numberSpan = reader.ValueSpan;
                                }
                                else
                                {
                                    int len = CopySequenceToScratch(reader.ValueSequence, ref this.scratch, ScratchMinForNumbers, this.pool);
                                    numberSpan = this.scratch.AsSpan(0, len);
                                }

                                (TypeMarker marker, byte[] bytes, int length) = StreamProcessor.SerializeNumber(numberSpan, this.pool);
                                (byte[] encBytes, int encLen) = this.EncryptPayload(bytes, length, marker, this.pendingEncryptedPath);

                                this.pool.Return(bytes);
                                targetWriter.WriteBase64StringValue(encBytes.AsSpan(0, encLen));
                                this.pool.Return(encBytes);

                                this.pendingEncryptedPath = null;
                                this.PropertiesEncrypted++;
                            }
                            else
                            {
                                if (!reader.HasValueSequence)
                                {
                                    targetWriter.WriteRawValue(reader.ValueSpan, true);
                                }
                                else
                                {
                                    int len = CopySequenceToScratch(reader.ValueSequence, ref this.scratch, ScratchMinForNumbers, this.pool);
                                    targetWriter.WriteRawValue(this.scratch.AsSpan(0, len), true);
                                }
                            }

                            break;

                        case JsonTokenType.True:
                            if (this.pendingEncryptedPath != null && this.currentPayloadWriter == null)
                            {
                                (byte[] bytes, int length) = StreamProcessor.Serialize(true, this.pool);
                                (byte[] encBytes, int encLen) = this.EncryptPayload(bytes, length, TypeMarker.Boolean, this.pendingEncryptedPath);

                                this.pool.Return(bytes);
                                targetWriter.WriteBase64StringValue(encBytes.AsSpan(0, encLen));
                                this.pool.Return(encBytes);

                                this.pendingEncryptedPath = null;
                                this.PropertiesEncrypted++;
                            }
                            else
                            {
                                targetWriter.WriteBooleanValue(true);
                            }

                            break;

                        case JsonTokenType.False:
                            if (this.pendingEncryptedPath != null && this.currentPayloadWriter == null)
                            {
                                (byte[] bytes, int length) = StreamProcessor.Serialize(false, this.pool);
                                (byte[] encBytes, int encLen) = this.EncryptPayload(bytes, length, TypeMarker.Boolean, this.pendingEncryptedPath);

                                this.pool.Return(bytes);
                                targetWriter.WriteBase64StringValue(encBytes.AsSpan(0, encLen));
                                this.pool.Return(encBytes);

                                this.pendingEncryptedPath = null;
                                this.PropertiesEncrypted++;
                            }
                            else
                            {
                                targetWriter.WriteBooleanValue(false);
                            }

                            break;

                        case JsonTokenType.Null:
                            // Null top-level values under an encrypted property are not encrypted by contract.
                            targetWriter.WriteNullValue();
                            if (this.currentPayloadWriter == null)
                            {
                                this.pendingEncryptedPath = null;
                            }

                            break;
                    }
                }

                this.readerState = reader.CurrentState;
                return reader.BytesConsumed;
            }

            private void BeginBufferingContainer(bool isArray, string path)
            {
                this.payloadBuffer.Clear();
                this.payloadWriter.Reset(this.payloadBuffer);

                if (isArray)
                {
                    this.payloadWriter.WriteStartArray();
                }
                else
                {
                    this.payloadWriter.WriteStartObject();
                }

                this.currentPayloadWriter = this.payloadWriter;
                this.bufferingPath = path;
                this.bufferedDepth = 1;
            }

            private void EndBufferingContainer(TypeMarker marker)
            {
                this.currentPayloadWriter.Flush();
                (byte[] bytes, int len) = this.payloadBuffer.WrittenBuffer;

                (byte[] encBytes, int encLen) = this.EncryptPayload(bytes, len, marker, this.bufferingPath);
                this.writer.WriteBase64StringValue(encBytes.AsSpan(0, encLen));
                this.pool.Return(encBytes);

                this.PropertiesEncrypted++;

                // Reset buffering state
                this.pendingEncryptedPath = null;
                this.bufferingPath = null;
                this.currentPayloadWriter = null;
                this.bufferedDepth = 0;
                this.payloadBuffer.Clear();
            }

            private void EncryptAndWritePrimitive(TypeMarker marker, byte[] buffer, int length, string path)
            {
                (byte[] encBytes, int encLen) = this.EncryptPayload(buffer, length, marker, path);
                (this.currentPayloadWriter ?? this.writer).WriteBase64StringValue(encBytes.AsSpan(0, encLen));
                this.pool.Return(encBytes);
            }

            private (byte[] buffer, int length) EncryptPayload(byte[] payload, int payloadSize, TypeMarker typeMarker, string path)
            {
                byte[] processedBytes = payload;
                int processedLength = payloadSize;

                if (this.compressor != null && payloadSize >= this.minimalCompressedLength)
                {
                    this.compressedPaths ??= new Dictionary<string, int>();
                    byte[] compressedBytes = this.pool.Rent(BrotliCompressor.GetMaxCompressedSize(payloadSize));

                    processedLength = this.compressor.Compress(this.compressedPaths, path, processedBytes, payloadSize, compressedBytes);
                    processedBytes = compressedBytes;
                    this.CompressedPathsCompressed++;
                }

                (byte[] encryptedBytes, int encryptedCount) = this.mdeEncryptor.Encrypt(this.encryptionKey, typeMarker, processedBytes, processedLength, this.pool);

                if (!ReferenceEquals(processedBytes, payload) && this.compressor != null)
                {
                    this.pool.Return(processedBytes);
                }

                this.pathsEncrypted.Add(path);
                return (encryptedBytes, encryptedCount);
            }

            private static void EnsureCapacity(ref byte[] scratch, int needed, ArrayPoolManager pool)
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

            private static int CopySequenceToScratch(ReadOnlySequence<byte> sequence, ref byte[] scratch, int minCapacity, ArrayPoolManager pool)
            {
                int length = (int)sequence.Length;
                EnsureCapacity(ref scratch, Math.Max(length, minCapacity), pool);
                sequence.CopyTo(scratch.AsSpan(0, length));
                return length;
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