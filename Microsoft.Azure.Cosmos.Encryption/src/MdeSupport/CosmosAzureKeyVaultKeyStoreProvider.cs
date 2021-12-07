//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Data.Encryption.AzureKeyVaultProvider;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Implementation of key encryption key store provider that allows client applications to access data when a
    /// key encryption key is stored in Microsoft Azure Key Vault.
    /// </summary>
    public sealed class CosmosAzureKeyVaultKeyStoreProvider : CosmosEncryptionKeyStoreProvider
    {
        private readonly AzureKeyVaultKeyStoreProvider azureKeyVaultKeyStoreProvider;

        private TimeSpan? dataEncryptionKeyCacheTimeToLive;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosAzureKeyVaultKeyStoreProvider"/> class.
        /// Constructor that takes an implementation of Token Credential that is capable of providing an OAuth Token.
        /// </summary>
        /// <param name="tokenCredential"> returns token credentials. </param>
        public CosmosAzureKeyVaultKeyStoreProvider(TokenCredential tokenCredential)
        {
            this.azureKeyVaultKeyStoreProvider = new AzureKeyVaultKeyStoreProvider(tokenCredential);
        }

        /// <inheritdoc/>
        public override TimeSpan? DataEncryptionKeyCacheTimeToLive
        {
            get => this.dataEncryptionKeyCacheTimeToLive;
            set => this.azureKeyVaultKeyStoreProvider.DataEncryptionKeyCacheTimeToLive = this.dataEncryptionKeyCacheTimeToLive = value;
        }

        /// <summary>
        /// Gets name of the Encryption Key Store Provider implementation.
        /// </summary>
        public override string ProviderName => this.azureKeyVaultKeyStoreProvider.ProviderName;

        /// <summary>
        /// This function uses the asymmetric key specified by the key path
        /// and decrypts an encrypted data dencryption key with RSA encryption algorithm.
        /// </summary>.
        /// <param name="encryptionKeyId">Identifier of an asymmetric key in Azure Key Vault. </param>
        /// <param name="cosmosKeyEncryptionKeyAlgorithm">The key encryption algorithm.</param>
        /// <param name="encryptedKey">The ciphertext key.</param>
        /// <returns>Plain text data encryption key. </returns>
        public override Task<byte[]> UnwrapKeyAsync(string encryptionKeyId, string cosmosKeyEncryptionKeyAlgorithm, byte[] encryptedKey)
        {
            KeyEncryptionKeyAlgorithm keyEncryptionKeyAlgorithm = cosmosKeyEncryptionKeyAlgorithm switch
            {
                CosmosKeyEncryptionKeyAlgorithm.RsaOaep => KeyEncryptionKeyAlgorithm.RSA_OAEP,
                _ => throw new NotSupportedException("The specified KeyEncryptionAlgorithm is not supported. Please refer to https://aka.ms/CosmosClientEncryption for more details. "),
            };

            return Task.FromResult(this.azureKeyVaultKeyStoreProvider.UnwrapKey(encryptionKeyId, keyEncryptionKeyAlgorithm, encryptedKey));
        }

        /// <summary>
        /// This function uses the asymmetric key specified by the key path
        /// and encrypts an unencrypted data encryption key with RSA encryption algorithm.
        /// </summary>
        /// <param name="encryptionKeyId">Identifier of an asymmetric key in Azure Key Vault. </param>
        /// <param name="cosmosKeyEncryptionKeyAlgorithm">The key encryption algorithm.</param>
        /// <param name="key">The plaintext key.</param>
        /// <returns>Encrypted data encryption key. </returns>
        public override Task<byte[]> WrapKeyAsync(string encryptionKeyId, string cosmosKeyEncryptionKeyAlgorithm, byte[] key)
        {
            KeyEncryptionKeyAlgorithm keyEncryptionKeyAlgorithm = cosmosKeyEncryptionKeyAlgorithm switch
            {
                CosmosKeyEncryptionKeyAlgorithm.RsaOaep => KeyEncryptionKeyAlgorithm.RSA_OAEP,
                _ => throw new NotSupportedException("This specified KeyEncryptionAlgorithm is not supported. Please refer to https://aka.ms/CosmosClientEncryption for more details. "),
            };

            return Task.FromResult(this.azureKeyVaultKeyStoreProvider.WrapKey(encryptionKeyId, keyEncryptionKeyAlgorithm, key));
        }
    }
}
