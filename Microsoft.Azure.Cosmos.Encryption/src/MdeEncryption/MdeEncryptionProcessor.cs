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
        private Dictionary<string, MdeEncryptionSettings> perPropertyEncryptionSetting;

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

        public MdeEncryptionProcessor(
            Container container,
            EncryptionCosmosClient encryptionCosmosClient)
        {
            this.Container = container ?? throw new ArgumentNullException(nameof(container));
            this.EncryptionCosmosClient = encryptionCosmosClient ?? throw new ArgumentNullException(nameof(encryptionCosmosClient));
        }

        internal async Task InitializeEncryptionSettingsAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // update the property level setting.
            if (this.perPropertyEncryptionSetting != null && !forceRefresh)
            {
                throw new InvalidOperationException("The Encrypton Processor has already been initialized");
            }

            Dictionary<string, MdeEncryptionSettings> settingsByDekId = new Dictionary<string, MdeEncryptionSettings>();
            this.ClientEncryptionPolicy = await this.EncryptionCosmosClient.GetOrAddClientEncryptionPolicyAsync(this.Container, cancellationToken, false);

            if (this.ClientEncryptionPolicy == null)
            {
                throw new InvalidOperationException("Please configure ClientEncryptionPolicy when using Encryption based Cosmos Client");
            }

            foreach (string dataEncryptionKeyId in this.ClientEncryptionPolicy.IncludedPaths.Select(p => p.ClientEncryptionKeyId).Distinct())
            {
                ClientEncryptionKeyProperties clientEncryptionKeyProperties = await this.EncryptionCosmosClient.GetOrAddClientEncryptionKeyPropertiesAsync(
                    dataEncryptionKeyId,
                    this.Container,
                    cancellationToken,
                    false);

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

                    settingsByDekId[dataEncryptionKeyId] = new MdeEncryptionSettings
                    {
                        ClientEncryptionKeyId = dataEncryptionKeyId,
                        DataEncryptionKey = protectedDataEncryptionKey,
                    };
                }
                else
                {
                    throw new InvalidOperationException($"Failed to retrieve ClientEncryptionProperties for the Client Encryption Key : {dataEncryptionKeyId}.");
                }
            }

            this.perPropertyEncryptionSetting = new Dictionary<string, MdeEncryptionSettings>();
            foreach (ClientEncryptionIncludedPath propertyToEncrypt in this.ClientEncryptionPolicy.IncludedPaths)
            {
                Data.Encryption.Cryptography.EncryptionType encryptionType = Data.Encryption.Cryptography.EncryptionType.Plaintext;
                switch (propertyToEncrypt.EncryptionType)
                {
                    case "Deterministic":
                        encryptionType = Data.Encryption.Cryptography.EncryptionType.Deterministic;
                        break;
                    case "Randomized":
                        encryptionType = Data.Encryption.Cryptography.EncryptionType.Randomized;
                        break;
                    default:
                        Debug.Fail(string.Format("Invalid encryption type {0}", propertyToEncrypt.EncryptionType));
                        break;
                }

                string propertyName = propertyToEncrypt.Path.Substring(1);
                this.perPropertyEncryptionSetting[propertyName]
                    = MdeEncryptionSettings.Create(
                        settingsByDekId[propertyToEncrypt.ClientEncryptionKeyId],
                        encryptionType,
                        propertyToEncrypt.ClientEncryptionDataType);
            }
        }

        public async Task InitializeMdeProcessorIfNotInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (this.perPropertyEncryptionSetting == null)
            {
                await this.InitializeEncryptionSettingsAsync(false, cancellationToken);
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
                if (propertyValue.Children().Count() != 1 && !propertyValue.Children().First().Children().Any())
                {
                    for (int i = 0; i < propertyValue.Count(); i++)
                    {
                        propertyValue[i] = await this.EncryptAndSerializeValueAsync(propertyValue[i], settings);
                    }
                }

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

            ClientEncryptionDataType? clientEncryptionDataType = null;
            if (settings.ClientEncryptionDataType != null)
            {
                VerifyAndGetPropertyDataType(jToken, settings.ClientEncryptionDataType);
                clientEncryptionDataType = settings.ClientEncryptionDataType;
            }
            else
            {
                clientEncryptionDataType = VerifyAndGetPropertyDataType(propertyValueToEncrypt, clientEncryptionDataType);
            }

            (ClientEncryptionDataType? typeMarker, byte[] plainText) = this.Serialize(propertyValueToEncrypt, clientEncryptionDataType);

            byte[] cipherText = settings.AeadAes256CbcHmac256EncryptionAlgorithm.Encrypt(plainText);

            if (cipherText == null)
            {
                throw new InvalidOperationException($"{nameof(this.EncryptAndSerializeValueAsync)} returned null cipherText from {nameof(settings.AeadAes256CbcHmac256EncryptionAlgorithm.Encrypt)}.");
            }

            if (settings.ClientEncryptionDataType == null)
            {
                byte[] cipherTextWithTypeMarker = new byte[cipherText.Length + 1];
                cipherTextWithTypeMarker[0] = (byte)typeMarker;
                Buffer.BlockCopy(cipherText, 0, cipherTextWithTypeMarker, 1, cipherText.Length);
                return await Task.FromResult(cipherTextWithTypeMarker);
            }
            else
            {
                return await Task.FromResult(cipherText);
            }
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
            if (this.perPropertyEncryptionSetting == null)
            {
                await this.InitializeEncryptionSettingsAsync(false);
            }

            foreach (ClientEncryptionIncludedPath path in this.ClientEncryptionPolicy.IncludedPaths)
            {
                if (string.IsNullOrWhiteSpace(path.Path) || path.Path[0] != '/' || path.Path.LastIndexOf('/') != 0)
                {
                    throw new InvalidOperationException($"Invalid path {path.Path ?? string.Empty}, {nameof(path)}");
                }

                if (string.Equals(path.Path.Substring(1), "id"))
                {
                    throw new InvalidOperationException($"{path} includes a invalid path: '{path}'.");
                }
            }

            JObject itemJObj = MdeEncryptionProcessor.BaseSerializer.FromStream<JObject>(input);
            List<string> pathsEncrypted = new List<string>();

            foreach (ClientEncryptionIncludedPath pathToEncrypt in this.ClientEncryptionPolicy.IncludedPaths)
            {
                string propertyName = pathToEncrypt.Path.Substring(1);
                if (!itemJObj.TryGetValue(propertyName, out JToken propertyValue))
                {
                    throw new ArgumentException($"{nameof(pathToEncrypt)} includes a path: '{pathToEncrypt}' which was not found.");
                }

                if (propertyValue.Type == JTokenType.Null)
                {
                    continue;
                }

                MdeEncryptionSettings settings = await this.GetEncryptionSettingForPropertyAsync(propertyName);

                if (settings == null)
                {
                    throw new ArgumentException("Invalid Encryption Setting for the Property");
                }

                VerifyAndGetPropertyDataType(propertyValue, settings.ClientEncryptionDataType);
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

            byte[] cipherText;

            if (settings.ClientEncryptionDataType != null)
            {
                cipherText = cipherTextWithTypeMarker;
            }
            else
            {
                cipherText = new byte[cipherTextWithTypeMarker.Length - 1];
                Buffer.BlockCopy(cipherTextWithTypeMarker, 1, cipherText, 0, cipherTextWithTypeMarker.Length - 1);
            }

            byte[] plainText = await this.DecryptPropertyAsync(
                cipherText,
                settings,
                diagnosticsContext,
                cancellationToken);

            ClientEncryptionDataType? clientEncryptionDataType;
            if (settings.ClientEncryptionDataType != null)
            {
                clientEncryptionDataType = settings.ClientEncryptionDataType;
            }
            else
            {
                ClientEncryptionDataType typeMarker = (ClientEncryptionDataType)cipherTextWithTypeMarker[0];
                clientEncryptionDataType = typeMarker;
            }

            return this.DeserializeAndAddProperty(
                plainText,
                clientEncryptionDataType);
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
                if (propertyValue.Children().Count() != 1 && !propertyValue.Children().First().Children().Any())
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
                    MdeEncryptionSettings settings = await this.GetEncryptionSettingForPropertyAsync(propertyName);

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
            if (this.perPropertyEncryptionSetting == null)
            {
                await this.InitializeEncryptionSettingsAsync(false);
            }

            if (input == null)
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
            if (this.perPropertyEncryptionSetting == null)
            {
                await this.InitializeEncryptionSettingsAsync(false);
            }

            Debug.Assert(document != null);

            await this.DecryptObjectAsync(
                         document,
                         diagnosticsContext,
                         cancellationToken);

            return document;
        }

        public async Task<MdeEncryptionSettings> GetEncryptionSettingForPropertyAsync(string propertyName)
        {
            if (this.perPropertyEncryptionSetting.TryGetValue(propertyName, out MdeEncryptionSettings settings))
            {
                if (settings.MdeEncryptionSettingsExpiry <= DateTime.UtcNow)
                {
                    await this.InitializeEncryptionSettingsAsync(true);
                    if (this.perPropertyEncryptionSetting.TryGetValue(propertyName, out settings))
                    {
                        return settings;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return settings;
                }
            }
            else
            {
                return null;
            }
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

        private (ClientEncryptionDataType?, byte[]) Serialize(JToken propertyValue, ClientEncryptionDataType? clientEncryptionDataType = null)
        {
            SqlSerializerFactory sqlSerializerFactory = new SqlSerializerFactory();
            SqlNvarcharSerializer sqlNvarcharSerializer = new SqlNvarcharSerializer(-1);

            return clientEncryptionDataType switch
            {
                ClientEncryptionDataType.Bool => (ClientEncryptionDataType.Bool, sqlSerializerFactory.GetDefaultSerializer<bool>().Serialize(propertyValue.ToObject<bool>())),
                ClientEncryptionDataType.Double => (ClientEncryptionDataType.Double, sqlSerializerFactory.GetDefaultSerializer<double>().Serialize(propertyValue.ToObject<double>())),
                ClientEncryptionDataType.Long => (ClientEncryptionDataType.Long, sqlSerializerFactory.GetDefaultSerializer<long>().Serialize(propertyValue.ToObject<long>())),
                ClientEncryptionDataType.String => (ClientEncryptionDataType.String, sqlNvarcharSerializer.Serialize(propertyValue.ToObject<string>())),
                _ => throw new InvalidOperationException($" Invalid or Unsupported Data Type Passed : {propertyValue.Type}"),
            };
        }

        private JToken DeserializeAndAddProperty(
            byte[] serializedBytes,
            ClientEncryptionDataType? clientEncryptionDataType = null)
        {
            SqlSerializerFactory sqlSerializerFactory = new SqlSerializerFactory();

            return clientEncryptionDataType switch
            {
                ClientEncryptionDataType.Bool => sqlSerializerFactory.GetDefaultSerializer<bool>().Deserialize(serializedBytes),
                ClientEncryptionDataType.Double => sqlSerializerFactory.GetDefaultSerializer<double>().Deserialize(serializedBytes),
                ClientEncryptionDataType.Long => sqlSerializerFactory.GetDefaultSerializer<long>().Deserialize(serializedBytes),
                ClientEncryptionDataType.String => sqlSerializerFactory.GetDefaultSerializer<string>().Deserialize(serializedBytes),
                _ => throw new InvalidOperationException($" Invalid or Unsupported Data Type Passed : {clientEncryptionDataType}"),
            };
        }

        private static ClientEncryptionDataType? VerifyAndGetPropertyDataType(
            JToken propertyValueToEncrypt,
            ClientEncryptionDataType? passedclientEncryptionDataType = null)
        {
            ClientEncryptionDataType? clientEncryptionDataType = null;
            switch (propertyValueToEncrypt.Type)
            {
                case JTokenType.Boolean:
                    clientEncryptionDataType = ClientEncryptionDataType.Bool;
                    break;
                case JTokenType.Float:
                    clientEncryptionDataType = ClientEncryptionDataType.Double;
                    break;
                case JTokenType.Integer:
                    clientEncryptionDataType = ClientEncryptionDataType.Long;
                    break;
                case JTokenType.String:
                    clientEncryptionDataType = ClientEncryptionDataType.String;
                    break;
            }

            if (passedclientEncryptionDataType != null && clientEncryptionDataType != passedclientEncryptionDataType)
            {
                throw new ArgumentException($"Incorrect DataType:{passedclientEncryptionDataType} configured in the ClientEncryptionPolicy");
            }

            return clientEncryptionDataType;
        }
    }
}