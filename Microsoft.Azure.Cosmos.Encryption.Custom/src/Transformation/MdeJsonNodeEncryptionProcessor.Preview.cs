// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if IS_PREVIEW && NET8_0_OR_GREATER

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation.SystemTextJson;

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
    }
}

#endif