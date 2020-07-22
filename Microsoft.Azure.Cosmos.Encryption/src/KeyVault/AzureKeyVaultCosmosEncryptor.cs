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
        private readonly CosmosEncryptor cosmosEncryptor;
        private readonly CosmosDataEncryptionKeyProvider cosmosDekProvider;
        private bool isDisposed = false;

        /// <summary>
        /// Gets Container for data encryption keys.
        /// </summary>
        public DataEncryptionKeyContainer DataEncryptionKeyContainer => this.cosmosDekProvider.DataEncryptionKeyContainer;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureKeyVaultCosmosEncryptor"/> class.
        /// </summary>
        /// <param name="clientId">Client id for authentication</param>
        /// <param name="certificateThumbprint">Certificte thumbprint for authentication</param>
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

        /// <summary>
        /// Initialize Cosmos DB container to store wrapped DEKs
        /// </summary>
        /// <param name="dekStorageDatabase">DEK storage database</param>
        /// <param name="dekStorageContainerId">DEK storage container id</param>
        /// <returns>A task to await on.</returns>
        public Task InitializeAsync(
            Database dekStorageDatabase,
            string dekStorageContainerId)
        {
            return this.cosmosDekProvider.InitializeAsync(
                dekStorageDatabase,
                dekStorageContainerId);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override void Dispose()
        {
            if (!this.isDisposed)
            {
                this.cosmosEncryptor.Dispose();
                this.cosmosDekProvider.Dispose();
                this.isDisposed = true;
            }
        }
    }
}
