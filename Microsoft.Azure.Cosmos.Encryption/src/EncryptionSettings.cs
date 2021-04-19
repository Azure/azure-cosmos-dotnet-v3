//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class EncryptionSettings
    {
        internal AsyncCache<string, EncryptionSettingForProperty> EncryptionSettingCacheByPropertyName { get; } = new AsyncCache<string, EncryptionSettingForProperty>();

        public EncryptionProcessor EncryptionProcessor { get; }

        public EncryptionSettings(EncryptionProcessor encryptionProcessor)
        {
            this.EncryptionProcessor = encryptionProcessor ?? throw new ArgumentNullException(nameof(encryptionProcessor));
        }

        internal async Task<EncryptionSettingForProperty> GetEncryptionSettingForPropertyAsync(
            string propertyName,
            CancellationToken cancellationToken)
        {
            EncryptionSettingForProperty encryptionSettingsForProperty = await this.EncryptionSettingCacheByPropertyName.GetAsync(
                propertyName,
                obsoleteValue: null,
                async () => await this.FetchEncryptionSettingForPropertyAsync(propertyName, cancellationToken),
                cancellationToken);

            if (encryptionSettingsForProperty == null)
            {
                return null;
            }

            return encryptionSettingsForProperty;
        }

        private async Task<EncryptionSettingForProperty> FetchEncryptionSettingForPropertyAsync(
            string propertyName,
            CancellationToken cancellationToken)
        {
            ClientEncryptionPolicy clientEncryptionPolicy = await this.EncryptionProcessor.EncryptionCosmosClient.GetClientEncryptionPolicyAsync(
                this.EncryptionProcessor.Container,
                cancellationToken: cancellationToken,
                shouldForceRefresh: false);

            if (clientEncryptionPolicy != null)
            {
                foreach (ClientEncryptionIncludedPath propertyToEncrypt in clientEncryptionPolicy.IncludedPaths)
                {
                    if (string.Equals(propertyToEncrypt.Path.Substring(1), propertyName))
                    {
                        EncryptionType encryptionType = this.GetEncryptionTypeForProperty(propertyToEncrypt);

                        return new EncryptionSettingForProperty(
                            propertyToEncrypt.ClientEncryptionKeyId,
                            encryptionType,
                            this.EncryptionProcessor);
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

        internal void SetEncryptionSettingForProperty(
            string propertyName,
            EncryptionSettingForProperty encryptionSettingsForProperty)
        {
            this.EncryptionSettingCacheByPropertyName.Set(propertyName, encryptionSettingsForProperty);
        }
    }
}