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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography.Serializers;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Allows encrypting items in a container using MDE Encryption Algorithm.
    /// </summary>
    internal sealed class MdeEncryptionProcessor : EncryptionProcessor
    {
        /// <remarks>
        /// If there isn't any PathsToEncrypt, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public override async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            this.ValidateInputForEncrypt(
                input,
                encryptor,
                encryptionOptions);

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
            List<string> pathsEncrypted = new List<string>();

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

                (TypeMarker typeMarker, byte[] plainText) = MdeEncryptionProcessor.Serialize(propertyValue);

                byte[] cipherText = await encryptor.EncryptAsync(
                    plainText,
                    encryptionOptions.DataEncryptionKeyId,
                    encryptionOptions.EncryptionAlgorithm);

                if (cipherText == null)
                {
                    throw new InvalidOperationException($"{nameof(Encryptor)} returned null cipherText from {nameof(this.EncryptAsync)}.");
                }

                byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
                cipherTextWithTypeMarker[0] = (byte)typeMarker;
                Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
                itemJObj[propertyName] = cipherTextWithTypeMarker;
                pathsEncrypted.Add(pathToEncrypt);
            }

            EncryptionProperties encryptionProperties = new EncryptionProperties(
                  encryptionFormatVersion: 3,
                  encryptionOptions.EncryptionAlgorithm,
                  encryptionOptions.DataEncryptionKeyId,
                  encryptedData: null,
                  pathsEncrypted);

            itemJObj.Add(Constants.EncryptedInfo, JObject.FromObject(encryptionProperties));
            input.Dispose();
            return EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        /// <remarks>
        /// If there isn't any data that needs to be decrypted, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public override async Task<(Stream, DecryptionContext)> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return (input, null);
            }

            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj = this.RetrieveItem(input);
            JObject encryptionPropertiesJObj = this.RetrieveEncryptionProperties(itemJObj);

            if (encryptionPropertiesJObj == null)
            {
                input.Position = 0;
                return (input, null);
            }

            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();
            DecryptionContext decryptionContext = await MdeEncryptionProcessor.DecryptObjectAsync(
                itemJObj,
                encryptor,
                encryptionProperties,
                diagnosticsContext,
                cancellationToken);

            input.Dispose();
            return (EncryptionProcessor.BaseSerializer.ToStream(itemJObj), decryptionContext);
        }

        public override async Task<(JObject, DecryptionContext)> DecryptAsync(
            JObject document,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (document == null)
            {
                return (document, null);
            }

            Debug.Assert(encryptor != null);

            if (!document.TryGetValue(Constants.EncryptedInfo, out JToken encryptedInfo))
            {
                return (document, null);
            }

            EncryptionProperties encryptionProperties = JsonConvert.DeserializeObject<EncryptionProperties>(encryptedInfo.ToString());

            DecryptionContext decryptionContext = await MdeEncryptionProcessor.DecryptObjectAsync(
                document,
                encryptor,
                encryptionProperties,
                diagnosticsContext,
                cancellationToken);

            return (document, decryptionContext);
        }

        private static async Task<DecryptionContext> DecryptObjectAsync(
            JObject document,
            Encryptor encryptor,
            EncryptionProperties encryptionProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
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

                    byte[] plainText = await MdeEncryptionProcessor.DecryptPropertyAsync(
                        encryptionProperties,
                        cipherText,
                        encryptor,
                        diagnosticsContext,
                        cancellationToken);

                    MdeEncryptionProcessor.DeserializeAndAddProperty(
                        (TypeMarker)cipherTextWithTypeMarker[0],
                        plainText,
                        plainTextJObj,
                        path.Substring(1));
                }
            }

            document.Remove(Constants.EncryptedInfo);

            List<string> pathsDecrypted = new List<string>();
            foreach (JProperty property in plainTextJObj.Properties())
            {
                document[property.Name] = property.Value;
                pathsDecrypted.Add("/" + property.Name);
            }

            DecryptionInfo decryptionInfo = new DecryptionInfo(
                pathsDecrypted,
                encryptionProperties.DataEncryptionKeyId);

            DecryptionContext decryptionContext = new DecryptionContext(
                new List<DecryptionInfo>() { decryptionInfo });

            return decryptionContext;
        }

        private static async Task<byte[]> DecryptPropertyAsync(
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

        private static (TypeMarker, byte[]) Serialize(JToken propertyValue)
        {
            StandardSerializerFactory standardSerializerFactory = new StandardSerializerFactory();
            switch (propertyValue.Type)
            {
                case JTokenType.Undefined:
                    Debug.Assert(false, "Undefined value cannot be in the JSON");
                    return (default, null);
                case JTokenType.Null:
                    Debug.Assert(false, "Null type should have been handled by caller");
                    return (TypeMarker.Null, null);
                case JTokenType.Boolean:
                    return (TypeMarker.Boolean, standardSerializerFactory.GetDefaultSerializer<bool>().Serialize(propertyValue.ToObject<bool>()));
                case JTokenType.Float:
                    return (TypeMarker.Float, standardSerializerFactory.GetDefaultSerializer<double>().Serialize(propertyValue.ToObject<double>()));
                case JTokenType.Integer:
                    return (TypeMarker.Integer, standardSerializerFactory.GetDefaultSerializer<long>().Serialize(propertyValue.ToObject<long>()));
                case JTokenType.String:
                    return (TypeMarker.String, standardSerializerFactory.GetDefaultSerializer<string>().Serialize(propertyValue.ToObject<string>()));
                case JTokenType.Array:
                    return (TypeMarker.Array, standardSerializerFactory.GetDefaultSerializer<string>().Serialize(propertyValue.ToString()));
                default:
                    return (TypeMarker.RawText, standardSerializerFactory.GetDefaultSerializer<string>().Serialize(propertyValue.ToString()));
            }
        }

        private static void DeserializeAndAddProperty(
            TypeMarker typeMarker,
            byte[] serializedBytes,
            JObject jObject,
            string key)
        {
            StandardSerializerFactory standardSerializerFactory = new StandardSerializerFactory();
            switch (typeMarker)
            {
                case TypeMarker.Boolean:
                    jObject.Add(key, standardSerializerFactory.GetDefaultSerializer<bool>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.Float:
                    jObject.Add(key, standardSerializerFactory.GetDefaultSerializer<double>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.Integer:
                    jObject.Add(key, standardSerializerFactory.GetDefaultSerializer<long>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.String:
                    jObject.Add(key, standardSerializerFactory.GetDefaultSerializer<string>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.Array:
                    jObject.Add(key, JsonConvert.DeserializeObject<JArray>(standardSerializerFactory.GetDefaultSerializer<string>().Deserialize(serializedBytes)));
                    break;
                case TypeMarker.RawText:
                    jObject.Add(key, standardSerializerFactory.GetDefaultSerializer<string>().Deserialize(serializedBytes));
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
