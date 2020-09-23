//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Data.AAP_PH.AzureKeyVaultProvider;
    using Microsoft.Data.AAP_PH.Cryptography;

    /// <summary>
    /// Provides the default implementation for client-side encryption for Cosmos DB.
    /// Provides interfaces to use AAP and its encryption alogrithm and for initialization
    /// of EncryptionKeyStoreProvider via default AzureKeyVaultProvider.
    /// Custom KeyStore provider can be passed via CosmosDataEncryptionKeyProvider.
    /// Data Encryption Keys (which intermediate keys) are stored in a Cosmos DB container
    /// that instances of this class are initialized with after wrapping (aka encrypting)
    /// it using the Azure Key Vault key provided during the creation of each Data Encryption Key.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public sealed class AapCosmosEncryptor : Encryptor
    {
        private readonly CosmosEncryptor cosmosEncryptor;

        private readonly CosmosDataEncryptionKeyProvider cosmosDekProvider;

        /// <summary>
        /// Gets Container for data encryption keys.
        /// </summary>
        public DataEncryptionKeyContainer DataEncryptionKeyContainer => this.cosmosDekProvider.DataEncryptionKeyContainer;

        /// <summary>
        /// Gets DataEncryptionKeyProvider
        /// </summary>
        public DataEncryptionKeyProvider DataEncryptionKeyProvider { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AapCosmosEncryptor"/> class.
        /// Creates an Encryption Key Provider for wrap and unwrapping Data Encryption key via AAP's AzureKeyVaultProvider.
        /// </summary>
        /// <param name="tokenCredential"> User provided TokenCredential for accessing Key Vault services. </param>
        public AapCosmosEncryptor(TokenCredential tokenCredential)
        {
            EncryptionKeyStoreProvider encryptionKeyStoreProvider = new AzureKeyVaultProvider(tokenCredential);
            this.cosmosDekProvider = new CosmosDataEncryptionKeyProvider(encryptionKeyStoreProvider);
            this.cosmosEncryptor = new CosmosEncryptor(this.cosmosDekProvider);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AapCosmosEncryptor"/> class.
        /// Creates an Encryption Key Provider for wrap and unwrapping Data Encryption key via AAP  Key Vault.
        /// </summary>
        /// <param name="authenticationCallback"> Auth CallBack</param>
        public AapCosmosEncryptor(AzureKeyVaultProviderTokenCredential.AuthenticationCallback authenticationCallback)
        {
            EncryptionKeyStoreProvider wrapProvider = new AzureKeyVaultProvider(authenticationCallback);
            this.cosmosDekProvider = new CosmosDataEncryptionKeyProvider(wrapProvider);
            this.cosmosEncryptor = new CosmosEncryptor(this.cosmosDekProvider);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AapCosmosEncryptor"/> class.
        /// Creates an Encryption Key Provider for wrap and unwrapping Data Encryption key via EncryptionKeyStoreProvider.
        /// </summary>
        /// <param name="wrapProvider"> User provided TokenCredential for accessing Key Vault services. </param>
        public AapCosmosEncryptor(EncryptionKeyStoreProvider wrapProvider)
        {
            this.cosmosDekProvider = new CosmosDataEncryptionKeyProvider(wrapProvider);
            this.cosmosEncryptor = new CosmosEncryptor(this.cosmosDekProvider);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AapCosmosEncryptor"/> class.
        /// Creates an Encryption Key Provider for wrap and unwrapping Data Encryption key via AAP  Key Vault.
        /// </summary>
        /// <param name="cosmosDataEncryptionKeyProvider"> CosmosDataEncryptionKeyProvider </param>
        public AapCosmosEncryptor(CosmosDataEncryptionKeyProvider cosmosDataEncryptionKeyProvider)
        {
            this.cosmosDekProvider = cosmosDataEncryptionKeyProvider;
            this.cosmosEncryptor = new CosmosEncryptor(this.cosmosDekProvider);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AapCosmosEncryptor"/> class.
        /// Creates an Encryption Key Provider for wrap and unwrapping Data Encryption key via AAP  Key Vault.
        /// </summary>
        /// <param name="dataEncryptionKeyProvider"> dataEncryptionKeyProvider </param>
        public AapCosmosEncryptor(DataEncryptionKeyProvider dataEncryptionKeyProvider)
        {
            this.DataEncryptionKeyProvider = dataEncryptionKeyProvider;
            this.cosmosEncryptor = new CosmosEncryptor(this.DataEncryptionKeyProvider);
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
            return this.cosmosDekProvider.InitializeAsync(dekStorageDatabase, dekStorageContainerId);
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
    }
}
