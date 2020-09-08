//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides functionality to wrap (encrypt) and unwrap (decrypt) data encryption keys using master keys stored in Azure Key Vault.
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
        /// Amount of time the unencrypted form of the data encryption key can be cached on the client before <see cref="UnwrapKeyAsync"/> needs to be called again.
        public AzureKeyVaultKeyWrapProvider(KeyVaultTokenCredentialFactory keyVaultTokenCredentialFactory)
        {
            this.keyVaultAccessClient = new KeyVaultAccessClient(keyVaultTokenCredentialFactory);
            this.rawDekCacheTimeToLive = TimeSpan.FromHours(1);
        }

        /// <summary>
        /// Creates a new instance of a provider to wrap (encrypt) and unwrap (decrypt) data encryption keys using master keys stored in Azure Key Vault.
        /// </summary>
        /// <param name="keyVaultTokenCredentialFactory"> KeyVaultTokenCredentialFactory instance </param>
        /// <param name="keyClientFactory"> KeyClient Factory Methods </param>
        /// <param name="cryptographyClientFactory"> CryptographyClient Factory Methods </param>
        internal AzureKeyVaultKeyWrapProvider(KeyVaultTokenCredentialFactory keyVaultTokenCredentialFactory, KeyClientFactory keyClientFactory, CryptographyClientFactory cryptographyClientFactory)
        {
            this.keyVaultAccessClient = new KeyVaultAccessClient(keyVaultTokenCredentialFactory, keyClientFactory, cryptographyClientFactory);
            this.rawDekCacheTimeToLive = TimeSpan.FromHours(1);
        }

        /// <inheritdoc />
        public override async Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
            byte[] wrappedKey,
            EncryptionKeyWrapMetadata metadata,
            RequestOptions requestOptions,
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

            if (!KeyVaultKeyUriProperties.TryParse(new Uri(metadata.Value), out KeyVaultKeyUriProperties keyVaultUriProperties))
            {
                throw new ArgumentException("KeyVault Key Uri {0} is invalid.", metadata.Value);
            }

            byte[] result = await this.keyVaultAccessClient.UnwrapKeyAsync(wrappedKey, keyVaultUriProperties, cancellationToken);
            return new EncryptionKeyUnwrapResult(result, this.rawDekCacheTimeToLive);
        }

        /// <inheritdoc />
        public override async Task<EncryptionKeyWrapResult> WrapKeyAsync(
            byte[] key,
            EncryptionKeyWrapMetadata metadata,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (metadata.Type != AzureKeyVaultKeyWrapMetadata.TypeConstant)
            {
                throw new ArgumentException("Invalid metadata", nameof(metadata));
            }

            if (!KeyVaultKeyUriProperties.TryParse(new Uri(metadata.Value), out KeyVaultKeyUriProperties keyVaultUriProperties))
            {
                throw new ArgumentException("KeyVault Key Uri {0} is invalid.", metadata.Value);
            }

            if (!await this.keyVaultAccessClient.ValidatePurgeProtectionAndSoftDeleteSettingsAsync(keyVaultUriProperties, cancellationToken))
            {
                throw new ArgumentException(string.Format("Key Vault {0} provided must have soft delete and purge protection enabled.", keyVaultUriProperties.KeyUri));
            }

            byte[] result = await this.keyVaultAccessClient.WrapKeyAsync(key, keyVaultUriProperties, cancellationToken);
            EncryptionKeyWrapMetadata responseMetadata = new EncryptionKeyWrapMetadata(metadata.Type, metadata.Value, KeyVaultConstants.RsaOaep256);
            return new EncryptionKeyWrapResult(result, responseMetadata);
        }
    }
}
