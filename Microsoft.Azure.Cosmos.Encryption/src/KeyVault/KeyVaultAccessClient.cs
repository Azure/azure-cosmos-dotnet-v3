//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using global::Azure.Core;
    using global::Azure.Security.KeyVault.Keys;
    using global::Azure.Security.KeyVault.Keys.Cryptography;
    using Microsoft.Azure.Cosmos.Common;

    /// <summary>
    /// Implements Core KeyVault access methods that uses the
    /// <see cref="KeyVaultAccessClient"/> client.
    /// </summary>
    internal sealed class KeyVaultAccessClient
    {
        private readonly AsyncCache<Uri, KeyClient> akvClientCache;
        private readonly AsyncCache<Uri, CryptographyClient> akvCryptoClientCache;
        private readonly KeyVaultTokenCredentialFactory keyVaultTokenCredentialFactory;
        private readonly KeyClientFactory keyClientFactory;
        private readonly CryptographyClientFactory cryptographyClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyVaultAccessClient"/> class.
        /// </summary>
        /// <param name="keyVaultTokenCredentialFactory"> TokenCredentials </param>
        public KeyVaultAccessClient(KeyVaultTokenCredentialFactory keyVaultTokenCredentialFactory)
        {
            this.keyVaultTokenCredentialFactory = keyVaultTokenCredentialFactory;
            this.akvClientCache = new AsyncCache<Uri, KeyClient>();
            this.akvCryptoClientCache = new AsyncCache<Uri, CryptographyClient>();
            this.keyClientFactory = new KeyClientFactory();
            this.cryptographyClientFactory = new CryptographyClientFactory();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyVaultAccessClient"/> class.
        /// Invokes internal Facotory Methods.
        /// </summary>
        /// <param name="keyVaultTokenCredentialFactory"> TokenCredential </param>
        /// <param name="keyClientFactory"> KeyClient Factory </param>
        /// <param name="cryptographyClientFactory"> CryptoClient Factory </param>
        internal KeyVaultAccessClient(KeyVaultTokenCredentialFactory keyVaultTokenCredentialFactory, KeyClientFactory keyClientFactory, CryptographyClientFactory cryptographyClientFactory)
        {
            this.keyVaultTokenCredentialFactory = keyVaultTokenCredentialFactory;
            this.akvClientCache = new AsyncCache<Uri, KeyClient>();
            this.akvCryptoClientCache = new AsyncCache<Uri, CryptographyClient>();
            this.keyClientFactory = keyClientFactory;
            this.cryptographyClientFactory = cryptographyClientFactory;
        }

        /// <summary>
        /// Unwrap the encrypted Key.
        /// Only supports encrypted bytes in base64 format.
        /// </summary>
        /// <param name="wrappedKey">encrypted bytes.</param>
        /// <param name="keyVaultUriProperties">Parsed key Vault Uri Properties.Properties as in sample Format: https://{keyvault-name}.vault.azure.net/keys/{key-name}/{key-version}.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result including KeyIdentifier and decrypted bytes in base64 string format, can be convert to bytes using Convert.FromBase64String().</returns>
        public async Task<byte[]> UnwrapKeyAsync(
            byte[] wrappedKey,
            KeyVaultKeyUriProperties keyVaultUriProperties,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            UnwrapResult keyOpResult;

            // Get a Crypto Client for Wrap and UnWrap,this gets init per Key ID
            CryptographyClient cryptoClient = await this.GetCryptoClientAsync(keyVaultUriProperties, cancellationToken);

            try
            {
                keyOpResult = await cryptoClient.UnwrapKeyAsync(KeyVaultConstants.RsaOaep256, wrappedKey, cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                throw new KeyVaultAccessException(
                            ex.Status,
                            ex.ErrorCode,
                            "UnwrapKeyAsync:Failed to Unwrap the encrypted key.",
                            ex);
            }

            return keyOpResult.Key;
        }

        /// <summary>
        /// Wrap the Key with latest Key version.
        /// Only supports bytes in base64 format.
        /// </summary>
        /// <param name="key">plain text key.</param>
        /// <param name="keyVaultUriProperties">Parsed key Vault Uri Properties.Properties as in sample Format: https://{keyvault-name}.vault.azure.net/keys/{key-name}/{key-version}.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result including KeyIdentifier and encrypted bytes in base64 string format.</returns>
        public async Task<byte[]> WrapKeyAsync(
            byte[] key,
            KeyVaultKeyUriProperties keyVaultUriProperties,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            WrapResult keyOpResult;

            // Get a Crypto Client for Wrap and UnWrap,this gets init per Key ID
            CryptographyClient cryptoClient = await this.GetCryptoClientAsync(keyVaultUriProperties, cancellationToken);

            try
            {
                keyOpResult = await cryptoClient.WrapKeyAsync(KeyVaultConstants.RsaOaep256, key, cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                throw new KeyVaultAccessException(
                            ex.Status,
                            ex.ErrorCode,
                            "WrapKeyAsync: Failed to Wrap the data encryption key.",
                            ex);
            }

            return keyOpResult.EncryptedKey;
        }

        /// <summary>
        /// Validate the Purge Protection AndSoft Delete Settings.
        /// </summary>
        /// <param name="keyVaultUriProperties">Parsed key Vault Uri Properties. </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether The Customer has the correct Deletion Level. </returns>
        public async Task<bool> ValidatePurgeProtectionAndSoftDeleteSettingsAsync(
            KeyVaultKeyUriProperties keyVaultUriProperties,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            KeyClient akvClient = await this.GetAkvClientAsync(keyVaultUriProperties, cancellationToken);
            try
            {
                KeyVaultKey getKeyResponse = await akvClient.GetKeyAsync(keyVaultUriProperties.KeyName, cancellationToken: cancellationToken);

                string keyDeletionRecoveryLevel = getKeyResponse?.Properties?.RecoveryLevel;

                return keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.Recoverable)
                        || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.RecoverableProtectedSubscription)
                        || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.CustomizedRecoverable)
                        || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.CustomizedRecoverableProtectedSubscription);
            }
            catch (RequestFailedException ex)
            {
                throw new KeyVaultAccessException(
                            ex.Status,
                            ex.ErrorCode,
                            "ValidatePurgeProtectionAndSoftDeleteSettingsAsync: Failed to fetch Key from Key Vault.",
                            ex);
            }
        }

        /// <summary>
        /// Obtains the KeyClient to retrieve keys from Keyvault.
        /// </summary>
        /// <param name="keyVaultUriProperties"> Parsed key Vault Uri Properties. </param>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns> Key Client </returns>
        private async Task<KeyClient> GetAkvClientAsync(
            KeyVaultKeyUriProperties keyVaultUriProperties,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Called once per KEYVALTNAME
            // Eg:https://KEYVALTNAME.vault.azure.net/
            KeyClient akvClient = await this.akvClientCache.GetAsync(
                key: keyVaultUriProperties.KeyVaultUri,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    TokenCredential tokenCred = await this.keyVaultTokenCredentialFactory.GetTokenCredentialAsync(keyVaultUriProperties.KeyUri, cancellationToken);
                    return this.keyClientFactory.GetKeyClient(keyVaultUriProperties, tokenCred);
                },
                cancellationToken: cancellationToken);

            return akvClient;
        }

        /// <summary>
        /// Obtains the Crypto Client for Wrap/UnWrap.
        /// </summary>
        /// <param name="keyVaultUriProperties"> Parsed key Vault Uri Properties. </param>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <returns> CryptographyClient </returns>
        private async Task<CryptographyClient> GetCryptoClientAsync(
            KeyVaultKeyUriProperties keyVaultUriProperties,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get a Crypto Client for Wrap and UnWrap,this gets init per Key Version
            // Cache it against the KeyVersion/KeyId
            // Eg: :https://KEYVAULTNAME.vault.azure.net/keys/keyname/KEYID
            CryptographyClient cryptoClient = await this.akvCryptoClientCache.GetAsync(
                key: keyVaultUriProperties.KeyUri,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    // we need to acquire the Client Cert Creds for cases where we directly access Crypto Services.
                    TokenCredential tokenCred = await this.keyVaultTokenCredentialFactory.GetTokenCredentialAsync(keyVaultUriProperties.KeyUri, cancellationToken);
                    return this.cryptographyClientFactory.GetCryptographyClient(keyVaultUriProperties, tokenCred);
                },
                cancellationToken: cancellationToken);
            return cryptoClient;
        }
    }
}