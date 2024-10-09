// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if IS_PREVIEW && NET8_0_OR_GREATER

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
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.SystemTextJson;
    using Newtonsoft.Json.Linq;

    internal class MdeJsonNodeEncryptionProcessor
    {
        internal JsonNodeSqlSerializer Serializer { get; set; } = new JsonNodeSqlSerializer();

        internal MdeEncryptor Encryptor { get; set; } = new MdeEncryptor();

        internal JsonSerializerOptions JsonSerializerOptions { get; set; }

        private JsonWriterOptions jsonWriterOptions = new () { SkipValidation = true };

        public MdeJsonNodeEncryptionProcessor()
        {
            this.JsonSerializerOptions = new JsonSerializerOptions();
            this.JsonSerializerOptions.Converters.Add(new JsonBytesConverter());
        }

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

                byte[] plainText = null;
                (typeMarker, plainText, int plainTextLength) = this.Serializer.Serialize(propertyValue, arrayPoolManager);

                if (plainText == null)
                {
                    continue;
                }

                (byte[] encryptedBytes, int encryptedBytesCount) = this.Encryptor.Encrypt(encryptionKey, typeMarker, plainText, plainTextLength, arrayPoolManager);

                itemObj[propertyName] = JsonValue.Create(new JsonBytes(encryptedBytes, 0, encryptedBytesCount));
                pathsEncrypted.Add(pathToEncrypt);
            }

            EncryptionProperties encryptionProperties = new (
                encryptionFormatVersion: 3,
                encryptionOptions.EncryptionAlgorithm,
                encryptionOptions.DataEncryptionKeyId,
                encryptedData: null,
                pathsEncrypted);

            JsonNode propertiesNode = JsonSerializer.SerializeToNode(encryptionProperties);

            itemObj.Add(Constants.EncryptedInfo, propertiesNode);

            MemoryStream ms = new ();
            Utf8JsonWriter writer = new (ms, this.jsonWriterOptions);

            JsonSerializer.Serialize(writer, document, this.JsonSerializerOptions);

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

            if (encryptionProperties.EncryptionFormatVersion != 3)
            {
                throw new NotSupportedException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            using ArrayPoolManager arrayPoolManager = new ();
            using ArrayPoolManager<char> charPoolManager = new ();

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionProperties.DataEncryptionKeyId, encryptionProperties.EncryptionAlgorithm, cancellationToken);

            List<string> pathsDecrypted = new (encryptionProperties.EncryptedPaths.Count());

            JsonObject itemObj = document.AsObject();

            foreach (string path in encryptionProperties.EncryptedPaths)
            {
#if NET8_0_OR_GREATER
                string propertyName = path[1..];
#else
                string propertyName = path.Substring(1);
#endif

                if (!itemObj.TryGetPropertyValue(propertyName, out JsonNode propertyValue))
                {
                    // malformed document, such record shouldn't be there at all
                    continue;
                }

                //TODO: figure out if we can get char span out from JsonNode
                byte[] cipherTextWithTypeMarker = arrayPoolManager.Rent(propertyValue.)

                byte[] cipherTextWithTypeMarker = Convert.FromBase64String.TryFromBase64StringBase64Encoder propertyValue.propertyValue.ToObject<byte[]>();
                if (cipherTextWithTypeMarker == null)
                {
                    continue;
                }

                (byte[] plainText, int decryptedCount) = this.Encryptor.Decrypt(encryptionKey, cipherTextWithTypeMarker, arrayPoolManager);

                this.Serializer.DeserializeAndAddProperty(
                    (TypeMarker)cipherTextWithTypeMarker[0],
                    plainText.AsSpan(0, decryptedCount),
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