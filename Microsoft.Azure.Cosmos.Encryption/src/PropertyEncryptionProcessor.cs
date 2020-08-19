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
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class PropertyEncryptionProcessor
    {
        internal static readonly CosmosJsonDotNetSerializer BaseSerializer = new CosmosJsonDotNetSerializer();

        /// <remarks>
        /// If there isn't any PathsToEncrypt, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public static async Task<Stream> EncryptAsync(
            Stream input,
            Encryptor encryptor,
            List<EncryptionOptions> propertyEncryptionOptions,
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

            JObject itemJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(input);

            foreach (EncryptionOptions encryptionOptions in propertyEncryptionOptions)
            {
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
                }

                if (itemJObj != null)
                {
                    foreach (string propertiesToEncrypt in encryptionOptions.PathsToEncrypt)
                    {
                        string propertyName = propertiesToEncrypt.Substring(1);
                        if (!itemJObj.TryGetValue(propertyName, out JToken propertyValue))
                        {
                            throw new ArgumentException($"{nameof(encryptionOptions.PathsToEncrypt)} includes a path: '{propertiesToEncrypt}' which was not found.");
                        }

                        string value = null;
                        byte[] plainText;

                        if (encryptionOptions.Serializer != null)
                        {
                            plainText = encryptionOptions.Serializer.Serialize(propertyValue.ToObject(encryptionOptions.PropertyDataType));
                        }
                        else
                        {
                            value = propertyValue.ToObject<string>();
                            Debug.Assert(value != null);
                            plainText = Encoding.UTF8.GetBytes(value);
                        }

                        byte[] cipherText = await encryptor.EncryptAsync(
                            plainText,
                            encryptionOptions.DataEncryptionKeyId,
                            encryptionOptions.EncryptionAlgorithm,
                            cancellationToken);

                        if (cipherText == null)
                        {
                            throw new InvalidOperationException($"{nameof(Encryptor)} returned null cipherText from {nameof(EncryptAsync)}.");
                        }

                        itemJObj[propertyName] = cipherText;
                    }
                }
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
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            ClientEncryptionPolicy clientEncryptionPolicy,
            CancellationToken cancellationToken)
        {
            Debug.Assert(input != null);
            Debug.Assert(input.CanSeek);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj;
            using (StreamReader sr = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true))
            {
                using JsonTextReader jsonTextReader = new JsonTextReader(sr);
                itemJObj = JsonSerializer.Create().Deserialize<JObject>(jsonTextReader);
            }

            if (clientEncryptionPolicy.PropertyEncryptionSetting != null)
            {
                foreach (List<string> paths in clientEncryptionPolicy.PropertyEncryptionSetting.Keys)
                {
                    foreach (string path in paths)
                    {
                        if (itemJObj.TryGetValue(path.Substring(1), out JToken propertyValue))
                        {
                            JObject propPlainTextJObj = await PropertyEncryptionProcessor.DecryptContentAsync(
                            clientEncryptionPolicy.PropertyEncryptionSetting[paths],
                            path,
                            propertyValue.ToObject<byte[]>(),
                            encryptor,
                            diagnosticsContext,
                            cancellationToken);

                            foreach (JProperty property in propPlainTextJObj.Properties())
                            {
                                itemJObj[property.Name] = property.Value;
                            }
                        }
                    }

                    input.Dispose();
                }
            }

            return EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        public static async Task<JObject> DecryptAsync(
            JObject document,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            ClientEncryptionPolicy clientEncryptionPolicy,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);
            Debug.Assert(encryptor != null);
            Debug.Assert(diagnosticsContext != null);

            foreach (List<string> paths in clientEncryptionPolicy.PropertyEncryptionSetting.Keys)
            {
                foreach (string path in paths)
                {
                    if (document.TryGetValue(path.Substring(1), out JToken propertyValue))
                    {
                        JObject propPlainTextJObj = await PropertyEncryptionProcessor.DecryptContentAsync(
                        clientEncryptionPolicy.PropertyEncryptionSetting[paths],
                        path,
                        propertyValue.ToObject<byte[]>(),
                        encryptor,
                        diagnosticsContext,
                        cancellationToken);
                        foreach (JProperty property in propPlainTextJObj.Properties())
                        {
                            document[property.Name] = property.Value;
                        }
                    }
                }
            }

            return document;
        }

        private static async Task<JObject> DecryptContentAsync(
            EncryptionSettings encryptionSettings,
            string propertyName,
            byte[] encryptedData,
            Encryptor encryptor,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (encryptionSettings.EncryptionFormatVersion != 2)
            {
                throw new NotSupportedException($"Unknown encryption format version: {encryptionSettings.EncryptionFormatVersion}. Please upgrade your SDK to the latest version.");
            }

            byte[] plainText = await encryptor.DecryptAsync(
                encryptedData,
                encryptionSettings.DataEncryptionKeyId,
                encryptionSettings.EncryptionAlgorithm,
                cancellationToken);

            if (plainText == null)
            {
                throw new InvalidOperationException($"{nameof(Encryptor)} returned null plainText from {nameof(DecryptAsync)}.");
            }

            string val = null;
            ISerializer serializer = encryptionSettings.GetSerializer();
            if (serializer != null)
            {
                val = serializer.Deserialize(plainText).ToString();
            }
            else
            {
                val = Encoding.UTF8.GetString(plainText);
            }

            JObject plainTextJObj = new JObject();

            string key = propertyName.Substring(1);
            plainTextJObj.Add(key, val);
            return plainTextJObj;
        }
    }
}