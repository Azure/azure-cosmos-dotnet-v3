// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if !ENCRYPTION_CUSTOM_PREVIEW

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal class MdeEncryptionProcessor
    {
        internal JObjectSqlSerializer Serializer { get; set; } = new JObjectSqlSerializer();

        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

        public async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken token)
        {
            _ = diagnosticsContext;
            JObject itemJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(input);
            List<string> pathsEncrypted = new ();
            TypeMarker typeMarker;

            using ArrayPoolManager arrayPoolManager = new ();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionOptions.DataEncryptionKeyId, encryptionOptions.EncryptionAlgorithm, token);

            foreach (string pathToEncrypt in encryptionOptions.PathsToEncrypt)
            {
                string propertyName = pathToEncrypt.Substring(1);
                if (!itemJObj.TryGetValue(propertyName, out JToken propertyValue))
                {
                    continue;
                }

                if (propertyValue.Type == JTokenType.Null)
                {
                    continue;
                }

                byte[] plainText = null;
                (typeMarker, plainText) = this.Serializer.Serialize(propertyValue);

                if (plainText == null)
                {
                    continue;
                }

                byte[] encryptedBytes = this.Encryptor.Encrypt(encryptionKey, typeMarker, plainText, plainText.Length);

                itemJObj[propertyName] = encryptedBytes.ToArray();
                pathsEncrypted.Add(pathToEncrypt);
            }

            EncryptionProperties encryptionProperties = new (
                encryptionFormatVersion: 3,
                encryptionOptions.EncryptionAlgorithm,
                encryptionOptions.DataEncryptionKeyId,
                encryptedData: null,
                pathsEncrypted);

            itemJObj.Add(Constants.EncryptedInfo, JObject.FromObject(encryptionProperties));
            await input.DisposeCompatAsync();
            // Direct serialize to a new recyclable MemoryStream to avoid intermediate large object allocations.
            MemoryStream result = new (capacity: 1024);
            EncryptionProcessor.BaseSerializer.WriteToStream(itemJObj, result);
            return result;
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

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionProperties.DataEncryptionKeyId, encryptionProperties.EncryptionAlgorithm, cancellationToken);

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

                (byte[] plainText, int decryptedCount) = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, cipherTextWithTypeMarker.Length, arrayPoolManager);

                this.Serializer.DeserializeAndAddProperty(
                    (TypeMarker)cipherTextWithTypeMarker[0],
                    plainText.AsSpan(0, decryptedCount).ToArray(),
                    document,
                    propertyName);

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