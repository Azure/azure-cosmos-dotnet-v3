// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal class MdeJObjectEncryptionProcessor
    {
        internal JObjectSqlSerializer Serializer { get; set; } = new JObjectSqlSerializer();

        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

        public async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CancellationToken token)
        {
            JObject itemJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(input);

            Stream result = await this.EncryptAsync(itemJObj, encryptor, encryptionOptions, token);

#if NET8_0_OR_GREATER
            await input.DisposeAsync();
#else
            input.Dispose();
#endif

            return result;
        }

        public async Task<Stream> EncryptAsync(
            JObject input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CancellationToken token)
        {
            List<string> pathsEncrypted = new ();
            TypeMarker typeMarker;

            using ArrayPoolManager arrayPoolManager = new ();

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
                if (!input.TryGetValue(propertyName, out JToken propertyValue))
                {
                    continue;
                }

                if (propertyValue.Type == JTokenType.Null)
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

                byte[] encryptedBytes = this.Encryptor.Encrypt(encryptionKey, typeMarker, processedBytes, processedBytesLength);

                input[propertyName] = encryptedBytes;

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

            input.Add(Constants.EncryptedInfo, JObject.FromObject(encryptionProperties));

            return EncryptionProcessor.BaseSerializer.ToStream(input);
        }

        internal async Task<DecryptionContext> DecryptObjectAsync(
            JObject document,
            Encryptor encryptor,
            EncryptionProperties encryptionProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            _ = diagnosticsContext;

            if (encryptionProperties.EncryptionFormatVersion != EncryptionFormatVersion.Mde && encryptionProperties.EncryptionFormatVersion != EncryptionFormatVersion.MdeWithCompression)
            {
                throw new NotSupportedException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            using ArrayPoolManager arrayPoolManager = new ();
            using ArrayPoolManager<char> charPoolManager = new ();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionProperties.DataEncryptionKeyId, encryptionProperties.EncryptionAlgorithm, cancellationToken);

            List<string> pathsDecrypted = new (encryptionProperties.EncryptedPaths.Count());

#if NET8_0_OR_GREATER
            BrotliCompressor decompressor = null;
            if (encryptionProperties.EncryptionFormatVersion == EncryptionFormatVersion.MdeWithCompression)
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
#if NET8_0_OR_GREATER
                string propertyName = path[1..];
#else
                string propertyName = path.Substring(1);
#endif

                if (!document.TryGetValue(propertyName, out JToken propertyValue))
                {
                    // malformed document, such record shouldn't be there at all
                    continue;
                }

                byte[] cipherTextWithTypeMarker = propertyValue.ToObject<byte[]>();
                if (cipherTextWithTypeMarker == null)
                {
                    continue;
                }

                (byte[] bytes, int processedBytes) = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextWithTypeMarker.Length, arrayPoolManager);

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

                this.Serializer.DeserializeAndAddProperty(
                    (TypeMarker)cipherTextWithTypeMarker[0],
                    bytes.AsSpan(0, processedBytes),
                    document,
                    propertyName,
                    charPoolManager);

                pathsDecrypted.Add(path);
            }

            DecryptionContext decryptionContext = EncryptionProcessor.CreateDecryptionContext(
                pathsDecrypted,
                encryptionProperties.DataEncryptionKeyId);

            document.Remove(Constants.EncryptedInfo);
            return decryptionContext;
        }
    }
}

#endif