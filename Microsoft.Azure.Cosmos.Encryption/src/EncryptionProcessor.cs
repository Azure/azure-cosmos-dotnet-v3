//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
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

            List<string> visited = new List<string>();
            foreach (string propertyName in encryptionSettings.PropertiesToEncrypt)
            {
                // possibly a wrong path configured in the Client Encryption Policy, ignore.
                string parentPath = propertyName.Split('/')[0];
                JProperty propertyToEncrypt = itemJObj.Property(parentPath);
                if (propertyToEncrypt == null || visited.Contains(parentPath))
                {
                    continue;
                }

                visited.Add(parentPath);

                Dictionary<string, EncryptionSettingForProperty> settingForProperty = encryptionSettings.GetEncryptionSettingForProperty(parentPath);

                if (settingForProperty == null)
                {
                    throw new ArgumentException($"Invalid Encryption Setting for the Property:{propertyName}. ");
                }

                // pass the parent path Setting.Find all path perhaps which start /parent/* and build it in settingforProperty
                await EncryptJTokenAsync(
                    propertyToEncrypt.Value,
                    settingForProperty,
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

            Debug.Assert(input.CanSeek, "DecryptAsync input.CanSeek false");

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
            Debug.Assert(document != null,  "DecryptAsync document null");

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
            Dictionary<string, EncryptionSettingForProperty> settingsForProperty,
            CancellationToken cancellationToken,
            string parentPath)
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
                await EncryptJTokenAsync(encryptedPropertyValue, settingsForProperty, cancellationToken, parentPath);
            }
            else
            {
                encryptedPropertyValue = await SerializeAndEncryptValueAsync(propertyValueToEncrypt, settingsForProperty[parentPath], cancellationToken);
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

        private static async Task<string> EncryptJTokenAsync(
           JToken jTokenToEncrypt,
           Dictionary<string, EncryptionSettingForProperty> encryptionSettingForPropertyForParentPath,
           CancellationToken cancellationToken,
           string absolutePath = null)
        {
            // since just the value is sent out.
            if (string.IsNullOrEmpty(absolutePath))
            {
                absolutePath = absolutePath + "/" + jTokenToEncrypt.Parent.Path;
            }

            // Top Level can be an Object
            if (jTokenToEncrypt.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in jTokenToEncrypt.Children<JProperty>())
                {
                    absolutePath = absolutePath + "/" + jProperty.Name;
                    absolutePath = await EncryptJTokenAsync(
                        jProperty.Value,
                        encryptionSettingForPropertyForParentPath,
                        cancellationToken,
                        absolutePath);

                    // dont remove the parent path
                    if (absolutePath.LastIndexOf("/") != 0)
                    {
                        absolutePath = absolutePath.Remove(absolutePath.LastIndexOf("/"));
                    }
                }
            }
            else if (jTokenToEncrypt.Type == JTokenType.Array)
            {
                // Get the parent and pass just its encryptionSetting.
                if (jTokenToEncrypt.Children().Any())
                {
                    absolutePath = absolutePath + "/" + jTokenToEncrypt.Path;
                    for (int i = 0; i < jTokenToEncrypt.Count(); i++)
                    {
                        absolutePath = await EncryptJTokenAsync(
                            jTokenToEncrypt[i],
                            encryptionSettingForPropertyForParentPath,
                            cancellationToken,
                            absolutePath);

                        // dont remove the parent path
                        if (absolutePath.LastIndexOf("/") != 0)
                        {
                            absolutePath = absolutePath.Remove(absolutePath.LastIndexOf("/"));
                        }
                    }
                }
            }
            else
            {
                if (!encryptionSettingForPropertyForParentPath.TryGetValue(absolutePath, out EncryptionSettingForProperty encryptionSettingForProperty))
                {
                    string parentPropertyName = "/" + absolutePath.Split('/').ElementAt(1);

                    // check if the parent path is part of policy
                    if (!encryptionSettingForPropertyForParentPath.TryGetValue(parentPropertyName, out encryptionSettingForProperty))
                    {
                    }
                }

                if (encryptionSettingForProperty != null)
                {
                    jTokenToEncrypt.Replace(await SerializeAndEncryptValueAsync(jTokenToEncrypt, encryptionSettingForProperty, cancellationToken));
                }

                // parent path was not passed in policy.
                else
                {
                    jTokenToEncrypt.Replace(jTokenToEncrypt);
                }
            }

            return absolutePath;
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

        private static async Task<string> DecryptJTokenAsync(
            JToken jTokenToDecrypt,
            Dictionary<string, EncryptionSettingForProperty> encryptionSettingForPropertyForParentPath,
            CancellationToken cancellationToken,
            string absolutePath = null)
        {
            // since just the value is sent out.
            if (string.IsNullOrEmpty(absolutePath))
            {
                absolutePath = absolutePath + "/" + jTokenToDecrypt.Parent.Path;
            }

            if (jTokenToDecrypt.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in jTokenToDecrypt.Children<JProperty>())
                {
                    absolutePath = absolutePath + "/" + jProperty.Name;
                    await DecryptJTokenAsync(
                        jProperty.Value,
                        encryptionSettingForPropertyForParentPath,
                        cancellationToken,
                        absolutePath);

                    if (absolutePath.LastIndexOf("/") != 0)
                    {
                        absolutePath = absolutePath.Remove(absolutePath.LastIndexOf("/"));
                    }
                }
            }
            else if (jTokenToDecrypt.Type == JTokenType.Array)
            {
                if (jTokenToDecrypt.Children().Any())
                {
                    absolutePath = absolutePath + "/" + jTokenToDecrypt.Path;
                    for (int i = 0; i < jTokenToDecrypt.Count(); i++)
                    {
                        await DecryptJTokenAsync(
                            jTokenToDecrypt[i],
                            encryptionSettingForPropertyForParentPath,
                            cancellationToken,
                            absolutePath);

                        if (absolutePath.LastIndexOf("/") != 0)
                        {
                            absolutePath = absolutePath.Remove(absolutePath.LastIndexOf("/"));
                        }
                    }
                }
            }
            else
            {
                if (!encryptionSettingForPropertyForParentPath.TryGetValue(absolutePath, out EncryptionSettingForProperty encryptionSettingForProperty))
                {
                    string parentPropertyName = "/" + absolutePath.Split('/').ElementAt(1);

                    // check if the parent path is part of policy.
                    if (!encryptionSettingForPropertyForParentPath.TryGetValue(parentPropertyName, out encryptionSettingForProperty))
                    {
                    }
                }

                if (encryptionSettingForProperty != null)
                {
                    jTokenToDecrypt.Replace(await DecryptAndDeserializeValueAsync(
                        jTokenToDecrypt,
                        encryptionSettingForProperty,
                        cancellationToken));
                }

                // not part of the policy.
                else
                {
                    jTokenToDecrypt.Replace(jTokenToDecrypt);
                }
            }

            return absolutePath;
        }

        private static async Task<int> DecryptObjectAsync(
            JObject document,
            EncryptionSettings encryptionSettings,
            CancellationToken cancellationToken)
        {
            int propertiesDecryptedCount = 0;
            List<string> visited = new List<string>();
            foreach (string propertyName in encryptionSettings.PropertiesToEncrypt)
            {
                string parentPath = propertyName.Split('/')[0];

                JProperty propertyToDecrypt = document.Property(parentPath);
                if (propertyToDecrypt != null && !visited.Contains(parentPath))
                {
                    Dictionary<string, EncryptionSettingForProperty> settingsForProperty = encryptionSettings.GetEncryptionSettingForProperty(parentPath);

                    if (settingsForProperty == null)
                    {
                        throw new ArgumentException($"Invalid Encryption Setting for Property:{propertyName}. ");
                    }

                    await DecryptJTokenAsync(
                        propertyToDecrypt.Value,
                        settingsForProperty,
                        cancellationToken,
                        "/" + parentPath);

                    propertiesDecryptedCount++;
                    visited.Add(parentPath);
                }
            }

            return propertiesDecryptedCount;
        }

        private static JObject RetrieveItem(
            Stream input)
        {
            Debug.Assert(input != null, "RetrieveItem input stream null");

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