//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
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

            JObject itemJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(input);

            foreach (string propertyName in encryptionSettings.PropertiesToEncrypt)
            {
                // possibly a wrong path configured in the Client Encryption Policy, ignore.
                JProperty propertyToEncrypt = itemJObj.Property(propertyName);
                if (propertyToEncrypt == null)
                {
                    continue;
                }

                EncryptionSettingForProperty settingforProperty = encryptionSettings.GetEncryptionSettingForProperty(propertyName);

                if (settingforProperty == null)
                {
                    throw new ArgumentException($"Invalid Encryption Setting for the Property:{propertyName}. ");
                }

                await EncryptJTokenAsync(
                    propertyToEncrypt.Value,
                    settingforProperty,
                    cancellationToken);

                propertiesEncryptedCount++;
            }

            Stream result = EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
            input.Dispose();

            operationDiagnostics?.End(propertiesEncryptedCount);
            return result;
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

            Debug.Assert(input.CanSeek);

            operationDiagnostics?.Begin(Constants.DiagnosticsDecryptOperation);
            JObject itemJObj = RetrieveItem(input);

            int propertiesDecryptedCount = await DecryptObjectAsync(
                itemJObj,
                encryptionSettings,
                cancellationToken);

            Stream result = EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
            input.Dispose();

            operationDiagnostics?.End(propertiesDecryptedCount);

            return result;
        }

        public static async Task<(JObject, int)> DecryptAsync(
            JObject document,
            EncryptionSettings encryptionSettings,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);

            int propertiesDecryptedCount = await DecryptObjectAsync(
                document,
                encryptionSettings,
                cancellationToken);

            return (document, propertiesDecryptedCount);
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
            JObject contentJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(content);

            if (!(contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is JArray documents))
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed response did not have an array of Documents. ");
            }

            int totalPropertiesDecryptedCount = 0;
            foreach (JToken value in documents)
            {
                if (value is not JObject document)
                {
                    continue;
                }

                (_, int propertiesDecrypted) = await EncryptionProcessor.DecryptAsync(
                    document,
                    encryptionSettings,
                    cancellationToken);

                totalPropertiesDecryptedCount += propertiesDecrypted;
            }

            operationDiagnostics?.End(totalPropertiesDecryptedCount);

            // the contents get decrypted in place by DecryptAsync.
            return EncryptionProcessor.BaseSerializer.ToStream(contentJObj);
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

            JToken propertyValueToEncrypt = EncryptionProcessor.BaseSerializer.FromStream<JToken>(valueStream);

            JToken encryptedPropertyValue = propertyValueToEncrypt;
            if (propertyValueToEncrypt.Type == JTokenType.Object || propertyValueToEncrypt.Type == JTokenType.Array)
            {
                await EncryptJTokenAsync(encryptedPropertyValue, settingsForProperty, cancellationToken);
            }
            else
            {
                encryptedPropertyValue = await SerializeAndEncryptValueAsync(propertyValueToEncrypt, settingsForProperty, cancellationToken);
            }

            return EncryptionProcessor.BaseSerializer.ToStream(encryptedPropertyValue);
        }

        private static (TypeMarker, byte[]) Serialize(JToken propertyValue)
        {
            return propertyValue.Type switch
            {
                JTokenType.Boolean => (TypeMarker.Boolean, SqlSerializerFactory.GetDefaultSerializer<bool>().Serialize(propertyValue.ToObject<bool>())),
                JTokenType.Float => (TypeMarker.Double, SqlSerializerFactory.GetDefaultSerializer<double>().Serialize(propertyValue.ToObject<double>())),
                JTokenType.Integer => (TypeMarker.Long, SqlSerializerFactory.GetDefaultSerializer<long>().Serialize(propertyValue.ToObject<long>())),
                JTokenType.String => (TypeMarker.String, SqlVarcharSerializer.Serialize(propertyValue.ToObject<string>())),
                _ => throw new InvalidOperationException($"Invalid or Unsupported Data Type Passed : {propertyValue.Type}. "),
            };
        }

        private static JToken DeserializeAndAddProperty(
            byte[] serializedBytes,
            TypeMarker typeMarker)
        {
            return typeMarker switch
            {
                TypeMarker.Boolean => SqlSerializerFactory.GetDefaultSerializer<bool>().Deserialize(serializedBytes),
                TypeMarker.Double => SqlSerializerFactory.GetDefaultSerializer<double>().Deserialize(serializedBytes),
                TypeMarker.Long => SqlSerializerFactory.GetDefaultSerializer<long>().Deserialize(serializedBytes),
                TypeMarker.String => SqlVarcharSerializer.Deserialize(serializedBytes),
                _ => throw new InvalidOperationException($"Invalid or Unsupported Data Type Passed : {typeMarker}. "),
            };
        }

        private static async Task EncryptJTokenAsync(
           JToken jTokenToEncrypt,
           EncryptionSettingForProperty encryptionSettingForProperty,
           CancellationToken cancellationToken)
        {
            // Top Level can be an Object
            if (jTokenToEncrypt.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in jTokenToEncrypt.Children<JProperty>())
                {
                    await EncryptJTokenAsync(
                        jProperty.Value,
                        encryptionSettingForProperty,
                        cancellationToken);
                }
            }
            else if (jTokenToEncrypt.Type == JTokenType.Array)
            {
                if (jTokenToEncrypt.Children().Any())
                {
                    for (int i = 0; i < jTokenToEncrypt.Count(); i++)
                    {
                        await EncryptJTokenAsync(
                            jTokenToEncrypt[i],
                            encryptionSettingForProperty,
                            cancellationToken);
                    }
                }
            }
            else
            {
                jTokenToEncrypt.Replace(await SerializeAndEncryptValueAsync(jTokenToEncrypt, encryptionSettingForProperty, cancellationToken));
            }

            return;
        }

        private static async Task<JToken> SerializeAndEncryptValueAsync(
           JToken jToken,
           EncryptionSettingForProperty encryptionSettingForProperty,
           CancellationToken cancellationToken)
        {
            JToken propertyValueToEncrypt = jToken;

            if (propertyValueToEncrypt.Type == JTokenType.Null)
            {
                return propertyValueToEncrypt;
            }

            (TypeMarker typeMarker, byte[] plainText) = Serialize(propertyValueToEncrypt);

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

        private static async Task<JToken> DecryptAndDeserializeValueAsync(
           JToken jToken,
           EncryptionSettingForProperty encryptionSettingForProperty,
           CancellationToken cancellationToken)
        {
            byte[] cipherTextWithTypeMarker = jToken.ToObject<byte[]>();

            if (cipherTextWithTypeMarker == null)
            {
                return null;
            }

            byte[] cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
            Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);

            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = await encryptionSettingForProperty.BuildEncryptionAlgorithmForSettingAsync(cancellationToken: cancellationToken);
            byte[] plainText = aeadAes256CbcHmac256EncryptionAlgorithm.Decrypt(cipherText);

            if (plainText == null)
            {
                throw new InvalidOperationException($"{nameof(DecryptAndDeserializeValueAsync)} returned null plainText from {nameof(aeadAes256CbcHmac256EncryptionAlgorithm.Decrypt)}. ");
            }

            return DeserializeAndAddProperty(
                plainText,
                (TypeMarker)cipherTextWithTypeMarker[0]);
        }

        private static async Task DecryptJTokenAsync(
            JToken jTokenToDecrypt,
            EncryptionSettingForProperty encryptionSettingForProperty,
            CancellationToken cancellationToken)
        {
            if (jTokenToDecrypt.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in jTokenToDecrypt.Children<JProperty>())
                {
                    await DecryptJTokenAsync(
                        jProperty.Value,
                        encryptionSettingForProperty,
                        cancellationToken);
                }
            }
            else if (jTokenToDecrypt.Type == JTokenType.Array)
            {
                if (jTokenToDecrypt.Children().Any())
                {
                    for (int i = 0; i < jTokenToDecrypt.Count(); i++)
                    {
                        await DecryptJTokenAsync(
                            jTokenToDecrypt[i],
                            encryptionSettingForProperty,
                            cancellationToken);
                    }
                }
            }
            else
            {
                jTokenToDecrypt.Replace(await DecryptAndDeserializeValueAsync(
                    jTokenToDecrypt,
                    encryptionSettingForProperty,
                    cancellationToken));
            }
        }

        private static async Task<int> DecryptObjectAsync(
            JObject document,
            EncryptionSettings encryptionSettings,
            CancellationToken cancellationToken)
        {
            int propertiesDecryptedCount = 0;
            foreach (string propertyName in encryptionSettings.PropertiesToEncrypt)
            {
                JProperty propertyToDecrypt = document.Property(propertyName);
                if (propertyToDecrypt != null)
                {
                    EncryptionSettingForProperty settingsForProperty = encryptionSettings.GetEncryptionSettingForProperty(propertyName);

                    if (settingsForProperty == null)
                    {
                        throw new ArgumentException($"Invalid Encryption Setting for Property:{propertyName}. ");
                    }

                    await DecryptJTokenAsync(
                        propertyToDecrypt.Value,
                        settingsForProperty,
                        cancellationToken);

                    propertiesDecryptedCount++;
                }
            }

            return propertiesDecryptedCount;
        }

        private static JObject RetrieveItem(
            Stream input)
        {
            Debug.Assert(input != null);

            JObject itemJObj;
            using (StreamReader sr = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
            {
                JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
                {
                    DateParseHandling = DateParseHandling.None,
                };

                itemJObj = JsonSerializer.Create(jsonSerializerSettings).Deserialize<JObject>(jsonTextReader);
            }

            return itemJObj;
        }
    }
}