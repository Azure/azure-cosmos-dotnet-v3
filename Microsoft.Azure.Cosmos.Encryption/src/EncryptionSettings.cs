//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class EncryptionSettings
    {
        // TODO: Good to have constants available in the Cosmos SDK. Tracked via https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2431
        private const string IntendedCollectionHeader = "x-ms-cosmos-intended-collection-rid";

        private const string IsClientEncryptedHeader = "x-ms-cosmos-is-client-encrypted";

        private readonly ConcurrentDictionary<string, EncryptionSettingForProperty> encryptionSettingsDictByPropertyName = new ConcurrentDictionary<string, EncryptionSettingForProperty>();

        private readonly EncryptionContainer encryptionContainer;

        private ClientEncryptionPolicy clientEncryptionPolicy;

        private string databaseRidValue;

        public string ContainerRidValue { get; private set; }

        public ICollection<string> PropertiesToEncrypt => this.encryptionSettingsDictByPropertyName.Keys;

        public static Task<EncryptionSettings> CreateAsync(EncryptionContainer encryptionContainer)
        {
            EncryptionSettings encryptionSettings = new EncryptionSettings(encryptionContainer);

            return encryptionSettings.InitializeEncryptionSettingsAsync();
        }

        public EncryptionSettingForProperty GetEncryptionSettingForProperty(string propertyName)
        {
            this.encryptionSettingsDictByPropertyName.TryGetValue(propertyName, out EncryptionSettingForProperty encryptionSettingsForProperty);

            return encryptionSettingsForProperty;
        }

        public void SetRequestHeaders(RequestOptions requestOptions)
        {
            requestOptions.AddRequestHeaders = (headers) =>
            {
                headers.Add(IsClientEncryptedHeader, bool.TrueString);
                headers.Add(IntendedCollectionHeader, this.ContainerRidValue);
            };
        }

        private EncryptionSettings(EncryptionContainer encryptionContainer)
        {
            this.encryptionContainer = encryptionContainer;
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

            // set the Database Rid.
            this.databaseRidValue = containerResponse.Resource.SelfLink.Split('/').ElementAt(1);

            // set the Container Rid.
            this.ContainerRidValue = containerResponse.Resource.SelfLink.Split('/').ElementAt(3);

            // set the ClientEncryptionPolicy for the Settings.
            this.clientEncryptionPolicy = containerResponse.Resource.ClientEncryptionPolicy;
            if (this.clientEncryptionPolicy == null)
            {
                return this;
            }

            // for each of the unique keys in the policy Add it in /Update the cache.
            foreach (string clientEncryptionKeyId in this.clientEncryptionPolicy.IncludedPaths.Select(x => x.ClientEncryptionKeyId).Distinct())
            {
                await this.encryptionContainer.EncryptionCosmosClient.GetClientEncryptionKeyPropertiesAsync(
                     clientEncryptionKeyId: clientEncryptionKeyId,
                     encryptionContainer: this.encryptionContainer,
                     databaseRid: this.databaseRidValue,
                     cancellationToken: cancellationToken);
            }

            // update the property level setting.
            foreach (ClientEncryptionIncludedPath propertyToEncrypt in this.clientEncryptionPolicy.IncludedPaths)
            {
                EncryptionType encryptionType = this.GetEncryptionTypeForProperty(propertyToEncrypt);

                EncryptionSettingForProperty encryptionSettingsForProperty = new EncryptionSettingForProperty(
                    propertyToEncrypt.ClientEncryptionKeyId,
                    encryptionType,
                    this.encryptionContainer,
                    this.databaseRidValue);

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
    }
}