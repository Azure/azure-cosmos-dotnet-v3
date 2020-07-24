//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides functionality to wrap (encrypt) and unwrap (decrypt) data encryption keys using master keys stored in Azure Key Vault.
    /// Please see <see href="https://docs.microsoft.com/en-us/rest/api/azure/index#register-your-client-application-with-azure-ad">this link</see> for details on registering your application with Azure AD.
    /// The registered application must have the keys/readKey, keys/wrapKey and keys/unwrapKey permissions on the Azure Key Vaults that will be used for wrapping and unwrapping data encryption keys
    /// -Please see <see href="https://docs.microsoft.com/en-us/azure/key-vault/about-keys-secrets-and-certificates#key-access-control">this link</see> for details on this.
    /// Azure key vaults used with client side encryption for Cosmos DB need to have soft delete and purge protection enabled -
    /// Please see <see href="https://docs.microsoft.com/en-us/azure/key-vault/key-vault-ovw-soft-delete">this link</see> for details regarding the same.
    /// Unwrapped data encryption keys will be cached within the client SDK for a period of 1 hour.
    /// </summary>
    internal class AzureKeyVaultKeyWrapProvider : EncryptionKeyWrapProvider
    {
        private readonly KeyVaultAccessClient keyVaultAccessClient;
        private readonly TimeSpan rawDekCacheTimeToLive;

        /// <summary>
        /// Creates a new instance of a provider to wrap (encrypt) and unwrap (decrypt) data encryption keys using master keys stored in Azure Key Vault.
        /// </summary>
        /// <param name="keyVaultTokenCredentialFactory"> KeyVaultTokenCredentialFactory instance </param>
        /// <param name="rawDataEncryptionKeyCacheTimeToLive">
        /// Amount of time the unencrypted form of the data encryption key can be cached on the client before <see cref="UnwrapKeyAsync"/> needs to be called again.
        public AzureKeyVaultKeyWrapProvider(KeyVaultTokenCredentialFactory keyVaultTokenCredentialFactory)
        {
            this.keyVaultAccessClient = new KeyVaultAccessClient(keyVaultTokenCredentialFactory);
            this.rawDekCacheTimeToLive = TimeSpan.FromHours(1);
        }

        /// <inheritdoc />
        public override async Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
            byte[] wrappedKey,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            if (metadata.Type != AzureKeyVaultKeyWrapMetadata.TypeConstant)
            {
                throw new ArgumentException("Invalid metadata", nameof(metadata));
            }

            if (metadata.Algorithm != KeyVaultConstants.RsaOaep256)
            {
                throw new ArgumentException(
                    string.Format("Unknown encryption key wrap algorithm {0}", metadata.Algorithm),
                    nameof(metadata));
            }

            try
            {
                byte[] result = await this.keyVaultAccessClient.UnwrapKeyAsync(wrappedKey, new Uri(metadata.Value), cancellationToken);
                return new EncryptionKeyUnwrapResult(result, this.rawDekCacheTimeToLive);
            }
            catch (KeyVaultAccessException ex)
            {
                if (ex.KeyVaultErrorCode == KeyVaultErrorCode.KeyVaultKeyNotFound)
                {
                    throw new KeyNotFoundException(ex.Message, ex);
                }

                throw;
            }
        }

        /// <inheritdoc />
        public override async Task<EncryptionKeyWrapResult> WrapKeyAsync(
            byte[] key,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            if (metadata.Type != AzureKeyVaultKeyWrapMetadata.TypeConstant)
            {
                throw new ArgumentException("Invalid metadata", nameof(metadata));
            }

            try
            {
                Uri keyVaultKeyUri = new Uri(metadata.Value);
                if (!await this.keyVaultAccessClient.ValidatePurgeProtectionAndSoftDeleteSettingsAsync(keyVaultKeyUri, cancellationToken))
                {
                    throw new ArgumentException("Key Vault provided must have soft delete and purge protection enabled.");
                }

                byte[] result = await this.keyVaultAccessClient.WrapKeyAsync(key, keyVaultKeyUri, cancellationToken);
                EncryptionKeyWrapMetadata responseMetadata = new EncryptionKeyWrapMetadata(metadata.Type, metadata.Value, KeyVaultConstants.RsaOaep256);
                return new EncryptionKeyWrapResult(result, responseMetadata);
            }
            catch (KeyVaultAccessException ex)
            {
                if (ex.KeyVaultErrorCode == KeyVaultErrorCode.KeyVaultKeyNotFound)
                {
                    throw new KeyNotFoundException(ex.Message, ex);
                }

                throw;
            }
        }
    }
}
