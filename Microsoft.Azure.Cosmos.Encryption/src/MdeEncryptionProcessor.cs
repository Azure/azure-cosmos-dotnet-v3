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

    internal sealed class MdeEncryptionProcessor
    {
        private static readonly SemaphoreSlim EncryptionSettingSema = new SemaphoreSlim(1, 1);

        private bool isEncryptionSettingsInitDone;

        /// <summary>
        /// Gets the container that has items which are to be encrypted.
        /// </summary>
        public Container Container { get; }

        /// <summary>
        /// Gets the provider that allows interaction with the master keys.
        /// </summary>
        internal EncryptionKeyStoreProvider EncryptionKeyStoreProvider => this.EncryptionCosmosClient.EncryptionKeyStoreProvider;

        internal ClientEncryptionPolicy ClientEncryptionPolicy { get; set; }

        internal EncryptionCosmosClient EncryptionCosmosClient { get; }

        internal static readonly CosmosJsonDotNetSerializer BaseSerializer = new CosmosJsonDotNetSerializer(
            new JsonSerializerSettings()
            {
                DateParseHandling = DateParseHandling.None,
            });

        internal MdeEncryptionSettings MdeEncryptionSettings { get; }

        public MdeEncryptionProcessor(
            Container container,
            EncryptionCosmosClient encryptionCosmosClient)
        {
            this.Container = container ?? throw new ArgumentNullException(nameof(container));
            this.EncryptionCosmosClient = encryptionCosmosClient ?? throw new ArgumentNullException(nameof(encryptionCosmosClient));
            this.isEncryptionSettingsInitDone = false;
            this.MdeEncryptionSettings = new MdeEncryptionSettings();
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

            // update the property level setting.
            if (this.isEncryptionSettingsInitDone)
            {
                throw new InvalidOperationException("The Encrypton Processor has already been initialized");
            }

            Dictionary<string, MdeEncryptionSettings> settingsByDekId = new Dictionary<string, MdeEncryptionSettings>();
            this.ClientEncryptionPolicy = await this.EncryptionCosmosClient.GetOrAddClientEncryptionPolicyAsync(this.Container, cancellationToken, false);

            // no policy was configured.
            if (this.ClientEncryptionPolicy == null)
            {
                this.isEncryptionSettingsInitDone = true;
                return;
            }

            foreach (string clientEncryptionKeyId in this.ClientEncryptionPolicy.IncludedPaths.Select(p => p.ClientEncryptionKeyId).Distinct())
            {
                CachedClientEncryptionProperties cachedClientEncryptionProperties = await this.EncryptionCosmosClient.GetOrAddClientEncryptionKeyPropertiesAsync(
                    clientEncryptionKeyId,
                    this.Container,
                    cancellationToken,
                    false);

                ClientEncryptionKeyProperties clientEncryptionKeyProperties = cachedClientEncryptionProperties.ClientEncryptionKeyProperties;

                if (clientEncryptionKeyProperties != null)
                {
                    KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                        clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
                        clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value,
                        this.EncryptionKeyStoreProvider);

                    ProtectedDataEncryptionKey protectedDataEncryptionKey = new ProtectedDataEncryptionKey(
                               clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
                               keyEncryptionKey,
                               clientEncryptionKeyProperties.WrappedDataEncryptionKey);

                    settingsByDekId[clientEncryptionKeyId] = new MdeEncryptionSettings
                    {
                        EncryptionSettingTimeToLive = cachedClientEncryptionProperties.ClientEncryptionKeyPropertiesExpiryUtc,
                        ClientEncryptionKeyId = clientEncryptionKeyId,
                        DataEncryptionKey = protectedDataEncryptionKey,
                    };
                }
                else
                {
                    throw new InvalidOperationException($"Failed to retrieve ClientEncryptionProperties for the Client Encryption Key : {clientEncryptionKeyId}.");
                }
            }

            foreach (ClientEncryptionIncludedPath propertyToEncrypt in this.ClientEncryptionPolicy.IncludedPaths)
            {
                EncryptionType encryptionType = EncryptionType.Plaintext;
                switch (propertyToEncrypt.EncryptionType)
                {
                    case MdeEncryptionType.Deterministic:
                        encryptionType = EncryptionType.Deterministic;
                        break;
                    case MdeEncryptionType.Randomized:
                        encryptionType = EncryptionType.Randomized;
                        break;
                    default:
                        Debug.Fail(string.Format("Invalid encryption type {0}", propertyToEncrypt.EncryptionType));
                        break;
                }

                string propertyName = propertyToEncrypt.Path.Substring(1);

                this.MdeEncryptionSettings.SetEncryptionSettingForProperty(
                    propertyName,
                    MdeEncryptionSettings.Create(
                        settingsByDekId[propertyToEncrypt.ClientEncryptionKeyId],
                        encryptionType),
                    settingsByDekId[propertyToEncrypt.ClientEncryptionKeyId].EncryptionSettingTimeToLive);
            }

            this.isEncryptionSettingsInitDone = true;
        }

        /// <summary>
        /// Initializes the Encryption Setting for the processor if not initialized or if shouldForceRefresh is true.
        /// </summary>
        /// <param name="cancellationToken">(Optional) Token to cancel the operation.</param>
        /// <returns>Task to await.</returns>
        public async Task InitEncryptionSettingsIfNotInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (await EncryptionSettingSema.WaitAsync(-1))
            {
                try
                {
                    if (!this.isEncryptionSettingsInitDone)
                    {
                        await this.InitializeEncryptionSettingsAsync(cancellationToken);
                    }
                }
                finally
                {
                    EncryptionSettingSema.Release(1);
                }
            }
        }

        private async Task EncryptAndSerializePropertyAsync(
            JObject itemJObj,
            JToken propertyValue,
            MdeEncryptionSettings settings,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            /* Top Level can be an Object*/
            if (propertyValue.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in propertyValue)
                {
                    if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                    {
                        await this.EncryptAndSerializePropertyAsync(
                        itemJObj,
                        jProperty.Value,
                        settings,
                        diagnosticsContext,
                        cancellationToken);
                    }
                    else
                    {
                        jProperty.Value = await this.EncryptAndSerializeValueAsync(jProperty.Value, settings);
                    }
                }
            }
            else if (propertyValue.Type == JTokenType.Array)
            {
                if (propertyValue.Children().Count() > 0)
                {
                    if (!propertyValue.Children().First().Children().Any())
                    {
                        for (int i = 0; i < propertyValue.Count(); i++)
                        {
                            propertyValue[i] = await this.EncryptAndSerializeValueAsync(propertyValue[i], settings);
                        }
                    }
                    else
                    {
                        foreach (JObject arrayjObject in propertyValue.Children<JObject>())
                        {
                            foreach (JProperty jProperty in arrayjObject.Properties())
                            {
                                if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                                {
                                    await this.EncryptAndSerializePropertyAsync(
                                            null,
                                            jProperty.Value,
                                            settings,
                                            diagnosticsContext,
                                            cancellationToken);
                                }
                                else
                                {
                                    jProperty.Value = await this.EncryptAndSerializeValueAsync(jProperty.Value, settings);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                itemJObj.Property(propertyValue.Path).Value = await this.EncryptAndSerializeValueAsync(
                    itemJObj.Property(propertyValue.Path).Value,
                    settings);
                await Task.Yield();
                return;
            }
        }

        private async Task<JToken> EncryptAndSerializeValueAsync(
           JToken jToken,
           MdeEncryptionSettings settings)
        {
            JToken propertyValueToEncrypt = jToken;

            (TypeMarker typeMarker, byte[] plainText) = Serialize(propertyValueToEncrypt);

            byte[] cipherText = settings.AeadAes256CbcHmac256EncryptionAlgorithm.Encrypt(plainText);

            if (cipherText == null)
            {
                throw new InvalidOperationException($"{nameof(this.EncryptAndSerializeValueAsync)} returned null cipherText from {nameof(settings.AeadAes256CbcHmac256EncryptionAlgorithm.Encrypt)}.");
            }

            byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
            cipherTextWithTypeMarker[0] = (byte)typeMarker;
            Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
            return await Task.FromResult(cipherTextWithTypeMarker);
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
            await this.InitEncryptionSettingsIfNotInitializedAsync(cancellationToken);

            if (this.ClientEncryptionPolicy == null)
            {
                return input;
            }

            foreach (ClientEncryptionIncludedPath path in this.ClientEncryptionPolicy.IncludedPaths)
            {
                if (string.IsNullOrWhiteSpace(path.Path) || path.Path[0] != '/' || path.Path.LastIndexOf('/') != 0)
                {
                    throw new InvalidOperationException($"Invalid path {path.Path ?? string.Empty}, {nameof(path)}");
                }

                if (string.Equals(path.Path.Substring(1), "id"))
                {
                    throw new InvalidOperationException($"{path} includes an invalid path: '{path.Path}'.");
                }
            }

            JObject itemJObj = MdeEncryptionProcessor.BaseSerializer.FromStream<JObject>(input);
            List<string> pathsEncrypted = new List<string>();

            foreach (ClientEncryptionIncludedPath pathToEncrypt in this.ClientEncryptionPolicy.IncludedPaths)
            {
                string propertyName = pathToEncrypt.Path.Substring(1);

                // possibly a wrong path configured in the Client Encryption Policy, ignore.
                if (!itemJObj.TryGetValue(propertyName, out JToken propertyValue))
                {
                    continue;
                }

                if (propertyValue.Type == JTokenType.Null)
                {
                    continue;
                }

                MdeEncryptionSettings settings = await this.MdeEncryptionSettings.GetorUpdateEncryptionSettingForPropertyAsync(propertyName, this, cancellationToken);

                if (settings == null)
                {
                    throw new ArgumentException("Invalid Encryption Setting for the Property:{0}", propertyName);
                }

                await this.EncryptAndSerializePropertyAsync(
                                itemJObj,
                                propertyValue,
                                settings,
                                diagnosticsContext,
                                cancellationToken);

                pathsEncrypted.Add(pathToEncrypt.Path);
            }

            input.Dispose();
            return MdeEncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        private async Task<JToken> DecryptAndDeserializeValueAsync(
           JToken jToken,
           MdeEncryptionSettings settings,
           CosmosDiagnosticsContext diagnosticsContext,
           CancellationToken cancellationToken)
        {
            byte[] cipherTextWithTypeMarker = jToken.ToObject<byte[]>();

            if (cipherTextWithTypeMarker == null)
            {
                return null;
            }

            byte[] cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
            Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);

            byte[] plainText = await this.DecryptPropertyAsync(
                cipherText,
                settings,
                diagnosticsContext,
                cancellationToken);

            return DeserializeAndAddProperty(
                plainText,
                (TypeMarker)cipherTextWithTypeMarker[0]);
        }

        private async Task<byte[]> DecryptPropertyAsync(
           byte[] cipherText,
           MdeEncryptionSettings settings,
           CosmosDiagnosticsContext diagnosticsContext,
           CancellationToken cancellationToken)
        {
            byte[] plainText = settings.AeadAes256CbcHmac256EncryptionAlgorithm.Decrypt(cipherText);

            if (plainText == null)
            {
                throw new InvalidOperationException($"{nameof(this.DecryptPropertyAsync)} returned null plainText from {nameof(settings.AeadAes256CbcHmac256EncryptionAlgorithm.Decrypt)}.");
            }

            return await Task.FromResult(plainText);
        }

        private async Task DecryptAndDeserializePropertyAsync(
            JObject itemJObj,
            MdeEncryptionSettings settings,
            string propertyName,
            JToken propertyValue,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            if (propertyValue.Type == JTokenType.Object)
            {
                foreach (JProperty jProperty in propertyValue)
                {
                    if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                    {
                        await this.DecryptAndDeserializePropertyAsync(
                            itemJObj,
                            settings,
                            propertyName,
                            jProperty.Value,
                            diagnosticsContext,
                            cancellationToken);
                    }
                    else
                    {
                        jProperty.Value = await this.DecryptAndDeserializeValueAsync(
                                    jProperty.Value,
                                    settings,
                                    diagnosticsContext,
                                    cancellationToken);
                    }
                }
            }
            else if (propertyValue.Type == JTokenType.Array)
            {
                if (propertyValue.Children().Count() > 0)
                {
                    if (!propertyValue.Children().First().Children().Any())
                    {
                        for (int i = 0; i < propertyValue.Count(); i++)
                        {
                            propertyValue[i] = await this.DecryptAndDeserializeValueAsync(
                                         propertyValue[i],
                                         settings,
                                         diagnosticsContext,
                                         cancellationToken);
                        }
                    }
                    else
                    {
                        foreach (JObject arrayjObject in propertyValue.Children<JObject>())
                        {
                            foreach (JProperty jProperty in arrayjObject.Properties())
                            {
                                if (jProperty.Value.Type == JTokenType.Object || jProperty.Value.Type == JTokenType.Array)
                                {
                                    await this.DecryptAndDeserializePropertyAsync(
                                           itemJObj,
                                           settings,
                                           propertyName,
                                           jProperty.Value,
                                           diagnosticsContext,
                                           cancellationToken);
                                }
                                else
                                {
                                    jProperty.Value = await this.DecryptAndDeserializeValueAsync(
                                             jProperty.Value,
                                             settings,
                                             diagnosticsContext,
                                             cancellationToken);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                itemJObj.Property(propertyName).Value = await this.DecryptAndDeserializeValueAsync(
                                     itemJObj.Property(propertyName).Value,
                                     settings,
                                     diagnosticsContext,
                                     cancellationToken);

                await Task.Yield();
                return;
            }
        }

        private async Task DecryptObjectAsync(
            JObject document,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            List<string> pathsDecrypted = new List<string>();
            foreach (ClientEncryptionIncludedPath path in this.ClientEncryptionPolicy.IncludedPaths)
            {
                if (document.TryGetValue(path.Path.Substring(1), out JToken propertyValue))
                {
                    string propertyName = path.Path.Substring(1);
                    MdeEncryptionSettings settings = await this.MdeEncryptionSettings.GetorUpdateEncryptionSettingForPropertyAsync(propertyName, this, cancellationToken);

                    if (settings == null)
                    {
                        throw new ArgumentException("Invalid Encryption Setting for the Property");
                    }

                    await this.DecryptAndDeserializePropertyAsync(
                            document,
                            settings,
                            propertyName,
                            propertyValue,
                            diagnosticsContext,
                            cancellationToken);

                    pathsDecrypted.Add(path.Path);
                }
            }
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
            await this.InitEncryptionSettingsIfNotInitializedAsync(cancellationToken);

            if (input == null || this.ClientEncryptionPolicy == null)
            {
                return input;
            }

            Debug.Assert(input.CanSeek);
            Debug.Assert(diagnosticsContext != null);

            JObject itemJObj = this.RetrieveItem(input);

            await this.DecryptObjectAsync(
                    itemJObj,
                    diagnosticsContext,
                    cancellationToken);

            input.Dispose();
            return MdeEncryptionProcessor.BaseSerializer.ToStream(itemJObj);
        }

        public async Task<JObject> DecryptAsync(
            JObject document,
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            await this.InitEncryptionSettingsIfNotInitializedAsync(cancellationToken);

            if (this.ClientEncryptionPolicy == null)
            {
                return document;
            }

            Debug.Assert(document != null);

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
            SqlNvarcharSerializer sqlNvarcharSerializer = new SqlNvarcharSerializer(-1);

            return propertyValue.Type switch
            {
                JTokenType.Boolean => (TypeMarker.Boolean, sqlSerializerFactory.GetDefaultSerializer<bool>().Serialize(propertyValue.ToObject<bool>())),
                JTokenType.Float => (TypeMarker.Double, sqlSerializerFactory.GetDefaultSerializer<double>().Serialize(propertyValue.ToObject<double>())),
                JTokenType.Integer => (TypeMarker.Long, sqlSerializerFactory.GetDefaultSerializer<long>().Serialize(propertyValue.ToObject<long>())),
                JTokenType.String => (TypeMarker.String, sqlNvarcharSerializer.Serialize(propertyValue.ToObject<string>())),
                _ => throw new InvalidOperationException($" Invalid or Unsupported Data Type Passed : {propertyValue.Type}"),
            };
        }

        internal static JToken DeserializeAndAddProperty(
            byte[] serializedBytes,
            TypeMarker typeMarker)
        {
            SqlSerializerFactory sqlSerializerFactory = new SqlSerializerFactory();

            return typeMarker switch
            {
                TypeMarker.Boolean => sqlSerializerFactory.GetDefaultSerializer<bool>().Deserialize(serializedBytes),
                TypeMarker.Double => sqlSerializerFactory.GetDefaultSerializer<double>().Deserialize(serializedBytes),
                TypeMarker.Long => sqlSerializerFactory.GetDefaultSerializer<long>().Deserialize(serializedBytes),
                TypeMarker.String => sqlSerializerFactory.GetDefaultSerializer<string>().Deserialize(serializedBytes),
                _ => throw new InvalidOperationException($" Invalid or Unsupported Data Type Passed : {typeMarker}"),
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