//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.KeyVault;

    /// <summary>
    /// Provides the default implementation for client-side encryption for Cosmos DB.
    /// Azure Key Vault has keys which are used to control the data access.
    /// Data Encryption Keys (which intermediate keys) are stored in a Cosmos DB container 
    /// that instances of this class are initialized with after wrapping (aka encrypting) 
    /// it using the Azure Key Vault key provided during the creation of each Data Encryption Key.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public sealed class AzureKeyVaultCosmosEncryptor : Encryptor
    {
        private CosmosEncryptor cosmosEncryptor;

        private CosmosDataEncryptionKeyProvider cosmosDekProvider;

        public DataEncryptionKeyContainer DataEncryptionKeyContainer => this.cosmosDekProvider.DataEncryptionKeyContainer;

        public AzureKeyVaultCosmosEncryptor(
            string clientId, 
            string certificateThumbprint)
        {
            EncryptionKeyWrapProvider wrapProvider = new AzureKeyVaultKeyWrapProvider(
                clientId, 
                certificateThumbprint);

            this.cosmosDekProvider = new CosmosDataEncryptionKeyProvider(wrapProvider);
            this.cosmosEncryptor = new CosmosEncryptor(this.cosmosDekProvider);
        }

        public Task InitializeAsync(
            Database dekStorageDatabase,
            string dekStorageContainerId)
        {
            return this.cosmosDekProvider.InitializeAsync(dekStorageDatabase, dekStorageContainerId);
        }

        public override Task<byte[]> EncryptAsync(
            byte[] plainText, 
            string dataEncryptionKeyId, 
            string encryptionAlgorithm, 
            CancellationToken cancellationToken = default)
        {
            return this.cosmosEncryptor.EncryptAsync(
                plainText,
                dataEncryptionKeyId,
                encryptionAlgorithm,
                cancellationToken);
        }

        public override Task<byte[]> DecryptAsync(
            byte[] cipherText, 
            string dataEncryptionKeyId, 
            string encryptionAlgorithm, 
            CancellationToken cancellationToken = default)
        {
            return this.cosmosEncryptor.DecryptAsync(
                cipherText,
                dataEncryptionKeyId,
                encryptionAlgorithm,
                cancellationToken);
        }
    }
}
