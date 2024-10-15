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

            if (properties.EncryptionFormatVersion != 3 && properties.EncryptionFormatVersion != 4)
            {
                throw new NotSupportedException($"Unknown encryption format version: {properties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            using ArrayPoolManager arrayPoolManager = new ();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(properties.DataEncryptionKeyId, properties.EncryptionAlgorithm, cancellationToken);

            List<string> pathsDecrypted = new (properties.EncryptedPaths.Count());

            using Utf8JsonWriter writer = new (outputStream);

            byte[] buffer = arrayPoolManager.Rent(InitialBufferSize);

            JsonReaderState state = new (StreamProcessor.JsonReaderOptions);

            int leftOver = 0;

            bool isFinalBlock = false;
            bool isIgnoredBlock = false;

            string decryptPropertyName = null;

            bool containsCompressed = properties.CompressedEncryptedPaths?.Count > 0;

            while (!isFinalBlock)
            {
                int dataLength = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                int dataSize = dataLength + leftOver;
                isFinalBlock = dataSize == 0;
                long bytesConsumed = 0;

                // processing itself here
                bytesConsumed = this.TransformDecryptBuffer(
                    buffer.AsSpan(0, dataSize),
                    isFinalBlock,
                    writer,
                    ref state,
                    ref isIgnoredBlock,
                    ref decryptPropertyName,
                    pathsDecrypted,
                    properties,
                    containsCompressed,
                    arrayPoolManager,
                    encryptionKey);

                leftOver = dataSize - (int)bytesConsumed;

                // we need to scale out buffer
                if (leftOver == dataSize)
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
        }

        private long TransformDecryptBuffer(Span<byte> buffer, bool isFinalBlock, Utf8JsonWriter writer, ref JsonReaderState state, ref bool isIgnoredBlock, ref string decryptPropertyName, List<string> pathsDecrypted, EncryptionProperties properties, bool containsCompressed, ArrayPoolManager arrayPoolManager, DataEncryptionKey encryptionKey)
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
                            this.TransformDecryptProperty(
                                ref reader,
                                writer,
                                decryptPropertyName,
                                properties,
                                encryptionKey,
                                containsCompressed,
                                arrayPoolManager);

                            pathsDecrypted.Add(decryptPropertyName);
                        }

                        decryptPropertyName = null;
                        break;
                    case JsonTokenType.Number:
                        decryptPropertyName = null;
                        writer.WriteRawValue(reader.ValueSpan);
                        break;
                    case JsonTokenType.None:
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
                        if (properties.EncryptedPaths.Contains(propertyName))
                        {
                            decryptPropertyName = propertyName;
                        }
                        else if (propertyName == StreamProcessor.EncryptionPropertiesPath)
                        {
                            isIgnoredBlock = true;
                            break;
                        }

                        writer.WritePropertyName(reader.ValueSpan);
                        break;
                    case JsonTokenType.Comment:
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

        private void TransformDecryptProperty(ref Utf8JsonReader reader, Utf8JsonWriter writer, string decryptPropertyName, EncryptionProperties properties, DataEncryptionKey encryptionKey, bool containsCompressed, ArrayPoolManager arrayPoolManager)
        {
            BrotliCompressor decompressor = null;
            if (properties.EncryptionFormatVersion == 4)
            {
                if (properties.CompressionAlgorithm != CompressionOptions.CompressionAlgorithm.Brotli && containsCompressed)
                {
                    throw new NotSupportedException($"Unknown compression algorithm {properties.CompressionAlgorithm}");
                }

                if (containsCompressed)
                {
                    decompressor = new ();
                }
            }

            byte[] cipherTextWithTypeMarker = arrayPoolManager.Rent(reader.ValueSpan.Length);

            // necessary for proper un-escaping
            int initialLength = reader.CopyString(cipherTextWithTypeMarker);

            OperationStatus status = Base64.DecodeFromUtf8InPlace(cipherTextWithTypeMarker.AsSpan(0, initialLength), out int cipherTextLength);
            if (status != OperationStatus.Done)
            {
                throw new InvalidOperationException($"Base64 decoding failed: {status}");
            }

            (byte[] bytes, int processedBytes) = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextLength, arrayPoolManager);

            if (containsCompressed)
            {
                if (properties.CompressedEncryptedPaths?.TryGetValue(decryptPropertyName, out int decompressedSize) == true)
                {
                    byte[] buffer = arrayPoolManager.Rent(decompressedSize);
                    processedBytes = decompressor.Decompress(bytes, processedBytes, buffer);

                    bytes = buffer;
                }
            }

            Span<byte> bytesToWrite = bytes.AsSpan(0, processedBytes);
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
                case TypeMarker.Null:
                    writer.WriteNullValue();
                    break;
                default:
                    writer.WriteRawValue(bytes.AsSpan(0, processedBytes), true);
                    break;
            }
        }
    }
}
#endif