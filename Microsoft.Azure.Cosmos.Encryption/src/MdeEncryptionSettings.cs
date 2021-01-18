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

    internal sealed class MdeEncryptionSettings
    {
        internal AsyncCache<string, CachedEncryptionSettings> EncryptionSettingCacheByPropertyName { get; } = new AsyncCache<string, CachedEncryptionSettings>();

        public string ClientEncryptionKeyId { get; set; }

        public DateTime EncryptionSettingTimeToLive { get; set; }

        public DataEncryptionKey DataEncryptionKey { get; set; }

        public AeadAes256CbcHmac256EncryptionAlgorithm AeadAes256CbcHmac256EncryptionAlgorithm { get; set; }

        public EncryptionType EncryptionType { get; set; }

        public MdeEncryptionSettings()
        {
        }

        internal async Task<MdeEncryptionSettings> GetEncryptionSettingForPropertyAsync(
            string propertyName,
            MdeEncryptionProcessor mdeEncryptionProcessor,
            CancellationToken cancellationToken)
        {
            CachedEncryptionSettings cachedEncryptionSettings = await this.EncryptionSettingCacheByPropertyName.GetAsync(
                propertyName,
                null,
                async () => await this.FetchCachedEncryptionSettingsAsync(propertyName, mdeEncryptionProcessor, cancellationToken, shouldForceRefresh: false),
                cancellationToken);

            if (cachedEncryptionSettings == null)
            {
                return null;
            }

            if (cachedEncryptionSettings.MdeEncryptionSettingsExpiryUtc <= DateTime.UtcNow)
            {
                cachedEncryptionSettings = await this.EncryptionSettingCacheByPropertyName.GetAsync(
                    propertyName,
                    null,
                    async () => await this.FetchCachedEncryptionSettingsAsync(propertyName, mdeEncryptionProcessor, cancellationToken, shouldForceRefresh: true),
                    cancellationToken,
                    forceRefresh: true);
            }

            return cachedEncryptionSettings.MdeEncryptionSettings;
        }

        private async Task<CachedEncryptionSettings> FetchCachedEncryptionSettingsAsync(
            string propertyName,
            MdeEncryptionProcessor mdeEncryptionProcessor,
            CancellationToken cancellationToken,
            bool shouldForceRefresh = false)
        {
            ClientEncryptionPolicy clientEncryptionPolicy = await mdeEncryptionProcessor.EncryptionCosmosClient.GetClientEncryptionPolicyAsync(
                mdeEncryptionProcessor.Container,
                cancellationToken,
                false);

            foreach (ClientEncryptionIncludedPath propertyToEncrypt in clientEncryptionPolicy.IncludedPaths)
            {
                if (string.Equals(propertyToEncrypt.Path.Substring(1), propertyName))
                {
                    CachedClientEncryptionProperties cachedClientEncryptionProperties = await mdeEncryptionProcessor.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                            clientEncryptionKeyId: propertyToEncrypt.ClientEncryptionKeyId,
                            container: mdeEncryptionProcessor.Container,
                            cancellationToken: cancellationToken,
                            shouldForceRefresh: shouldForceRefresh);

                    ClientEncryptionKeyProperties clientEncryptionKeyProperties = cachedClientEncryptionProperties.ClientEncryptionKeyProperties;

                    KeyEncryptionKey keyEncryptionKey = KeyEncryptionKey.GetOrCreate(
                               clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Name,
                               clientEncryptionKeyProperties.EncryptionKeyWrapMetadata.Value,
                               mdeEncryptionProcessor.EncryptionKeyStoreProvider);

                    ProtectedDataEncryptionKey protectedDataEncryptionKey = new ProtectedDataEncryptionKey(
                               propertyToEncrypt.ClientEncryptionKeyId,
                               keyEncryptionKey,
                               clientEncryptionKeyProperties.WrappedDataEncryptionKey);

                    MdeEncryptionSettings mdeEncryptionSettings = new MdeEncryptionSettings
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

                    mdeEncryptionSettings = MdeEncryptionSettings.Create(mdeEncryptionSettings, encryptionType);
                    return new CachedEncryptionSettings(mdeEncryptionSettings, mdeEncryptionSettings.EncryptionSettingTimeToLive);
                }
            }

            return null;
        }

        internal void SetEncryptionSettingForProperty(string propertyName, MdeEncryptionSettings mdeEncryptionSettings, DateTime expiryUtc)
        {
            CachedEncryptionSettings cachedEncryptionSettings = new CachedEncryptionSettings(mdeEncryptionSettings, expiryUtc);
            this.EncryptionSettingCacheByPropertyName.Set(propertyName, cachedEncryptionSettings);
        }

        internal static MdeEncryptionSettings Create(
            MdeEncryptionSettings settingsForKey,
            EncryptionType encryptionType)
        {
            return new MdeEncryptionSettings()
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