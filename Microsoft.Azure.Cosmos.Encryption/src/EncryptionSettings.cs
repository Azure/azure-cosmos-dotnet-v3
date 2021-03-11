//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class EncryptionSettings
    {
        internal AsyncCache<string, EncryptionSettings> EncryptionSettingCacheByPropertyName { get; } = new AsyncCache<string, EncryptionSettings>();

        public string ClientEncryptionKeyId { get; set; }

        public EncryptionType EncryptionType { get; set; }

        public EncryptionSettings()
        {
        }

        internal async Task<EncryptionSettings> GetEncryptionSettingForPropertyAsync(
            string propertyName,
            EncryptionProcessor encryptionProcessor,
            CancellationToken cancellationToken)
        {
            EncryptionSettings encryptionSettings = await this.EncryptionSettingCacheByPropertyName.GetAsync(
                propertyName,
                obsoleteValue: null,
                async () => await this.FetchEncryptionSettingForPropertyAsync(propertyName, encryptionProcessor, cancellationToken),
                cancellationToken);

            if (encryptionSettings == null)
            {
                return null;
            }

            return encryptionSettings;
        }

        private async Task<EncryptionSettings> FetchEncryptionSettingForPropertyAsync(
            string propertyName,
            EncryptionProcessor encryptionProcessor,
            CancellationToken cancellationToken)
        {
            ClientEncryptionPolicy clientEncryptionPolicy = await encryptionProcessor.EncryptionCosmosClient.GetClientEncryptionPolicyAsync(
                encryptionProcessor.Container,
                cancellationToken,
                false);

            if (clientEncryptionPolicy != null)
            {
                foreach (ClientEncryptionIncludedPath propertyToEncrypt in clientEncryptionPolicy.IncludedPaths)
                {
                    if (string.Equals(propertyToEncrypt.Path.Substring(1), propertyName))
                    {
                        EncryptionType encryptionType = this.GetEncryptionTypeForProperty(propertyToEncrypt);

                        EncryptionSettings encryptionSettings = new EncryptionSettings
                        {
                            ClientEncryptionKeyId = propertyToEncrypt.ClientEncryptionKeyId,
                            EncryptionType = encryptionType,
                        };

                        return EncryptionSettings.Create(encryptionSettings);
                    }
                }
            }

            return null;
        }

        internal EncryptionType GetEncryptionTypeForProperty(ClientEncryptionIncludedPath clientEncryptionIncludedPath)
        {
            switch (clientEncryptionIncludedPath.EncryptionType)
            {
                case CosmosEncryptionType.Deterministic:
                    return EncryptionType.Deterministic;
                case CosmosEncryptionType.Randomized:
                    return EncryptionType.Randomized;
                case CosmosEncryptionType.Plaintext:
                    return EncryptionType.Plaintext;
                default:
                    throw new ArgumentException($"Invalid encryption type {clientEncryptionIncludedPath.EncryptionType}. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
            }
        }

        internal async Task<AeadAes256CbcHmac256EncryptionAlgorithm> BuildEncryptionAlgorithmForSettingAsync(
            EncryptionProcessor encryptionProcessor,
            CancellationToken cancellationToken)
        {
            ClientEncryptionKeyProperties clientEncryptionKeyProperties = await encryptionProcessor.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                    clientEncryptionKeyId: this.ClientEncryptionKeyId,
                    container: encryptionProcessor.Container,
                    cancellationToken: cancellationToken,
                    shouldForceRefresh: false);

            ProtectedDataEncryptionKey protectedDataEncryptionKey;

            try
            {
                // we pull out the Encrypted Data Encryption Key and build the Protected Data Encryption key
                // Here a request is sent out to unwrap using the Master Key configured via the Key Encryption Key.
                protectedDataEncryptionKey = this.BuildProtectedDataEncryptionKey(
                    clientEncryptionKeyProperties,
                    encryptionProcessor.EncryptionKeyStoreProvider,
                    this.ClientEncryptionKeyId);
            }
            catch (RequestFailedException ex)
            {
                // The access to master key was probably revoked. Try to fetch the latest ClientEncryptionKeyProperties from the backend.
                // This will succeed provided the user has rewraped the Client Encryption Key with right set of meta data.
                // This is based on the AKV provider implementaion so we expect a RequestFailedException in case other providers are used in unwrap implementation.
                if (ex.Status == (int)HttpStatusCode.Forbidden)
                {
                    clientEncryptionKeyProperties = await encryptionProcessor.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                        clientEncryptionKeyId: this.ClientEncryptionKeyId,
                        container: encryptionProcessor.Container,
                        cancellationToken: cancellationToken,
                        shouldForceRefresh: true);

                    // just bail out if this fails.
                    protectedDataEncryptionKey = this.BuildProtectedDataEncryptionKey(
                        clientEncryptionKeyProperties,
                        encryptionProcessor.EncryptionKeyStoreProvider,
                        this.ClientEncryptionKeyId);
                }
                else
                {
                    throw;
                }
            }

            AeadAes256CbcHmac256EncryptionAlgorithm aeadAes256CbcHmac256EncryptionAlgorithm = new AeadAes256CbcHmac256EncryptionAlgorithm(
                   protectedDataEncryptionKey,
                   this.EncryptionType);

            return aeadAes256CbcHmac256EncryptionAlgorithm;
        }

        internal ProtectedDataEncryptionKey BuildProtectedDataEncryptionKey(
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            EncryptionKeyStoreProvider encryptionKeyStoreProvider,
            string keyId)
        {
            KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
               clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
               clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value,
               encryptionKeyStoreProvider);

            ProtectedDataEncryptionKey protectedDataEncryptionKey = ProtectedDataEncryptionKey.GetOrCreate(
                   keyId,
                   keyEncryptionKey,
                   clientEncryptionKeyProperties.WrappedDataEncryptionKey);

            protectedDataEncryptionKey.TimeToLive = (TimeSpan)keyEncryptionKey.KeyStoreProvider.DataEncryptionKeyCacheTimeToLive;

            return protectedDataEncryptionKey;
        }

        internal void SetEncryptionSettingForProperty(string propertyName, EncryptionSettings encryptionSettings)
        {
            this.EncryptionSettingCacheByPropertyName.Set(propertyName, encryptionSettings);
        }

        internal static EncryptionSettings Create(
            EncryptionSettings settings)
        {
            return new EncryptionSettings()
            {
                ClientEncryptionKeyId = settings.ClientEncryptionKeyId,
                EncryptionType = settings.EncryptionType,
            };
        }
    }
}