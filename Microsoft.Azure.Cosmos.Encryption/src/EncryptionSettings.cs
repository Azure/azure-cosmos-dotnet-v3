//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class EncryptionSettings
    {
        private readonly ConcurrentDictionary<string, EncryptionSettingForProperty> encryptionSettingsDictByPropertyName = new ConcurrentDictionary<string, EncryptionSettingForProperty>();

        private EncryptionContainer encryptionContainer;

        private ClientEncryptionPolicy clientEncryptionPolicy;

        public string ContainerRidValue { get; private set; }

        internal System.Collections.Generic.ICollection<string> GetClientEncryptionPolicyPaths => this.encryptionSettingsDictByPropertyName.Keys;

        internal EncryptionSettingForProperty GetEncryptionSettingForProperty(string propertyName)
        {
            this.encryptionSettingsDictByPropertyName.TryGetValue(propertyName, out EncryptionSettingForProperty encryptionSettingsForProperty);

            return encryptionSettingsForProperty;
        }

        private EncryptionType GetEncryptionTypeForProperty(ClientEncryptionIncludedPath clientEncryptionIncludedPath)
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

        private async Task<EncryptionSettings> InitializeEncryptionSettingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ContainerResponse containerResponse = await this.encryptionContainer.ReadContainerAsync();

            // also set the Container Rid.
            this.ContainerRidValue = containerResponse.Resource.SelfLink.Split('/').ElementAt(3);

            // set the ClientEncryptionPolicy for the Settings.
            this.clientEncryptionPolicy = containerResponse.Resource.ClientEncryptionPolicy;
            if (this.clientEncryptionPolicy == null)
            {
                return this;
            }

            // update the property level setting.
            foreach (ClientEncryptionIncludedPath propertyToEncrypt in this.clientEncryptionPolicy.IncludedPaths)
            {
                EncryptionType encryptionType = this.GetEncryptionTypeForProperty(propertyToEncrypt);

                EncryptionSettingForProperty encryptionSettingsForProperty = new EncryptionSettingForProperty(
                    propertyToEncrypt.ClientEncryptionKeyId,
                    encryptionType,
                    this.encryptionContainer);

                string propertyName = propertyToEncrypt.Path.Substring(1);

                this.SetEncryptionSettingForProperty(
                    propertyName,
                    encryptionSettingsForProperty);
            }

            return this;
        }

        private void SetEncryptionSettingForProperty(
            string propertyName,
            EncryptionSettingForProperty encryptionSettingsForProperty)
        {
            this.encryptionSettingsDictByPropertyName[propertyName] = encryptionSettingsForProperty;
        }

        public static async Task<EncryptionSettings> GetEncryptionSettingsAsync(EncryptionContainer encryptionContainer)
        {
            EncryptionSettings encryptionSettings = new EncryptionSettings
            {
                encryptionContainer = encryptionContainer,
            };

            return await encryptionSettings.InitializeEncryptionSettingsAsync();
        }
    }
}