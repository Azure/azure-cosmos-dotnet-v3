// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Buffers;
    using System.Buffers.Text;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography.Serializers;

    internal partial class StreamProcessor
    {
        private const string EncryptedInfoPropertyPath = "/" + Constants.EncryptedInfo;

        // Buffer/stream thresholds
        // Changed from const to adjustable static (via test hook) so tests can lower ceiling and exercise overflow logic without huge allocations.
        private const int DefaultMaxBufferSizeBytes = 32 * 1024 * 1024; // 32 MB cap for safety

        // Test hook (internal) to reduce maximum buffer size for overflow testing; null => use default.
        internal static int? TestMaxBufferSizeBytesOverride { get; set; }

        private static int MaxBufferSizeBytes => TestMaxBufferSizeBytesOverride ?? DefaultMaxBufferSizeBytes;

        private const int BufferGrowthMinIncrement = 4096; // 4 KB minimal headroom to trigger growth

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

            int expectedDecrypted = properties.EncryptedPaths?.Count ?? 0;
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

                (buffer, leftOver, isFinalBlock, readerState, skip) = await this.StreamProcessAsync(inputStream, ctx, pool, buffer, leftOver, readerState, skip, cancellationToken).ConfigureAwait(false);

                return FinalizeAndCreateContext(outputStream, chunkedWriter, writer, ctx, properties);
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
            if (properties.EncryptionFormatVersion != EncryptionFormatVersion.Mde)
            {
                throw new NotSupportedException(
                    $"Unknown encryption format version: {properties.EncryptionFormatVersion}. Only MDE format without compression is supported.");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static DecryptionContext FinalizeAndCreateContext(
            Stream outputStream,
            StreamChunkedBufferWriter chunkedWriter,
            Utf8JsonWriter writer,
            ProcessingContext ctx,
            EncryptionProperties properties)
        {
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
            ctx.Diagnostics?.SetMetric("decrypt.writerFlushes", chunkedWriter.Flushes);
            long elapsedTicks = Stopwatch.GetTimestamp() - ctx.StartTimestamp;
            long elapsedMs = (long)(elapsedTicks * 1000.0 / Stopwatch.Frequency);
            ctx.Diagnostics?.SetMetric("decrypt.elapsedMs", elapsedMs);

            if (ctx.PathsDecrypted.Count == 0 && ctx.ExpectedDecrypted == 0)
            {
                return null; // preserve legacy behavior
            }

            return EncryptionProcessor.CreateDecryptionContext(ctx.PathsDecrypted, properties.DataEncryptionKeyId);
        }

        private async Task<(byte[] buffer, int leftOver, bool isFinalBlock, JsonReaderState readerState, SkipState skip)> StreamProcessAsync(
            Stream inputStream,
            ProcessingContext ctx,
            ArrayPoolManager pool,
            byte[] buffer,
            int leftOver,
            JsonReaderState readerState,
            SkipState skip,
            CancellationToken cancellationToken)
        {
            bool isFinalBlock = false;
            while (!isFinalBlock)
            {
                buffer ??= pool.Rent(this.initialBufferSize);
                int read = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken).ConfigureAwait(false);
                int dataSize = read + leftOver;
                ctx.BytesRead += read;
                isFinalBlock = dataSize == 0;
                if (isFinalBlock)
                {
                    // Nothing left to parse; avoid final empty-segment validation that triggers depth error when previous segment already closed document.
                    break;
                }

                long consumed = this.ProcessBufferChunk(ctx, buffer.AsSpan(0, dataSize), ref readerState, isFinalBlock: false, ref skip);
                leftOver = dataSize - (int)consumed;

#if DEBUG
                if (read > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DecryptStream] iter read={read} dataSize={dataSize} consumed={consumed} leftOver={leftOver} final?={isFinalBlock} path={ctx.CurrentPropertyPath}");
                }
#endif

                // Early finalize if root closed
                if (ctx.RootClosed)
                {
                    if (leftOver > 0 && !IsAllWhitespace(buffer.AsSpan(dataSize - leftOver, leftOver)))
                    {
                        throw new InvalidOperationException("Trailing non-whitespace content after root JSON closed.");
                    }

                    byte[] drain = ArrayPool<byte>.Shared.Rent(512);
                    try
                    {
                        while (true)
                        {
                            int extra = await inputStream.ReadAsync(drain.AsMemory(0, drain.Length), cancellationToken).ConfigureAwait(false);
                            if (extra == 0)
                            {
                                break;
                            }

                            if (!IsAllWhitespace(drain.AsSpan(0, extra)))
                            {
                                throw new InvalidOperationException("Trailing non-whitespace content after root JSON closed.");
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(drain);
                    }

                    isFinalBlock = true;
                    leftOver = 0;
                    break;
                }

                if (dataSize > 0 && leftOver == dataSize)
                {
                    int target = Math.Max(buffer.Length * 2, leftOver + BufferGrowthMinIncrement);
                    int capped = Math.Min(MaxBufferSizeBytes, target);
                    if (buffer.Length >= capped)
                    {
                        throw new InvalidOperationException($"JSON token exceeds maximum supported size of {MaxBufferSizeBytes} bytes at path {ctx.CurrentPropertyPath ?? "<unknown>"}.");
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

            return (buffer, leftOver, true, readerState, skip);
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
                if (this.HandleActiveSkip(ref reader, ref skip))
                {
                    continue;
                }

                if (this.ProcessToken(ref reader, ctx, ref skip))
                {
                    continue; // token handler requested next iteration (e.g., encrypted info skip)
                }
            }

            readerState = reader.CurrentState;
            return reader.BytesConsumed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ProcessToken(ref Utf8JsonReader reader, ProcessingContext ctx, ref SkipState skip)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.PropertyName:
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

                        return true; // skip writing property name and its value
                    }

                    if (reader.CurrentDepth == 1)
                    {
                        string matchedFullPath = null;
                        int propLen = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
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

                case JsonTokenType.String:
                    if (ctx.DecryptPropertyName == null)
                    {
                        try
                        {
                            if (!reader.HasValueSequence && !reader.ValueIsEscaped)
                            {
                                ctx.Writer.WriteStringValue(reader.ValueSpan);
                            }
                            else
                            {
                                int estimatedLength = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                                Span<byte> strSpan = CopyStringToSpan(ref reader, ref ctx.TempScratch, ctx.Pool, estimatedLength);
                                ctx.Writer.WriteStringValue(strSpan);
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException($"Invalid UTF-8 while writing string at path {ctx.CurrentPropertyPath ?? "<unknown>"}", ex);
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

                case JsonTokenType.Number:
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
                    if (reader.CurrentDepth == 0)
                    {
                        ctx.RootSeen = true;
                    }

                    ctx.Writer.WriteStartObject();
                    break;

                case JsonTokenType.EndObject:
                    ctx.DecryptPropertyName = null;
                    ctx.Writer.WriteEndObject();
                    if (reader.CurrentDepth == 0 && ctx.RootSeen)
                    {
                        ctx.RootClosed = true;
                    }

                    break;

                case JsonTokenType.StartArray:
                    ctx.DecryptPropertyName = null;
                    if (reader.CurrentDepth == 0)
                    {
                        ctx.RootSeen = true;
                    }

                    ctx.Writer.WriteStartArray();
                    break;

                case JsonTokenType.EndArray:
                    ctx.DecryptPropertyName = null;
                    ctx.Writer.WriteEndArray();
                    if (reader.CurrentDepth == 0 && ctx.RootSeen)
                    {
                        ctx.RootClosed = true;
                    }

                    break;
            }

            return false; // do not force loop continue beyond normal flow
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HandleActiveSkip(ref Utf8JsonReader reader, ref SkipState skip)
        {
            if (!skip.Active)
            {
                return false;
            }

            if (skip.FirstValueTokenPending)
            {
                skip.FirstValueTokenPending = false;
                if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                {
                    skip.Depth = 1;
                }
                else
                {
                    // Primitive value: done skipping
                    skip.Active = false;
                }

                return true; // consume this token
            }

            if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
            {
                skip.Depth++;
                return true;
            }

            if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
            {
                skip.Depth--;
                if (skip.Depth == 0)
                {
                    skip.Active = false;
                }

                return true;
            }

            // Other tokens inside skipped metadata are ignored
            return true;
        }

        private static bool IsAllWhitespace(ReadOnlySpan<byte> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                byte b = span[i];
                if (b != 0x20 && b != 0x09 && b != 0x0A && b != 0x0D)
                {
                    return false;
                }
            }

            return true;
        }

        private void DecryptAndWriteEncryptedValue(
            ProcessingContext ctx,
            ref Utf8JsonReader reader,
            string pathLabel)
        {
            int srcLen = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
            int maxDecoded = Base64.GetMaxDecodedFromUtf8Length(srcLen);
            EnsureCapacity(ref ctx.CipherScratch, maxDecoded, ctx.Pool);

            Span<byte> cipher = ctx.CipherScratch.AsSpan();
            int cipherTextLength;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int DecodeValidated(ReadOnlySpan<byte> source, Span<byte> cipher, int expectedLen, string pathLabel)
            {
                OperationStatus status = Base64.DecodeFromUtf8(source, cipher, out int consumed, out int written);
                if (status != OperationStatus.Done || consumed != expectedLen)
                {
                    throw new InvalidOperationException(
                        $"Base64 decoding failed for encrypted field at path {pathLabel}: {status}.");
                }

                return written;
            }

            if (!reader.HasValueSequence && !reader.ValueIsEscaped)
            {
                cipherTextLength = DecodeValidated(reader.ValueSpan, cipher, srcLen, pathLabel);
            }
            else
            {
                if (srcLen <= Base64StackThreshold)
                {
                    Span<byte> local = stackalloc byte[srcLen];
                    int copied = reader.CopyString(local);
                    cipherTextLength = DecodeValidated(local[..copied], cipher, copied, pathLabel);
                }
                else
                {
                    EnsureCapacity(ref ctx.TempScratch, Math.Max(srcLen, 64), ctx.Pool);
                    int copied = reader.CopyString(ctx.TempScratch);
                    cipherTextLength = DecodeValidated(ctx.TempScratch.AsSpan(0, copied), cipher, copied, pathLabel);
                }
            }

            TypeMarker marker = (TypeMarker)cipher[0];
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

            ReadOnlySpan<byte> span = ctx.PlainScratch.AsSpan(0, decryptedLength);
            WriteDecryptedPayload(marker, span, ctx.Writer, ctx.Diagnostics, pathLabel);

            if (!ReferenceEquals(ctx.PlainScratch, ctx.PlainScratch))
            {
                ctx.Pool.Return(ctx.PlainScratch);
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

            const int minSize = 64;
            int requested = Math.Max(needed, minSize);
            int capped = Math.Min(requested, MaxBufferSizeBytes);
            byte[] newBuf = pool.Rent(capped);
            if (scratch != null)
            {
                pool.Return(scratch);
            }

            scratch = newBuf;
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

                IReadOnlyCollection<string> encryptedPaths = properties.EncryptedPaths ?? Array.Empty<string>();
                this.PathMatcher = CandidatePaths.Build(encryptedPaths);

                if (decryptor is not MdeEncryptor)
                {
                    throw new NotSupportedException("StreamProcessor currently supports only the MDE encryption format (MdeEncryptor hierarchy).");
                }
            }

            public ArrayPoolManager Pool { get; }

            public Utf8JsonWriter Writer { get; }

            public EncryptionProperties Properties { get; }

            public CosmosDiagnosticsContext Diagnostics { get; }

            public MdeEncryptor Decryptor { get; }

            public DataEncryptionKey DataKey { get; }

            public CandidatePaths PathMatcher { get; }

            // Scratch buffers (pooled) with ref-return properties to preserve by-ref passing semantics
            // while complying with SA1401 (fields should be private). Ref properties allow calls like
            // EnsureCapacity(ref ctx.TempScratch, ...) without exposing mutable fields publicly.
            private byte[] tempScratch;          // transient string / number copies
            private byte[] cipherScratch;        // decoded ciphertext (Base64 -> bytes)
            private byte[] plainScratch;         // decrypted plaintext prior to write

            public ref byte[] TempScratch => ref this.tempScratch;

            public ref byte[] CipherScratch => ref this.cipherScratch;

            public ref byte[] PlainScratch => ref this.plainScratch;

            public string CurrentPropertyPath { get; set; }

            public string DecryptPropertyName { get; set; }

            public long StartTimestamp { get; set; }

            public long BytesRead { get; set; }

            public long PropertiesDecrypted { get; set; }

            public int ExpectedDecrypted { get; set; }

            public List<string> PathsDecrypted { get; set; }

            public bool RootSeen { get; set; }

            public bool RootClosed { get; set; }

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