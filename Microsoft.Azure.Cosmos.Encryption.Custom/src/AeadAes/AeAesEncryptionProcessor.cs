// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

#pragma warning disable IDE0057 // Use range operator
#pragma warning disable VSTHRD103 // Call async methods when in an async method
    internal static class AeAesEncryptionProcessor
    {
        public static async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CancellationToken cancellationToken)
        {
            JObject itemJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(input);
            List<string> pathsEncrypted = new ();
            EncryptionProperties encryptionProperties = null;
            byte[] plainText = null;
            byte[] cipherText = null;

            JObject toEncryptJObj = new ();

            foreach (string pathToEncrypt in encryptionOptions.PathsToEncrypt)
            {
                string propertyName = pathToEncrypt.Substring(1);
                if (!itemJObj.TryGetValue(propertyName, out JToken propertyValue))
                {
                    continue;
                }

                toEncryptJObj.Add(propertyName, propertyValue.Value<JToken>());
                itemJObj.Remove(propertyName);
            }

            MemoryStream memoryStream = EncryptionProcessor.BaseSerializer.ToStream<JObject>(toEncryptJObj);
            Debug.Assert(memoryStream != null);
            Debug.Assert(memoryStream.TryGetBuffer(out _));
            plainText = memoryStream.ToArray();

            cipherText = await encryptor.EncryptAsync(
                plainText,
                encryptionOptions.DataEncryptionKeyId,
                encryptionOptions.EncryptionAlgorithm,
                cancellationToken);

            if (cipherText == null)
            {
                throw new InvalidOperationException($"{nameof(Encryptor)} returned null cipherText from {nameof(EncryptAsync)}.");
            }

            encryptionProperties = new EncryptionProperties(
                    encryptionFormatVersion: 2,
                    encryptionOptions.EncryptionAlgorithm,
                    encryptionOptions.DataEncryptionKeyId,
                    encryptedData: cipherText,
                    encryptionOptions.PathsToEncrypt);

            itemJObj.Add(Constants.EncryptedInfo, JObject.FromObject(encryptionProperties));

            input.Dispose();
            return EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        internal static async Task<DecryptionContext> DecryptContentAsync(
            JObject document,
            EncryptionProperties encryptionProperties,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            _ = diagnosticsContext;

            if (encryptionProperties.EncryptionFormatVersion != EncryptionFormatVersion.AeAes)
            {
                throw new NotSupportedException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            byte[] plainText = await encryptor.DecryptAsync(
                encryptionProperties.EncryptedData,
                encryptionProperties.DataEncryptionKeyId,
                encryptionProperties.EncryptionAlgorithm,
                cancellationToken) ?? throw new InvalidOperationException($"{nameof(Encryptor)} returned null plainText from {nameof(EncryptionProcessor.DecryptAsync)}.");
            JObject plainTextJObj;
            using (MemoryStream memoryStream = new (plainText))
            using (StreamReader streamReader = new (memoryStream))
            using (JsonTextReader jsonTextReader = new (streamReader))
            {
                jsonTextReader.ArrayPool = JsonArrayPool.Instance;
                plainTextJObj = JObject.Load(jsonTextReader);
            }

            List<string> pathsDecrypted = new ();
            foreach (JProperty property in plainTextJObj.Properties())
            {
                document.Add(property.Name, property.Value);
                pathsDecrypted.Add("/" + property.Name);
            }

            DecryptionContext decryptionContext = EncryptionProcessor.CreateDecryptionContext(
                pathsDecrypted,
                encryptionProperties.DataEncryptionKeyId);

            document.Remove(Constants.EncryptedInfo);

            return decryptionContext;
        }
    }

#pragma warning restore IDE0057 // Use range operator
#pragma warning restore VSTHRD103 // Call async methods when in an async method
}
