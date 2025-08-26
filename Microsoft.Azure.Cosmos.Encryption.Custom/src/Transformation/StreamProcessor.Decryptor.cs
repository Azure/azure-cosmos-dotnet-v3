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
    private readonly int initialBufferSize;
        private const string EncryptionPropertiesPath = "/" + Constants.EncryptedInfo;
        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();

        private static readonly JsonReaderOptions JsonReaderOptions = new () { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        internal static int InitialBufferSize { get; set; } = 16384;

        internal StreamProcessor()
            : this(InitialBufferSize)
        {
        }

        internal StreamProcessor(int initialBufferSize)
        {
            // Ensure a positive buffer size; fallback to legacy default if invalid
            this.initialBufferSize = initialBufferSize > 0 ? initialBufferSize : InitialBufferSize;
        }

    internal MdeEncryptor MdeEngine { get; set; } = new MdeEncryptor();

        internal async Task<DecryptionContext> DecryptStreamAsync(
            Stream inputStream,
            Stream outputStream,
            Encryptor encryptor,
            EncryptionProperties properties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            _ = diagnosticsContext;

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

            using Utf8JsonWriter writer = new (outputStream);

            byte[] buffer = arrayPoolManager.Rent(this.initialBufferSize);

            JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

            // Build a top-level property map: property name (without leading slash) -> is encrypted
            Dictionary<string, bool> encryptedTopLevel = new(StringComparer.Ordinal);
            foreach (string p in properties.EncryptedPaths)
            {
                if (!string.IsNullOrEmpty(p) && p[0] == '/')
                {
                    string name = p.Substring(1);
                    // Only consider top-level, ignore nested paths (keeps behavior consistent for current design)
                    if (!name.Contains('/'))
                    {
                        encryptedTopLevel[name] = true;
                    }
                }
            }

            int leftOver = 0;

            bool isFinalBlock = false;
            bool isIgnoredBlock = false;

            string decryptPropertyName = null;
            string currentPropertyPath = null;

            while (!isFinalBlock)
            {
                int dataLength = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                int dataSize = dataLength + leftOver;
                isFinalBlock = dataSize == 0;
                long bytesConsumed = 0;

                // processing itself here
                bytesConsumed = TransformDecryptBuffer(buffer.AsSpan(0, dataSize));

                leftOver = dataSize - (int)bytesConsumed;

                // we need to scale out buffer
                // Guard against end-of-stream: when dataSize == 0, don't resize unnecessarily
                if (dataSize > 0 && leftOver == dataSize)
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

            writer.Flush();
            outputStream.Position = 0;

            return EncryptionProcessor.CreateDecryptionContext(pathsDecrypted, properties.DataEncryptionKeyId);

            long TransformDecryptBuffer(ReadOnlySpan<byte> buffer)
            {
                Utf8JsonReader reader = new (buffer, isFinalBlock, state);

                while (reader.Read())
                {
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
                            ReadOnlySpan<byte> rawName = reader.HasValueSequence ? reader.ValueSequence.ToArray().AsSpan() : reader.ValueSpan;
                            // Fast-path skip for _ei without allocating a string
                            if (reader.ValueTextEquals(Constants.EncryptedInfo))
                            {
                                currentPropertyPath = EncryptionPropertiesPath;
                                if (!reader.TrySkip())
                                {
                                    isIgnoredBlock = true;
                                }
                                break;
                            }

                            string propertyName = null;
                            // Minimal allocation: only materialize string if we need to check encryption map
                            if (!rawName.IsEmpty)
                            {
                                propertyName = System.Text.Encoding.UTF8.GetString(rawName);
                            }
                            currentPropertyPath = "/" + propertyName;
                            if (propertyName != null && encryptedTopLevel.ContainsKey(propertyName))
                            {
                                decryptPropertyName = currentPropertyPath;
                            }
                            writer.WritePropertyName(reader.ValueSpan);
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

                (byte[] bytes, int processedBytes) = this.MdeEngine.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextLength, arrayPoolManager);
                // Early return the ciphertext buffer since it's no longer needed
                arrayPoolManager.Return(cipherTextWithTypeMarker);

                if (containsCompressed && properties.CompressedEncryptedPaths.TryGetValue(decryptPropertyName, out int decompressedSize))
                {
                    BrotliCompressor decompressor = new ();
                    byte[] decompressed = arrayPoolManager.Rent(decompressedSize);
                    processedBytes = decompressor.Decompress(bytes, processedBytes, decompressed);
                    // Early return the previous buffer if it was rented
                    arrayPoolManager.Return(bytes);
                    bytes = decompressed;
                }

                ReadOnlySpan<byte> bytesToWrite = bytes.AsSpan(0, processedBytes);
                TypeMarker marker = (TypeMarker)cipherTextWithTypeMarker[0];
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
                    default:
                        // Option A: emit a safe JSON string token instead of raw bytes to preserve valid JSON,
                        // and record a diagnostics scope for visibility.
                        using (diagnosticsContext?.CreateScope($"DecryptUnknownTypeMarker Path={decryptPropertyName} Marker={(byte)marker}"))
                        {
                            // Use a redaction token to avoid leaking decrypted plaintext.
                            writer.WriteStringValue("[[unsupported_encrypted_value]]");
                        }
                        break;
                }
            }
        }
    }
}
#endif