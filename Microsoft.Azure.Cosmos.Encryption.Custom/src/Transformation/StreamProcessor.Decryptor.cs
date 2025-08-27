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
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
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
        private static readonly JsonWriterOptions JsonWriterOptions = new () { Indented = false };

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

            List<string> pathsDecrypted = new (properties.EncryptedPaths.Count());

            // Write directly to the provided output stream; we'll compute bytes written via Utf8JsonWriter.BytesCommitted
            using Utf8JsonWriter writer = new (outputStream, StreamProcessor.JsonWriterOptions);

            byte[] buffer = arrayPoolManager.Rent(this.initialBufferSize);
            try
            {
                JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

            // Reuse a single decompressor instance per call to avoid per-field allocations
            BrotliCompressor decompressor = containsCompressed ? new () : null;

            // Build a top-level property name set: property name (without leading slash) -> encrypted
            HashSet<string> encryptedTopLevel = new (StringComparer.Ordinal);

            // Also keep a full-path set for a conservative fallback check
            HashSet<string> encryptedFullPaths = new (properties.EncryptedPaths ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (string p in properties.EncryptedPaths)
            {
                if (!string.IsNullOrEmpty(p) && p[0] == '/')
                {
                    string name = p[1..];

                    // Only consider top-level, ignore nested paths (keeps behavior consistent for current design)
                    if (!name.Contains('/'))
                    {
                        encryptedTopLevel.Add(name);
                    }
                }
            }

            int leftOver = 0;

            bool isFinalBlock = false;
            bool isIgnoredBlock = false;

            string decryptPropertyName = null;
            string currentPropertyPath = null;

            // Robust cross-buffer skipping state for the special encrypted info property value
            bool skippingEi = false;
            bool skipEiFirstTokenPending = false;
            int skipEiContainerDepth = 0;

                while (!isFinalBlock)
                {
                    int dataLength = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                    int dataSize = dataLength + leftOver;
                    bytesRead += dataLength;
                    isFinalBlock = dataSize == 0;
                    long bytesConsumed = 0;

                    // processing itself here
                    bytesConsumed = TransformDecryptBuffer(buffer.AsSpan(0, dataSize));

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

                return EncryptionProcessor.CreateDecryptionContext(pathsDecrypted, properties.DataEncryptionKeyId);
            }
            finally
            {
                arrayPoolManager.Return(buffer);
            }

            long TransformDecryptBuffer(ReadOnlySpan<byte> buffer)
            {
                Utf8JsonReader reader = new (buffer, isFinalBlock, state);

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

                    if (isIgnoredBlock && reader.CurrentDepth == 1 && tokenType == JsonTokenType.EndObject)
                    {
                        isIgnoredBlock = false;
                        continue;
                    }
                    else if (isIgnoredBlock)
                    {
                        continue;
                    }

                    switch (tokenType)
                    {
                        case JsonTokenType.String:
                            if (decryptPropertyName == null)
                            {
                                try
                                {
                                    writer.WriteStringValue(reader.ValueSpan);
                                }
                                catch (Exception ex)
                                {
                                    throw new InvalidOperationException($"Invalid UTF-8 while writing string at path {currentPropertyPath ?? "<unknown>"}", ex);
                                }
                            }
                            else
                            {
                                TransformDecryptProperty(ref reader);
                                pathsDecrypted.Add(decryptPropertyName);
                                propertiesDecrypted++;
                            }

                            decryptPropertyName = null;
                            break;
                        case JsonTokenType.Number:
                            decryptPropertyName = null;
                            writer.WriteRawValue(reader.ValueSpan);
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
                            bool isEncryptedProp = false;
                            string matchedName = null;
                            foreach (string name in encryptedTopLevel)
                            {
                                if (reader.ValueTextEquals(name))
                                {
                                    isEncryptedProp = true;
                                    matchedName = name;
                                    break;
                                }
                            }

                            if (isEncryptedProp)
                            {
                                // Only build the path string when we actually need it
                                decryptPropertyName = "/" + matchedName;
                                currentPropertyPath = decryptPropertyName;
                            }
                            else
                            {
                                // Fallback: allocate a small string and check full-path set to avoid false negatives
                                string nameStr;
                                if (!reader.HasValueSequence)
                                {
                                    nameStr = System.Text.Encoding.UTF8.GetString(reader.ValueSpan);
                                }
                                else
                                {
                                    int estimatedLength = (int)reader.ValueSequence.Length;
                                    byte[] tmpName = arrayPoolManager.Rent(Math.Max(estimatedLength, 64));
                                    int copiedName = reader.CopyString(tmpName);
                                    nameStr = System.Text.Encoding.UTF8.GetString(tmpName, 0, copiedName);
                                    arrayPoolManager.Return(tmpName);
                                }

                                string fullPath = "/" + nameStr;
                                if (encryptedFullPaths.Contains(fullPath))
                                {
                                    decryptPropertyName = fullPath;
                                    currentPropertyPath = fullPath;
                                }
                                else
                                {
                                    // Avoid per-property path allocation for non-encrypted properties
                                    currentPropertyPath = null;
                                }
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
                                byte[] tmp = arrayPoolManager.Rent(Math.Max(estimatedLength, 64));
                                int copied = reader.CopyString(tmp);
                                writer.WritePropertyName(tmp.AsSpan(0, copied));
                                arrayPoolManager.Return(tmp);
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

            void TransformDecryptProperty(ref Utf8JsonReader reader)
            {
                byte[] cipherTextWithTypeMarker = arrayPoolManager.Rent(reader.ValueSpan.Length);

                // necessary for proper un-escaping
                int initialLength = reader.CopyString(cipherTextWithTypeMarker);

                OperationStatus status = Base64.DecodeFromUtf8InPlace(cipherTextWithTypeMarker.AsSpan(0, initialLength), out int cipherTextLength);
                if (status != OperationStatus.Done)
                {
                    string pathInfo = decryptPropertyName ?? currentPropertyPath ?? "<unknown>";
                    throw new InvalidOperationException($"Base64 decoding failed for encrypted field at path {pathInfo}: {status}. The field may be corrupted or not a valid base64 string.");
                }

                // Capture the type marker before the ciphertext buffer is returned
                TypeMarker marker = (TypeMarker)cipherTextWithTypeMarker[0];

                (byte[] bytes, int processedBytes) = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextLength, arrayPoolManager);

                // Early return the ciphertext buffer since it's no longer needed
                arrayPoolManager.Return(cipherTextWithTypeMarker);

                if (decompressor != null && properties.CompressedEncryptedPaths.TryGetValue(decryptPropertyName, out int decompressedSize))
                {
                    byte[] decompressed = arrayPoolManager.Rent(decompressedSize);
                    processedBytes = decompressor.Decompress(bytes, processedBytes, decompressed);

                    // Early return the previous buffer if it was rented
                    arrayPoolManager.Return(bytes);
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
                                throw new InvalidOperationException($"Invalid UTF-8 while writing decrypted string at path {decryptPropertyName ?? "<unknown>"}", ex);
                            }

                            break;
                        case TypeMarker.Long:
                            try
                            {
                                writer.WriteNumberValue(SqlLongSerializer.Deserialize(bytesToWrite));
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException($"Failed to deserialize decrypted payload at path {decryptPropertyName ?? "<unknown>"} as Long. The ciphertext may be corrupted or forged.", ex);
                            }

                            break;
                        case TypeMarker.Double:
                            try
                            {
                                writer.WriteNumberValue(SqlDoubleSerializer.Deserialize(bytesToWrite));
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException($"Failed to deserialize decrypted payload at path {decryptPropertyName ?? "<unknown>"} as Double. The ciphertext may be corrupted or forged.", ex);
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
                    arrayPoolManager.Return(bytes);
                }
            }
        }
    }
}

#endif