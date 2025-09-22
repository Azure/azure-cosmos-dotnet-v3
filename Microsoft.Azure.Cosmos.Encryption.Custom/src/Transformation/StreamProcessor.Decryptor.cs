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
    using Microsoft.Azure.Cosmos.Encryption.Custom; // For PooledBufferWriter
    using Microsoft.Data.Encryption.Cryptography.Serializers;

    internal partial class StreamProcessor
    {
        private const string EncryptionPropertiesPath = "/" + Constants.EncryptedInfo;
        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();

        private static readonly JsonReaderOptions JsonReaderOptions = new () { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        internal static int InitialBufferSize { get; set; } = 16384;

        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

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

            using PooledBufferWriter<byte> stagingBuffer = new (InitialBufferSize);

            JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

            HashSet<string> encryptedPaths = properties.EncryptedPaths as HashSet<string> ?? new (properties.EncryptedPaths, StringComparer.Ordinal);

            bool inputCompleted = false;    // Physical end-of-stream observed
            bool isFinalBlock = false;      // Logical final block (EOF and no buffered data)
            bool isIgnoredBlock = false;

            string decryptPropertyName = null;

            while (!isFinalBlock)
            {
                if (stagingBuffer.FreeCapacity == 0)
                {
                    stagingBuffer.EnsureCapacity(stagingBuffer.Count == 0 ? InitialBufferSize : stagingBuffer.Count * 2);
                }

                int read = await inputStream.ReadAsync(stagingBuffer.GetInternalArray().AsMemory(stagingBuffer.Count, stagingBuffer.FreeCapacity), cancellationToken);
                if (read > 0)
                {
                    stagingBuffer.Advance(read);
                }
                else
                {
                    inputCompleted = true; // EOF
                }

                int dataSize = stagingBuffer.Count;
                isFinalBlock = inputCompleted && dataSize == 0;

                long bytesConsumed = TransformDecryptBuffer(stagingBuffer.WrittenSpan.Slice(0, dataSize));
                int consumed = (int)bytesConsumed;
                int remaining = dataSize - consumed;

                if (consumed > 0)
                {
                    stagingBuffer.ConsumePrefix(consumed);
                }

                // Truncated / incomplete JSON detection: EOF reached, buffer has data, but no forward progress.
                if (remaining == dataSize && inputCompleted && dataSize > 0)
                {
                    throw new InvalidOperationException("Incomplete or truncated JSON input.");
                }

                if ((remaining == dataSize) && !inputCompleted)
                {
                    stagingBuffer.EnsureCapacity((stagingBuffer.Count * 2) + 1);
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
                                writer.WriteStringValue(reader.ValueSpan);
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
                            string propertyName = "/" + reader.GetString();
                            if (encryptedPaths.Contains(propertyName))
                            {
                                decryptPropertyName = propertyName;
                            }
                            else if (propertyName == StreamProcessor.EncryptionPropertiesPath)
                            {
                                if (!reader.TrySkip())
                                {
                                    isIgnoredBlock = true;
                                }

                                break;
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
                    throw new InvalidOperationException($"Base64 decoding failed: {status}");
                }

                (byte[] bytes, int processedBytes) = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextLength, arrayPoolManager);

                if (containsCompressed && properties.CompressedEncryptedPaths.TryGetValue(decryptPropertyName, out int decompressedSize))
                {
                    BrotliCompressor decompressor = new ();
                    byte[] buffer = arrayPoolManager.Rent(decompressedSize);
                    processedBytes = decompressor.Decompress(bytes, processedBytes, buffer);

                    bytes = buffer;
                }

                ReadOnlySpan<byte> bytesToWrite = bytes.AsSpan(0, processedBytes);
                switch ((TypeMarker)cipherTextWithTypeMarker[0])
                {
                    case TypeMarker.String:
                        writer.WriteStringValue(bytesToWrite);
                        break;
                    case TypeMarker.Long:
                        writer.WriteNumberValue(SqlLongSerializer.Deserialize(bytesToWrite));
                        break;
                    case TypeMarker.Double:
                        writer.WriteNumberValue(SqlDoubleSerializer.Deserialize(bytesToWrite));
                        break;
                    case TypeMarker.Boolean:
                        writer.WriteBooleanValue(SqlBoolSerializer.Deserialize(bytesToWrite));
                        break;
                    case TypeMarker.Null: // Produced only if ciphertext was forged or future versions choose to encrypt nulls; current encryptor skips nulls.
                        writer.WriteNullValue();
                        break;
                    default:
                        writer.WriteRawValue(bytesToWrite, true);
                        break;
                }
            }
        }
    }
}
#endif