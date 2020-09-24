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
    using System.Text.Json;
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
            CancellationToken cancellationToken = default)
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

            if (encryptionOptions.PathsToEncrypt == null)
            {
                throw new ArgumentNullException(nameof(encryptionOptions.PathsToEncrypt));
            }

            if (string.IsNullOrWhiteSpace(encryptionOptions.DataEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(encryptionOptions.DataEncryptionKeyId));
            }

            if (string.IsNullOrWhiteSpace(encryptionOptions.EncryptionAlgorithm))
            {
                throw new ArgumentNullException(nameof(encryptionOptions.EncryptionAlgorithm));
            }

            if (!encryptionOptions.PathsToEncrypt.Any())
            {
                return input;
            }

            MemoryStream outputStream = new MemoryStream();
            Exception rethrow_ex = new Exception();
            bool encryption_failed = false;

            EncryptionProperties encryptionProperties = new EncryptionProperties(
                   encryptionFormatVersion: 3,
                   encryptionOptions.EncryptionAlgorithm,
                   encryptionOptions.DataEncryptionKeyId,
                   encryptedData: null,
                   encryptionOptions.PathsToEncrypt);

            using (Utf8JsonWriter writer = new Utf8JsonWriter(outputStream))
            using (JsonDocument document = JsonDocument.Parse(input))
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Invalid document to encrypt", nameof(input));
                }
                else
                {
                    writer.WriteStartObject();
                }

                foreach (string pathToEncrypt in encryptionOptions.PathsToEncrypt)
                {
                    if (string.IsNullOrWhiteSpace(pathToEncrypt) || pathToEncrypt[0] != '/' || pathToEncrypt.LastIndexOf('/') != 0)
                    {
                        throw new ArgumentException($"Invalid path {pathToEncrypt ?? string.Empty}", nameof(encryptionOptions.PathsToEncrypt));
                    }

                    if (!root.TryGetProperty(pathToEncrypt.Substring(1), out JsonElement propertyValue))
                    {
                        throw new ArgumentException($"{nameof(encryptionOptions.PathsToEncrypt)} includes a path: '{pathToEncrypt}' which was not found.");
                    }
                    else
                    {
                        if (string.Equals(pathToEncrypt.Substring(1), "id"))
                        {
                            throw new ArgumentException($"{nameof(encryptionOptions.PathsToEncrypt)} includes a invalid path: '{pathToEncrypt}'.");
                        }
                    }
                }

                foreach (JsonProperty property in root.EnumerateObject())
                {
                    // nulls are not encrypted
                    if (property.Value.ValueKind != JsonValueKind.Null &&
                        encryptionProperties.EncryptedPaths.Any() &&
                        encryptionProperties.EncryptedPaths.Contains('/' + property.Name))
                    {
                        try
                        {
                            (TypeMarker typeMarker, byte[] plainText) = Serialize(property.Value);

                            byte[] cipherText = await encryptor.EncryptAsync(
                                plainText,
                                encryptionOptions.DataEncryptionKeyId,
                                encryptionOptions.EncryptionAlgorithm);

                            byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
                            cipherTextWithTypeMarker[0] = (byte)typeMarker;
                            Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
                            writer.WriteBase64String(property.Name, cipherTextWithTypeMarker);
                        }
                        catch (Exception ex)
                        {
                            property.WriteTo(writer);
                            encryption_failed = true;
                            rethrow_ex = ex;
                        }
                    }
                    else
                    {
                        property.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
                writer.Flush();
            }

            if (encryptionOptions != null)
            {
                // save the Item Level Policy
                outputStream.Seek(0, SeekOrigin.Begin);
                JObject itemJObj = AapEncryptionProcessor.BaseSerializer.FromStream<JObject>(outputStream);
                itemJObj.Add(Constants.EncryptedInfo, JObject.FromObject(encryptionProperties));
                outputStream = AapEncryptionProcessor.BaseSerializer.ToStream(itemJObj);
            }

            if (encryption_failed)
            {
                throw rethrow_ex;
            }

            return await Task.FromResult(outputStream);
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
                itemJObj = Newtonsoft.Json.JsonSerializer.Create().Deserialize<JObject>(jsonTextReader);
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

                    byte[]plainText = await AapEncryptionProcessor.DecryptContentAsync(
                        encryptionProperties,
                        cipherText,
                        encryptor,
                        diagnosticsContext,
                        cancellationToken);

                    string key = path.Substring(1);
                    DeserializeAndAddProperty(
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
            return AapEncryptionProcessor.BaseSerializer.ToStream(itemJObj);
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

                        DeserializeAndAddProperty(
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

        private static (TypeMarker, byte[]) Serialize(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return (TypeMarker.Boolean, SerializerDefaultMappings.GetDefaultSerializer<bool>().Serialize(element.GetBoolean()));
                case JsonValueKind.Undefined:
                    Debug.Assert(false, "Undefined value cannot be in the JSON");
                    return (default(TypeMarker), null);
                case JsonValueKind.Null:
                    Debug.Assert(false, "Null type should have been handled by caller");
                    return (TypeMarker.Null, null);
                case JsonValueKind.Number:
                    // todo: int64 vs double separation?
                    return (TypeMarker.Number, SerializerDefaultMappings.GetDefaultSerializer<double>().Serialize(element.GetDouble()));
                case JsonValueKind.String:
                    return (TypeMarker.String, SerializerDefaultMappings.GetDefaultSerializer<string>().Serialize(element.GetString()));
                case JsonValueKind.Array:
                    return (TypeMarker.Array, SerializerDefaultMappings.GetDefaultSerializer<string>().Serialize(element.GetRawText()));
                default: // Object / Array
                    return (TypeMarker.RawText, SerializerDefaultMappings.GetDefaultSerializer<string>().Serialize(element.GetRawText()));
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
                case TypeMarker.Number:
                    jObject.Add(key, SerializerDefaultMappings.GetDefaultSerializer<double>().Deserialize(serializedBytes));
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
            Number = 3,
            Boolean = 4,
            Array = 5,
            RawText = 6,
        }
    }
}
