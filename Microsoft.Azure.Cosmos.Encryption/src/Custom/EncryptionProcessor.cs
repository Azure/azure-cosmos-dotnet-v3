//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography.Serializers;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Allows encrypting items in a container using Cosmos Legacy Encryption Algorithm and MDE Encryption Algorithm.
    /// </summary>
    internal static class EncryptionProcessor
    {
        internal static readonly CosmosJsonDotNetSerializer BaseSerializer = new CosmosJsonDotNetSerializer(
            new JsonSerializerSettings()
            {
                DateParseHandling = DateParseHandling.None,
            });

        /// <remarks>
        /// If there isn't any PathsToEncrypt, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public static async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            EncryptionProcessor.ValidateInputForEncrypt(
                input,
                encryptor,
                encryptionOptions);

            if (!encryptionOptions.PathsToEncrypt.Any())
            {
                return input;
            }

            if (!encryptionOptions.PathsToEncrypt.Distinct().SequenceEqual(encryptionOptions.PathsToEncrypt))
            {
                throw new InvalidOperationException("Duplicate paths in PathsToEncrypt passed via EncryptionOptions.");
            }

            foreach (string path in encryptionOptions.PathsToEncrypt)
            {
                if (string.IsNullOrWhiteSpace(path) || path[0] != '/' || path.LastIndexOf('/') != 0)
                {
                    throw new InvalidOperationException($"Invalid path {path ?? string.Empty}, {nameof(encryptionOptions.PathsToEncrypt)}");
                }

                if (string.Equals(path.Substring(1), "id"))
                {
                    throw new InvalidOperationException($"{nameof(encryptionOptions.PathsToEncrypt)} includes a invalid path: '{path}'.");
                }
            }

            JObject itemJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(input);
            List<string> pathsEncrypted = new List<string>();
            EncryptionProperties encryptionProperties = null;
            byte[] plainText = null;
            byte[] cipherText = null;
            TypeMarker typeMarker;

            switch (encryptionOptions.EncryptionAlgorithm)
            {
                case CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized:

                    foreach (string pathToEncrypt in encryptionOptions.PathsToEncrypt)
                    {
                        string propertyName = pathToEncrypt.Substring(1);
                        if (!itemJObj.TryGetValue(propertyName, out JToken propertyValue))
                        {
                            continue;
                        }

                        if (propertyValue.Type == JTokenType.Null)
                        {
                            continue;
                        }

                        (typeMarker, plainText) = EncryptionProcessor.Serialize(propertyValue);

                        cipherText = await encryptor.EncryptAsync(
                            plainText,
                            encryptionOptions.DataEncryptionKeyId,
                            encryptionOptions.EncryptionAlgorithm);

                        if (cipherText == null)
                        {
                            throw new InvalidOperationException($"{nameof(Encryptor)} returned null cipherText from {nameof(EncryptAsync)}.");
                        }

                        byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
                        cipherTextWithTypeMarker[0] = (byte)typeMarker;
                        Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
                        itemJObj[propertyName] = cipherTextWithTypeMarker;
                        pathsEncrypted.Add(pathToEncrypt);
                    }

                    encryptionProperties = new EncryptionProperties(
                          encryptionFormatVersion: 3,
                          encryptionOptions.EncryptionAlgorithm,
                          encryptionOptions.DataEncryptionKeyId,
                          encryptedData: null,
                          pathsEncrypted);
                    break;

                case CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized:

                    JObject toEncryptJObj = new JObject();

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
                    break;

                default:
                    throw new NotSupportedException($"Encryption Algorithm : {encryptionOptions.EncryptionAlgorithm} is not supported.");
            }

            itemJObj.Add(Constants.EncryptedInfo, JObject.FromObject(encryptionProperties));
            input.Dispose();
            return EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        /// <remarks>
        /// If there isn't any data that needs to be decrypted, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public static async Task<(Stream, DecryptionContext)> DecryptAsync(
            Stream input,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return (input, null);
            }

            Debug.Assert(input.CanSeek);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj = EncryptionProcessor.RetrieveItem(input);
            JObject encryptionPropertiesJObj = EncryptionProcessor.RetrieveEncryptionProperties(itemJObj);

            if (encryptionPropertiesJObj == null)
            {
                input.Position = 0;
                return (input, null);
            }

            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();
            DecryptionContext decryptionContext;

            switch (encryptionProperties.EncryptionAlgorithm)
            {
                case CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized:
                    decryptionContext = await EncryptionProcessor.MdeEncAlgoDecryptObjectAsync(
                        itemJObj,
                        encryptor,
                        encryptionProperties,
                        diagnosticsContext,
                        cancellationToken);
                    break;

                case CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized:
                    decryptionContext = await EncryptionProcessor.LegacyEncAlgoDecryptContentAsync(
                        itemJObj,
                        encryptionProperties,
                        encryptor,
                        diagnosticsContext,
                        cancellationToken);
                    break;

                default:
                    throw new NotSupportedException($"Encryption Algorithm : {encryptionProperties.EncryptionAlgorithm} is not supported.");
            }

            input.Dispose();
            return (EncryptionProcessor.BaseSerializer.ToStream(itemJObj), decryptionContext);
        }

        public static async Task<(JObject, DecryptionContext)> DecryptAsync(
            JObject document,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);

            Debug.Assert(encryptor != null);

            JObject encryptionPropertiesJObj = EncryptionProcessor.RetrieveEncryptionProperties(document);

            if (encryptionPropertiesJObj == null)
            {
                return (document, null);
            }

            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();

            DecryptionContext decryptionContext;

            switch (encryptionProperties.EncryptionAlgorithm)
            {
                case CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized:
                    decryptionContext = await EncryptionProcessor.MdeEncAlgoDecryptObjectAsync(
                        document,
                        encryptor,
                        encryptionProperties,
                        diagnosticsContext,
                        cancellationToken);
                    break;

                case CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized:
                    decryptionContext = await EncryptionProcessor.LegacyEncAlgoDecryptContentAsync(
                        document,
                        encryptionProperties,
                        encryptor,
                        diagnosticsContext,
                        cancellationToken);
                    break;

                default:
                    throw new NotSupportedException($"Encryption Algorithm : {encryptionProperties.EncryptionAlgorithm} is not supported.");
            }

            return (document, decryptionContext);
        }

        private static async Task<DecryptionContext> MdeEncAlgoDecryptObjectAsync(
            JObject document,
            Encryptor encryptor,
            EncryptionProperties encryptionProperties,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            JObject plainTextJObj = new JObject();
            foreach (string path in encryptionProperties.EncryptedPaths)
            {
                string propertyName = path.Substring(1);
                if (!document.TryGetValue(propertyName, out JToken propertyValue))
                {
                    continue;
                }

                byte[] cipherTextWithTypeMarker = propertyValue.ToObject<byte[]>();

                if (cipherTextWithTypeMarker == null)
                {
                    continue;
                }

                byte[] cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
                Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);

                byte[] plainText = await EncryptionProcessor.MdeEncAlgoDecryptPropertyAsync(
                    encryptionProperties,
                    cipherText,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken);

                EncryptionProcessor.DeserializeAndAddProperty(
                    (TypeMarker)cipherTextWithTypeMarker[0],
                    plainText,
                    plainTextJObj,
                    propertyName);
            }

            List<string> pathsDecrypted = new List<string>();
            foreach (JProperty property in plainTextJObj.Properties())
            {
                document[property.Name] = property.Value;
                pathsDecrypted.Add("/" + property.Name);
            }

            DecryptionContext decryptionContext = EncryptionProcessor.CreateDecryptionContext(
                pathsDecrypted,
                encryptionProperties.DataEncryptionKeyId);

            document.Remove(Constants.EncryptedInfo);
            return decryptionContext;
        }

        private static DecryptionContext CreateDecryptionContext(
            List<string> pathsDecrypted,
            string dataEncryptionKeyId)
        {
            DecryptionInfo decryptionInfo = new DecryptionInfo(
                pathsDecrypted,
                dataEncryptionKeyId);

            DecryptionContext decryptionContext = new DecryptionContext(
                new List<DecryptionInfo>() { decryptionInfo });

            return decryptionContext;
        }

        private static async Task<byte[]> MdeEncAlgoDecryptPropertyAsync(
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

        private static async Task<DecryptionContext> LegacyEncAlgoDecryptContentAsync(
            JObject document,
            EncryptionProperties encryptionProperties,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (encryptionProperties.EncryptionFormatVersion != 2)
            {
                throw new NotSupportedException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            byte[] plainText = await encryptor.DecryptAsync(
                encryptionProperties.EncryptedData,
                encryptionProperties.DataEncryptionKeyId,
                encryptionProperties.EncryptionAlgorithm,
                cancellationToken);

            if (plainText == null)
            {
                throw new InvalidOperationException($"{nameof(Encryptor)} returned null plainText from {nameof(DecryptAsync)}.");
            }

            JObject plainTextJObj;
            using (MemoryStream memoryStream = new MemoryStream(plainText))
            using (StreamReader streamReader = new StreamReader(memoryStream))
            using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
            {
                plainTextJObj = JObject.Load(jsonTextReader);
            }

            List<string> pathsDecrypted = new List<string>();
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

        private static void ValidateInputForEncrypt(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions)
        {
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

        private static JObject RetrieveEncryptionProperties(
            JObject item)
        {
            JProperty encryptionPropertiesJProp = item.Property(Constants.EncryptedInfo);
            JObject encryptionPropertiesJObj = null;
            if (encryptionPropertiesJProp != null && encryptionPropertiesJProp.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
            {
                encryptionPropertiesJObj = (JObject)encryptionPropertiesJProp.Value;
            }

            return encryptionPropertiesJObj;
        }

        private static (TypeMarker, byte[]) Serialize(JToken propertyValue)
        {
            SqlSerializerFactory sqlSerializerFactory = new SqlSerializerFactory();

            // UTF-8 encoding.
            SqlVarCharSerializer sqlVarCharSerializer = new SqlVarCharSerializer(size: -1, codePageCharacterEncoding: 65001);

            switch (propertyValue.Type)
            {
                case JTokenType.Undefined:
                    Debug.Assert(false, "Undefined value cannot be in the JSON");
                    return (default, null);
                case JTokenType.Null:
                    Debug.Assert(false, "Null type should have been handled by caller");
                    return (TypeMarker.Null, null);
                case JTokenType.Boolean:
                    return (TypeMarker.Boolean, sqlSerializerFactory.GetDefaultSerializer<bool>().Serialize(propertyValue.ToObject<bool>()));
                case JTokenType.Float:
                    return (TypeMarker.Double, sqlSerializerFactory.GetDefaultSerializer<double>().Serialize(propertyValue.ToObject<double>()));
                case JTokenType.Integer:
                    return (TypeMarker.Long, sqlSerializerFactory.GetDefaultSerializer<long>().Serialize(propertyValue.ToObject<long>()));
                case JTokenType.String:
                    return (TypeMarker.String, sqlVarCharSerializer.Serialize(propertyValue.ToObject<string>()));
                case JTokenType.Array:
                    return (TypeMarker.Array, sqlVarCharSerializer.Serialize(propertyValue.ToString()));
                case JTokenType.Object:
                    return (TypeMarker.Object, sqlVarCharSerializer.Serialize(propertyValue.ToString()));
                default:
                    throw new InvalidOperationException($" Invalid or Unsupported Data Type Passed : {propertyValue.Type}");
            }
        }

        private static void DeserializeAndAddProperty(
            TypeMarker typeMarker,
            byte[] serializedBytes,
            JObject jObject,
            string key)
        {
            SqlSerializerFactory sqlSerializerFactory = new SqlSerializerFactory();

            // UTF-8 encoding.
            SqlVarCharSerializer sqlVarCharSerializer = new SqlVarCharSerializer(size: -1, codePageCharacterEncoding: 65001);

            switch (typeMarker)
            {
                case TypeMarker.Boolean:
                    jObject.Add(key, sqlSerializerFactory.GetDefaultSerializer<bool>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.Double:
                    jObject.Add(key, sqlSerializerFactory.GetDefaultSerializer<double>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.Long:
                    jObject.Add(key, sqlSerializerFactory.GetDefaultSerializer<long>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.String:
                    jObject.Add(key, sqlVarCharSerializer.Deserialize(serializedBytes));
                    break;
                case TypeMarker.Array:
                    jObject.Add(key, JsonConvert.DeserializeObject<JArray>(sqlVarCharSerializer.Deserialize(serializedBytes)));
                    break;
                case TypeMarker.Object:
                    jObject.Add(key, JsonConvert.DeserializeObject<JObject>(sqlVarCharSerializer.Deserialize(serializedBytes)));
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
            Double = 3,
            Long = 4,
            Boolean = 5,
            Array = 6,
            Object = 7,
        }
    }
}
