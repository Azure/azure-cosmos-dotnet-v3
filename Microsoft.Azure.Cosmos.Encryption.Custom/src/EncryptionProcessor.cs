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
        // UTF-8 encoding.
        private static readonly SqlVarCharSerializer SqlVarCharSerializer = new (size: -1, codePageCharacterEncoding: 65001);
        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();

        private static readonly JsonSerializerSettings JsonSerializerSettings = new ()
        {
            DateParseHandling = DateParseHandling.None,
        };

        internal static readonly CosmosJsonDotNetSerializer BaseSerializer = new (JsonSerializerSettings);

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
            _ = diagnosticsContext;

            EncryptionProcessor.ValidateInputForEncrypt(
                input,
                encryptor,
                encryptionOptions);

            if (!encryptionOptions.PathsToEncrypt.Any())
            {
                return input;
            }

            if (encryptionOptions.PathsToEncrypt.Distinct().Count() != encryptionOptions.PathsToEncrypt.Count())
            {
                throw new InvalidOperationException("Duplicate paths in PathsToEncrypt passed via EncryptionOptions.");
            }

            foreach (string path in encryptionOptions.PathsToEncrypt)
            {
                if (string.IsNullOrWhiteSpace(path) || path[0] != '/' || path.IndexOf('/', 1) != -1)
                {
                    throw new InvalidOperationException($"Invalid path {path ?? string.Empty}, {nameof(encryptionOptions.PathsToEncrypt)}");
                }

                if (path.AsSpan(1).Equals("id".AsSpan(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{nameof(encryptionOptions.PathsToEncrypt)} includes a invalid path: '{path}'.");
                }
            }

            JObject itemJObj = BaseSerializer.FromStream<JObject>(input);
            List<string> pathsEncrypted = new (encryptionOptions.PathsToEncrypt.Count());
            EncryptionProperties encryptionProperties = null;
            byte[] plainText = null;
            byte[] cipherText = null;
            TypeMarker typeMarker;

            using ArrayPoolManager arrayPoolManager = new ();

#pragma warning disable CS0618 // Type or member is obsolete
            switch (encryptionOptions.EncryptionAlgorithm)
            {
                case CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized:

                    DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionOptions.DataEncryptionKeyId, encryptionOptions.EncryptionAlgorithm, cancellationToken);

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

                        (typeMarker, plainText, int plainTextLength) = Serialize(propertyValue, arrayPoolManager);

                        if (plainText == null)
                        {
                            continue;
                        }

                        int cipherTextLength = encryptionKey.GetEncryptByteCount(plainText.Length);

                        byte[] cipherTextWithTypeMarker = arrayPoolManager.Rent(cipherTextLength + 1);

                        cipherTextWithTypeMarker[0] = (byte)typeMarker;

                        int encryptedBytesCount = encryptionKey.EncryptData(
                            plainText,
                            plainTextOffset: 0,
                            plainTextLength,
                            cipherTextWithTypeMarker,
                            outputOffset: 1);

                        if (encryptedBytesCount < 0)
                        {
                            throw new InvalidOperationException($"{nameof(Encryptor)} returned null cipherText from {nameof(EncryptAsync)}.");
                        }

                        itemJObj[propertyName] = cipherTextWithTypeMarker.AsSpan(0, encryptedBytesCount + 1).ToArray();
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

                    JObject toEncryptJObj = new ();

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

                    MemoryStream memoryStream = BaseSerializer.ToStream<JObject>(toEncryptJObj);
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
#pragma warning restore CS0618 // Type or member is obsolete

            itemJObj.Add(Constants.EncryptedInfo, JObject.FromObject(encryptionProperties));
            input.Dispose();
            return BaseSerializer.ToStream(itemJObj);
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

            JObject itemJObj = RetrieveItem(input);
            JObject encryptionPropertiesJObj = RetrieveEncryptionProperties(itemJObj);

            if (encryptionPropertiesJObj == null)
            {
                input.Position = 0;
                return (input, null);
            }

            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();
#pragma warning disable CS0618 // Type or member is obsolete
            DecryptionContext decryptionContext = encryptionProperties.EncryptionAlgorithm switch
            {
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized => await MdeEncAlgoDecryptObjectAsync(
                    itemJObj,
                    encryptor,
                    encryptionProperties,
                    diagnosticsContext,
                    cancellationToken),
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized => await LegacyEncAlgoDecryptContentAsync(
                    itemJObj,
                    encryptionProperties,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken),
                _ => throw new NotSupportedException($"Encryption Algorithm : {encryptionProperties.EncryptionAlgorithm} is not supported."),
            };
#pragma warning restore CS0618 // Type or member is obsolete

            input.Dispose();
            return (BaseSerializer.ToStream(itemJObj), decryptionContext);
        }

        public static async Task<(JObject, DecryptionContext)> DecryptAsync(
            JObject document,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);

            Debug.Assert(encryptor != null);

            JObject encryptionPropertiesJObj = RetrieveEncryptionProperties(document);

            if (encryptionPropertiesJObj == null)
            {
                return (document, null);
            }

            EncryptionProperties encryptionProperties = encryptionPropertiesJObj.ToObject<EncryptionProperties>();
#pragma warning disable CS0618 // Type or member is obsolete
            DecryptionContext decryptionContext = encryptionProperties.EncryptionAlgorithm switch
            {
                CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized => await MdeEncAlgoDecryptObjectAsync(
                    document,
                    encryptor,
                    encryptionProperties,
                    diagnosticsContext,
                    cancellationToken),
                CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized => await LegacyEncAlgoDecryptContentAsync(
                    document,
                    encryptionProperties,
                    encryptor,
                    diagnosticsContext,
                    cancellationToken),
                _ => throw new NotSupportedException($"Encryption Algorithm : {encryptionProperties.EncryptionAlgorithm} is not supported."),
            };
#pragma warning restore CS0618 // Type or member is obsolete

            return (document, decryptionContext);
        }

        private static async Task<DecryptionContext> MdeEncAlgoDecryptObjectAsync(
            JObject document,
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

            DataEncryptionKey encryptionKey = await encryptor.GetEncryptionKeyAsync(encryptionProperties.DataEncryptionKeyId, encryptionProperties.EncryptionAlgorithm, cancellationToken);

            List<string> pathsDecrypted = new (encryptionProperties.EncryptedPaths.Count());
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

                int plainTextLength = encryptionKey.GetDecryptByteCount(cipherTextWithTypeMarker.Length - 1);

                byte[] plainText = arrayPoolManager.Rent(plainTextLength);

                int decryptedCount = MdeEncAlgoDecryptProperty(
                    encryptionKey,
                    cipherTextWithTypeMarker,
                    cipherTextOffset: 1,
                    cipherTextWithTypeMarker.Length - 1,
                    plainText);

                DeserializeAndAddProperty(
                    (TypeMarker)cipherTextWithTypeMarker[0],
                    plainText.AsSpan(0, decryptedCount),
                    document,
                    propertyName);

                pathsDecrypted.Add(path);
            }

            DecryptionContext decryptionContext = CreateDecryptionContext(
                pathsDecrypted,
                encryptionProperties.DataEncryptionKeyId);

            document.Remove(Constants.EncryptedInfo);
            return decryptionContext;
        }

        private static DecryptionContext CreateDecryptionContext(
            List<string> pathsDecrypted,
            string dataEncryptionKeyId)
        {
            DecryptionInfo decryptionInfo = new (
                pathsDecrypted,
                dataEncryptionKeyId);

            DecryptionContext decryptionContext = new (
                new List<DecryptionInfo>() { decryptionInfo });

            return decryptionContext;
        }

        private static int MdeEncAlgoDecryptProperty(
            DataEncryptionKey encryptionKey,
            byte[] cipherText,
            int cipherTextOffset,
            int cipherTextLength,
            byte[] buffer)
        {
            int decryptedCount = encryptionKey.DecryptData(
                cipherText,
                cipherTextOffset,
                cipherTextLength,
                buffer,
                outputOffset: 0);

            if (decryptedCount < 0)
            {
                throw new InvalidOperationException($"{nameof(Encryptor)} returned null plainText from {nameof(DecryptAsync)}.");
            }

            return decryptedCount;
        }

        private static async Task<DecryptionContext> LegacyEncAlgoDecryptContentAsync(
            JObject document,
            EncryptionProperties encryptionProperties,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            _ = diagnosticsContext;

            if (encryptionProperties.EncryptionFormatVersion != 2)
            {
                throw new NotSupportedException($"Unknown encryption format version: {encryptionProperties.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            byte[] plainText = await encryptor.DecryptAsync(
                encryptionProperties.EncryptedData,
                encryptionProperties.DataEncryptionKeyId,
                encryptionProperties.EncryptionAlgorithm,
                cancellationToken) ?? throw new InvalidOperationException($"{nameof(Encryptor)} returned null plainText from {nameof(DecryptAsync)}.");
            JObject plainTextJObj;
            using (MemoryStream memoryStream = new (plainText))
            using (StreamReader streamReader = new (memoryStream))
            using (JsonTextReader jsonTextReader = new (streamReader))
            {
                jsonTextReader.ArrayPool = JsonArrayPool.Instance;
                plainTextJObj = JObject.Load(jsonTextReader);
            }

            List<string> pathsDecrypted = new ();
            foreach (JProperty property in plainTextJObj.Properties())
            {
                document.Add(property.Name, property.Value);
                pathsDecrypted.Add("/" + property.Name);
            }

            DecryptionContext decryptionContext = CreateDecryptionContext(
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
            using (StreamReader sr = new (input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            using (JsonTextReader jsonTextReader = new (sr))
            {
                jsonTextReader.ArrayPool = JsonArrayPool.Instance;
                JsonSerializerSettings jsonSerializerSettings = new ()
                {
                    DateParseHandling = DateParseHandling.None,
                    MaxDepth = 64, // https://github.com/advisories/GHSA-5crp-9r3c-p9vr
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
            if (encryptionPropertiesJProp?.Value != null && encryptionPropertiesJProp.Value.Type == JTokenType.Object)
            {
                encryptionPropertiesJObj = (JObject)encryptionPropertiesJProp.Value;
            }

            return encryptionPropertiesJObj;
        }

        private static (TypeMarker typeMarker, byte[] serializedBytes, int serializedBytesCount) Serialize(JToken propertyValue, ArrayPoolManager arrayPoolManager)
        {
            byte[] buffer;
            int length;
            switch (propertyValue.Type)
            {
                case JTokenType.Undefined:
                    Debug.Assert(false, "Undefined value cannot be in the JSON");
                    return (default, null, -1);
                case JTokenType.Null:
                    Debug.Assert(false, "Null type should have been handled by caller");
                    return (TypeMarker.Null, null, -1);
                case JTokenType.Boolean:
                    (buffer, length) = SerializeFixed(SqlBoolSerializer);
                    return (TypeMarker.Boolean, buffer, length);
                case JTokenType.Float:
                    (buffer, length) = SerializeFixed(SqlDoubleSerializer);
                    return (TypeMarker.Double, buffer, length);
                case JTokenType.Integer:
                    (buffer, length) = SerializeFixed(SqlLongSerializer);
                    return (TypeMarker.Long, buffer, length);
                case JTokenType.String:
                    (buffer, length) = SerializeString(propertyValue.ToObject<string>());
                    return (TypeMarker.String, buffer, length);
                case JTokenType.Array:
                    (buffer, length) = SerializeString(propertyValue.ToString());
                    return (TypeMarker.Array, buffer, length);
                case JTokenType.Object:
                    (buffer, length) = SerializeString(propertyValue.ToString());
                    return (TypeMarker.Object, buffer, length);
                default:
                    throw new InvalidOperationException($" Invalid or Unsupported Data Type Passed : {propertyValue.Type}");
            }

            (byte[], int) SerializeFixed<T>(IFixedSizeSerializer<T> serializer)
            {
                byte[] buffer = arrayPoolManager.Rent(serializer.GetSerializedMaxByteCount());
                int length = serializer.Serialize(propertyValue.ToObject<T>(), buffer);
                return (buffer, length);
            }

            (byte[], int) SerializeString(string value)
            {
                byte[] buffer = arrayPoolManager.Rent(SqlVarCharSerializer.GetSerializedMaxByteCount(value.Length));
                int length = SqlVarCharSerializer.Serialize(value, buffer);
                return (buffer, length);
            }
        }

        private static void DeserializeAndAddProperty(
            TypeMarker typeMarker,
            ReadOnlySpan<byte> serializedBytes,
            JObject jObject,
            string key)
        {
            switch (typeMarker)
            {
                case TypeMarker.Boolean:
                    jObject[key] = SqlBoolSerializer.Deserialize(serializedBytes);
                    break;
                case TypeMarker.Double:
                    jObject[key] = SqlDoubleSerializer.Deserialize(serializedBytes);
                    break;
                case TypeMarker.Long:
                    jObject[key] = SqlLongSerializer.Deserialize(serializedBytes);
                    break;
                case TypeMarker.String:
                    jObject[key] = SqlVarCharSerializer.Deserialize(serializedBytes);
                    break;
                case TypeMarker.Array:
                    DeserializeAndAddProperty<JArray>(serializedBytes);
                    break;
                case TypeMarker.Object:
                    DeserializeAndAddProperty<JObject>(serializedBytes);
                    break;
                default:
                    Debug.Fail($"Unexpected type marker {typeMarker}");
                    break;
            }

            void DeserializeAndAddProperty<T>(ReadOnlySpan<byte> serializedBytes)
                where T : JToken
            {
                using ArrayPoolManager<char> manager = new ();

                char[] buffer = manager.Rent(SqlVarCharSerializer.GetDeserializedMaxLength(serializedBytes.Length));
                int length = SqlVarCharSerializer.Deserialize(serializedBytes, buffer.AsSpan());

                JsonSerializer serializer = JsonSerializer.Create(JsonSerializerSettings);

                using MemoryTextReader memoryTextReader = new (new Memory<char>(buffer, 0, length));
                using JsonTextReader reader = new (memoryTextReader);

                jObject[key] = serializer.Deserialize<T>(reader);
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

        internal static async Task<Stream> DeserializeAndDecryptResponseAsync(
            Stream content,
            Encryptor encryptor,
            CancellationToken cancellationToken)
        {
            JObject contentJObj = BaseSerializer.FromStream<JObject>(content);

            if (contentJObj.SelectToken(Constants.DocumentsResourcePropertyName) is not JArray documents)
            {
                throw new InvalidOperationException("Feed Response body contract was violated. Feed response did not have an array of Documents");
            }

            foreach (JToken value in documents)
            {
                if (value is not JObject document)
                {
                    continue;
                }

                CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(null);
                using (diagnosticsContext.CreateScope("EncryptionProcessor.DeserializeAndDecryptResponseAsync"))
                {
                    await DecryptAsync(
                        document,
                        encryptor,
                        diagnosticsContext,
                        cancellationToken);
                }
            }

            // the contents of contentJObj get decrypted in place for MDE algorithm model, and for legacy model _ei property is removed
            // and corresponding decrypted properties are added back in the documents.
            return BaseSerializer.ToStream(contentJObj);
        }
    }
}