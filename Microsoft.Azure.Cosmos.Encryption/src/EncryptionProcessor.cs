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

                AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = await settingforProperty.BuildEncryptionAlgorithmForSettingAsync(cancellationToken: cancellationToken);

                EncryptProperty(
                    itemJObj,
                    propertyValue,
                    aeadAes256CbcHmac256EncryptionAlgorithm);
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

            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = await settingsForProperty.BuildEncryptionAlgorithmForSettingAsync(cancellationToken: cancellationToken);

            JToken propertyValueToEncrypt = EncryptionProcessor.BaseSerializer.FromStream<JToken>(valueStream);
            (EncryptionProcessor.TypeMarker typeMarker, byte[] serializedData) = EncryptionProcessor.Serialize(propertyValueToEncrypt);

            byte[] cipherText = aeadAes256CbcHmac256EncryptionAlgorithm.Encrypt(serializedData);

            if (cipherText == null)
            {
                throw new InvalidOperationException($"{nameof(EncryptValueStreamAsync)} returned null cipherText from {nameof(aeadAes256CbcHmac256EncryptionAlgorithm.Encrypt)}. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }

            byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
            cipherTextWithTypeMarker[0] = (byte)typeMarker;
            Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);

            return EncryptionProcessor.BaseSerializer.ToStream(cipherTextWithTypeMarker);
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

        internal static JToken EncryptProperty(
            JToken propertyValue,
            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm)
        {
            /* Top Level can be an Object*/
            if (propertyValue.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in propertyValue.Children<JProperty>())
                {
                    jProperty.Value = EncryptionProcessor.EncryptProperty(
                        jProperty.Value,
                        aeadAes256CbcHmac256EncryptionAlgorithm);
                }
            }
            else if (propertyValue.Type == JTokenType.Array)
            {
                if (propertyValue.Children().Any())
                {
                    // objects as array elements.
                    if (propertyValue.Children().First().Type == JTokenType.Object)
                    {
                        foreach (JObject arrayjObject in propertyValue.Children<JObject>())
                        {
                            foreach (JProperty jProperty in arrayjObject.Properties())
                            {
                                jProperty.Value = EncryptionProcessor.EncryptProperty(
                                    jProperty.Value,
                                    aeadAes256CbcHmac256EncryptionAlgorithm);
                            }
                        }
                    }

                    // array as elements.
                    else if (propertyValue.Children().First().Type == JTokenType.Array)
                    {
                        foreach (JArray jArray in propertyValue.Value<JArray>())
                        {
                            for (int i = 0; i < jArray.Count(); i++)
                            {
                                // iterates over individual elements
                                jArray[i] = EncryptionProcessor.EncryptProperty(
                                    jArray[i],
                                    aeadAes256CbcHmac256EncryptionAlgorithm);
                            }
                        }
                    }

                    // array of primitive types.
                    else
                    {
                        for (int i = 0; i < propertyValue.Count(); i++)
                        {
                            propertyValue[i] = SerializeAndEncryptValue(propertyValue[i], aeadAes256CbcHmac256EncryptionAlgorithm);
                        }
                    }
                }
            }
            else
            {
                propertyValue = SerializeAndEncryptValue(
                    propertyValue,
                    aeadAes256CbcHmac256EncryptionAlgorithm);
            }

            return propertyValue;
        }

        private static void EncryptProperty(
            JObject itemJObj,
            JToken propertyValue,
            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm)
        {
            /* Top Level can be an Object*/
            if (propertyValue.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in propertyValue.Children<JProperty>())
                {
                    if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                    {
                        EncryptProperty(
                            itemJObj,
                            jProperty.Value,
                            aeadAes256CbcHmac256EncryptionAlgorithm);
                    }
                    else
                    {
                        jProperty.Value = SerializeAndEncryptValue(jProperty.Value, aeadAes256CbcHmac256EncryptionAlgorithm);
                    }
                }
            }
            else if (propertyValue.Type == JTokenType.Array)
            {
                if (propertyValue.Children().Any())
                {
                    // objects as array elements.
                    if (propertyValue.Children().First().Type == JTokenType.Object)
                    {
                        foreach (JObject arrayjObject in propertyValue.Children<JObject>())
                        {
                            foreach (JProperty jProperty in arrayjObject.Properties())
                            {
                                if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                                {
                                    EncryptProperty(
                                        itemJObj,
                                        jProperty.Value,
                                        aeadAes256CbcHmac256EncryptionAlgorithm);
                                }

                                // primitive type
                                else
                                {
                                    jProperty.Value = SerializeAndEncryptValue(jProperty.Value, aeadAes256CbcHmac256EncryptionAlgorithm);
                                }
                            }
                        }
                    }

                    // array as elements.
                    else if (propertyValue.Children().First().Type == JTokenType.Array)
                    {
                        foreach (JArray jArray in propertyValue.Value<JArray>())
                        {
                            for (int i = 0; i < jArray.Count(); i++)
                            {
                                // iterates over individual elements
                                if (jArray[i].Type == JTokenType.Object || jArray[i].Type == JTokenType.Array)
                                {
                                    EncryptProperty(
                                        itemJObj,
                                        jArray[i],
                                        aeadAes256CbcHmac256EncryptionAlgorithm);
                                }

                                // primitive type
                                else
                                {
                                    jArray[i] = SerializeAndEncryptValue(jArray[i], aeadAes256CbcHmac256EncryptionAlgorithm);
                                }
                            }
                        }
                    }

                    // array of primitive types.
                    else
                    {
                        for (int i = 0; i < propertyValue.Count(); i++)
                        {
                            propertyValue[i] = SerializeAndEncryptValue(propertyValue[i], aeadAes256CbcHmac256EncryptionAlgorithm);
                        }
                    }
                }
            }
            else
            {
                itemJObj.Property(propertyValue.Path).Value = SerializeAndEncryptValue(
                    itemJObj.Property(propertyValue.Path).Value,
                    aeadAes256CbcHmac256EncryptionAlgorithm);
            }
        }

        private static JToken SerializeAndEncryptValue(
           JToken jToken,
           AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm)
        {
            JToken propertyValueToEncrypt = jToken;

            if (propertyValueToEncrypt.Type == JTokenType.Null)
            {
                return propertyValueToEncrypt;
            }

            (TypeMarker typeMarker, byte[] plainText) = Serialize(propertyValueToEncrypt);

            byte[] cipherText = aeadAes256CbcHmac256EncryptionAlgorithm.Encrypt(plainText);

            if (cipherText == null)
            {
                throw new InvalidOperationException($"{nameof(SerializeAndEncryptValue)} returned null cipherText from {nameof(aeadAes256CbcHmac256EncryptionAlgorithm.Encrypt)}. ");
            }

            byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
            cipherTextWithTypeMarker[0] = (byte)typeMarker;
            Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
            return cipherTextWithTypeMarker;
        }

        private static JToken DecryptAndDeserializeValue(
           JToken jToken,
           AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm)
        {
            byte[] cipherTextWithTypeMarker = jToken.ToObject<byte[]>();

            if (cipherTextWithTypeMarker == null)
            {
                return null;
            }

            byte[] cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
            Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);

            byte[] plainText = aeadAes256CbcHmac256EncryptionAlgorithm.Decrypt(cipherText);

            if (plainText == null)
            {
                throw new InvalidOperationException($"{nameof(DecryptAndDeserializeValue)} returned null plainText from {nameof(aeadAes256CbcHmac256EncryptionAlgorithm.Decrypt)}. ");
            }

            return DeserializeAndAddProperty(
                plainText,
                (TypeMarker)cipherTextWithTypeMarker[0]);
        }

        private static void DecryptProperty(
            JObject itemJObj,
            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm,
            string propertyName,
            JToken propertyValue)
        {
            if (propertyValue.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in propertyValue.Children<JProperty>())
                {
                    if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                    {
                        DecryptProperty(
                            itemJObj,
                            aeadAes256CbcHmac256EncryptionAlgorithm,
                            jProperty.Name,
                            jProperty.Value);
                    }
                    else
                    {
                        jProperty.Value = DecryptAndDeserializeValue(
                            jProperty.Value,
                            aeadAes256CbcHmac256EncryptionAlgorithm);
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
                                    DecryptProperty(
                                        itemJObj,
                                        aeadAes256CbcHmac256EncryptionAlgorithm,
                                        jProperty.Name,
                                        jProperty.Value);
                                }
                                else
                                {
                                    jProperty.Value = DecryptAndDeserializeValue(
                                        jProperty.Value,
                                        aeadAes256CbcHmac256EncryptionAlgorithm);
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
                                    DecryptProperty(
                                        itemJObj,
                                        aeadAes256CbcHmac256EncryptionAlgorithm,
                                        jArray[i].Path,
                                        jArray[i]);
                                }
                                else
                                {
                                    jArray[i] = DecryptAndDeserializeValue(
                                        jArray[i],
                                        aeadAes256CbcHmac256EncryptionAlgorithm);
                                }
                            }
                        }
                    }

                    // primitive type
                    else
                    {
                        for (int i = 0; i < propertyValue.Count(); i++)
                        {
                            propertyValue[i] = DecryptAndDeserializeValue(
                                propertyValue[i],
                                aeadAes256CbcHmac256EncryptionAlgorithm);
                        }
                    }
                }
            }
            else
            {
                itemJObj.Property(propertyName).Value = DecryptAndDeserializeValue(
                    itemJObj.Property(propertyName).Value,
                    aeadAes256CbcHmac256EncryptionAlgorithm);
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

                    AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = await settingsForProperty.BuildEncryptionAlgorithmForSettingAsync(cancellationToken: cancellationToken);

                    DecryptProperty(
                        document,
                        aeadAes256CbcHmac256EncryptionAlgorithm,
                        propertyName,
                        propertyValue);
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