// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.Buffers.Text;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography.Serializers;

    internal partial class StreamProcessor
    {
        private const string EncryptedInfoPropertyPath = "/" + Constants.EncryptedInfo;

        // Buffer/stream thresholds
        private const int MaxBufferSizeBytes = 32 * 1024 * 1024; // 32 MB cap for safety
        private const int BufferGrowthMinIncrement = 4096; // 4 KB minimal headroom to trigger growth
        private const int SmallPayloadMaxBytes = 2048; // One-shot parse threshold
        private const int ProbeSize = 2048; // Non-seekable probe size
        private const int StackallocStringThreshold = 256; // Small strings/property names use stackalloc
        private const int Base64StackThreshold = 4096; // Small base64 decode temp uses stackalloc

        private static readonly SqlBitSerializer SqlBoolSerializer = new SqlBitSerializer();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new SqlFloatSerializer();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new SqlBigIntSerializer();

        private static readonly JsonReaderOptions JsonReaderOptions =
            new JsonReaderOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        private static readonly JsonWriterOptions JsonWriterOptions = new JsonWriterOptions()
        {
            Indented = false,
            SkipValidation = true,
        };

        internal static int InitialBufferSize { get; set; } = 8192;

        private readonly int initialBufferSize;

        internal StreamProcessor()
            : this(InitialBufferSize)
        {
        }

        internal StreamProcessor(int initialBufferSize)
        {
            this.initialBufferSize = initialBufferSize > 0 ? initialBufferSize : InitialBufferSize;
        }

        // Used for decryption logic (fast-path when exact type match); key retrieval uses method parameter
        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

        internal async Task<DecryptionContext> DecryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            Encryptor encryptor, // key provider
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ValidateEncryptionProperties(properties);

            using ArrayPoolManager pool = new ArrayPoolManager();
            DataEncryptionKey dataKey = await encryptor
                .GetEncryptionKeyAsync(properties.DataEncryptionKeyId, properties.EncryptionAlgorithm, cancellationToken)
                .ConfigureAwait(false);

            int expectedDecrypted = properties.EncryptedPaths is ICollection<string> c ? c.Count : 0;
            List<string> pathsDecrypted = new List<string>(expectedDecrypted);

            using StreamChunkedBufferWriter chunkedWriter =
                new StreamChunkedBufferWriter(outputStream, pool, this.initialBufferSize);
            await using Utf8JsonWriter writer = new Utf8JsonWriter(chunkedWriter, JsonWriterOptions);

            ProcessingContext ctx = new ProcessingContext(
                pool: pool,
                writer: writer,
                properties: properties,
                diagnostics: diagnosticsContext,
                decryptor: this.Encryptor,
                dataKey: dataKey,
                expectedDecrypted: expectedDecrypted,
                pathsDecrypted: pathsDecrypted);

            JsonReaderState readerState = new JsonReaderState(JsonReaderOptions);
            SkipState skip = default;
            byte[] buffer = null;
            int leftOver = 0;
            bool isFinalBlock = false;

            try
            {
                long startTimestamp = Stopwatch.GetTimestamp();
                ctx.StartTimestamp = startTimestamp;

                // Try small payload one-shot for seekable streams
                if (inputStream.CanSeek)
                {
                    long remaining = inputStream.Length - inputStream.Position;
                    if (remaining > 0 && remaining <= SmallPayloadMaxBytes)
                    {
                        int len = (int)remaining;
                        byte[] oneShot = pool.Rent(Bucket(len));
                        try
                        {
                            int total = 0;
                            while (total < len)
                            {
                                int toRead = Math.Min(len - total, oneShot.Length - total);
                                int r = await inputStream.ReadAsync(oneShot.AsMemory(total, toRead), cancellationToken)
                                    .ConfigureAwait(false);
                                if (r == 0)
                                {
                                    break;
                                }

                                total += r;
                            }

                            int read = Math.Min(total, len);
                            ctx.BytesRead += read;

                            _ = this.ProcessBufferChunk(
                                ctx,
                                oneShot.AsSpan(0, read),
                                ref readerState,
                                isFinalBlock: true,
                                ref skip);

                            isFinalBlock = true;
                        }
                        finally
                        {
                            pool.Return(oneShot);
                        }
                    }
                }
                else
                {
                    // Non-seekable probe: read up to ProbeSize once
                    buffer = pool.Rent(Bucket(ProbeSize));
                    int read = await inputStream.ReadAsync(buffer.AsMemory(0, ProbeSize), cancellationToken)
                        .ConfigureAwait(false);
                    ctx.BytesRead += read;

                    if (read > 0)
                    {
                        bool finalProbe = read < ProbeSize;
                        long consumed = this.ProcessBufferChunk(
                            ctx,
                            buffer.AsSpan(0, read),
                            ref readerState,
                            isFinalBlock: finalProbe,
                            ref skip);

                        leftOver = read - (int)consumed;
                        if (finalProbe && leftOver == 0)
                        {
                            isFinalBlock = true;
                        }
                    }
                    else
                    {
                        isFinalBlock = true;
                    }
                }

                // Streaming loop
                while (!isFinalBlock)
                {
                    buffer ??= pool.Rent(this.initialBufferSize);

                    int read = await inputStream
                        .ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken)
                        .ConfigureAwait(false);
                    int dataSize = read + leftOver;
                    ctx.BytesRead += read;
                    isFinalBlock = dataSize == 0;

                    long consumed = this.ProcessBufferChunk(
                        ctx,
                        buffer.AsSpan(0, dataSize),
                        ref readerState,
                        isFinalBlock,
                        ref skip);

                    leftOver = dataSize - (int)consumed;

                    if (dataSize > 0 && leftOver == dataSize)
                    {
                        // Grow buffer to fit token spanning buffers
                        int target = Math.Max(buffer.Length * 2, leftOver + BufferGrowthMinIncrement);
                        int bucketedGrow = Bucket(target);
                        int capped = Math.Min(MaxBufferSizeBytes, bucketedGrow);
                        if (buffer.Length >= capped)
                        {
                            throw new InvalidOperationException(
                                $"JSON token exceeds maximum supported size of {MaxBufferSizeBytes} bytes at path {ctx.CurrentPropertyPath ?? "<unknown>"}.");
                        }

                        byte[] old = buffer;
                        byte[] newBuf = pool.Rent(capped);
                        old.AsSpan(0, dataSize).CopyTo(newBuf);
                        buffer = newBuf;
                        pool.Return(old);
                    }
                    else if (leftOver != 0)
                    {
                        buffer.AsSpan(dataSize - leftOver, leftOver).CopyTo(buffer);
                    }
                }

                writer.Flush();
                chunkedWriter.FinalFlush();

                long bytesWritten = writer.BytesCommitted;
                if (outputStream.CanSeek)
                {
                    outputStream.Position = 0;
                }

                // Metrics
                ctx.Diagnostics?.SetMetric("decrypt.bytesRead", ctx.BytesRead);
                ctx.Diagnostics?.SetMetric("decrypt.bytesWritten", bytesWritten);
                ctx.Diagnostics?.SetMetric("decrypt.propertiesDecrypted", ctx.PropertiesDecrypted);
                ctx.Diagnostics?.SetMetric("decrypt.compressedPathsDecompressed", ctx.CompressedPathsDecompressed);
                ctx.Diagnostics?.SetMetric("decrypt.writerFlushes", chunkedWriter.Flushes);
                long elapsedTicks = Stopwatch.GetTimestamp() - ctx.StartTimestamp;
                long elapsedMs = (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
                ctx.Diagnostics?.SetMetric("decrypt.elapsedMs", elapsedMs);

                // Preserve legacy behavior: return null context if nothing decrypted and no encrypted paths requested
                if (ctx.PathsDecrypted.Count == 0 && ctx.ExpectedDecrypted == 0)
                {
                    return null;
                }

                return EncryptionProcessor.CreateDecryptionContext(ctx.PathsDecrypted, properties.DataEncryptionKeyId);
            }
            finally
            {
                if (buffer != null)
                {
                    pool.Return(buffer);
                }

                // return pooled scratch arrays
                ctx.Dispose();
            }
        }

        private static void ValidateEncryptionProperties(EncryptionProperties properties)
        {
            if (properties.EncryptionFormatVersion != EncryptionFormatVersion.Mde &&
                properties.EncryptionFormatVersion != EncryptionFormatVersion.MdeWithCompression)
            {
                throw new NotSupportedException(
                    $"Unknown encryption format version: {properties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            bool containsCompressed = properties.CompressedEncryptedPaths?.Count > 0;
            if (containsCompressed && properties.CompressionAlgorithm != CompressionOptions.CompressionAlgorithm.Brotli)
            {
                throw new NotSupportedException($"Unknown compression algorithm {properties.CompressionAlgorithm}");
            }
        }

        private long ProcessBufferChunk(
            ProcessingContext ctx,
            ReadOnlySpan<byte> bufferSpan,
            ref JsonReaderState readerState,
            bool isFinalBlock,
            ref SkipState skip)
        {
            Utf8JsonReader reader = new Utf8JsonReader(bufferSpan, isFinalBlock, readerState);

            while (reader.Read())
            {
                // Handle ongoing skip of EncryptedInfo value across buffers
                if (skip.Active)
                {
                    if (skip.FirstValueTokenPending)
                    {
                        skip.FirstValueTokenPending = false;
                        if (reader.TokenType == JsonTokenType.StartObject ||
                            reader.TokenType == JsonTokenType.StartArray)
                        {
                            skip.Depth = 1;
                        }
                        else
                        {
                            skip.Active = false;
                        }

                        continue;
                    }

                    if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                    {
                        skip.Depth++;
                        continue;
                    }

                    if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
                    {
                        skip.Depth--;
                        if (skip.Depth == 0)
                        {
                            skip.Active = false;
                        }
                    }

                    continue;
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        {
                            // Special-case skip for encrypted info property
                            if (reader.ValueTextEquals(Constants.EncryptedInfo))
                            {
                                ctx.CurrentPropertyPath = EncryptedInfoPropertyPath;

                                if (!reader.TrySkip())
                                {
                                    skip.Active = true;
                                    skip.FirstValueTokenPending = true;
                                    skip.Depth = 0;
                                }

                                // Skip writing property name and its value entirely
                                continue;
                            }

                            // Only top-level properties are eligible for encryption by path
                            if (reader.CurrentDepth == 1)
                            {
                                string matchedFullPath = null;
                                int propLen = reader.HasValueSequence
                                    ? (int)reader.ValueSequence.Length
                                    : reader.ValueSpan.Length;
                                if (ctx.PathMatcher.TryMatch(ref reader, propLen, out string path))
                                {
                                    matchedFullPath = path;
                                }

                                if (matchedFullPath != null)
                                {
                                    ctx.DecryptPropertyName = matchedFullPath;
                                    ctx.CurrentPropertyPath = matchedFullPath;
                                }
                                else
                                {
                                    ctx.DecryptPropertyName = null;
                                    ctx.CurrentPropertyPath = null;
                                }
                            }
                            else
                            {
                                ctx.DecryptPropertyName = null;
                                ctx.CurrentPropertyPath = null;
                            }

                            // Write property name with minimal allocation
                            if (!reader.HasValueSequence)
                            {
                                ctx.Writer.WritePropertyName(reader.ValueSpan);
                            }
                            else
                            {
                                int estimatedLength = (int)reader.ValueSequence.Length;
                                Span<byte> nameSpan = CopyStringToSpan(ref reader, ref ctx.TempScratch, ctx.Pool, estimatedLength);
                                ctx.Writer.WritePropertyName(nameSpan);
                            }

                            break;
                        }

                    case JsonTokenType.String:
                        {
                            if (ctx.DecryptPropertyName == null)
                            {
                                // Pass-through string
                                try
                                {
                                    if (!reader.HasValueSequence && !reader.ValueIsEscaped)
                                    {
                                        ctx.Writer.WriteStringValue(reader.ValueSpan);
                                    }
                                    else
                                    {
                                        int estimatedLength = reader.HasValueSequence
                                            ? (int)reader.ValueSequence.Length
                                            : reader.ValueSpan.Length;
                                        Span<byte> strSpan = CopyStringToSpan(ref reader, ref ctx.TempScratch, ctx.Pool, estimatedLength);
                                        ctx.Writer.WriteStringValue(strSpan);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    throw new InvalidOperationException(
                                        $"Invalid UTF-8 while writing string at path {ctx.CurrentPropertyPath ?? "<unknown>"}",
                                        ex);
                                }
                            }
                            else
                            {
                                this.DecryptAndWriteEncryptedValue(ctx, ref reader, ctx.DecryptPropertyName);
                                ctx.PathsDecrypted.Add(ctx.DecryptPropertyName);
                                ctx.PropertiesDecrypted++;
                            }

                            ctx.DecryptPropertyName = null;
                            break;
                        }

                    case JsonTokenType.Number:
                        {
                            ctx.DecryptPropertyName = null;

                            if (!reader.HasValueSequence)
                            {
                                ctx.Writer.WriteRawValue(reader.ValueSpan, skipInputValidation: true);
                            }
                            else
                            {
                                int len = (int)reader.ValueSequence.Length;
                                if (len <= StackallocStringThreshold)
                                {
                                    Span<byte> local = stackalloc byte[StackallocStringThreshold];
                                    reader.ValueSequence.CopyTo(local);
                                    ctx.Writer.WriteRawValue(local[..len], true);
                                }
                                else
                                {
                                    EnsureCapacity(ref ctx.TempScratch, Math.Max(len, 32), ctx.Pool);
                                    reader.ValueSequence.CopyTo(ctx.TempScratch);
                                    ctx.Writer.WriteRawValue(ctx.TempScratch.AsSpan(0, len), true);
                                }
                            }

                            break;
                        }

                    case JsonTokenType.True:
                        ctx.DecryptPropertyName = null;
                        ctx.Writer.WriteBooleanValue(true);
                        break;

                    case JsonTokenType.False:
                        ctx.DecryptPropertyName = null;
                        ctx.Writer.WriteBooleanValue(false);
                        break;

                    case JsonTokenType.Null:
                        ctx.DecryptPropertyName = null;
                        ctx.Writer.WriteNullValue();
                        break;

                    case JsonTokenType.StartObject:
                        ctx.DecryptPropertyName = null;
                        ctx.Writer.WriteStartObject();
                        break;

                    case JsonTokenType.EndObject:
                        ctx.DecryptPropertyName = null;
                        ctx.Writer.WriteEndObject();
                        break;

                    case JsonTokenType.StartArray:
                        ctx.DecryptPropertyName = null;
                        ctx.Writer.WriteStartArray();
                        break;

                    case JsonTokenType.EndArray:
                        ctx.DecryptPropertyName = null;
                        ctx.Writer.WriteEndArray();
                        break;
                }
            }

            readerState = reader.CurrentState;
            return reader.BytesConsumed;
        }

        private void DecryptAndWriteEncryptedValue(
            ProcessingContext ctx,
            ref Utf8JsonReader reader,
            string pathLabel)
        {
            // Decode base64 to cipher buffer
            int srcLen = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
            int maxDecoded = Base64.GetMaxDecodedFromUtf8Length(srcLen);
            EnsureCapacity(ref ctx.CipherScratch, maxDecoded, ctx.Pool);

            Span<byte> cipher = ctx.CipherScratch.AsSpan();
            int cipherTextLength;

            if (!reader.HasValueSequence && !reader.ValueIsEscaped)
            {
                OperationStatus status =
                    Base64.DecodeFromUtf8(reader.ValueSpan, cipher, out int consumed, out int written);
                if (status != OperationStatus.Done || consumed != srcLen)
                {
                    throw new InvalidOperationException(
                        $"Base64 decoding failed for encrypted field at path {pathLabel}: {status}.");
                }

                cipherTextLength = written;
            }
            else
            {
                if (srcLen <= Base64StackThreshold)
                {
                    Span<byte> local = stackalloc byte[srcLen];
                    int copied = reader.CopyString(local);
                    OperationStatus status =
                        Base64.DecodeFromUtf8(local[..copied], cipher, out int consumed, out int written);
                    if (status != OperationStatus.Done || consumed != copied)
                    {
                        throw new InvalidOperationException(
                            $"Base64 decoding failed for encrypted field at path {pathLabel}: {status}.");
                    }

                    cipherTextLength = written;
                }
                else
                {
                    EnsureCapacity(ref ctx.TempScratch, Math.Max(srcLen, 64), ctx.Pool);
                    int copied = reader.CopyString(ctx.TempScratch);
                    OperationStatus status = Base64.DecodeFromUtf8(
                        ctx.TempScratch.AsSpan(0, copied), cipher, out int consumed, out int written);
                    if (status != OperationStatus.Done || consumed != copied)
                    {
                        throw new InvalidOperationException(
                            $"Base64 decoding failed for encrypted field at path {pathLabel}: {status}.");
                    }

                    cipherTextLength = written;
                }
            }

            // First byte (index 0) is type marker, ciphertext starts at offset 1
            TypeMarker marker = (TypeMarker)cipher[0];

            byte[] bytes;
            int processedBytes;

            if (ctx.IsBaseDecryptor)
            {
                int needed = ctx.DataKey.GetDecryptByteCount(cipherTextLength - 1);
                EnsureCapacity(ref ctx.PlainScratch, needed, ctx.Pool);

                int decryptedLength = ctx.DataKey.DecryptData(
                    ctx.CipherScratch,
                    cipherTextOffset: 1,
                    cipherTextLength: cipherTextLength - 1,
                    ctx.PlainScratch,
                    outputOffset: 0);

                if (decryptedLength < 0)
                {
                    throw new InvalidOperationException(
                        $"{nameof(DataEncryptionKey)} returned invalid plainText from {nameof(DataEncryptionKey.DecryptData)}.");
                }

                bytes = ctx.PlainScratch;
                processedBytes = decryptedLength;

                TryDecompressIfConfigured(ctx, pathLabel, ref bytes, ref processedBytes);
                ReadOnlySpan<byte> span = bytes.AsSpan(0, processedBytes);
                WriteDecryptedPayload(marker, span, ctx.Writer, ctx.Diagnostics, pathLabel);

                if (!ReferenceEquals(bytes, ctx.PlainScratch))
                {
                    ctx.Pool.Return(bytes);
                }
            }
            else
            {
                // Fallback to decrypt with rented buffer (previously used DecryptOwned before revert)
                (byte[] rentedPlain, int decryptedLen) = ctx.Decryptor.Decrypt(
                    ctx.DataKey,
                    ctx.CipherScratch,
                    cipherTextLength,
                    ctx.Pool);

                bytes = rentedPlain;
                processedBytes = decryptedLen;

                TryDecompressIfConfigured(ctx, pathLabel, ref bytes, ref processedBytes);
                ReadOnlySpan<byte> span = bytes.AsSpan(0, processedBytes);
                WriteDecryptedPayload(marker, span, ctx.Writer, ctx.Diagnostics, pathLabel);

                // Return any transient buffer used for decompression (if different from original decrypt buffer)
                if (!ReferenceEquals(bytes, rentedPlain))
                {
                    ctx.Pool.Return(bytes);
                }

                ctx.Pool.Return(rentedPlain);
            }
        }

        private static void WriteDecryptedPayload(
            TypeMarker marker,
            ReadOnlySpan<byte> payload,
            Utf8JsonWriter writer,
            CosmosDiagnosticsContext diagnosticsContext,
            string pathLabel)
        {
            switch (marker)
            {
                case TypeMarker.String:
                    try
                    {
                        writer.WriteStringValue(payload);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Invalid UTF-8 while writing decrypted string at path {pathLabel}", ex);
                    }

                    break;

                case TypeMarker.Long:
                    try
                    {
                        writer.WriteNumberValue(SqlLongSerializer.Deserialize(payload));
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to deserialize decrypted payload at path {pathLabel} as Long.", ex);
                    }

                    break;

                case TypeMarker.Double:
                    try
                    {
                        writer.WriteNumberValue(SqlDoubleSerializer.Deserialize(payload));
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to deserialize decrypted payload at path {pathLabel} as Double.", ex);
                    }

                    break;

                case TypeMarker.Boolean:
                    writer.WriteBooleanValue(SqlBoolSerializer.Deserialize(payload));
                    break;

                case TypeMarker.Null:
                    writer.WriteNullValue();
                    break;

                case TypeMarker.Array:
                case TypeMarker.Object:
                    writer.WriteRawValue(payload, skipInputValidation: true);
                    break;

                default:
                    using (diagnosticsContext?.CreateScope(
                               $"DecryptUnknownTypeMarker Path={pathLabel} Marker={(byte)marker}"))
                    {
                        writer.WriteRawValue(payload, skipInputValidation: true);
                    }

                    break;
            }
        }

        private static void TryDecompressIfConfigured(
            ProcessingContext ctx,
            string pathLabel,
            ref byte[] bytes,
            ref int length)
        {
            if (ctx.Decompressor == null || pathLabel == null || ctx.Properties.CompressedEncryptedPaths == null)
            {
                return;
            }

            if (!ctx.Properties.CompressedEncryptedPaths.TryGetValue(pathLabel, out int decompressedSize))
            {
                return;
            }

            const int maxDecompressedSizeBytes = MaxBufferSizeBytes;
            if (decompressedSize is <= 0 or > maxDecompressedSizeBytes)
            {
                throw new InvalidOperationException(
                    $"Invalid decompressed size {decompressedSize} for path {pathLabel}. Max allowed is {maxDecompressedSizeBytes}.");
            }

            byte[] decompressed = ctx.Pool.Rent(decompressedSize);
            int newLen = ctx.Decompressor.Decompress(bytes, length, decompressed);
            bytes = decompressed;
            length = newLen;
            ctx.CompressedPathsDecompressed++;
        }

        private static Span<byte> CopyStringToSpan(
            ref Utf8JsonReader reader,
            ref byte[] scratch,
            ArrayPoolManager pool,
            int estimatedLength)
        {
            EnsureCapacity(ref scratch, Math.Max(estimatedLength, 64), pool);
            int len = reader.CopyString(scratch);
            return scratch.AsSpan(0, len);
        }

        private static void EnsureCapacity(ref byte[] scratch, int needed, ArrayPoolManager pool)
        {
            if (scratch != null && scratch.Length >= needed)
            {
                return;
            }

            int bucketed = Bucket(needed);
            byte[] newBuf = pool.Rent(bucketed);
            if (scratch != null)
            {
                pool.Return(scratch);
            }

            scratch = newBuf;
        }

        private static int Bucket(int value)
        {
            const int minBucket = 64;
            if (value <= minBucket)
            {
                return minBucket;
            }

            if (value >= MaxBufferSizeBytes)
            {
                return MaxBufferSizeBytes;
            }

            // next power of two
            uint v = (uint)(value - 1);
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            int pow2 = (int)(v + 1);
            return pow2 > MaxBufferSizeBytes ? MaxBufferSizeBytes : pow2;
        }

        private struct SkipState
        {
            public bool Active;
            public bool FirstValueTokenPending;
            public int Depth;
        }

        private sealed class ProcessingContext : IDisposable
        {
            public ProcessingContext(
                ArrayPoolManager pool,
                Utf8JsonWriter writer,
                EncryptionProperties properties,
                CosmosDiagnosticsContext diagnostics,
                MdeEncryptor decryptor,
                DataEncryptionKey dataKey,
                int expectedDecrypted,
                List<string> pathsDecrypted)
            {
                this.Pool = pool;
                this.Writer = writer;
                this.Properties = properties;
                this.Diagnostics = diagnostics;
                this.Decryptor = decryptor;
                this.DataKey = dataKey;
                this.ExpectedDecrypted = expectedDecrypted;
                this.PathsDecrypted = pathsDecrypted;

                IEnumerable<string> encryptedPaths = properties.EncryptedPaths ?? Array.Empty<string>();
                this.PathMatcher = CandidatePaths.Build(encryptedPaths);

                bool containsCompressed = properties.CompressedEncryptedPaths?.Count > 0;
                this.Decompressor = containsCompressed ? new BrotliCompressor() : null;

                this.IsBaseDecryptor = decryptor.GetType() == typeof(MdeEncryptor);
            }

            public ArrayPoolManager Pool { get; }

            public Utf8JsonWriter Writer { get; }

            public EncryptionProperties Properties { get; }

            public CosmosDiagnosticsContext Diagnostics { get; }

            public MdeEncryptor Decryptor { get; }

            public DataEncryptionKey DataKey { get; }

            public CandidatePaths PathMatcher { get; }

            public BrotliCompressor Decompressor { get; }

            public bool IsBaseDecryptor { get; }

            // Scratch buffers (pooled) with ref-return properties to preserve by-ref passing semantics
            // while complying with SA1401 (fields should be private). Ref properties allow calls like
            // EnsureCapacity(ref ctx.TempScratch, ...) without exposing mutable fields publicly.
            private byte[] tempScratch;          // transient string / number copies
            private byte[] cipherScratch;        // decoded ciphertext (Base64 -> bytes)
            private byte[] plainScratch;         // decrypted plaintext prior to write

            public ref byte[] TempScratch => ref this.tempScratch;

            public ref byte[] CipherScratch => ref this.cipherScratch;

            public ref byte[] PlainScratch => ref this.plainScratch;

            // Streaming/decryption state
            public string CurrentPropertyPath { get; set; } // current full path (only for matched properties)

            public string DecryptPropertyName { get; set; } // non-null when next value token must be decrypted

            // Metrics (frequently updated)
            public long StartTimestamp { get; set; }

            public long BytesRead { get; set; }

            public long PropertiesDecrypted { get; set; }

            public long CompressedPathsDecompressed { get; set; }

            public int ExpectedDecrypted { get; set; } // expected properties decrypted (capacity hint)

            public List<string> PathsDecrypted { get; set; } // collects decrypted property paths

            public void Dispose()
            {
                if (this.tempScratch != null)
                {
                    this.Pool.Return(this.tempScratch);
                }

                if (this.cipherScratch != null)
                {
                    this.Pool.Return(this.cipherScratch);
                }

                if (this.plainScratch != null)
                {
                    this.Pool.Return(this.plainScratch);
                }
            }
        }
    }
}

#endif