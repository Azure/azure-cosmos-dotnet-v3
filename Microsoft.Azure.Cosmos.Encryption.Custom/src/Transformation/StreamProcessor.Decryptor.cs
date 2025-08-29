// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// Temporarily disable strict StyleCop rules for this preview-only implementation to unblock iteration.
#pragma warning disable SA1512 // Single-line comments should not be followed by blank line

// Temporarily disable strict StyleCop rules for this preview-only implementation to unblock iteration.
#pragma warning disable SA1513 // Closing brace should be followed by blank line
#pragma warning disable SA1512 // Single-line comments should not be followed by blank line
#pragma warning disable SA1510 // 'else' statement should not be preceded by a blank line
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
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
    using System.Buffers.Text;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Data.Encryption.Cryptography.Serializers;

    internal partial class StreamProcessor
    {
        private const string EncryptionPropertiesPath = "/" + Constants.EncryptedInfo;

        // Cap buffer growth to prevent unbounded memory usage for extremely large tokens
        private const int MaxBufferSizeBytes = 8 * 1024 * 1024; // 8 MB
        private const int BufferGrowthMinIncrement = 4096; // 4 KB minimal additional headroom

        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();

        private static readonly JsonReaderOptions JsonReaderOptions = new () { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        private static readonly JsonWriterOptions JsonWriterOptions = new ()
        {
            Indented = false,
            SkipValidation = true,
        };

        internal static int InitialBufferSize { get; set; } = 16384;

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

            if (properties.EncryptionFormatVersion != EncryptionFormatVersion.Mde && properties.EncryptionFormatVersion != EncryptionFormatVersion.MdeWithCompression)
            {
                throw new NotSupportedException($"Unknown encryption format version: {properties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            bool containsCompressed = properties.CompressedEncryptedPaths?.Count > 0;

            if (properties.CompressionAlgorithm != CompressionOptions.CompressionAlgorithm.Brotli && containsCompressed)
            {
                throw new NotSupportedException($"Unknown compression algorithm {properties.CompressionAlgorithm}");
            }

            using ArrayPoolManager arrayPoolManager = new ();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(properties.DataEncryptionKeyId, properties.EncryptionAlgorithm, cancellationToken);

            // Track decrypted paths; pre-size conservatively and guard against null
            List<string> pathsDecrypted = new (properties.EncryptedPaths?.Count() ?? 0);

            // Write directly to the provided output stream; we'll compute bytes written via Utf8JsonWriter.BytesCommitted
            using Utf8JsonWriter writer = new (outputStream, StreamProcessor.JsonWriterOptions);

            byte[] buffer = arrayPoolManager.Rent(this.initialBufferSize);
            // Reusable pooled scratch buffers to reduce per-field rents; declared outside try so they can be returned in finally
            byte[] base64Scratch = null; // used for copying base64 text and in-place decoding
            byte[] tmpScratch = null;    // used for multi-segment strings, property names, and numbers
            try
            {
                JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

                // Reuse a single decompressor instance per call to avoid per-field allocations
                BrotliCompressor decompressor = containsCompressed ? new () : null;

                // Keep a full-path set for diagnostics and final context
                HashSet<string> encryptedFullPaths = new (properties.EncryptedPaths ?? Array.Empty<string>(), StringComparer.Ordinal);
                // Back-compat variable removed; full paths set retained for diagnostics and context

                // Precompute top-level names as UTF8 bytes alongside their canonical full-path ("/name").
                // This avoids per-hit string concatenation and allocations when matching/writing.
                List<byte[]> topLevelNameUtf8 = new (encryptedFullPaths.Count);
                List<string> topLevelFullPaths = new (encryptedFullPaths.Count);
                foreach (string p in encryptedFullPaths)
                {
                    if (string.IsNullOrEmpty(p) || p[0] != '/')
                    {
                        continue;
                    }

                    string name = p.Length > 1 ? p[1..] : string.Empty;
                    // Only consider top-level (no additional '/'); nested encryption paths are not supported here.
                    if (name.IndexOf('/') >= 0)
                    {
                        continue;
                    }

                    byte[] nameUtf8 = System.Text.Encoding.UTF8.GetBytes(name);
                    topLevelNameUtf8.Add(nameUtf8);
                    topLevelFullPaths.Add(p); // reuse provided canonical full path string, avoids "/" + name per hit
                }

                int leftOver = 0;

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
                    static string PathLabel(string a, string b) => a ?? b ?? "<unknown>";
                    int base64Len = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                    // For in-place decode, destination length <= source length; ensure we have enough capacity for the source.
                    // If switching to non in-place in the future, consider Base64.GetMaxDecodedFromUtf8Length(base64Len) for destination sizing.
                    EnsureCapacity(ref base64Scratch, Math.Max(base64Len, 64), arrayPoolManager);

                    int initialLength = reader.CopyString(base64Scratch);
                    OperationStatus status = Base64.DecodeFromUtf8InPlace(base64Scratch.AsSpan(0, initialLength), out int cipherTextLength);
                    if (status != OperationStatus.Done)
                    {
                        throw new InvalidOperationException($"Base64 decoding failed for encrypted field at path {PathLabel(decryptPropertyName, currentPropertyPath)}: {status}. The field may be corrupted or not a valid base64 string.");
                    }
                    TypeMarker marker = (TypeMarker)base64Scratch[0];

                    // Note: do not short-circuit on marker (including Null) before decrypting; we must verify/authenticate ciphertext.

                    using PooledByteOwner owner = encryptor.DecryptOwned(encryptionKey, base64Scratch, cipherTextLength, arrayPoolManager);
                    byte[] bytes = owner.Array;
                    int processedBytes = owner.Length;

                    // Decompression handling (optional)
                    if (decompressor != null
                        && decryptPropertyName != null
                        && properties.CompressedEncryptedPaths != null
                        && properties.CompressedEncryptedPaths.TryGetValue(decryptPropertyName, out int decompressedSize))
                    {
                        // Validate target size to avoid pathological allocations from corrupted metadata
                        const int MaxDecompressedSizeBytes = MaxBufferSizeBytes; // align cap with token growth cap; keep fast const in method
                        if (decompressedSize <= 0 || decompressedSize > MaxDecompressedSizeBytes)
                        {
                            throw new InvalidOperationException($"Invalid decompressed size {decompressedSize} for path {PathLabel(decryptPropertyName, currentPropertyPath)}. Max allowed is {MaxDecompressedSizeBytes}.");
                        }

                        byte[] decompressed = arrayPoolManager.Rent(decompressedSize);
                        processedBytes = decompressor.Decompress(bytes, processedBytes, decompressed);
                        // Do not return the original buffer here; owner.Dispose() will handle it. Swap to decompressed.
                        bytes = decompressed;
                        compressedPathsDecompressed++;
                    }

                    ReadOnlySpan<byte> bytesToWrite = bytes.AsSpan(0, processedBytes);

                    // Ensure the pooled buffer is returned even if any write throws
                    try
                    {
                        switch (marker)
                        {
                            case TypeMarker.String:
                                try
                                {
                                    writer.WriteStringValue(bytesToWrite);
                                }
                                catch (Exception ex)
                                {
                                    throw new InvalidOperationException($"Invalid UTF-8 while writing decrypted string at path {PathLabel(decryptPropertyName, currentPropertyPath)}", ex);
                                }

                                break;
                            case TypeMarker.Long:
                                try
                                {
                                    writer.WriteNumberValue(SqlLongSerializer.Deserialize(bytesToWrite));
                                }
                                catch (Exception ex)
                                {
                                    throw new InvalidOperationException($"Failed to deserialize decrypted payload at path {PathLabel(decryptPropertyName, currentPropertyPath)} as Long. The ciphertext may be corrupted or forged.", ex);
                                }

                                break;
                            case TypeMarker.Double:
                                try
                                {
                                    writer.WriteNumberValue(SqlDoubleSerializer.Deserialize(bytesToWrite));
                                }
                                catch (Exception ex)
                                {
                                    throw new InvalidOperationException($"Failed to deserialize decrypted payload at path {PathLabel(decryptPropertyName, currentPropertyPath)} as Double. The ciphertext may be corrupted or forged.", ex);
                                }

                                break;
                            case TypeMarker.Boolean:
                                writer.WriteBooleanValue(SqlBoolSerializer.Deserialize(bytesToWrite));
                                break;
                            case TypeMarker.Null: // Produced only if ciphertext was forged or future versions choose to encrypt nulls; current encryptor skips nulls.
                                writer.WriteNullValue();
                                break;
                            case TypeMarker.Array:
                                // The decrypted payload contains a JSON array in UTF-8 form
                                writer.WriteRawValue(bytesToWrite, skipInputValidation: true);
                                break;
                            case TypeMarker.Object:
                                // The decrypted payload contains a JSON object in UTF-8 form
                                writer.WriteRawValue(bytesToWrite, skipInputValidation: true);
                                break;
                            default:
                                // Unknown type marker: write raw bytes. Tests expect invalid JSON if the plaintext isn't a valid token.
                                using (diagnosticsContext?.CreateScope($"DecryptUnknownTypeMarker Path={decryptPropertyName} Marker={(byte)marker}"))
                                {
                                    writer.WriteRawValue(bytesToWrite, skipInputValidation: true);
                                }

                                break;
                        }
                    }
                    finally
                    {
                        // Return decompressed buffer if present; the original buffer (owner.Array) is returned by owner.Dispose().
                        if (!ReferenceEquals(bytes, owner.Array))
                        {
                            arrayPoolManager.Return(bytes);
                        }
                    }
                }

                long TransformDecryptBuffer(
                    ReadOnlySpan<byte> bufferSpan,
                    ref JsonReaderState state,
                    bool isFinalBlock,
                    HashSet<string> encryptedFullPaths,
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
                    Utf8JsonReader reader = new (bufferSpan, isFinalBlock, state);

                    while (reader.Read())
                    {
                        // If we're currently skipping the value of the EncryptedInfo property, consume tokens until complete
                    if (skippingEi)
                    {
                        if (skipEiFirstTokenPending)
                        {
                            skipEiFirstTokenPending = false;
                            if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
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

                        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)
                        {
                            skipEiContainerDepth++;
                            continue;
                        }

                        if (reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray)
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
                                        // Copy and unescape into a reusable pooled buffer when needed (multi-segment or escaped)
                                        int estimatedLength = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
                                        EnsureCapacity(ref tmpScratch, Math.Max(estimatedLength, 64), arrayPoolManager);
                                        int copied = reader.CopyString(tmpScratch);
                                        writer.WriteStringValue(tmpScratch.AsSpan(0, copied));
                                    }
                                }
                                catch (Exception ex)
                                {
                                    throw new InvalidOperationException($"Invalid UTF-8 while writing string at path {currentPropertyPath ?? "<unknown>"}", ex);
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
                                writer.WriteRawValue(reader.ValueSpan);
                            }
                            else
                            {
                                int len = (int)reader.ValueSequence.Length;
                                EnsureCapacity(ref tmpScratch, Math.Max(len, 32), arrayPoolManager);
                                reader.ValueSequence.CopyTo(tmpScratch);
                                writer.WriteRawValue(tmpScratch.AsSpan(0, len));
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
                                    skipEiFirstTokenPending = true; // next token starts the value
                                    skipEiContainerDepth = 0;
                                }
                                // Do not write the property name nor its value
                                continue; // continue the outer read loop
                            }

                            // Check if current property is an encrypted top-level name without allocating a string
                                    // Top-level fast-path: try zero-allocation match against precomputed UTF8 names
                                    // PropertyName depth at root object is 1
                                    if (reader.CurrentDepth == 1 && topLevelNameUtf8.Count != 0)
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

                                        if (matchedFullPath != null)
                                        {
                                            decryptPropertyName = matchedFullPath; // reuse canonical full-path string
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
                                // Handle rare multi-segment names without allocating a new array
                                int estimatedLength = (int)reader.ValueSequence.Length;
                                EnsureCapacity(ref tmpScratch, Math.Max(estimatedLength, 64), arrayPoolManager);
                                int copied = reader.CopyString(tmpScratch);
                                writer.WritePropertyName(tmpScratch.AsSpan(0, copied));
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

                while (!isFinalBlock)
                {
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
                        encryptedFullPaths,
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
                            throw new InvalidOperationException($"JSON token exceeds maximum supported size of {MaxBufferSizeBytes} bytes at path {currentPropertyPath ?? "<unknown>"}.");
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
                long bytesWritten = writer.BytesCommitted;
                if (outputStream.CanSeek)
                {
                    outputStream.Position = 0;
                }

                // finalize diagnostics
                diagnosticsContext?.SetMetric("decrypt.bytesRead", bytesRead);
                diagnosticsContext?.SetMetric("decrypt.bytesWritten", bytesWritten);
                diagnosticsContext?.SetMetric("decrypt.propertiesDecrypted", propertiesDecrypted);
                diagnosticsContext?.SetMetric("decrypt.compressedPathsDecompressed", compressedPathsDecompressed);
                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
                long elapsedMs = (long)(elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                diagnosticsContext?.SetMetric("decrypt.elapsedMs", elapsedMs);

                // Preserve legacy behavior: if nothing was decrypted AND no encrypted paths were requested, return null context.
                // Use the derived set to avoid relying on re-enumeration semantics of properties.EncryptedPaths.
                if (pathsDecrypted.Count == 0 && encryptedFullPaths.Count == 0)
                {
                    return null;
                }

                return EncryptionProcessor.CreateDecryptionContext(pathsDecrypted, properties.DataEncryptionKeyId);
            }
            finally
            {
                arrayPoolManager.Return(buffer);
                if (base64Scratch != null)
                {
                    arrayPoolManager.Return(base64Scratch);
                }

                if (tmpScratch != null)
                {
                    arrayPoolManager.Return(tmpScratch);
                }
            }
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
#pragma warning restore SA1512
#pragma warning restore SA1510
#pragma warning restore SA1513