//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class EncryptionSettings
    {
        internal AsyncCache<string, CachedEncryptionSettings> EncryptionSettingCacheByPropertyName { get; } = new AsyncCache<string, CachedEncryptionSettings>();

        public string ClientEncryptionKeyId { get; set; }

        public DateTime EncryptionSettingTimeToLive { get; set; }

        internal DataEncryptionKey DataEncryptionKey { get; set; }

        internal AeadAes256CbcHmac256EncryptionAlgorithm AeadAes256CbcHmac256EncryptionAlgorithm { get; set; }

        public EncryptionType EncryptionType { get; set; }

        public EncryptionSettings()
        {
        }

        internal async Task<EncryptionSettings> GetEncryptionSettingForPropertyAsync(
            string propertyName,
            EncryptionProcessor encryptionProcessor,
            CancellationToken cancellationToken)
        {
            CachedEncryptionSettings cachedEncryptionSettings = await this.EncryptionSettingCacheByPropertyName.GetAsync(
                propertyName,
                null,
                async () => await this.FetchCachedEncryptionSettingsAsync(propertyName, encryptionProcessor, cancellationToken),
                cancellationToken);

            if (cachedEncryptionSettings == null)
            {
                return null;
            }

            // we just cache the algo for the property for a duration of  1 hour and when it expires we try to fetch the cached Encrypted key
            // from the Cosmos Client and try to create a Protected Data Encryption Key which tries to unwrap the key.
            // 1) Try to check if the KEK has been revoked may be post rotation. If the request fails this could mean the KEK was revoked,
            // the user might have rewraped the Key and that is when we try to force fetch it from the Backend.
            // So we only read back from the backend only when an operation like wrap/unwrap with the Master Key fails.
            if (cachedEncryptionSettings.EncryptionSettingsExpiryUtc <= DateTime.UtcNow)
            {
                cachedEncryptionSettings = await this.EncryptionSettingCacheByPropertyName.GetAsync(
                    propertyName,
                    null,
                    async () => await this.FetchCachedEncryptionSettingsAsync(propertyName, encryptionProcessor, cancellationToken),
                    cancellationToken,
                    forceRefresh: true);
            }

            return cachedEncryptionSettings.EncryptionSettings;
        }

        private async Task<CachedEncryptionSettings> FetchCachedEncryptionSettingsAsync(
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
                        ClientEncryptionKeyProperties clientEncryptionKeyProperties = await encryptionProcessor.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                               clientEncryptionKeyId: propertyToEncrypt.ClientEncryptionKeyId,
                               container: encryptionProcessor.Container,
                               cancellationToken: cancellationToken,
                               shouldForceRefresh: false);

                        ProtectedDataEncryptionKey protectedDataEncryptionKey = null;

                        try
                        {
                            protectedDataEncryptionKey = this.BuildProtectedDataEncryptionKey(
                                clientEncryptionKeyProperties,
                                encryptionProcessor.EncryptionKeyStoreProvider,
                                propertyToEncrypt.ClientEncryptionKeyId);
                        }
                        catch (RequestFailedException ex)
                        {
                            // the key was revoked. Try to fetch the latest EncryptionKeyProperties from the backend.
                            // This should succeed provided the user has rewraped the key with right set of meta data.
                            if (ex.Status == (int)HttpStatusCode.Forbidden)
                            {
                                clientEncryptionKeyProperties = await encryptionProcessor.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                                clientEncryptionKeyId: propertyToEncrypt.ClientEncryptionKeyId,
                                container: encryptionProcessor.Container,
                                cancellationToken: cancellationToken,
                                shouldForceRefresh: true);

                                protectedDataEncryptionKey = this.BuildProtectedDataEncryptionKey(
                                clientEncryptionKeyProperties,
                                encryptionProcessor.EncryptionKeyStoreProvider,
                                propertyToEncrypt.ClientEncryptionKeyId);
                            }
                        }

                        EncryptionSettings encryptionSettings = new EncryptionSettings
                        {
                            EncryptionSettingTimeToLive = DateTime.UtcNow + TimeSpan.FromMinutes(Constants.CachedEncryptionSettingsDefaultTTLInMinutes),
                            ClientEncryptionKeyId = propertyToEncrypt.ClientEncryptionKeyId,
                            DataEncryptionKey = protectedDataEncryptionKey,
                        };

                        EncryptionType encryptionType = EncryptionType.Plaintext;
                        switch (propertyToEncrypt.EncryptionType)
                        {
                            case CosmosEncryptionType.Deterministic:
                                encryptionType = EncryptionType.Deterministic;
                                break;
                            case CosmosEncryptionType.Randomized:
                                encryptionType = EncryptionType.Randomized;
                                break;
                            default:
                                Debug.Fail(string.Format("Invalid encryption type {0}. ", propertyToEncrypt.EncryptionType));
                                break;
                        }

                        encryptionSettings = EncryptionSettings.Create(encryptionSettings, encryptionType);
                        return new CachedEncryptionSettings(encryptionSettings, encryptionSettings.EncryptionSettingTimeToLive);
                    }
                }
            }

            return null;
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

            return new ProtectedDataEncryptionKey(
                   keyId,
                   keyEncryptionKey,
                   clientEncryptionKeyProperties.WrappedDataEncryptionKey);
        }

        internal void SetEncryptionSettingForProperty(string propertyName, EncryptionSettings encryptionSettings, DateTime expiryUtc)
        {
            CachedEncryptionSettings cachedEncryptionSettings = new CachedEncryptionSettings(encryptionSettings, expiryUtc);
            this.EncryptionSettingCacheByPropertyName.Set(propertyName, cachedEncryptionSettings);
        }

        internal static EncryptionSettings Create(
            EncryptionSettings settingsForKey,
            EncryptionType encryptionType)
        {
            return new EncryptionSettings()
            {
                ClientEncryptionKeyId = settingsForKey.ClientEncryptionKeyId,
                DataEncryptionKey = settingsForKey.DataEncryptionKey,
                EncryptionType = encryptionType,
                EncryptionSettingTimeToLive = settingsForKey.EncryptionSettingTimeToLive,
                AeadAes256CbcHmac256EncryptionAlgorithm = AeadAes256CbcHmac256EncryptionAlgorithm.GetOrCreate(
                settingsForKey.DataEncryptionKey,
                encryptionType),
            };
        }
    }
}