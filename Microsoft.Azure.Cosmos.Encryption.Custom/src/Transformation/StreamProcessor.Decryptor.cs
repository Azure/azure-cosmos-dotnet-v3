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
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Data.Encryption.Cryptography.Serializers;

    internal partial class StreamProcessor
    {
        private const string EncryptionPropertiesPath = "/" + Constants.EncryptedInfo;

        // Cap buffer growth to prevent unbounded memory usage for extremely large tokens
        private const int MaxBufferSizeBytes = 32 * 1024 * 1024; // 32 MB
        private const int BufferGrowthMinIncrement = 4096; // 4 KB minimal additional headroom

        private static readonly SqlBitSerializer SqlBoolSerializer = new SqlBitSerializer();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new SqlFloatSerializer();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new SqlBigIntSerializer();

        private static readonly JsonReaderOptions JsonReaderOptions =
            new JsonReaderOptions() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        private static readonly JsonWriterOptions JsonWriterOptions = new JsonWriterOptions()
        {
            Indented = false, SkipValidation = true,
        };

        // Use 8 KB as an amortized sweet spot; avoids early resizes while not too large for small docs.
        internal static int InitialBufferSize { get; set; } = 8192;

        private readonly int initialBufferSize;

        internal StreamProcessor()
            : this(InitialBufferSize)
        {
        }

        internal StreamProcessor(int initialBufferSize)
        {
            // Ensure a positive buffer size; fallback to legacy default if invalid
            this.initialBufferSize = initialBufferSize > 0 ? initialBufferSize : InitialBufferSize;
        }

        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

        internal async Task<DecryptionContext> DecryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            Encryptor encryptor,
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            long bytesRead = 0;
            long propertiesDecrypted = 0;
            long compressedPathsDecompressed = 0;
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

            if (properties.EncryptionFormatVersion != EncryptionFormatVersion.Mde &&
                properties.EncryptionFormatVersion != EncryptionFormatVersion.MdeWithCompression)
            {
                throw new NotSupportedException(
                    $"Unknown encryption format version: {properties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            bool containsCompressed = properties.CompressedEncryptedPaths?.Count > 0;

            if (properties.CompressionAlgorithm != CompressionOptions.CompressionAlgorithm.Brotli && containsCompressed)
            {
                throw new NotSupportedException($"Unknown compression algorithm {properties.CompressionAlgorithm}");
            }

            using ArrayPoolManager arrayPoolManager = new ArrayPoolManager();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(properties.DataEncryptionKeyId, properties.EncryptionAlgorithm, cancellationToken);

            // Track decrypted paths; pre-size when collection count is known
            int expectedDecrypted = 0;
            if (properties.EncryptedPaths is ICollection<string> encColl)
            {
                expectedDecrypted = encColl.Count;
            }

            List<string> pathsDecrypted = new List<string>(expectedDecrypted);

            // Write through a chunked buffer writer to avoid large contiguous growth when producing JSON output.
            using StreamChunkedBufferWriter chunkedWriter = new StreamChunkedBufferWriter(outputStream, arrayPoolManager, this.initialBufferSize);
            using Utf8JsonWriter writer = new Utf8JsonWriter(chunkedWriter, StreamProcessor.JsonWriterOptions);

            // Lazily rent the main buffer only if we don't take the small-payload fast path
            byte[] buffer = null;

            // Reusable pooled scratch buffers to reduce per-field rents; declared outside try so they can be returned in finally
            byte[] tmpScratch = null; // used for large multi-segment strings, property names, and numbers
            byte[] cipherScratch = null; // shared base64 decode buffer across all encrypted properties
            byte[] plainScratch = null; // shared plaintext buffer when using base encryptor (avoids per-field rent)
            try
            {
                JsonReaderState state = new JsonReaderState(StreamProcessor.JsonReaderOptions);

                // Reuse a single decompressor instance per call to avoid per-field allocations
                BrotliCompressor decompressor = containsCompressed ? new BrotliCompressor() : null;

                // Build candidate matcher for fast, allocation-free top-level path matching (supports "/").
                IEnumerable<string> encryptedPaths = properties.EncryptedPaths ?? Array.Empty<string>();
                CandidatePaths candidatePaths = CandidatePaths.Build(encryptedPaths);

                int leftOver = 0;

                // Local helper: ensure pooled buffer capacity with bucketed growth (next power-of-two) to
                // increase ArrayPool hit rate. Without bucketing we might rent many subtly different lengths
                // (e.g., 1372, 1419, 1510 ...) that the shared pool keeps as distinct buckets, increasing LOH
                // pressure and fragmentation over time when large docs with varying token sizes are processed.
                static void EnsureCapacity(ref byte[] scratch, int needed, ArrayPoolManager pool)
                {
                    if (scratch != null && scratch.Length >= needed)
                    {
                        return; // already large enough
                    }

                    // Round to next power-of-two up to MaxBufferSizeBytes to maximize reuse while capping growth.
                    // Minimum practical bucket of 64 bytes (caller often already does Math.Max(x, 64)).
                    static int Bucket(int value)
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

                        // safeguard (should not exceed MaxBufferSizeBytes due to earlier check)
                        return pow2 > MaxBufferSizeBytes ? MaxBufferSizeBytes : pow2;
                    }

                    int bucketed = Bucket(needed);
                    byte[] newBuf = pool.Rent(bucketed);
                    if (scratch != null)
                    {
                        pool.Return(scratch);
                    }

                    scratch = newBuf;
                }

                bool isFinalBlock = false;
                string decryptPropertyName = null;
                string currentPropertyPath = null;

                // Robust cross-buffer skipping state for the special encrypted info property value
                bool skippingEi = false;
                bool skipEiFirstTokenPending = false;
                int skipEiContainerDepth = 0;

                // Local helper: decrypt a single property value using the provided context/state
                void TransformDecryptProperty(
                    ref Utf8JsonReader reader,
                    ArrayPoolManager arrayPoolManager,
                    MdeEncryptor encryptor,
                    DataEncryptionKey encryptionKey,
                    BrotliCompressor decompressor,
                    EncryptionProperties properties,
                    Utf8JsonWriter writer,
                    CosmosDiagnosticsContext diagnosticsContext,
                    ref long compressedPathsDecompressed,
                    string decryptPropertyName,
                    string currentPropertyPath)
                {
                    static string PathLabel(string a, string b)
                    {
                        return a ?? b ?? "<unknown>";
                    }

                    int srcLen = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                    int maxDecoded = Base64.GetMaxDecodedFromUtf8Length(srcLen);
                    EnsureCapacity(ref cipherScratch, maxDecoded, arrayPoolManager);
                    Span<byte> cipher = cipherScratch.AsSpan();
                    int cipherTextLength;

                    try
                    {
                        if (!reader.HasValueSequence && !reader.ValueIsEscaped)
                        {
                            OperationStatus status = Base64.DecodeFromUtf8(
                                reader.ValueSpan,
                                cipher,
                                out int consumed,
                                out int written);
                            if (status != OperationStatus.Done || consumed != srcLen)
                            {
                                throw new InvalidOperationException(
                                    $"Base64 decoding failed for encrypted field at path {PathLabel(decryptPropertyName, currentPropertyPath)}: {status}.");
                            }

                            cipherTextLength = written;
                        }
                        else
                        {
                            // Multi-segment or escaped; use stackalloc for small sizes to avoid renting
                            const int stackThreshold = 256;
                            if (srcLen <= stackThreshold)
                            {
                                Span<byte> local = stackalloc byte[stackThreshold];
                                int copied = reader.CopyString(local);
                                OperationStatus status = Base64.DecodeFromUtf8(
                                    local[..copied],
                                    cipher,
                                    out int consumed,
                                    out int written);
                                if (status != OperationStatus.Done || consumed != copied)
                                {
                                    throw new InvalidOperationException(
                                        $"Base64 decoding failed for encrypted field at path {PathLabel(decryptPropertyName, currentPropertyPath)}: {status}.");
                                }

                                cipherTextLength = written;
                            }
                            else
                            {
                                EnsureCapacity(ref tmpScratch, Math.Max(srcLen, 64), arrayPoolManager);
                                int copied = reader.CopyString(tmpScratch);
                                OperationStatus status = Base64.DecodeFromUtf8(
                                    tmpScratch.AsSpan(0, copied),
                                    cipher,
                                    out int consumed,
                                    out int written);
                                if (status != OperationStatus.Done || consumed != copied)
                                {
                                    throw new InvalidOperationException(
                                        $"Base64 decoding failed for encrypted field at path {PathLabel(decryptPropertyName, currentPropertyPath)}: {status}.");
                                }

                                cipherTextLength = written;
                            }
                        }

                        // Type marker (not encrypted) at index 0
                        TypeMarker marker = (TypeMarker)cipher[0];

                        byte[] bytes;
                        int processedBytes;

                        // Fast path: if encryptor is the base implementation (not overridden), decrypt directly into reusable scratch buffer
                        if (encryptor.GetType() == typeof(MdeEncryptor))
                        {
                            int needed = encryptionKey.GetDecryptByteCount(cipherTextLength - 1);
                            EnsureCapacity(ref plainScratch, needed, arrayPoolManager);
                            int decryptedLength = encryptionKey.DecryptData(
                                cipherScratch,
                                cipherTextOffset: 1,
                                cipherTextLength: cipherTextLength - 1,
                                plainScratch,
                                outputOffset: 0);
                            if (decryptedLength < 0)
                            {
                                throw new InvalidOperationException(
                                    $"{nameof(DataEncryptionKey)} returned null plainText from {nameof(DataEncryptionKey.DecryptData)}.");
                            }

                            bytes = plainScratch;
                            processedBytes = decryptedLength;
                        }
                        else
                        {
                            using PooledByteOwner owner = encryptor.DecryptOwned(
                                encryptionKey,
                                cipherScratch,
                                cipherTextLength,
                                arrayPoolManager);
                            bytes = owner.Array;
                            processedBytes = owner.Length;

                            // Optional decompression
                            if (decompressor != null &&
                                decryptPropertyName != null &&
                                properties.CompressedEncryptedPaths != null &&
                                properties.CompressedEncryptedPaths.TryGetValue(decryptPropertyName, out int decompressedSizeCustom))
                            {
                                const int MaxDecompressedSizeBytes = MaxBufferSizeBytes;
                                if (decompressedSizeCustom <= 0 || decompressedSizeCustom > MaxDecompressedSizeBytes)
                                {
                                    throw new InvalidOperationException(
                                        $"Invalid decompressed size {decompressedSizeCustom} for path {PathLabel(decryptPropertyName, currentPropertyPath)}. Max allowed is {MaxDecompressedSizeBytes}.");
                                }

                                byte[] decompressed = arrayPoolManager.Rent(decompressedSizeCustom);
                                int newLen = decompressor.Decompress(bytes, processedBytes, decompressed);
                                bytes = decompressed;
                                processedBytes = newLen;
                                compressedPathsDecompressed++;
                            }

                            ReadOnlySpan<byte> customSpan = bytes.AsSpan(0, processedBytes);
                            try
                            {
                                switch (marker)
                                {
                                    case TypeMarker.String:
                                        try
                                        {
                                            writer.WriteStringValue(customSpan);
                                        }
                                        catch (Exception ex)
                                        {
                                            throw new InvalidOperationException(
                                                $"Invalid UTF-8 while writing decrypted string at path {PathLabel(decryptPropertyName, currentPropertyPath)}",
                                                ex);
                                        }

                                        break;

                                    case TypeMarker.Long:
                                        try
                                        {
                                            writer.WriteNumberValue(SqlLongSerializer.Deserialize(customSpan));
                                        }
                                        catch (Exception ex)
                                        {
                                            throw new InvalidOperationException(
                                                $"Failed to deserialize decrypted payload at path {PathLabel(decryptPropertyName, currentPropertyPath)} as Long.",
                                                ex);
                                        }

                                        break;

                                    case TypeMarker.Double:
                                        try
                                        {
                                            writer.WriteNumberValue(SqlDoubleSerializer.Deserialize(customSpan));
                                        }
                                        catch (Exception ex)
                                        {
                                            throw new InvalidOperationException(
                                                $"Failed to deserialize decrypted payload at path {PathLabel(decryptPropertyName, currentPropertyPath)} as Double.",
                                                ex);
                                        }

                                        break;

                                    case TypeMarker.Boolean:
                                        writer.WriteBooleanValue(SqlBoolSerializer.Deserialize(customSpan));
                                        break;

                                    case TypeMarker.Null:
                                        writer.WriteNullValue();
                                        break;

                                    case TypeMarker.Array:
                                        writer.WriteRawValue(customSpan, skipInputValidation: true);
                                        break;

                                    case TypeMarker.Object:
                                        writer.WriteRawValue(customSpan, skipInputValidation: true);
                                        break;
                                    default:
                                        using (diagnosticsContext?.CreateScope(
                                                   $"DecryptUnknownTypeMarker Path={decryptPropertyName} Marker={(byte)marker}"))
                                        {
                                            writer.WriteRawValue(customSpan, skipInputValidation: true);
                                        }

                                        break;
                                }
                            }
                            finally
                            {
                                // Return decompressed buffer if allocated (not owner.Array)
                                if (!ReferenceEquals(bytes, owner.Array))
                                {
                                    arrayPoolManager.Return(bytes);
                                }
                            }

                            return;
                        }

                        // Direct decrypt path continues here (owner not used)
                        if (encryptor.GetType() == typeof(MdeEncryptor))
                        {
                            if (decompressor != null
                                && decryptPropertyName != null
                                && properties.CompressedEncryptedPaths != null
                                && properties.CompressedEncryptedPaths.TryGetValue(decryptPropertyName, out int decompressedSize))
                            {
                                const int maxDecompressedSizeBytes = MaxBufferSizeBytes;
                                if (decompressedSize <= 0 || decompressedSize > maxDecompressedSizeBytes)
                                {
                                    throw new InvalidOperationException(
                                        $"Invalid decompressed size {decompressedSize} for path {PathLabel(decryptPropertyName, currentPropertyPath)}. Max allowed is {maxDecompressedSizeBytes}.");
                                }

                                byte[] decompressed = arrayPoolManager.Rent(decompressedSize);
                                processedBytes = decompressor.Decompress(bytes, processedBytes, decompressed);
                                bytes = decompressed;
                                compressedPathsDecompressed++;
                            }

                            ReadOnlySpan<byte> spanToWrite = bytes.AsSpan(0, processedBytes);
                            try
                            {
                                switch (marker)
                                {
                                    case TypeMarker.String:
                                        try
                                        {
                                            writer.WriteStringValue(spanToWrite);
                                        }
                                        catch (Exception ex)
                                        {
                                            throw new InvalidOperationException(
                                                $"Invalid UTF-8 while writing decrypted string at path {PathLabel(decryptPropertyName, currentPropertyPath)}",
                                                ex);
                                        }

                                        break;

                                    case TypeMarker.Long:
                                        try
                                        {
                                            writer.WriteNumberValue(SqlLongSerializer.Deserialize(spanToWrite));
                                        }
                                        catch (Exception ex)
                                        {
                                            throw new InvalidOperationException(
                                                $"Failed to deserialize decrypted payload at path {PathLabel(decryptPropertyName, currentPropertyPath)} as Long.",
                                                ex);
                                        }

                                        break;

                                    case TypeMarker.Double:
                                        try
                                        {
                                            writer.WriteNumberValue(SqlDoubleSerializer.Deserialize(spanToWrite));
                                        }
                                        catch (Exception ex)
                                        {
                                            throw new InvalidOperationException(
                                                $"Failed to deserialize decrypted payload at path {PathLabel(decryptPropertyName, currentPropertyPath)} as Double.",
                                                ex);
                                        }

                                        break;

                                    case TypeMarker.Boolean:
                                        writer.WriteBooleanValue(SqlBoolSerializer.Deserialize(spanToWrite));
                                        break;

                                    case TypeMarker.Null:
                                        writer.WriteNullValue();
                                        break;

                                    case TypeMarker.Array:
                                        writer.WriteRawValue(spanToWrite, skipInputValidation: true);
                                        break;

                                    case TypeMarker.Object:
                                        writer.WriteRawValue(spanToWrite, skipInputValidation: true);
                                        break;
                                    default:
                                        using (diagnosticsContext?.CreateScope(
                                                   $"DecryptUnknownTypeMarker Path={decryptPropertyName} Marker={(byte)marker}"))
                                        {
                                            writer.WriteRawValue(spanToWrite, skipInputValidation: true);
                                        }

                                        break;
                                }
                            }
                            finally
                            {
                                if (!ReferenceEquals(bytes, plainScratch))
                                {
                                    arrayPoolManager.Return(bytes);
                                }
                            }
                        }
                    }
                    finally
                    {
                        // cipherScratch reused across properties
                    }
                }

                long TransformDecryptBuffer(
                    ReadOnlySpan<byte> bufferSpan,
                    ref JsonReaderState state,
                    bool isFinalBlock,
                    ArrayPoolManager arrayPoolManager,
                    Utf8JsonWriter writer,
                    BrotliCompressor decompressor,
                    EncryptionProperties properties,
                    ref long propertiesDecrypted,
                    List<string> pathsDecrypted,
                    ref long compressedPathsDecompressed,
                    DataEncryptionKey encryptionKey,
                    ref string decryptPropertyName,
                    ref string currentPropertyPath,
                    ref bool skippingEi,
                    ref bool skipEiFirstTokenPending,
                    ref int skipEiContainerDepth)
                {
                    Utf8JsonReader reader = new Utf8JsonReader(bufferSpan, isFinalBlock, state);

                    while (reader.Read())
                    {
                        // If we're currently skipping the value of the EncryptedInfo property, consume tokens until complete
                        if (skippingEi)
                        {
                            if (skipEiFirstTokenPending)
                            {
                                skipEiFirstTokenPending = false;
                                if (reader.TokenType == JsonTokenType.StartObject ||
                                    reader.TokenType == JsonTokenType.StartArray)
                                {
                                    // Start of a container value; track nested depth
                                    skipEiContainerDepth = 1;
                                }
                                else
                                {
                                    // Scalar value skipped in one token
                                    skippingEi = false;
                                }

                                continue;
                            }

                            if (reader.TokenType == JsonTokenType.StartObject ||
                                reader.TokenType == JsonTokenType.StartArray)
                            {
                                skipEiContainerDepth++;
                                continue;
                            }

                            if (reader.TokenType == JsonTokenType.EndObject ||
                                reader.TokenType == JsonTokenType.EndArray)
                            {
                                skipEiContainerDepth--;
                                if (skipEiContainerDepth == 0)
                                {
                                    // Finished skipping the container
                                    skippingEi = false;
                                }

                                continue;
                            }

                            // Intermediate token inside the container being skipped
                            continue;
                        }

                        JsonTokenType tokenType = reader.TokenType;

                        switch (tokenType)
                        {
                            case JsonTokenType.String:
                                if (decryptPropertyName == null)
                                {
                                    try
                                    {
                                        // If the value is contiguous and unescaped, fast-path write
                                        if (!reader.HasValueSequence && !reader.ValueIsEscaped)
                                        {
                                            writer.WriteStringValue(reader.ValueSpan);
                                        }
                                        else
                                        {
                                            int estimatedLength = reader.HasValueSequence
                                                ? (int)reader.ValueSequence.Length
                                                : reader.ValueSpan.Length;
                                            const int stackScratch = 256;
                                            if (estimatedLength <= stackScratch)
                                            {
#pragma warning disable CA2014
                                                Span<byte> local = stackalloc byte[stackScratch];
#pragma warning restore CA2014
                                                int copied = reader.CopyString(local);
                                                writer.WriteStringValue(local[..copied]);
                                            }
                                            else
                                            {
                                                EnsureCapacity(ref tmpScratch, Math.Max(estimatedLength, 64), arrayPoolManager);
                                                int copied = reader.CopyString(tmpScratch);
                                                writer.WriteStringValue(tmpScratch.AsSpan(0, copied));
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new InvalidOperationException(
                                            $"Invalid UTF-8 while writing string at path {currentPropertyPath ?? "<unknown>"}",
                                            ex);
                                    }
                                }
                                else
                                {
                                    TransformDecryptProperty(
                                        ref reader,
                                        arrayPoolManager,
                                        this.Encryptor,
                                        encryptionKey,
                                        decompressor,
                                        properties,
                                        writer,
                                        diagnosticsContext,
                                        ref compressedPathsDecompressed,
                                        decryptPropertyName,
                                        currentPropertyPath);
                                    pathsDecrypted.Add(decryptPropertyName);
                                    propertiesDecrypted++;
                                }

                                decryptPropertyName = null;
                                break;
                            case JsonTokenType.Number:
                                decryptPropertyName = null;
                                if (!reader.HasValueSequence)
                                {
                                    writer.WriteRawValue(reader.ValueSpan, true);
                                }
                                else
                                {
                                    int len = (int)reader.ValueSequence.Length;
                                    const int stackScratch = 256;
                                    if (len <= stackScratch)
                                    {
#pragma warning disable CA2014
                                        Span<byte> local = stackalloc byte[stackScratch];
#pragma warning restore CA2014
                                        reader.ValueSequence.CopyTo(local);
                                        writer.WriteRawValue(local[..len], true);
                                    }
                                    else
                                    {
                                        EnsureCapacity(ref tmpScratch, Math.Max(len, 32), arrayPoolManager);
                                        reader.ValueSequence.CopyTo(tmpScratch);
                                        writer.WriteRawValue(tmpScratch.AsSpan(0, len), true);
                                    }
                                }

                                break;
                            case JsonTokenType.None: // Unreachable: pre-first-Read state
                                decryptPropertyName = null;
                                break;
                            case JsonTokenType.StartObject:
                                decryptPropertyName = null;
                                writer.WriteStartObject();
                                break;
                            case JsonTokenType.EndObject:
                                decryptPropertyName = null;
                                writer.WriteEndObject();
                                break;
                            case JsonTokenType.StartArray:
                                decryptPropertyName = null;
                                writer.WriteStartArray();
                                break;
                            case JsonTokenType.EndArray:
                                decryptPropertyName = null;
                                writer.WriteEndArray();
                                break;
                            case JsonTokenType.PropertyName:

                                // Fast-path skip for encrypted info property without allocating a string
                                if (reader.ValueTextEquals(Constants.EncryptedInfo))
                                {
                                    currentPropertyPath = EncryptionPropertiesPath;

                                    // Try to skip the value within the current buffer; if it doesn't fit, set state to continue skipping across buffers
                                    if (!reader.TrySkip())
                                    {
                                        skippingEi = true;

                                        // next token starts the value
                                        skipEiFirstTokenPending = true;
                                        skipEiContainerDepth = 0;
                                    }

                                    // Do not write the property name nor its value
                                    continue; // continue the outer read loop
                                }

                                // Check if current property is an encrypted top-level name without allocating a string
                                // PropertyName depth at root object is 1
                                if (reader.CurrentDepth == 1)
                                {
                                    string matchedFullPath = null;
                                    int propNameUtf8Len = reader.HasValueSequence
                                        ? (int)reader.ValueSequence.Length
                                        : reader.ValueSpan.Length;
                                    if (candidatePaths.TryMatch(ref reader, propNameUtf8Len, out string path))
                                    {
                                        matchedFullPath = path;
                                    }

                                    if (matchedFullPath != null)
                                    {
                                        decryptPropertyName = matchedFullPath;
                                        currentPropertyPath = matchedFullPath;
                                    }
                                    else
                                    {
                                        decryptPropertyName = null;
                                        currentPropertyPath = null;
                                    }
                                }
                                else
                                {
                                    // Non top-level properties are not encrypted by path in this implementation; avoid extra lookups and allocations
                                    decryptPropertyName = null;
                                    currentPropertyPath = null;
                                }

                                // Write the property name with zero allocation in the common case
                                if (!reader.HasValueSequence)
                                {
                                    writer.WritePropertyName(reader.ValueSpan);
                                }
                                else
                                {
                                    int estimatedLength = (int)reader.ValueSequence.Length;
                                    const int stackScratch = 256;
                                    if (estimatedLength <= stackScratch)
                                    {
#pragma warning disable CA2014
                                        Span<byte> local = stackalloc byte[stackScratch];
#pragma warning restore CA2014
                                        int copied = reader.CopyString(local);
                                        writer.WritePropertyName(local[..copied]);
                                    }
                                    else
                                    {
                                        EnsureCapacity(ref tmpScratch, Math.Max(estimatedLength, 64), arrayPoolManager);
                                        int copied = reader.CopyString(tmpScratch);
                                        writer.WritePropertyName(tmpScratch.AsSpan(0, copied));
                                    }
                                }

                                break;
                            case JsonTokenType.Comment: // Skipped via reader options
                                break;
                            case JsonTokenType.True:
                                decryptPropertyName = null;
                                writer.WriteBooleanValue(true);
                                break;
                            case JsonTokenType.False:
                                decryptPropertyName = null;
                                writer.WriteBooleanValue(false);
                                break;
                            case JsonTokenType.Null:
                                decryptPropertyName = null;
                                writer.WriteNullValue();
                                break;
                        }
                    }

                    state = reader.CurrentState;
                    return reader.BytesConsumed;
                }

                // Small-payload fast path: if stream is seekable and remaining length <= 2KB, read once and parse once.
                if (inputStream.CanSeek)
                {
                    long remaining = inputStream.Length - inputStream.Position;
                    if (remaining > 0 && remaining <= 2048)
                    {
                        int len = (int)remaining;
                        byte[] oneShot = arrayPoolManager.Rent(len);
                        try
                        {
                            int total = 0;
                            while (total < len)
                            {
                                int r = await inputStream.ReadAsync(oneShot.AsMemory(total, len - total), cancellationToken);
                                if (r == 0)
                                {
                                    break;
                                }

                                total += r;
                            }

                            int read = total;
                            bytesRead += read;

                            // Process the full payload in one pass and mark final block to skip the streaming loop.
                            _ = TransformDecryptBuffer(
                                oneShot.AsSpan(0, read),
                                ref state,
                                isFinalBlock: true,
                                arrayPoolManager,
                                writer,
                                decompressor,
                                properties,
                                ref propertiesDecrypted,
                                pathsDecrypted,
                                ref compressedPathsDecompressed,
                                encryptionKey,
                                ref decryptPropertyName,
                                ref currentPropertyPath,
                                ref skippingEi,
                                ref skipEiFirstTokenPending,
                                ref skipEiContainerDepth);

                            isFinalBlock = true;
                        }
                        finally
                        {
                            arrayPoolManager.Return(oneShot);
                        }
                    }
                }
                else
                {
                    // Non-seekable small payload probe: optimistically read up to 2KB once; if stream ends, we finish in a single pass.
                    const int ProbeSize = 2048;
                    buffer = arrayPoolManager.Rent(ProbeSize); // reuse as main buffer if more data follows
                    int read = await inputStream.ReadAsync(buffer.AsMemory(0, ProbeSize), cancellationToken);
                    bytesRead += read;
                    if (read > 0)
                    {
                        bool finalProbe = read < ProbeSize; // if fewer bytes than requested, very likely end-of-stream
                        long bytesConsumed = TransformDecryptBuffer(
                            buffer.AsSpan(0, read),
                            ref state,
                            finalProbe,
                            arrayPoolManager,
                            writer,
                            decompressor,
                            properties,
                            ref propertiesDecrypted,
                            pathsDecrypted,
                            ref compressedPathsDecompressed,
                            encryptionKey,
                            ref decryptPropertyName,
                            ref currentPropertyPath,
                            ref skippingEi,
                            ref skipEiFirstTokenPending,
                            ref skipEiContainerDepth);
                        leftOver = read - (int)bytesConsumed;
                        if (finalProbe && leftOver == 0)
                        {
                            isFinalBlock = true; // done; skip streaming loop
                        }
                    }
                    else
                    {
                        isFinalBlock = true; // empty stream
                    }
                }

                while (!isFinalBlock)
                {
                    buffer ??= arrayPoolManager.Rent(this.initialBufferSize);

                    int dataLength = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                    int dataSize = dataLength + leftOver;
                    bytesRead += dataLength;
                    isFinalBlock = dataSize == 0;
                    long bytesConsumed = 0;

                    // processing itself here
                    bytesConsumed = TransformDecryptBuffer(
                        buffer.AsSpan(0, dataSize),
                        ref state,
                        isFinalBlock,
                        arrayPoolManager,
                        writer,
                        decompressor,
                        properties,
                        ref propertiesDecrypted,
                        pathsDecrypted,
                        ref compressedPathsDecompressed,
                        encryptionKey,
                        ref decryptPropertyName,
                        ref currentPropertyPath,
                        ref skippingEi,
                        ref skipEiFirstTokenPending,
                        ref skipEiContainerDepth);

                    leftOver = dataSize - (int)bytesConsumed;

                    if (dataSize > 0 && leftOver == dataSize)
                    {
                        int target = Math.Max(buffer.Length * 2, leftOver + BufferGrowthMinIncrement);
                        int capped = Math.Min(MaxBufferSizeBytes, target);
                        if (buffer.Length >= capped)
                        {
                            throw new InvalidOperationException(
                                $"JSON token exceeds maximum supported size of {MaxBufferSizeBytes} bytes at path {currentPropertyPath ?? "<unknown>"}.");
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

                writer.Flush(); // pushes any Utf8JsonWriter buffered data into the IBufferWriter
                chunkedWriter.FinalFlush(); // write any remaining chunk bytes to the target stream
                long bytesWritten = writer.BytesCommitted; // committed bytes == total JSON length
                if (outputStream.CanSeek)
                {
                    outputStream.Position = 0;
                }

                // finalize diagnostics
                diagnosticsContext?.SetMetric("decrypt.bytesRead", bytesRead);
                diagnosticsContext?.SetMetric("decrypt.bytesWritten", bytesWritten);
                diagnosticsContext?.SetMetric("decrypt.propertiesDecrypted", propertiesDecrypted);
                diagnosticsContext?.SetMetric("decrypt.compressedPathsDecompressed", compressedPathsDecompressed);
                diagnosticsContext?.SetMetric("decrypt.writerFlushes", chunkedWriter.Flushes);
                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
                long elapsedMs = (long)(elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                diagnosticsContext?.SetMetric("decrypt.elapsedMs", elapsedMs);

                // Preserve legacy behavior: if nothing was decrypted AND no encrypted paths were requested, return null context.
                if (pathsDecrypted.Count == 0 && expectedDecrypted == 0)
                {
                    return null;
                }

                return EncryptionProcessor.CreateDecryptionContext(pathsDecrypted, properties.DataEncryptionKeyId);
            }
            finally
            {
                if (buffer != null)
                {
                    arrayPoolManager.Return(buffer);
                }

                if (tmpScratch != null)
                {
                    arrayPoolManager.Return(tmpScratch);
                }

                if (cipherScratch != null)
                {
                    arrayPoolManager.Return(cipherScratch);
                }

                if (plainScratch != null)
                {
                    arrayPoolManager.Return(plainScratch);
                }
            }
        }
    }
}

#endif