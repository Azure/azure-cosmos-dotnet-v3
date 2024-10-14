// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    internal class StreamProcessor
    {
        private readonly JsonReaderOptions jsonReaderOptions = new () { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

        internal async Task<(Stream, DecryptionContext)> DecryptStreamAsync(
            Stream inputStream,
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

            MemoryStream outputStream = new ();
            Utf8JsonWriter writer = new (outputStream);

            // we determine initial buffer size based on max uncompressed path length, it still might need scale out in case there is large non-encrypted object, but likehood is rather low
            int bufferSize = 16384; // Math.Max(16384, properties.CompressedEncryptedPaths.Values.Max());
            byte[] buffer = arrayPoolManager.Rent(bufferSize);

            JsonReaderState state = new (this.jsonReaderOptions);

            int leftOver = 0;

            bool isFinalBlock = false;

            while (!isFinalBlock)
            {
                int dataLength = await inputStream.ReadAsync(buffer.AsMemory(leftOver, buffer.Length - leftOver), cancellationToken);
                int dataSize = dataLength + leftOver;
                isFinalBlock = dataSize == 0;
                long bytesConsumed = 0;

                // processing itself here
                bytesConsumed = this.TransformReadBuffer(
                    buffer.AsSpan(0, dataSize),
                    isFinalBlock,
                    writer,
                    ref state,
                    pathsDecrypted,
                    properties,
                    arrayPoolManager,
                    encryptionKey);

                leftOver = dataSize - (int)bytesConsumed;

                // we need to scale out buffer
                if (leftOver == dataSize)
                {
                    bufferSize *= 2;
                    byte[] newBuffer = arrayPoolManager.Rent(bufferSize);
                    buffer.AsSpan(0, leftOver).CopyTo(newBuffer);
                    buffer = newBuffer;
                }
                else if (leftOver != 0)
                {
                    buffer.AsSpan(dataSize - leftOver, leftOver).CopyTo(buffer);
                }
            }

            writer.Flush();
            inputStream.Position = 0;
            outputStream.Position = 0;

            return (
                outputStream,
                EncryptionProcessor.CreateDecryptionContext(pathsDecrypted, properties.DataEncryptionKeyId));
        }

        /*
        private static Dictionary<byte[], int> GetUtf8DecryptionList(EncryptionProperties properties)
        {
            Dictionary<byte[], int> output = new (properties.EncryptedPaths.Count());
            foreach (KeyValuePair<string, int> compressedPath in properties.CompressedEncryptedPaths)
            {
                byte[] utf8String = Encoding.UTF8.GetBytes(compressedPath.Key, 1, compressedPath.Key.Length - 1);
                output[utf8String] = compressedPath.Value;
            }

            foreach (string encryptedPath in properties.EncryptedPaths)
            {
                byte[] utf8String = Encoding.UTF8.GetBytes(encryptedPath, 1, encryptedPath.Length - 1);
                output.TryAdd(utf8String, -1);
            }

            return output;
        }*/

        private long TransformReadBuffer(Span<byte> buffer, bool isFinalBlock, Utf8JsonWriter writer, ref JsonReaderState state, List<string> pathsDecrypted, EncryptionProperties properties, ArrayPoolManager arrayPoolManager, DataEncryptionKey encryptionKey)
        {
            Utf8JsonReader json = new (buffer, isFinalBlock, state);

            string decryptPropertyName = null;

            while (json.Read())
            {
                JsonTokenType tokenType = json.TokenType;

                switch (tokenType)
                {
                    case JsonTokenType.String:
                        if (decryptPropertyName == null)
                        {
                            writer.WriteRawValue(json.ValueSpan);
                        }
                        else
                        {
                            this.TransformDecryptProperty(
                                json.GetBytesFromBase64(),
                                writer,
                                decryptPropertyName,
                                properties,
                                encryptionKey,
                                arrayPoolManager);

                            pathsDecrypted.Add("/" + decryptPropertyName);
                        }

                        decryptPropertyName = null;
                        break;
                    case JsonTokenType.Number:
                        decryptPropertyName = null;
                        writer.WriteRawValue(json.ValueSpan);
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
                        string propertyName = json.GetString();
                        if (properties.EncryptedPaths.Contains("/" + propertyName))
                        {
                            decryptPropertyName = propertyName;
                        }

                        writer.WritePropertyName(json.ValueSpan);
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

            state = json.CurrentState;
            return json.BytesConsumed;
        }

        private void TransformDecryptProperty(byte[] cipherTextWithTypeMarker, Utf8JsonWriter writer, string decryptPropertyName, EncryptionProperties properties, DataEncryptionKey encryptionKey, ArrayPoolManager arrayPoolManager)
        {
            BrotliCompressor decompressor = null;
            if (properties.EncryptionFormatVersion == 4)
            {
                bool containsCompressed = properties.CompressedEncryptedPaths?.Any() == true;
                if (properties.CompressionAlgorithm != CompressionOptions.CompressionAlgorithm.Brotli && containsCompressed)
                {
                    throw new NotSupportedException($"Unknown compression algorithm {properties.CompressionAlgorithm}");
                }

                if (containsCompressed)
                {
                    decompressor = new ();
                }
            }

            (byte[] bytes, int processedBytes) = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextWithTypeMarker.Length, arrayPoolManager);

            if (decompressor != null)
            {
                if (properties.CompressedEncryptedPaths?.TryGetValue(decryptPropertyName, out int decompressedSize) == true)
                {
                    byte[] buffer = arrayPoolManager.Rent(decompressedSize);
                    processedBytes = decompressor.Decompress(bytes, processedBytes, buffer);

                    bytes = buffer;
                }
            }

            writer.WriteRawValue(bytes.AsSpan(0, processedBytes));
        }
    }
}
#endif