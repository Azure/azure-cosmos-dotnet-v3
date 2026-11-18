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
            if (encryptor is not IDataEncryptionKeyAccessor)
            {
                token.ThrowIfCancellationRequested();
            }

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

            bool useDataEncryptionKeyDirectly = encryptor is IDataEncryptionKeyAccessor;
            if (!useDataEncryptionKeyDirectly)
            {
                token.ThrowIfCancellationRequested();
            }

            DataEncryptionKey encryptionKey = useDataEncryptionKeyDirectly
                ? await ((IDataEncryptionKeyAccessor)encryptor).GetEncryptionKeyAsync(
                    encryptionOptions.DataEncryptionKeyId,
                    encryptionOptions.EncryptionAlgorithm,
                    token) ?? throw new InvalidOperationException($"{nameof(IDataEncryptionKeyAccessor)} returned null {nameof(DataEncryptionKey)}.")
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
                    : await MdeCryptoOperations.EncryptAsync(
                        encryptor,
                        encryptionOptions.DataEncryptionKeyId,
                        encryptionOptions.EncryptionAlgorithm,
                        typeMarker,
                        processedBytes,
                        processedBytesLength,
                        token).ConfigureAwait(false);

                input[propertyName] = encryptedBytes;

                pathsEncrypted.Add(pathToEncrypt);
            }

            if (!useDataEncryptionKeyDirectly)
            {
                token.ThrowIfCancellationRequested();
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

            bool useDataEncryptionKeyDirectly = encryptor is IDataEncryptionKeyAccessor;
            if (!useDataEncryptionKeyDirectly)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            DataEncryptionKey encryptionKey = useDataEncryptionKeyDirectly
                ? await ((IDataEncryptionKeyAccessor)encryptor).GetEncryptionKeyAsync(
                    encryptionProperties.DataEncryptionKeyId,
                    encryptionProperties.EncryptionAlgorithm,
                    cancellationToken) ?? throw new InvalidOperationException($"{nameof(IDataEncryptionKeyAccessor)} returned null {nameof(DataEncryptionKey)}.")
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
                    bytes = await MdeCryptoOperations.DecryptAsync(
                        encryptor,
                        encryptionProperties.DataEncryptionKeyId,
                        encryptionProperties.EncryptionAlgorithm,
                        cipherTextWithTypeMarker,
                        cipherTextWithTypeMarker.Length,
                        cancellationToken).ConfigureAwait(false);
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

            if (!useDataEncryptionKeyDirectly)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            DecryptionContext decryptionContext = EncryptionProcessor.CreateDecryptionContext(
                pathsDecrypted,
                encryptionProperties.DataEncryptionKeyId);

            document.Remove(Constants.EncryptedInfo);
            return decryptionContext;
        }
    }
}
