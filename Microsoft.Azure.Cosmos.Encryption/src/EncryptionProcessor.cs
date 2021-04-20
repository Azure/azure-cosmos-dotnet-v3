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

    internal sealed class EncryptionProcessor
    {
        private bool isEncryptionSettingsInitDone;

        private static readonly SemaphoreSlim CacheInitSema = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Gets the container that has items which are to be encrypted.
        /// </summary>
        public Container Container { get; }

        /// <summary>
        /// Gets the provider that allows interaction with the master keys.
        /// </summary>
        public EncryptionKeyStoreProvider EncryptionKeyStoreProvider => this.EncryptionCosmosClient.EncryptionKeyStoreProvider;

        public ClientEncryptionPolicy ClientEncryptionPolicy { get; private set; }

        public EncryptionCosmosClient EncryptionCosmosClient { get; }

        internal static readonly CosmosJsonDotNetSerializer BaseSerializer = new CosmosJsonDotNetSerializer(
            new JsonSerializerSettings()
            {
                DateParseHandling = DateParseHandling.None,
            });

        internal EncryptionSettings EncryptionSettings { get; }

        public EncryptionProcessor(
            Container container,
            EncryptionCosmosClient encryptionCosmosClient)
        {
            this.Container = container ?? throw new ArgumentNullException(nameof(container));
            this.EncryptionCosmosClient = encryptionCosmosClient ?? throw new ArgumentNullException(nameof(encryptionCosmosClient));
            this.isEncryptionSettingsInitDone = false;
            this.EncryptionSettings = new EncryptionSettings(this);
        }

        /// <summary>
        /// Builds up and caches the Encryption Setting by getting the cached entries of Client Encryption Policy and the corresponding keys.
        /// Sets up the MDE Algorithm for encryption and decryption by initializing the KeyEncryptionKey and ProtectedDataEncryptionKey.
        /// </summary>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns> Task </returns>
        internal async Task InitializeEncryptionSettingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (this.isEncryptionSettingsInitDone)
            {
                throw new InvalidOperationException("The Encrypton Processor has already been initialized. ");
            }

            // fetch the cached policy.
            this.ClientEncryptionPolicy = await this.EncryptionCosmosClient.GetClientEncryptionPolicyAsync(
                container: this.Container,
                cancellationToken: cancellationToken,
                shouldForceRefresh: false);

            // no policy was configured.
            if (this.ClientEncryptionPolicy == null)
            {
                this.isEncryptionSettingsInitDone = true;
                return;
            }

            // update the property level setting.
            foreach (ClientEncryptionIncludedPath propertyToEncrypt in this.ClientEncryptionPolicy.IncludedPaths)
            {
                EncryptionType encryptionType = this.EncryptionSettings.GetEncryptionTypeForProperty(propertyToEncrypt);

                EncryptionSettingForProperty encryptionSettingsForProperty = new EncryptionSettingForProperty(
                    propertyToEncrypt.ClientEncryptionKeyId,
                    encryptionType,
                    this);

                string propertyName = propertyToEncrypt.Path.Substring(1);

                this.EncryptionSettings.SetEncryptionSettingForProperty(
                    propertyName,
                    encryptionSettingsForProperty);
            }

            this.isEncryptionSettingsInitDone = true;
        }

        /// <summary>
        /// Initializes the Encryption Setting for the processor if not initialized or if shouldForceRefresh is true.
        /// </summary>
        /// <param name="cancellationToken">(Optional) Token to cancel the operation.</param>
        /// <returns>Task to await.</returns>
        internal async Task InitEncryptionSettingsIfNotInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (this.isEncryptionSettingsInitDone)
            {
                return;
            }

            if (await CacheInitSema.WaitAsync(-1))
            {
                if (!this.isEncryptionSettingsInitDone)
                {
                    try
                    {
                        await this.InitializeEncryptionSettingsAsync(cancellationToken);
                    }
                    finally
                    {
                        CacheInitSema.Release(1);
                    }
                }
                else
                {
                    CacheInitSema.Release(1);
                }
            }
        }

        private void EncryptProperty(
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
                        this.EncryptProperty(
                            itemJObj,
                            jProperty.Value,
                            aeadAes256CbcHmac256EncryptionAlgorithm);
                    }
                    else
                    {
                        jProperty.Value = this.SerializeAndEncryptValue(jProperty.Value, aeadAes256CbcHmac256EncryptionAlgorithm);
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
                                    this.EncryptProperty(
                                        itemJObj,
                                        jProperty.Value,
                                        aeadAes256CbcHmac256EncryptionAlgorithm);
                                }

                                // primitive type
                                else
                                {
                                    jProperty.Value = this.SerializeAndEncryptValue(jProperty.Value, aeadAes256CbcHmac256EncryptionAlgorithm);
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
                                    this.EncryptProperty(
                                        itemJObj,
                                        jArray[i],
                                        aeadAes256CbcHmac256EncryptionAlgorithm);
                                }

                                // primitive type
                                else
                                {
                                    jArray[i] = this.SerializeAndEncryptValue(jArray[i], aeadAes256CbcHmac256EncryptionAlgorithm);
                                }
                            }
                        }
                    }

                    // array of primitive types.
                    else
                    {
                        for (int i = 0; i < propertyValue.Count(); i++)
                        {
                            propertyValue[i] = this.SerializeAndEncryptValue(propertyValue[i], aeadAes256CbcHmac256EncryptionAlgorithm);
                        }
                    }
                }
            }
            else
            {
                itemJObj.Property(propertyValue.Path).Value = this.SerializeAndEncryptValue(
                    itemJObj.Property(propertyValue.Path).Value,
                    aeadAes256CbcHmac256EncryptionAlgorithm);
            }
        }

        private JToken SerializeAndEncryptValue(
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
                throw new InvalidOperationException($"{nameof(this.SerializeAndEncryptValue)} returned null cipherText from {nameof(aeadAes256CbcHmac256EncryptionAlgorithm.Encrypt)}. ");
            }

            byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
            cipherTextWithTypeMarker[0] = (byte)typeMarker;
            Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
            return cipherTextWithTypeMarker;
        }

        /// <remarks>
        /// If there isn't any PathsToEncrypt, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public async Task<Stream> EncryptAsync(
            Stream input,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            Debug.Assert(diagnosticsContext != null);

            await this.InitEncryptionSettingsIfNotInitializedAsync(cancellationToken);

            if (this.ClientEncryptionPolicy == null)
            {
                return input;
            }

            JObject itemJObj = EncryptionProcessor.BaseSerializer.FromStream<JObject>(input);

            foreach (ClientEncryptionIncludedPath pathToEncrypt in this.ClientEncryptionPolicy.IncludedPaths)
            {
                string propertyName = pathToEncrypt.Path.Substring(1);

                // possibly a wrong path configured in the Client Encryption Policy, ignore.
                if (!itemJObj.TryGetValue(propertyName, out JToken propertyValue))
                {
                    continue;
                }

                EncryptionSettingForProperty settingforProperty = await this.EncryptionSettings.GetEncryptionSettingForPropertyAsync(propertyName,cancellationToken);

                if (settingforProperty == null)
                {
                    throw new ArgumentException($"Invalid Encryption Setting for the Property:{propertyName}. ");
                }

                AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = await settingforProperty.BuildEncryptionAlgorithmForSettingAsync(cancellationToken: cancellationToken);

                this.EncryptProperty(
                    itemJObj,
                    propertyValue,
                    aeadAes256CbcHmac256EncryptionAlgorithm);
            }

            input.Dispose();
            return EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        private JToken DecryptAndDeserializeValue(
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
                throw new InvalidOperationException($"{nameof(this.DecryptAndDeserializeValue)} returned null plainText from {nameof(aeadAes256CbcHmac256EncryptionAlgorithm.Decrypt)}. ");
            }

            return DeserializeAndAddProperty(
                plainText,
                (TypeMarker)cipherTextWithTypeMarker[0]);
        }

        private void DecryptProperty(
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
                        this.DecryptProperty(
                            itemJObj,
                            aeadAes256CbcHmac256EncryptionAlgorithm,
                            jProperty.Name,
                            jProperty.Value);
                    }
                    else
                    {
                        jProperty.Value = this.DecryptAndDeserializeValue(
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
                                    this.DecryptProperty(
                                        itemJObj,
                                        aeadAes256CbcHmac256EncryptionAlgorithm,
                                        jProperty.Name,
                                        jProperty.Value);
                                }
                                else
                                {
                                    jProperty.Value = this.DecryptAndDeserializeValue(
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
                                    this.DecryptProperty(
                                        itemJObj,
                                        aeadAes256CbcHmac256EncryptionAlgorithm,
                                        jArray[i].Path,
                                        jArray[i]);
                                }
                                else
                                {
                                    jArray[i] = this.DecryptAndDeserializeValue(
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
                            propertyValue[i] = this.DecryptAndDeserializeValue(
                                propertyValue[i],
                                aeadAes256CbcHmac256EncryptionAlgorithm);
                        }
                    }
                }
            }
            else
            {
                itemJObj.Property(propertyName).Value = this.DecryptAndDeserializeValue(
                    itemJObj.Property(propertyName).Value,
                    aeadAes256CbcHmac256EncryptionAlgorithm);
            }
        }

        private async Task DecryptObjectAsync(
            JObject document,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(diagnosticsContext != null);

            foreach (ClientEncryptionIncludedPath path in this.ClientEncryptionPolicy.IncludedPaths)
            {
                if (document.TryGetValue(path.Path.Substring(1), out JToken propertyValue))
                {
                    string propertyName = path.Path.Substring(1);
                    EncryptionSettingForProperty settingsForProperty = await this.EncryptionSettings.GetEncryptionSettingForPropertyAsync(propertyName, cancellationToken);

                    if (settingsForProperty == null)
                    {
                        throw new ArgumentException($"Invalid Encryption Setting for Property:{propertyName}. ");
                    }

                    AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = await settingsForProperty.BuildEncryptionAlgorithmForSettingAsync(cancellationToken: cancellationToken);

                    this.DecryptProperty(
                        document,
                        aeadAes256CbcHmac256EncryptionAlgorithm,
                        propertyName,
                        propertyValue);
                }
            }

            return;
        }

        /// <remarks>
        /// If there isn't any data that needs to be decrypted, input stream will be returned without any modification.
        /// Else input stream will be disposed, and a new stream is returned.
        /// In case of an exception, input stream won't be disposed, but position will be end of stream.
        /// </remarks>
        public async Task<Stream> DecryptAsync(
            Stream input,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (input == null)
            {
                return input;
            }

            Debug.Assert(input.CanSeek);
            Debug.Assert(diagnosticsContext != null);

            await this.InitEncryptionSettingsIfNotInitializedAsync(cancellationToken);

            if (this.ClientEncryptionPolicy == null)
            {
                input.Position = 0;
                return input;
            }

            JObject itemJObj = this.RetrieveItem(input);

            await this.DecryptObjectAsync(
                itemJObj,
                diagnosticsContext,
                cancellationToken);

            input.Dispose();
            return EncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        public async Task<JObject> DecryptAsync(
            JObject document,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            Debug.Assert(document != null);

            await this.InitEncryptionSettingsIfNotInitializedAsync(cancellationToken);

            if (this.ClientEncryptionPolicy == null)
            {
                return document;
            }

            await this.DecryptObjectAsync(
                document,
                diagnosticsContext,
                cancellationToken);

            return document;
        }

        private JObject RetrieveItem(
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

        internal static (TypeMarker, byte[]) Serialize(JToken propertyValue)
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

        internal static JToken DeserializeAndAddProperty(
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

        internal enum TypeMarker : byte
        {
            Null = 1, // not used
            Boolean = 2,
            Double = 3,
            Long = 4,
            String = 5,
        }
    }
}