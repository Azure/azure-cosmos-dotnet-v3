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
        internal static readonly CosmosJsonDotNetSerializer BaseSerializer = new CosmosJsonDotNetSerializer(
            new JsonSerializerSettings()
            {
                DateParseHandling = DateParseHandling.None,
            });

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
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(input);

            foreach (string propertyName in encryptionSettings.PropertiesToEncrypt)
            {
                // possibly a wrong path configured in the Client Encryption Policy, ignore.
                if (!itemJObj.TryGetValue(propertyName, out JToken propertyValue))
                {
                    continue;
                }

                EncryptionSettingForProperty settingforProperty = encryptionSettings.GetEncryptionSettingForProperty(propertyName);

                if (settingforProperty == null)
                {
                    throw new ArgumentException($"Invalid Encryption Setting for the Property:{propertyName}. ");
                }

                await EncryptPropertyAsync(
                    itemJObj,
                    propertyValue,
                    settingforProperty,
                    cancellationToken);

            }

            input.Dispose();
            return EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        /// <remarks>
        /// If there isn't any data that needs to be decrypted, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public static async Task<Stream> DecryptAsync(
            Stream input,
            EncryptionSettings encryptionSettings,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return input;
            }

            Debug.Assert(input.CanSeek);
            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj = RetrieveItem(input);

            await DecryptObjectAsync(
                itemJObj,
                encryptionSettings,
                diagnosticsContext,
                cancellationToken);

            input.Dispose();
            return EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        public static async Task<JObject> DecryptAsync(
            JObject document,
            EncryptionSettings encryptionSettings,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);

            await DecryptObjectAsync(
                document,
                encryptionSettings,
                diagnosticsContext,
                cancellationToken);

            return document;
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

            propertyValueToEncrypt = await EncryptJTokenAsync(propertyValueToEncrypt, settingsForProperty, cancellationToken);

            return EncryptionProcessor.BaseSerializer.ToStream(propertyValueToEncrypt);
        }

        private static (TypeMarker, byte[]) Serialize(JToken propertyValue)
        {
            SqlSerializerFactory sqlSerializerFactory = new SqlSerializerFactory();

            // UTF-8 Encoding
            SqlVarCharSerializer sqlVarcharSerializer = new SqlVarCharSerializer(size: -1, codePageCharacterEncoding: 65001);

            return propertyValue.Type switch
            {
                JTokenType.Boolean => (TypeMarker.Boolean, sqlSerializerFactory.GetDefaultSerializer<bool>().Serialize(propertyValue.ToObject<bool>())),
                JTokenType.Float => (TypeMarker.Double, sqlSerializerFactory.GetDefaultSerializer<double>().Serialize(propertyValue.ToObject<double>())),
                JTokenType.Integer => (TypeMarker.Long, sqlSerializerFactory.GetDefaultSerializer<long>().Serialize(propertyValue.ToObject<long>())),
                JTokenType.String => (TypeMarker.String, sqlVarcharSerializer.Serialize(propertyValue.ToObject<string>())),
                _ => throw new InvalidOperationException($"Invalid or Unsupported Data Type Passed : {propertyValue.Type}. "),
            };
        }

        private static JToken DeserializeAndAddProperty(
            byte[] serializedBytes,
            TypeMarker typeMarker)
        {
            SqlSerializerFactory sqlSerializerFactory = new SqlSerializerFactory();

            // UTF-8 Encoding
            SqlVarCharSerializer sqlVarcharSerializer = new SqlVarCharSerializer(size: -1, codePageCharacterEncoding: 65001);

            return typeMarker switch
            {
                TypeMarker.Boolean => sqlSerializerFactory.GetDefaultSerializer<bool>().Deserialize(serializedBytes),
                TypeMarker.Double => sqlSerializerFactory.GetDefaultSerializer<double>().Deserialize(serializedBytes),
                TypeMarker.Long => sqlSerializerFactory.GetDefaultSerializer<long>().Deserialize(serializedBytes),
                TypeMarker.String => sqlVarcharSerializer.Deserialize(serializedBytes),
                _ => throw new InvalidOperationException($"Invalid or Unsupported Data Type Passed : {typeMarker}. "),
            };
        }

        private static async Task<JToken> EncryptJTokenAsync(
           JToken propertyValueToEncrypt,
           EncryptionSettingForProperty encryptionSettingForProperty,
           CancellationToken cancellationToken)
        {
            /* Top Level can be an Object*/
            if (propertyValueToEncrypt.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in propertyValueToEncrypt.Children<JProperty>())
                {
                    if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                    {
                        await EncryptJTokenAsync(
                            jProperty.Value,
                            encryptionSettingForProperty,
                            cancellationToken);
                    }
                    else
                    {
                        jProperty.Value = await SerializeAndEncryptValueAsync(jProperty.Value, encryptionSettingForProperty, cancellationToken);
                    }
                }
            }
            else if (propertyValueToEncrypt.Type == JTokenType.Array)
            {
                if (propertyValueToEncrypt.Children().Any())
                {
                    // objects as array elements.
                    if (propertyValueToEncrypt.Children().First().Type == JTokenType.Object)
                    {
                        foreach (JObject arrayjObject in propertyValueToEncrypt.Children<JObject>())
                        {
                            foreach (JProperty jProperty in arrayjObject.Properties())
                            {
                                if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                                {
                                    await EncryptJTokenAsync(
                                        jProperty.Value,
                                        encryptionSettingForProperty,
                                        cancellationToken);
                                }

                                // primitive type
                                else
                                {
                                    jProperty.Value = await SerializeAndEncryptValueAsync(jProperty.Value, encryptionSettingForProperty, cancellationToken);
                                }
                            }
                        }
                    }

                    // array as elements.
                    else if (propertyValueToEncrypt.Children().First().Type == JTokenType.Array)
                    {
                        foreach (JArray jArray in propertyValueToEncrypt.Value<JArray>())
                        {
                            for (int i = 0; i < jArray.Count(); i++)
                            {
                                // iterates over individual elements
                                if (jArray[i].Type == JTokenType.Object || jArray[i].Type == JTokenType.Array)
                                {
                                    await EncryptJTokenAsync(
                                        jArray[i],
                                        encryptionSettingForProperty,
                                        cancellationToken);
                                }

                                // primitive type
                                else
                                {
                                    jArray[i] = await SerializeAndEncryptValueAsync(jArray[i], encryptionSettingForProperty, cancellationToken);
                                }
                            }
                        }
                    }

                    // array of primitive types.
                    else
                    {
                        for (int i = 0; i < propertyValueToEncrypt.Count(); i++)
                        {
                            propertyValueToEncrypt[i] = await SerializeAndEncryptValueAsync(propertyValueToEncrypt[i], encryptionSettingForProperty, cancellationToken);
                        }
                    }
                }
            }
            else
            {
                propertyValueToEncrypt = await SerializeAndEncryptValueAsync(propertyValueToEncrypt, encryptionSettingForProperty, cancellationToken);
            }

            return propertyValueToEncrypt;
        }

        private static async Task EncryptPropertyAsync(
            JObject itemJObj,
            JToken propertyValue,
            EncryptionSettingForProperty encryptionSettingForProperty,
            CancellationToken cancellationToken)
        {
            if (propertyValue.Type == JTokenType.Object || propertyValue.Type == JTokenType.Array)
            {
                await EncryptJTokenAsync(propertyValue, encryptionSettingForProperty, cancellationToken);
            }
            else
            {
                itemJObj.Property(propertyValue.Path).Value = await EncryptJTokenAsync(
                    propertyValue,
                    encryptionSettingForProperty,
                    cancellationToken);
            }
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
		
        private static async Task DecryptPropertyAsync(
            JObject itemJObj,
            EncryptionSettingForProperty encryptionSettingForProperty,
            string propertyName,
            JToken propertyValue,
            CancellationToken cancellationToken)
        {
            if (propertyValue.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in propertyValue.Children<JProperty>())
                {
                    if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                    {
                        await DecryptPropertyAsync(
                            itemJObj,
                            encryptionSettingForProperty,
                            jProperty.Name,
                            jProperty.Value,
                            cancellationToken);
                    }
                    else
                    {
                        jProperty.Value = await DecryptAndDeserializeValueAsync(
                            jProperty.Value,
                            encryptionSettingForProperty,
                            cancellationToken);
                    }
                }
            }
            else if (propertyValue.Type == JTokenType.Array)
            {
                if (propertyValue.Children().Any())
                {
                    if (propertyValue.Children().First().Type == JTokenType.Object)
                    {
                        foreach (JObject arrayjObject in propertyValue.Children<JObject>())
                        {
                            foreach (JProperty jProperty in arrayjObject.Properties())
                            {
                                if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                                {
                                    await DecryptPropertyAsync(
                                        itemJObj,
                                        encryptionSettingForProperty,
                                        jProperty.Name,
                                        jProperty.Value,
                                        cancellationToken);
                                }
                                else
                                {
                                    jProperty.Value = await DecryptAndDeserializeValueAsync(
                                        jProperty.Value,
                                        encryptionSettingForProperty,
                                        cancellationToken);
                                }
                            }
                        }
                    }
                    else if (propertyValue.Children().First().Type == JTokenType.Array)
                    {
                        foreach (JArray jArray in propertyValue.Value<JArray>())
                        {
                            for (int i = 0; i < jArray.Count(); i++)
                            {
                                // iterates over individual elements
                                if (jArray[i].Type == JTokenType.Object || jArray[i].Type == JTokenType.Array)
                                {
                                    await DecryptPropertyAsync(
                                        itemJObj,
                                        encryptionSettingForProperty,
                                        jArray[i].Path,
                                        jArray[i],
                                        cancellationToken);
                                }
                                else
                                {
                                    jArray[i] = await DecryptAndDeserializeValueAsync(
                                        jArray[i],
                                        encryptionSettingForProperty,
                                        cancellationToken);
                                }
                            }
                        }
                    }

                    // primitive type
                    else
                    {
                        for (int i = 0; i < propertyValue.Count(); i++)
                        {
                            propertyValue[i] = await DecryptAndDeserializeValueAsync(
                                propertyValue[i],
                                encryptionSettingForProperty,
                                cancellationToken);
                        }
                    }
                }
            }
            else
            {
                itemJObj.Property(propertyName).Value = await DecryptAndDeserializeValueAsync(
                    itemJObj.Property(propertyName).Value,
                    encryptionSettingForProperty,
                    cancellationToken);
            }
        }

        private static async Task DecryptObjectAsync(
            JObject document,
            EncryptionSettings encryptionSettings,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(diagnosticsContext != null);

            foreach (string propertyName in encryptionSettings.PropertiesToEncrypt)
            {
                if (document.TryGetValue(propertyName, out JToken propertyValue))
                {
                    EncryptionSettingForProperty settingsForProperty = encryptionSettings.GetEncryptionSettingForProperty(propertyName);

                    if (settingsForProperty == null)
                    {
                        throw new ArgumentException($"Invalid Encryption Setting for Property:{propertyName}. ");
                    }

                    await DecryptPropertyAsync(
                        document,
                        settingsForProperty,
                        propertyName,
                        propertyValue,
                        cancellationToken);
                }
            }

            return;
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