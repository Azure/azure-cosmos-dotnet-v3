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
    using Microsoft.Data.AAP_PH.Cryptography.Serializers;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Allows encrypting items in a container using AAP Encryption Algorithm .
    /// </summary>
    internal class AapEncryptionProcessor : EncryptionProcessor
    {
        public override async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(diagnosticsContext != null);

            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (encryptor == null)
            {
                throw new ArgumentNullException(nameof(encryptor));
            }

            if (encryptionOptions == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions));
            }

            if (string.IsNullOrWhiteSpace(encryptionOptions.DataEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(encryptionOptions.DataEncryptionKeyId));
            }

            if (string.IsNullOrWhiteSpace(encryptionOptions.EncryptionAlgorithm))
            {
                throw new ArgumentNullException(nameof(encryptionOptions.EncryptionAlgorithm));
            }

            if (encryptionOptions.PathsToEncrypt == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions.PathsToEncrypt));
            }

            if (!encryptionOptions.PathsToEncrypt.Any())
            {
                return input;
            }

            foreach (string path in encryptionOptions.PathsToEncrypt)
            {
                if (string.IsNullOrWhiteSpace(path) || path[0] != '/' || path.LastIndexOf('/') != 0)
                {
                    throw new ArgumentException($"Invalid path {path ?? string.Empty}", nameof(encryptionOptions.PathsToEncrypt));
                }

                if (string.Equals(path.Substring(1), "id"))
                {
                    throw new ArgumentException($"{nameof(encryptionOptions.PathsToEncrypt)} includes a invalid path: '{path}'.");
                }
            }

            JObject itemJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(input);

            foreach (string pathToEncrypt in encryptionOptions.PathsToEncrypt)
            {
                string propertyName = pathToEncrypt.Substring(1);
                if (!itemJObj.TryGetValue(propertyName, out JToken propertyValue))
                {
                    throw new ArgumentException($"{nameof(encryptionOptions.PathsToEncrypt)} includes a path: '{pathToEncrypt}' which was not found.");
                }

                if (propertyValue.Type == JTokenType.Null)
                {
                    continue;
                }

                (TypeMarker typeMarker, byte[] plainText) = AapEncryptionProcessor.Serialize(propertyValue);

                byte[] cipherText = await encryptor.EncryptAsync(
                    plainText,
                    encryptionOptions.DataEncryptionKeyId,
                    encryptionOptions.EncryptionAlgorithm);

                byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
                cipherTextWithTypeMarker[0] = (byte)typeMarker;
                Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
                itemJObj[propertyName] = cipherTextWithTypeMarker;

                if (cipherTextWithTypeMarker == null)
                {
                    throw new InvalidOperationException($"{nameof(Encryptor)} returned null cipherText from {nameof(this.EncryptAsync)}.");
                }
            }

            EncryptionProperties encryptionProperties = new EncryptionProperties(
                  encryptionFormatVersion: 3,
                  encryptionOptions.EncryptionAlgorithm,
                  encryptionOptions.DataEncryptionKeyId,
                  encryptedData: null,
                  encryptionOptions.PathsToEncrypt);

            itemJObj.Add(Constants.EncryptedInfo, JObject.FromObject(encryptionProperties));
            input.Dispose();
            return EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        /// <remarks>
        /// If there isn't any data that needs to be decrypted, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public override async Task<Stream> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(input.CanSeek);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj;
            using (StreamReader sr = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
            {
                itemJObj = JsonSerializer.Create().Deserialize<JObject>(jsonTextReader);
            }

            JProperty encryptionPropertiesJProp = itemJObj.Property(Constants.EncryptedInfo);
            JObject encryptionPropertiesJObj = null;
            if (encryptionPropertiesJProp != null && encryptionPropertiesJProp.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
            {
                encryptionPropertiesJObj = (JObject)encryptionPropertiesJProp.Value;
            }

            if (encryptionPropertiesJObj == null)
            {
                input.Position = 0;
                return input;
            }

            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();
            JObject plainTextJObj = new JObject();
            foreach (string path in encryptionProperties.EncryptedPaths)
            {
                if (itemJObj.TryGetValue(path.Substring(1), out JToken propertyValue))
                {
                    byte[] cipherTextWithTypeMarker = propertyValue.ToObject<byte[]>();

                    if (cipherTextWithTypeMarker == null)
                    {
                        continue;
                    }

                    byte[] cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
                    Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);

                    byte[] plainText = await AapEncryptionProcessor.DecryptContentAsync(
                        encryptionProperties,
                        cipherText,
                        encryptor,
                        diagnosticsContext,
                        cancellationToken);

                    string key = path.Substring(1);
                    AapEncryptionProcessor.DeserializeAndAddProperty(
                                (TypeMarker)cipherTextWithTypeMarker[0],
                                plainText,
                                plainTextJObj,
                                key);
                }
            }

            foreach (JProperty property in plainTextJObj.Properties())
            {
                itemJObj[property.Name] = property.Value;
            }

            input.Dispose();
            return EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        public override async Task<JObject> DecryptAsync(
            JObject document,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

            if (document.TryGetValue(Constants.EncryptedInfo, out JToken encryptedInfo))
            {
                EncryptionProperties encryptionProperties = JsonConvert.DeserializeObject<EncryptionProperties>(encryptedInfo.ToString());
                JObject plainTextJObj = new JObject();
                foreach (string path in encryptionProperties.EncryptedPaths)
                {
                    if (document.TryGetValue(path.Substring(1), out JToken propertyValue))
                    {
                        byte[] cipherTextWithTypeMarker = propertyValue.ToObject<byte[]>();

                        if (cipherTextWithTypeMarker == null)
                        {
                            continue;
                        }

                        byte[] cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
                        Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);

                        byte[] plainText = await AapEncryptionProcessor.DecryptContentAsync(
                            encryptionProperties,
                            cipherText,
                            encryptor,
                            diagnosticsContext,
                            cancellationToken);

                        string key = path.Substring(1);

                        AapEncryptionProcessor.DeserializeAndAddProperty(
                                    (TypeMarker)cipherTextWithTypeMarker[0],
                                    plainText,
                                    plainTextJObj,
                                    key);
                    }
                }

                foreach (JProperty property in plainTextJObj.Properties())
                {
                    document[property.Name] = property.Value;
                }
            }

            return document;
        }

        private static async Task<byte[]> DecryptContentAsync(
            EncryptionProperties encryptionProperties,
            byte[] cipherText,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (encryptionProperties.EncryptionFormatVersion != 3)
            {
                throw new NotSupportedException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            byte[] plainText = await encryptor.DecryptAsync(
                cipherText,
                encryptionProperties.DataEncryptionKeyId,
                encryptionProperties.EncryptionAlgorithm,
                cancellationToken);

            if (plainText == null)
            {
                throw new InvalidOperationException($"{nameof(Encryptor)} returned null plainText from {nameof(DecryptAsync)}.");
            }

            return plainText;
        }

        private static (TypeMarker, byte[]) Serialize(JToken element)
        {
            switch (element.Type)
            {
                case JTokenType.Boolean:
                    return (TypeMarker.Boolean, SerializerDefaultMappings.GetDefaultSerializer<bool>().Serialize(element.ToObject<bool>()));
                case JTokenType.Undefined:
                    Debug.Assert(false, "Undefined value cannot be in the JSON");
                    return (default(TypeMarker), null);
                case JTokenType.Null:
                    Debug.Assert(false, "Null type should have been handled by caller");
                    return (TypeMarker.Null, null);
                case JTokenType.Float:
                    return (TypeMarker.Float, SerializerDefaultMappings.GetDefaultSerializer<double>().Serialize(element.ToObject<double>()));
                case JTokenType.Integer:
                    return (TypeMarker.Integer, SerializerDefaultMappings.GetDefaultSerializer<int>().Serialize(element.ToObject<int>()));
                case JTokenType.String:
                    return (TypeMarker.String, SerializerDefaultMappings.GetDefaultSerializer<string>().Serialize(element.ToObject<string>()));
                case JTokenType.Array:
                    return (TypeMarker.Array, SerializerDefaultMappings.GetDefaultSerializer<string>().Serialize(element.ToString()));
                default:
                    return (TypeMarker.RawText, SerializerDefaultMappings.GetDefaultSerializer<string>().Serialize(element.ToString()));
            }
        }

        private static void DeserializeAndAddProperty(
            TypeMarker typeMarker,
            byte[] serializedBytes,
            JObject jObject,
            string key)
        {
            switch (typeMarker)
            {
                case TypeMarker.Boolean:
                    jObject.Add(key, SerializerDefaultMappings.GetDefaultSerializer<bool>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.Float:
                    jObject.Add(key, SerializerDefaultMappings.GetDefaultSerializer<double>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.Integer:
                    jObject.Add(key, SerializerDefaultMappings.GetDefaultSerializer<int>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.String:
                    jObject.Add(key, SerializerDefaultMappings.GetDefaultSerializer<string>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.Array:
                    jObject.Add(key, JsonConvert.DeserializeObject<JArray>(SerializerDefaultMappings.GetDefaultSerializer<string>().Deserialize(serializedBytes)));
                    break;
                case TypeMarker.RawText:
                    jObject.Add(key, SerializerDefaultMappings.GetDefaultSerializer<string>().Deserialize(serializedBytes));
                    break;
                default:
                    Debug.Fail(string.Format("Unexpected type marker {0}", typeMarker));
                    break;
            }
        }

        private enum TypeMarker : byte
        {
            Null = 1, // not used
            String = 2,
            Float = 3,
            Integer = 4,
            Boolean = 5,
            Array = 6,
            RawText = 7,
        }
    }
}
