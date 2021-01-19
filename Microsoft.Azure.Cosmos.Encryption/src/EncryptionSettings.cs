//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class EncryptionSettings
    {
        internal AsyncCache<string, CachedEncryptionSettings> EncryptionSettingCacheByPropertyName { get; } = new AsyncCache<string, CachedEncryptionSettings>();

        public string ClientEncryptionKeyId { get; set; }

        public DateTime EncryptionSettingTimeToLive { get; set; }

        public DataEncryptionKey DataEncryptionKey { get; set; }

        public AeadAes256CbcHmac256EncryptionAlgorithm AeadAes256CbcHmac256EncryptionAlgorithm { get; set; }

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
                async () => await this.FetchCachedEncryptionSettingsAsync(propertyName, encryptionProcessor, cancellationToken, shouldForceRefresh: false),
                cancellationToken);

            if (cachedEncryptionSettings == null)
            {
                return null;
            }

            if (cachedEncryptionSettings.EncryptionSettingsExpiryUtc <= DateTime.UtcNow)
            {
                cachedEncryptionSettings = await this.EncryptionSettingCacheByPropertyName.GetAsync(
                    propertyName,
                    null,
                    async () => await this.FetchCachedEncryptionSettingsAsync(propertyName, encryptionProcessor, cancellationToken, shouldForceRefresh: true),
                    cancellationToken,
                    forceRefresh: true);
            }

            return cachedEncryptionSettings.EncryptionSettings;
        }

        private async Task<CachedEncryptionSettings> FetchCachedEncryptionSettingsAsync(
            string propertyName,
            EncryptionProcessor encryptionProcessor,
            CancellationToken cancellationToken,
            bool shouldForceRefresh = false)
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
                        CachedClientEncryptionProperties cachedClientEncryptionProperties = await encryptionProcessor.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                                clientEncryptionKeyId: propertyToEncrypt.ClientEncryptionKeyId,
                                container: encryptionProcessor.Container,
                                cancellationToken: cancellationToken,
                                shouldForceRefresh: shouldForceRefresh);

                        ClientEncryptionKeyProperties clientEncryptionKeyProperties = cachedClientEncryptionProperties.ClientEncryptionKeyProperties;

                        KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                                   clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
                                   clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value,
                                   encryptionProcessor.EncryptionKeyStoreProvider);

                        ProtectedDataEncryptionKey protectedDataEncryptionKey = new ProtectedDataEncryptionKey(
                                   propertyToEncrypt.ClientEncryptionKeyId,
                                   keyEncryptionKey,
                                   clientEncryptionKeyProperties.WrappedDataEncryptionKey);

                        EncryptionSettings encryptionSettings = new EncryptionSettings
                        {
                            // the cached Encryption Setting will have the same TTL as the corresponding Cached Client Encryption Key.
                            EncryptionSettingTimeToLive = cachedClientEncryptionProperties.ClientEncryptionKeyPropertiesExpiryUtc,
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