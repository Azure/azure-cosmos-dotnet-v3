//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.Data.Encryption.Cryptography.Serializers;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class EncryptionProcessor
    {
        private static readonly CosmosJsonDotNetSerializer BaseSerializer = new CosmosJsonDotNetSerializer(
            new JsonSerializerSettings()
            {
                DateParseHandling = DateParseHandling.None,
            });

        private static readonly SqlSerializerFactory SqlSerializerFactory = SqlSerializerFactory.Default;

        // UTF-8 Encoding
        private static readonly SqlVarCharSerializer SqlVarcharSerializer = new SqlVarCharSerializer(size: -1, codePageCharacterEncoding: 65001);

        private enum TypeMarker : byte
        {
            Null = 1, // not used
            Boolean = 2,
            Double = 3,
            Long = 4,
            String = 5,
        }

        /// <remarks>
        /// If there isn't any PathsToEncrypt, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public static async Task<Stream> EncryptAsync(
            Stream input,
            EncryptionSettings encryptionSettings,
            EncryptionDiagnosticsContext operationDiagnostics,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            operationDiagnostics?.Begin(Constants.DiagnosticsEncryptOperation);
            int propertiesEncryptedCount = 0;

            MemoryStream encryptedOutputStream = new MemoryStream();
            using (Utf8JsonWriter encryptedDocumentWriter = new Utf8JsonWriter(encryptedOutputStream))
            using (JsonDocument document = JsonDocument.Parse(input))
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Invalid document to encrypt", nameof(input));
                }
                else
                {
                    encryptedDocumentWriter.WriteStartObject();
                }

                foreach (JsonProperty jsonProperty in root.EnumerateObject())
                {
                    if (encryptionSettings.PropertiesToEncrypt.Contains(jsonProperty.Name))
                    {
                        if (jsonProperty.Value.ValueKind != JsonValueKind.Null)
                        {
                            EncryptionSettingForProperty settingforProperty = encryptionSettings.GetEncryptionSettingForProperty(jsonProperty.Name);

                            if (settingforProperty == null)
                            {
                                throw new ArgumentException($"Invalid Encryption Setting for the Property:{jsonProperty.Name}. ");
                            }

                            await EncryptJsonPropertyAsync(
                                 jsonProperty,
                                 settingforProperty,
                                 cancellationToken,
                                 encryptedDocumentWriter);
                        }

                        // to make sure we processed this but did not encrypt it since its null amd updated the counter.
                        else
                        {
                            jsonProperty.WriteTo(encryptedDocumentWriter);
                        }

                        propertiesEncryptedCount++;
                    }
                    else
                    {
                        jsonProperty.WriteTo(encryptedDocumentWriter);
                    }
                }

                encryptedDocumentWriter.WriteEndObject();
                encryptedDocumentWriter.Flush();
            }

            input.Dispose();

            operationDiagnostics?.End(propertiesEncryptedCount);
            encryptedOutputStream.Position = 0;
            return encryptedOutputStream;
        }

        /// <remarks>
        /// If there isn't any data that needs to be decrypted, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public static async Task<Stream> DecryptAsync(
            Stream input,
            EncryptionSettings encryptionSettings,
            EncryptionDiagnosticsContext operationDiagnostics,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return input;
            }

            operationDiagnostics?.Begin(Constants.DiagnosticsDecryptOperation);
            int propertiesEncryptedCount = 0;

            MemoryStream encryptedOutputStream = new MemoryStream();
            using (Utf8JsonWriter decryptedDocumentWriter = new Utf8JsonWriter(encryptedOutputStream))
            using (JsonDocument document = JsonDocument.Parse(input))
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Invalid document to encrypt", nameof(input));
                }
                else
                {
                    decryptedDocumentWriter.WriteStartObject();
                }

                foreach (JsonProperty jsonProperty in root.EnumerateObject())
                {
                    if (encryptionSettings.PropertiesToEncrypt.Contains(jsonProperty.Name))
                    {
                        if (jsonProperty.Value.ValueKind != JsonValueKind.Null)
                        {
                            EncryptionSettingForProperty settingforProperty = encryptionSettings.GetEncryptionSettingForProperty(jsonProperty.Name);

                            if (settingforProperty == null)
                            {
                                throw new ArgumentException($"Invalid Encryption Setting for the Property:{jsonProperty.Name}. ");
                            }

                            await DecryptJsonPropertyAsync(
                                 jsonProperty,
                                 settingforProperty,
                                 cancellationToken,
                                 decryptedDocumentWriter);
                        }
                        else
                        {
                            jsonProperty.WriteTo(decryptedDocumentWriter);
                        }

                        propertiesEncryptedCount++;
                    }
                    else
                    {
                        jsonProperty.WriteTo(decryptedDocumentWriter);
                    }
                }

                decryptedDocumentWriter.WriteEndObject();
                decryptedDocumentWriter.Flush();
            }

            input.Dispose();

            operationDiagnostics?.End(propertiesEncryptedCount);
            encryptedOutputStream.Position = 0;
            return encryptedOutputStream;
        }

        public static async Task<(JObject, int)> DecryptAsync(
            JObject document,
            EncryptionSettings encryptionSettings,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        internal static async Task<Stream> DeserializeAndDecryptResponseAsync(
           Stream content,
           EncryptionSettings encryptionSettings,
           EncryptionDiagnosticsContext operationDiagnostics,
           CancellationToken cancellationToken)
        {
            if (!encryptionSettings.PropertiesToEncrypt.Any())
            {
                return content;
            }

            operationDiagnostics?.Begin(Constants.DiagnosticsDecryptOperation);
            int totalPropertiesDecryptedCount = 0;
            MemoryStream outputStream = new MemoryStream();
            using (JsonDocument response = JsonDocument.Parse(content))
            using (Utf8JsonWriter utf8JsonWriter = new Utf8JsonWriter(outputStream))
            {
                JsonElement root = response.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Invalid document to decrypt", nameof(content));
                }

                utf8JsonWriter.WriteStartObject();

                foreach (JsonProperty property in root.EnumerateObject())
                {
                    if (property.Name == Constants.DocumentsResourcePropertyName)
                    {
                        if (property.Value.ValueKind != JsonValueKind.Array)
                        {
                            throw new InvalidOperationException($"Unexpected type {property.Value.ValueKind} for {Constants.DocumentsResourcePropertyName}");
                        }
                        else
                        {
                            utf8JsonWriter.WritePropertyName(Constants.DocumentsResourcePropertyName);
                            utf8JsonWriter.WriteStartArray();

                            foreach (JsonElement doc in property.Value.EnumerateArray())
                            {
                                if (doc.ValueKind == JsonValueKind.Object)
                                {
                                    int propertiesDecrypted = await DecryptObjectAsync(doc, encryptionSettings, utf8JsonWriter, cancellationToken);
                                    totalPropertiesDecryptedCount += propertiesDecrypted;
                                }
                            }

                            utf8JsonWriter.WriteEndArray();
                        }
                    }
                    else
                    {
                        property.WriteTo(utf8JsonWriter);
                    }
                }

                utf8JsonWriter.WriteEndObject();
            }

            operationDiagnostics?.End(totalPropertiesDecryptedCount);
            return outputStream;
        }

        internal static async Task<Stream> EncryptValueStreamAsync(
            Stream valueStream,
            EncryptionSettingForProperty settingsForProperty,
            CancellationToken cancellationToken)
        {
            if (valueStream == null)
            {
                throw new ArgumentNullException(nameof(valueStream));
            }

            if (settingsForProperty == null)
            {
                throw new ArgumentNullException(nameof(settingsForProperty));
            }

            MemoryStream encryptedOutputStream = new MemoryStream();
            using (Utf8JsonWriter encryptedDocumentWriter = new Utf8JsonWriter(encryptedOutputStream))
            using (JsonDocument document = JsonDocument.Parse(valueStream))
            {
                JsonElement jsonElementToEncrypt = document.RootElement;

                if (jsonElementToEncrypt.ValueKind != JsonValueKind.Null && jsonElementToEncrypt.ValueKind != JsonValueKind.Undefined)
                {
                    if (jsonElementToEncrypt.ValueKind == JsonValueKind.Object || jsonElementToEncrypt.ValueKind == JsonValueKind.Array)
                    {
                        await EncryptJsonElementAsync(
                             jsonElementToEncrypt,
                             settingsForProperty,
                             cancellationToken,
                             encryptedDocumentWriter);
                    }
                    else
                    {
                        encryptedDocumentWriter.WriteBase64StringValue(await SerializeAndEncryptValueAsync(jsonElementToEncrypt, settingsForProperty, cancellationToken));
                    }
                }

                encryptedDocumentWriter.Flush();
            }

            valueStream.Dispose();
            encryptedOutputStream.Position = 0;
            return encryptedOutputStream;
        }

        private static (TypeMarker, byte[]) Serialize(JsonElement propertyValue)
        {
            switch(propertyValue.ValueKind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return (TypeMarker.Boolean, SqlSerializerFactory.GetDefaultSerializer<bool>().Serialize(propertyValue.GetBoolean()));
                case JsonValueKind.Number:
                    if (long.TryParse(propertyValue.ToString(), out _))
                    {
                        return (TypeMarker.Long, SqlSerializerFactory.GetDefaultSerializer<long>().Serialize(propertyValue.GetInt64()));
                    }
                    else
                    {
                        return (TypeMarker.Double, SqlSerializerFactory.GetDefaultSerializer<double>().Serialize(propertyValue.GetDouble()));
                    }

                case JsonValueKind.String:
                    return (TypeMarker.String, SqlVarcharSerializer.Serialize(propertyValue.GetString()));
                case JsonValueKind.Null:
                    Debug.Assert(false, "Null should have been handled");
                    return (TypeMarker.Null, null);
                case JsonValueKind.Undefined:
                    Debug.Assert(false, "Undefined");
                    return (default, null);
                default:
                    throw new InvalidOperationException($"Invalid or Unsupported Data Type Passed : {propertyValue.ValueKind}. ");
            }

        }

        private static void DeserializeAndWriteJson(
            byte[] serializedBytes,
            TypeMarker typeMarker,
            Utf8JsonWriter utf8JsonWriter = null)
        {
            switch(typeMarker)
            {
                case TypeMarker.Boolean:
                    utf8JsonWriter.WriteBooleanValue(SqlSerializerFactory.GetDefaultSerializer<bool>().Deserialize(serializedBytes));
                    break;

                case TypeMarker.Double:
                    utf8JsonWriter.WriteNumberValue(SqlSerializerFactory.GetDefaultSerializer<double>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.Long:
                    utf8JsonWriter.WriteNumberValue(SqlSerializerFactory.GetDefaultSerializer<long>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.String:
                    utf8JsonWriter.WriteStringValue(SqlVarcharSerializer.Deserialize(serializedBytes));
                    break;
                default:
                    throw new InvalidOperationException($"Invalid or Unsupported Data Type Passed : {typeMarker}. ");
            }
        }

        private static async Task EncryptJsonPropertyAsync(
           JsonProperty jsonPropertyToEncrypt,
           EncryptionSettingForProperty encryptionSettingForProperty,
           CancellationToken cancellationToken,
           Utf8JsonWriter utf8JsonWriter = null)
        {
            // Top Level can be an Object
            if (jsonPropertyToEncrypt.Value.ValueKind == JsonValueKind.Object)
            {
                utf8JsonWriter.WriteStartObject(jsonPropertyToEncrypt.Name);
                foreach (JsonProperty jsonElement in jsonPropertyToEncrypt.Value.EnumerateObject())
                {
                    await EncryptJsonPropertyAsync(
                        jsonElement,
                        encryptionSettingForProperty,
                        cancellationToken,
                        utf8JsonWriter);
                }

                utf8JsonWriter.WriteEndObject();
            }
            else if (jsonPropertyToEncrypt.Value.ValueKind == JsonValueKind.Array)
            {
                utf8JsonWriter.WriteStartArray(jsonPropertyToEncrypt.Name);
                foreach (JsonElement jsonElement in jsonPropertyToEncrypt.Value.EnumerateArray())
                {
                    await EncryptJsonElementAsync(
                        jsonElement,
                        encryptionSettingForProperty,
                        cancellationToken,
                        utf8JsonWriter);
                }

                utf8JsonWriter.WriteEndArray();
            }
            else
            {
                if (jsonPropertyToEncrypt.Value.ValueKind != JsonValueKind.Null)
                {
                    utf8JsonWriter.WriteBase64String(jsonPropertyToEncrypt.Name, await SerializeAndEncryptValueAsync(jsonPropertyToEncrypt.Value, encryptionSettingForProperty, cancellationToken));
                }
                else
                {
                    jsonPropertyToEncrypt.WriteTo(utf8JsonWriter);
                }
            }

            return;
        }

        private static async Task EncryptJsonElementAsync(
          JsonElement jsonElementToEncrypt,
          EncryptionSettingForProperty encryptionSettingForProperty,
          CancellationToken cancellationToken,
          Utf8JsonWriter utf8JsonWriter)
        {
            if (jsonElementToEncrypt.ValueKind == JsonValueKind.Object)
            {
                utf8JsonWriter.WriteStartObject();
                foreach (JsonProperty jsonElementObject in jsonElementToEncrypt.EnumerateObject())
                {
                    await EncryptJsonPropertyAsync(
                        jsonElementObject,
                        encryptionSettingForProperty,
                        cancellationToken,
                        utf8JsonWriter);
                }

                utf8JsonWriter.WriteEndObject();
            }
            else if (jsonElementToEncrypt.ValueKind == JsonValueKind.Array)
            {
                utf8JsonWriter.WriteStartArray();
                foreach (JsonElement jsonElementObject in jsonElementToEncrypt.EnumerateArray())
                {
                    await EncryptJsonElementAsync(
                        jsonElementObject,
                        encryptionSettingForProperty,
                        cancellationToken,
                        utf8JsonWriter);
                }

                utf8JsonWriter.WriteEndArray();
            }
            else
            {
                if (jsonElementToEncrypt.ValueKind != JsonValueKind.Null)
                {
                    utf8JsonWriter.WriteBase64StringValue(await SerializeAndEncryptValueAsync(jsonElementToEncrypt, encryptionSettingForProperty, cancellationToken));
                }
                else
                {
                    utf8JsonWriter.WriteNullValue();
                }
            }

            return;
        }

        private static async Task DecryptJsonPropertyAsync(
           JsonProperty jsonPropertyToDecrypt,
           EncryptionSettingForProperty encryptionSettingForProperty,
           CancellationToken cancellationToken,
           Utf8JsonWriter utf8JsonWriter = null)
        {
            // Top Level can be an Object
            if (jsonPropertyToDecrypt.Value.ValueKind == JsonValueKind.Object)
            {
                utf8JsonWriter.WriteStartObject(jsonPropertyToDecrypt.Name);
                foreach (JsonProperty jsonElement in jsonPropertyToDecrypt.Value.EnumerateObject())
                {
                    await DecryptJsonPropertyAsync(
                        jsonElement,
                        encryptionSettingForProperty,
                        cancellationToken,
                        utf8JsonWriter);
                }

                utf8JsonWriter.WriteEndObject();
            }
            else if (jsonPropertyToDecrypt.Value.ValueKind == JsonValueKind.Array)
            {
                utf8JsonWriter.WriteStartArray(jsonPropertyToDecrypt.Name);
                foreach (JsonElement jsonElement in jsonPropertyToDecrypt.Value.EnumerateArray())
                {
                    await DecryptJsonElementAsync(
                        jsonElement,
                        encryptionSettingForProperty,
                        cancellationToken,
                        utf8JsonWriter);
                }

                utf8JsonWriter.WriteEndArray();
            }
            else
            {
                // name value pair here:
                if (jsonPropertyToDecrypt.Value.ValueKind != JsonValueKind.Null)
                {
                    utf8JsonWriter.WritePropertyName(jsonPropertyToDecrypt.Name);
                    await DecryptAndDeserializeValueAsync(jsonPropertyToDecrypt.Value, encryptionSettingForProperty, utf8JsonWriter, cancellationToken);
                }
                else
                {
                    jsonPropertyToDecrypt.WriteTo(utf8JsonWriter);
                }
            }

            return;
        }

        private static async Task DecryptJsonElementAsync(
          JsonElement jsonElementToEncrypt,
          EncryptionSettingForProperty encryptionSettingForProperty,
          CancellationToken cancellationToken,
          Utf8JsonWriter utf8JsonWriter)
        {
            if (jsonElementToEncrypt.ValueKind == JsonValueKind.Object)
            {
                utf8JsonWriter.WriteStartObject();
                foreach (JsonProperty jsonElementObject in jsonElementToEncrypt.EnumerateObject())
                {
                    await DecryptJsonPropertyAsync(
                        jsonElementObject,
                        encryptionSettingForProperty,
                        cancellationToken,
                        utf8JsonWriter);
                }

                utf8JsonWriter.WriteEndObject();
            }
            else if (jsonElementToEncrypt.ValueKind == JsonValueKind.Array)
            {
                utf8JsonWriter.WriteStartArray();
                foreach (JsonElement jsonElementObject in jsonElementToEncrypt.EnumerateArray())
                {
                    await DecryptJsonElementAsync(
                        jsonElementObject,
                        encryptionSettingForProperty,
                        cancellationToken,
                        utf8JsonWriter);
                }

                utf8JsonWriter.WriteEndArray();
            }
            else
            {
                // writes just the value.Say in an array.
                if (jsonElementToEncrypt.ValueKind != JsonValueKind.Null)
                {
                    await DecryptAndDeserializeValueAsync(jsonElementToEncrypt, encryptionSettingForProperty,utf8JsonWriter, cancellationToken);
                }
                else
                {
                    utf8JsonWriter.WriteNullValue();
                }
            }

            return;
        }

        private static async Task<byte[]> SerializeAndEncryptValueAsync(
           JsonElement jsonElement,
           EncryptionSettingForProperty encryptionSettingForProperty,
           CancellationToken cancellationToken)
        {
            JsonElement jsonElementToEncrypt = jsonElement;

            (TypeMarker typeMarker, byte[] plainText) = Serialize(jsonElementToEncrypt);

            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = await encryptionSettingForProperty.BuildEncryptionAlgorithmForSettingAsync(cancellationToken: cancellationToken);
            byte[] cipherText = aeadAes256CbcHmac256EncryptionAlgorithm.Encrypt(plainText);

            if (cipherText == null)
            {
                throw new InvalidOperationException($"{nameof(SerializeAndEncryptValueAsync)} returned null cipherText from {nameof(aeadAes256CbcHmac256EncryptionAlgorithm.Encrypt)}. ");
            }

            byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
            cipherTextWithTypeMarker[0] = (byte)typeMarker;
            Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
            return cipherTextWithTypeMarker;
        }

        private static async Task DecryptAndDeserializeValueAsync(
           JsonElement jsonElement,
           EncryptionSettingForProperty encryptionSettingForProperty,
           Utf8JsonWriter utf8JsonWriter,
           CancellationToken cancellationToken)
        {
            byte[] cipherTextWithTypeMarker = jsonElement.GetBytesFromBase64();

            if (cipherTextWithTypeMarker == null)
            {
                return;
            }

            byte[] cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
            Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);

            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = await encryptionSettingForProperty.BuildEncryptionAlgorithmForSettingAsync(cancellationToken: cancellationToken);
            byte[] plainText = aeadAes256CbcHmac256EncryptionAlgorithm.Decrypt(cipherText);

            if (plainText == null)
            {
                throw new InvalidOperationException($"{nameof(DecryptAndDeserializeValueAsync)} returned null plainText from {nameof(aeadAes256CbcHmac256EncryptionAlgorithm.Decrypt)}. ");
            }

            DeserializeAndWriteJson(
                plainText,
                (TypeMarker)cipherTextWithTypeMarker[0],
                utf8JsonWriter);
        }

        private static async Task<int> DecryptObjectAsync(
            JsonElement document,
            EncryptionSettings encryptionSettings,
            Utf8JsonWriter utf8JsonWriter,
            CancellationToken cancellationToken)
        {
            int propertiesDecryptedCount = 0;

            if (document.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Invalid document to decrypt", nameof(document));
            }
            else
            {
                utf8JsonWriter.WriteStartObject();
            }

            foreach (JsonProperty jsonProperty in document.EnumerateObject())
            {
                if (encryptionSettings.PropertiesToEncrypt.Contains(jsonProperty.Name))
                {
                    if (jsonProperty.Value.ValueKind != JsonValueKind.Null)
                    {
                        EncryptionSettingForProperty settingforProperty = encryptionSettings.GetEncryptionSettingForProperty(jsonProperty.Name);

                        if (settingforProperty == null)
                        {
                            throw new ArgumentException($"Invalid Encryption Setting for the Property:{jsonProperty.Name}. ");
                        }

                        await DecryptJsonPropertyAsync(
                             jsonProperty,
                             settingforProperty,
                             cancellationToken,
                             utf8JsonWriter);
                    }
                    else
                    {
                        jsonProperty.WriteTo(utf8JsonWriter);
                    }

                    propertiesDecryptedCount++;
                }
                else
                {
                    jsonProperty.WriteTo(utf8JsonWriter);
                }
            }

            utf8JsonWriter.WriteEndObject();
            return propertiesDecryptedCount;
        }
    }
}