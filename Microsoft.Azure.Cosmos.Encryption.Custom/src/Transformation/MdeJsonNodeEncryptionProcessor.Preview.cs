// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    internal class MdeJsonNodeEncryptionProcessor
    {
        private readonly JsonWriterOptions jsonWriterOptions = new () { SkipValidation = true };

        internal JsonNodeSqlSerializer Serializer { get; set; } = new JsonNodeSqlSerializer();

        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

        public async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CancellationToken token)
        {
            JsonNode itemJObj = JsonNode.Parse(input);

            Stream result = await this.EncryptAsync(itemJObj, encryptor, encryptionOptions, token);

            await input.DisposeAsync();
            return result;
        }

        public async Task<Stream> EncryptAsync(
            JsonNode document,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CancellationToken token)
        {
            List<string> pathsEncrypted = new ();
            TypeMarker typeMarker;

            using ArrayPoolManager arrayPoolManager = new ();

            JsonObject itemObj = document.AsObject();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionOptions.DataEncryptionKeyId, encryptionOptions.EncryptionAlgorithm, token);

            bool compressionEnabled = encryptionOptions.CompressionOptions.Algorithm != CompressionOptions.CompressionAlgorithm.None;

#if NET8_0_OR_GREATER
            BrotliCompressor compressor = encryptionOptions.CompressionOptions.Algorithm == CompressionOptions.CompressionAlgorithm.Brotli
                ? new BrotliCompressor(encryptionOptions.CompressionOptions.CompressionLevel) : null;
#endif
            Dictionary<string, int> compressedPaths = new ();

            foreach (string pathToEncrypt in encryptionOptions.PathsToEncrypt)
            {
#if NET8_0_OR_GREATER
                string propertyName = pathToEncrypt[1..];
#else
                string propertyName = pathToEncrypt.Substring(1);
#endif
                if (!itemObj.TryGetPropertyValue(propertyName, out JsonNode propertyValue))
                {
                    continue;
                }

                if (propertyValue == null || propertyValue.GetValueKind() == JsonValueKind.Null)
                {
                    continue;
                }

                byte[] processedBytes = null;
                (typeMarker, processedBytes, int processedBytesLength) = this.Serializer.Serialize(propertyValue, arrayPoolManager);

                if (processedBytes == null)
                {
                    continue;
                }

#if NET8_0_OR_GREATER
                if (compressor != null && (processedBytesLength >= encryptionOptions.CompressionOptions.MinimalCompressedLength))
                {
                    byte[] compressedBytes = arrayPoolManager.Rent(BrotliCompressor.GetMaxCompressedSize(processedBytesLength));
                    processedBytesLength = compressor.Compress(compressedPaths, pathToEncrypt, processedBytes, processedBytesLength, compressedBytes);
                    processedBytes = compressedBytes;
                }
#endif
                (byte[] encryptedBytes, int encryptedBytesCount) = this.Encryptor.Encrypt(encryptionKey, typeMarker, processedBytes, processedBytesLength, arrayPoolManager);

                itemObj[propertyName] = JsonValue.Create(new Memory<byte>(encryptedBytes, 0, encryptedBytesCount));
                pathsEncrypted.Add(pathToEncrypt);
            }

#if NET8_0_OR_GREATER
            compressor?.Dispose();
#endif
            EncryptionProperties encryptionProperties = new (
                encryptionFormatVersion: compressionEnabled ? 4 : 3,
                encryptionOptions.EncryptionAlgorithm,
                encryptionOptions.DataEncryptionKeyId,
                encryptedData: null,
                pathsEncrypted,
                encryptionOptions.CompressionOptions.Algorithm,
                compressedPaths);

            JsonNode propertiesNode = JsonSerializer.SerializeToNode(encryptionProperties);

            itemObj.Add(Constants.EncryptedInfo, propertiesNode);

            MemoryStream ms = new ();
            Utf8JsonWriter writer = new (ms, this.jsonWriterOptions);

            JsonSerializer.Serialize(writer, document);

            ms.Position = 0;
            return ms;
        }

        internal async Task<DecryptionContext> DecryptObjectAsync(
            JsonNode document,
            Encryptor encryptor,
            EncryptionProperties encryptionProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            _ = diagnosticsContext;

            if (encryptionProperties.EncryptionFormatVersion != 3 && encryptionProperties.EncryptionFormatVersion != 4)
            {
                throw new NotSupportedException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            using ArrayPoolManager arrayPoolManager = new ();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionProperties.DataEncryptionKeyId, encryptionProperties.EncryptionAlgorithm, cancellationToken);

            List<string> pathsDecrypted = new (encryptionProperties.EncryptedPaths.Count());

            JsonObject itemObj = document.AsObject();

#if NET8_0_OR_GREATER
            BrotliCompressor decompressor = null;
            if (encryptionProperties.EncryptionFormatVersion == 4)
            {
                bool containsCompressed = encryptionProperties.CompressedEncryptedPaths?.Any() == true;
                if (encryptionProperties.CompressionAlgorithm != CompressionOptions.CompressionAlgorithm.Brotli && containsCompressed)
                {
                    throw new NotSupportedException($"Unknown compression algorithm {encryptionProperties.CompressionAlgorithm}");
                }

                if (containsCompressed)
                {
                    decompressor = new ();
                }
            }
#endif

            foreach (string path in encryptionProperties.EncryptedPaths)
            {
                string propertyName = path[1..];

                if (!itemObj.TryGetPropertyValue(propertyName, out JsonNode propertyValue))
                {
                    // malformed document, such record shouldn't be there at all
                    continue;
                }

                // can we get to internal JsonNode buffers to avoid string allocation here?
                string base64String = propertyValue.GetValue<string>();
                byte[] cipherTextWithTypeMarker = arrayPoolManager.Rent((base64String.Length * sizeof(char) * 3 / 4) + 4);
                if (!Convert.TryFromBase64Chars(base64String, cipherTextWithTypeMarker, out int cipherTextLength))
                {
                    continue;
                }

                (byte[] bytes, int processedBytes) = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextLength, arrayPoolManager);

#if NET8_0_OR_GREATER
                if (decompressor != null)
                {
                    if (encryptionProperties.CompressedEncryptedPaths?.TryGetValue(path, out int decompressedSize) == true)
                    {
                        byte[] buffer = arrayPoolManager.Rent(decompressedSize);
                        processedBytes = decompressor.Decompress(bytes, processedBytes, buffer);

                        bytes = buffer;
                    }
                }
#endif
                document[propertyName] = this.Serializer.Deserialize(
                    (TypeMarker)cipherTextWithTypeMarker[0],
                    bytes.AsSpan(0, processedBytes));

                pathsDecrypted.Add(path);
            }

            DecryptionContext decryptionContext = EncryptionProcessor.CreateDecryptionContext(
                pathsDecrypted,
                encryptionProperties.DataEncryptionKeyId);

            itemObj.Remove(Constants.EncryptedInfo);
            return decryptionContext;
        }
    }
}

#endif