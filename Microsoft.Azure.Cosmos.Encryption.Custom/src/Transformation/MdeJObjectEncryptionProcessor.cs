// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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

            await input.DisposeCompatAsync();

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

            // Custom Encryptor implementations that pre-date GetEncryptionKeyAsync are routed
            // through their EncryptAsync override (the original release behavior) so their
            // logic (auditing, key scoping, alternative crypto) is not silently bypassed.
            bool useDataEncryptionKeyDirectly = encryptor.ProvidesDataEncryptionKeyAccess();
            DataEncryptionKey encryptionKey = useDataEncryptionKeyDirectly
                ? await encryptor.GetEncryptionKeyAsync(encryptionOptions.DataEncryptionKeyId, encryptionOptions.EncryptionAlgorithm, token)
                : null;

            foreach (string pathToEncrypt in encryptionOptions.PathsToEncrypt)
            {
                string propertyName = pathToEncrypt.Substring(1);
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

                byte[] encryptedBytes = useDataEncryptionKeyDirectly
                    ? this.Encryptor.Encrypt(encryptionKey, typeMarker, processedBytes, processedBytesLength)
                    : await EncryptThroughEncryptorAsync(encryptor, encryptionOptions, typeMarker, processedBytes, processedBytesLength, token);

                input[propertyName] = encryptedBytes;

                pathsEncrypted.Add(pathToEncrypt);
            }

            EncryptionProperties encryptionProperties = new (
                encryptionFormatVersion: EncryptionFormatVersion.Mde,
                encryptionOptions.EncryptionAlgorithm,
                encryptionOptions.DataEncryptionKeyId,
                encryptedData: null,
                pathsEncrypted);

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

            if (encryptionProperties.EncryptionFormatVersion != EncryptionFormatVersion.Mde)
            {
                throw new NotSupportedException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            using ArrayPoolManager arrayPoolManager = new ();
            using ArrayPoolManager<char> charPoolManager = new ();

            // See EncryptAsync: legacy custom Encryptor implementations dispatch through
            // their DecryptAsync override instead of direct DataEncryptionKey access.
            bool useDataEncryptionKeyDirectly = encryptor.ProvidesDataEncryptionKeyAccess();
            DataEncryptionKey encryptionKey = useDataEncryptionKeyDirectly
                ? await encryptor.GetEncryptionKeyAsync(encryptionProperties.DataEncryptionKeyId, encryptionProperties.EncryptionAlgorithm, cancellationToken)
                : null;

            List<string> pathsDecrypted = new (encryptionProperties.EncryptedPaths.Count());

            foreach (string path in encryptionProperties.EncryptedPaths)
            {
                string propertyName = path.Substring(1);

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

                byte[] bytes;
                int processedBytes;
                if (useDataEncryptionKeyDirectly)
                {
                    (bytes, processedBytes) = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextWithTypeMarker.Length, arrayPoolManager);
                }
                else
                {
                    bytes = await DecryptThroughEncryptorAsync(encryptor, encryptionProperties, cipherTextWithTypeMarker, cancellationToken);
                    processedBytes = bytes.Length;
                }

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

        /// <summary>
        /// Per-property encryption through <see cref="Custom.Encryptor.EncryptAsync"/>, replicating the
        /// original release behavior for custom Encryptor implementations that do not expose a
        /// <see cref="DataEncryptionKey"/> via <see cref="Custom.Encryptor.GetEncryptionKeyAsync"/>.
        /// </summary>
        private static async Task<byte[]> EncryptThroughEncryptorAsync(
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            TypeMarker typeMarker,
            byte[] processedBytes,
            int processedBytesLength,
            CancellationToken token)
        {
            // The pooled buffer is oversized; the Encryptor contract takes an exact-size array.
            byte[] plainText = new byte[processedBytesLength];
            Buffer.BlockCopy(processedBytes, 0, plainText, 0, processedBytesLength);

            byte[] cipherText = await encryptor.EncryptAsync(
                plainText,
                encryptionOptions.DataEncryptionKeyId,
                encryptionOptions.EncryptionAlgorithm,
                token) ?? throw new InvalidOperationException($"{nameof(Encryptor)} returned null cipherText from {nameof(encryptor.EncryptAsync)}.");

            byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
            cipherTextWithTypeMarker[0] = (byte)typeMarker;
            Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
            return cipherTextWithTypeMarker;
        }

        /// <summary>
        /// Per-property decryption through <see cref="Custom.Encryptor.DecryptAsync"/>; counterpart of
        /// <see cref="EncryptThroughEncryptorAsync"/>.
        /// </summary>
        private static async Task<byte[]> DecryptThroughEncryptorAsync(
            Encryptor encryptor,
            EncryptionProperties encryptionProperties,
            byte[] cipherTextWithTypeMarker,
            CancellationToken cancellationToken)
        {
            byte[] cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
            Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);

            return await encryptor.DecryptAsync(
                cipherText,
                encryptionProperties.DataEncryptionKeyId,
                encryptionProperties.EncryptionAlgorithm,
                cancellationToken) ?? throw new InvalidOperationException($"{nameof(Encryptor)} returned null plainText from {nameof(encryptor.DecryptAsync)}.");
        }
    }
}
