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
    using Microsoft.Data.AAP_PH.Cryptography.Serializers;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Allows encrypting items in a container using AAP .
    /// </summary>
    internal sealed class AapEncryptionProcessor
    {
        internal static readonly CosmosJsonDotNetSerializer BaseSerializer =
            new CosmosJsonDotNetSerializer(
                new JsonSerializerSettings()
                {
                    DateParseHandling = DateParseHandling.None,
                });

        public static async Task<MemoryStream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            EncryptionOptions encryptionOptions = null,
            CancellationToken cancellationToken = default)
        {
            MemoryStream outputStream = new MemoryStream();
            EncryptionProperties encryptionProperties = null;
            JObject itemJObj = null;
            Exception rethrow_ex = new Exception();
            bool encryption_failed = false;

            if (encryptionOptions != null)
            {
                encryptionProperties = new EncryptionProperties(
                   encryptionFormatVersion: 3,
                   encryptionOptions.EncryptionAlgorithm,
                   encryptionOptions.DataEncryptionKeyId,
                   encryptedData: null,
                   encryptionOptions.PathsToEncrypt);

                itemJObj = AapEncryptionProcessor.BaseSerializer.FromStream<JObject>(input);

                foreach (string pathToEncrypt in encryptionOptions.PathsToEncrypt)
                {
                    if (string.IsNullOrWhiteSpace(pathToEncrypt) || pathToEncrypt[0] != '/' || pathToEncrypt.LastIndexOf('/') != 0)
                    {
                        throw new ArgumentException($"Invalid path {pathToEncrypt ?? string.Empty}", nameof(encryptionOptions.PathsToEncrypt));
                    }

                    string propertyName = pathToEncrypt.Substring(1);
                    if (!itemJObj.TryGetValue(propertyName, out JToken propertyValue))
                    {
                        throw new ArgumentException($"{nameof(encryptionOptions.PathsToEncrypt)} includes a path: '{pathToEncrypt}' which was not found.");
                    }

                    if (string.Equals(propertyName, "id"))
                    {
                        if (itemJObj.TryGetValue("id", out JToken idpropertyToken))
                        {
                            throw new ArgumentException($"{nameof(encryptionOptions.PathsToEncrypt)} includes a invalid path: '{pathToEncrypt}'.");
                        }
                    }
                }

                input = AapEncryptionProcessor.BaseSerializer.ToStream(itemJObj);
            }

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

                foreach (JsonProperty property in root.EnumerateObject())
                {
                    // nulls are not encrypted
                    if (property.Value.ValueKind != JsonValueKind.Null &
                        encryptionProperties != null)
                    {
                        if (encryptionProperties.EncryptedPaths != null &&
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
                itemJObj = AapEncryptionProcessor.BaseSerializer.FromStream<JObject>(outputStream);
                itemJObj.Add(Constants.EncryptedInfo, JObject.FromObject(encryptionProperties));
                outputStream = AapEncryptionProcessor.BaseSerializer.ToStream(itemJObj);
            }

            if (encryption_failed)
            {
                throw rethrow_ex;
            }

            return await Task.FromResult(outputStream);
        }

        public static async Task DecryptAndWriteAsync(
            JsonElement document,
            Encryptor encryptor,
            Utf8JsonWriter outputWriter,
            EncryptionProperties encryptionProperties = null,
            CancellationToken cancellationToken = default)
        {
            bool failed_decrypt = false;
            Exception rethrow_ex = null;

            if (document.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("Invalid document to decrypt", nameof(document));
            }
            else
            {
                outputWriter.WriteStartObject();
            }

            if (encryptionProperties != null && encryptionProperties.EncryptedPaths != null &&
                    encryptionProperties.EncryptedPaths.Any())
            {
                foreach (string path in encryptionProperties.EncryptedPaths)
                {
                    if (string.IsNullOrWhiteSpace(path) || path[0] != '/' || path.LastIndexOf('/') != 0)
                    {
                        throw new ArgumentException($"Invalid path {path ?? string.Empty}", nameof(encryptionProperties.EncryptedPaths));
                    }
                }
            }

            foreach (JsonProperty property in document.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Null &&
                    encryptionProperties != null && !failed_decrypt)
                {
                    if (encryptionProperties.EncryptedPaths != null &&
                        encryptionProperties.EncryptedPaths.Any() &&
                        encryptionProperties.EncryptedPaths.Contains('/' + property.Name))
                    {
                        try
                        {
                            byte[] cipherTextWithTypeMarker = property.Value.GetBytesFromBase64();
                            byte[] cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
                            Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);

                            byte[] plainText = await encryptor.DecryptAsync(
                                         cipherText,
                                         encryptionProperties.DataEncryptionKeyId,
                                         encryptionProperties.EncryptionAlgorithm);

                            outputWriter.WritePropertyName(property.Name);
                            DeserializeAndWritePropertyValue(
                                (TypeMarker)cipherTextWithTypeMarker[0],
                                plainText,
                                outputWriter);
                        }
                        catch (Exception ex)
                        {
                            rethrow_ex = ex;
                            property.WriteTo(outputWriter);
                            failed_decrypt = true;
                        }
                    }
                    else
                    {
                        property.WriteTo(outputWriter);
                    }
                }
                else
                {
                    property.WriteTo(outputWriter);
                }
            }

            outputWriter.WriteEndObject();

            // if document failed to decrypt any of the paths,pass Document to failure Handler
            if (failed_decrypt)
            {
                throw rethrow_ex;
            }
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
                default: // Object / Array
                    return (TypeMarker.RawText, SerializerDefaultMappings.GetDefaultSerializer<string>().Serialize(element.GetRawText()));
            }
        }

        private static void DeserializeAndWritePropertyValue(
            TypeMarker typeMarker,
            byte[] serializedBytes,
            Utf8JsonWriter writer)
        {
            switch (typeMarker)
            {
                case TypeMarker.Boolean:
                    writer.WriteBooleanValue(SerializerDefaultMappings.GetDefaultSerializer<bool>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.Number:
                    writer.WriteNumberValue(SerializerDefaultMappings.GetDefaultSerializer<double>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.String:
                    writer.WriteStringValue(SerializerDefaultMappings.GetDefaultSerializer<string>().Deserialize(serializedBytes));
                    break;
                case TypeMarker.RawText:
                    JsonDocument.Parse(SerializerDefaultMappings.GetDefaultSerializer<string>().Deserialize(serializedBytes)).WriteTo(writer);
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
            RawText = 5,
        }
    }
}
