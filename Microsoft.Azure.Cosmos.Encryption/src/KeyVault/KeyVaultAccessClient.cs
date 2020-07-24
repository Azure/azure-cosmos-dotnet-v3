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
        private readonly AsyncCache<string, KeyClient> akvClientCache;
        private readonly AsyncCache<Uri, CryptographyClient> akvCryptoClientCache;
        private readonly KeyVaultTokenCredentialFactory keyVaultTokenCredentialFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyVaultAccessClient"/> class.
        /// </summary>
        /// <param name="keyVaultTokenCredentialFactory"> TokenCredentials </param>
        internal KeyVaultAccessClient(KeyVaultTokenCredentialFactory keyVaultTokenCredentialFactory)
        {
            this.keyVaultTokenCredentialFactory = keyVaultTokenCredentialFactory;
            this.akvClientCache = new AsyncCache<string, KeyClient>();
            this.akvCryptoClientCache = new AsyncCache<Uri, CryptographyClient>();
            this.keyVaultTokenCredentialFactory = keyVaultTokenCredentialFactory;
        }

        /// <summary>
        /// Unwrap the encrypted Key.
        /// Only supports encrypted bytes in base64 format.
        /// </summary>
        /// <param name="wrappedKey">encrypted bytes.</param>
        /// <param name="keyVaultKeyUri">Sample Format: https://{keyvault-name}.vault.azure.net/keys/{key-name}/{key-version}, the /{key-version} is optional.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result including KeyIdentifier and decrypted bytes in base64 string format, can be convert to bytes using Convert.FromBase64String().</returns>
        public async Task<byte[]> UnwrapKeyAsync(
            byte[] wrappedKey,
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            UnwrapResult keyOpResult;

            if (!KeyVaultAccessClient.ValidateKeyVaultKeyUrl(keyVaultKeyUri))
            {
                throw new KeyVaultAccessException(HttpStatusCode.BadRequest, KeyVaultErrorCode.InvalidKeyVaultKeyURI, "Invalid KeyVaultKeyURI");
            }

            // Get a Crypto Client for Wrap and UnWrap,this gets init per Key ID
            CryptographyClient cryptoClient = await this.GetCryptoClientAsync(keyVaultKeyUri, cancellationToken);

            if (cryptoClient == null)
            {
                throw new KeyVaultAccessException(
                            HttpStatusCode.BadRequest,
                            KeyVaultErrorCode.KeyVaultWrapUnwrapFailure,
                            "UnwrapKeyAsync:Failed to acquire a CryptographyClient.Failed with HTTP status BadRequest 400");
            }

            try
            {
                keyOpResult = await cryptoClient.UnwrapKeyAsync(KeyVaultConstants.RsaOaep256, wrappedKey, cancellationToken);
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is ArgumentNullException || ex is RequestFailedException)
                {
                    throw new KeyVaultAccessException(
                            HttpStatusCode.BadRequest,
                            KeyVaultErrorCode.KeyVaultWrapUnwrapFailure,
                            "UnwrapKeyAsync Failed with HTTP status BadRequest 400");
                }

                throw;
            }

            return keyOpResult.Key;
        }

        /// <summary>
        /// Wrap the Key with latest Key version.
        /// Only supports bytes in base64 format.
        /// </summary>
        /// <param name="key">plain text key.</param>
        /// <param name="keyVaultKeyUri">Sample Format: https://{keyvault-name}.vault.azure.net/keys/{key-name}/{key-version}, the /{key-version} is optional.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result including KeyIdentifier and encrypted bytes in base64 string format.</returns>
        public async Task<byte[]> WrapKeyAsync(
            byte[] key,
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            WrapResult keyOpResult;

            if (!KeyVaultAccessClient.ValidateKeyVaultKeyUrl(keyVaultKeyUri))
            {
                throw new KeyVaultAccessException(HttpStatusCode.BadRequest, KeyVaultErrorCode.InvalidKeyVaultKeyURI, "Invalid KeyVaultKeyURI");
            }

            // Get a Crypto Client for Wrap and UnWrap,this gets init per Key ID
            CryptographyClient cryptoClient = await this.GetCryptoClientAsync(keyVaultKeyUri, cancellationToken);

            if (cryptoClient == null)
            {
                throw new KeyVaultAccessException(
                            HttpStatusCode.BadRequest,
                            KeyVaultErrorCode.KeyVaultWrapUnwrapFailure,
                            "WrapKeyAsync:Failed to acquire a CryptographyClient.Failed with HTTP status BadRequest 400");
            }

            try
            {
                keyOpResult = await cryptoClient.WrapKeyAsync(KeyVaultConstants.RsaOaep256, key, cancellationToken);
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is ArgumentNullException || ex is RequestFailedException)
                {
                    throw new KeyVaultAccessException(
                            HttpStatusCode.BadRequest,
                            KeyVaultErrorCode.KeyVaultWrapUnwrapFailure,
                            "WrapKeyAsync Failed with HTTP status BadRequest 400");
                }

                throw;
            }

            return keyOpResult.EncryptedKey;
        }

        /// <summary>
        /// Validate the Purge Protection AndSoft Delete Settings.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether The Customer has the correct Deletion Level. </returns>
        public async Task<bool> ValidatePurgeProtectionAndSoftDeleteSettingsAsync(
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            InternalGetKeyResponse getKeyResponse = await this.InternalGetKeyAsync(keyVaultKeyUri, cancellationToken);

            string keyDeletionRecoveryLevel = getKeyResponse?.Properties?.RecoveryLevel;

            return keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.Recoverable)
                    || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.RecoverableProtectedSubscription)
                    || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.CustomizedRecoverable)
                    || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.CustomizedRecoverableProtectedSubscription);
        }

        /// <summary>
        /// Get The Key Information like public key and recovery level.
        /// </summary>
        private async Task<InternalGetKeyResponse> InternalGetKeyAsync(
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            KeyVaultUriProperties uriparser = new KeyVaultUriProperties(keyVaultKeyUri);
            uriparser.TryParseUri();

            KeyClient akvClient = null;
            KeyVaultKey kvk = null;

            try
            {
                akvClient = await this.GetAkvClientAsync(keyVaultKeyUri, cancellationToken);

                // we would probably have caught it by now.
                if (akvClient != null)
                {
                    kvk = await akvClient.GetKeyAsync(uriparser.KeyName, cancellationToken: cancellationToken);
                }
                else
                {
                    throw new KeyVaultAccessException(
                            HttpStatusCode.BadRequest,
                            KeyVaultErrorCode.KeyVaultServiceUnavailable,
                            "InternalGetKeyAsync:Failed to acquire a KeyClient.Failed with HTTP status BadRequest 400");
                }
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is ArgumentNullException || ex is RequestFailedException)
                {
                    throw new KeyVaultAccessException(
                            HttpStatusCode.NotFound,
                            KeyVaultErrorCode.KeyVaultKeyNotFound,
                            "InternalGetKeyAsync Failed with HTTP status BadRequest 404");
                }

                throw;
            }

            return new InternalGetKeyResponse(kvk.Key, kvk.Properties);
        }

        /// <summary>
        /// Obtains the KeyClient to retrieve keys from Keyvault.
        /// </summary>
        /// <returns>KeyClient.</returns>
        private async Task<KeyClient> GetAkvClientAsync(
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            KeyVaultUriProperties uriparser = new KeyVaultUriProperties(keyVaultKeyUri);
            uriparser.TryParseUri();

            // Called once per KEYVALTNAME
            // Eg:https://KEYVALTNAME.vault.azure.net/
            try
            {
                KeyClient akvClient = await this.akvClientCache.GetAsync(
                key: uriparser.KeyValtName,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    TokenCredential tokenCred = await this.keyVaultTokenCredentialFactory.GetTokenCredentialAsync(keyVaultKeyUri, cancellationToken);
                    return new KeyClient(uriparser.KeyVaultUri, tokenCred);
                },
                cancellationToken: cancellationToken);
                return akvClient;
            }
            catch (Exception ex)
            {
                throw new KeyVaultAccessException(HttpStatusCode.ServiceUnavailable, KeyVaultErrorCode.AadServiceUnavailable, ex.ToString());
            }
        }

        /// <summary>
        /// Obtains the Crypto Client for Wrap/UnWrap.
        /// </summary>
        /// <returns>CryptographyClient client. </returns>
        private async Task<CryptographyClient> GetCryptoClientAsync(
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get a Crypto Client for Wrap and UnWrap,this gets init per KEYID
            // Cache it against the KEYID
            // Eg: :https://KEYVAULTNAME.vault.azure.net/keys/keyname/KEYID
            try
            {
                CryptographyClient cryptoClient = await this.akvCryptoClientCache.GetAsync(
                key: keyVaultKeyUri,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    // we need to acquire the Client Cert Creds for cases where we directly access Crypto Services.
                    TokenCredential tokenCred = await this.keyVaultTokenCredentialFactory.GetTokenCredentialAsync(keyVaultKeyUri, cancellationToken);
                    return new CryptographyClient(keyVaultKeyUri, tokenCred);
                },
                cancellationToken: cancellationToken);
                return cryptoClient;
            }
            catch (Exception ex)
            {
                throw new KeyVaultAccessException(HttpStatusCode.ServiceUnavailable, KeyVaultErrorCode.AadServiceUnavailable, ex.ToString());
            }
        }

        private static bool ValidateKeyVaultKeyUrl(Uri keyVaultKeyUri)
        {
            string[] segments = keyVaultKeyUri.Segments;
            return (segments.Length == 3 || segments.Length == 4) &&
                string.Equals(segments[1], KeyVaultConstants.KeysSegment, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}