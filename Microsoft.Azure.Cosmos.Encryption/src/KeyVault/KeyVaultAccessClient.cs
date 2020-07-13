//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography.X509Certificates;    
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using global::Azure;
    using global::Azure.Security.KeyVault.Keys.Cryptography;
    using global::Azure.Identity;
    using global::Azure.Security.KeyVault.Keys;

    /// <summary>
    /// Implementation of <see cref="IKeyVaultAccessClient"/> that uses the
    /// <see cref="KeyVaultAccessClient"/> client.
    /// </summary>
    internal sealed class KeyVaultAccessClient : IKeyVaultAccessClient
    {
        private string aadLoginUrl;
        private string keyVaultResourceEndpoint;
        private readonly string clientId;
        private readonly X509Certificate2 certificate;
        private readonly TimeSpan aadRetryInterval;
        private readonly int aadRetryCount;
        private  ClientCertificateCredential clientCertificateCredential;
        // cache the toke provider and Token based on KeyVault URI and Directory/Tenant ID
        private readonly AsyncCache<string, IAADTokenProvider> aadTokenProvider;
        private readonly AsyncCache<IAADTokenProvider, string> aadTokenCache;
        private readonly AsyncCache<string, KeyClient> akvClientCache;
        private readonly AsyncCache<Uri, CryptographyClient> akvCrytpoClientCache;
        private readonly AsyncCache<Uri, string> EndPointCache;
        private readonly KeyVaultHttpClient  keyvaulthttpclient;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeyVaultAccessClient"/> class.
        /// </summary>
        /// <param name="clientId">AAD client id or service principle id.</param>
        /// <param name="certificate">Authorization Certificate to authorize with AAD.</param>
        internal KeyVaultAccessClient(
            string clientId,
            X509Certificate2 certificate,
            HttpClient httpClient,
            TimeSpan aadRetryInterval,
            int aadRetryCount)
        {
            this.certificate = certificate;
            this.clientId = clientId;
            this.aadTokenProvider = new AsyncCache<string, IAADTokenProvider>();
            this.aadTokenCache = new AsyncCache<IAADTokenProvider, string>();
            this.akvClientCache = new AsyncCache<string, KeyClient>();
            this.akvCrytpoClientCache = new AsyncCache<Uri, CryptographyClient>();
            this.EndPointCache = new AsyncCache<Uri, string>();
            this.keyvaulthttpclient = new KeyVaultHttpClient(httpClient);
            this.aadRetryInterval = aadRetryInterval;
            this.aadRetryCount = aadRetryCount;
        }

        /// <summary>
        /// Unwrap the encrypted Key.
        /// Only supports encrypted bytes in base64 format.
        /// </summary>
        /// <param name="keyVaultKeyUri">Sample Format: https://{keyvault-name}.vault.azure.net/keys/{key-name}/{key-version}, the /{key-version} is optional.</param>
        /// <param name="bytesInBase64">encrypted bytes encoded to base64 string. </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result including KeyIdentifier and decrypted bytes in base64 string format, can be convert to bytes using Convert.FromBase64String().</returns>
        public override async Task<KeyVaultUnwrapResult> UnwrapKeyAsync(
            string bytesInBase64,
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string keyvaultUri = keyVaultKeyUri + KeyVaultConstants.UnwrapKeySegment;
            InternalWrapUnwrapResponse internalWrapUnwrapResponse = await this.InternalWrapUnwrapAsync(keyVaultKeyUri, keyvaultUri, bytesInBase64, cancellationToken);
            
            string responseBytesInBase64 = KeyVaultAccessClient.ConvertBase64UrlToBase64String(internalWrapUnwrapResponse.Value);
            Uri responseKeyVaultKeyUri = new Uri(internalWrapUnwrapResponse.Kid);

            return new KeyVaultUnwrapResult(
                unwrappedKeyBytesInBase64: responseBytesInBase64,
                keyVaultKeyUri: responseKeyVaultKeyUri);
            
        }

        /// <summary>
        /// Wrap the Key with latest Key version. 
        /// Only supports bytes in base64 format.
        /// </summary>
        /// <param name="bytesInBase64">bytes encoded to base64 string. E.g. Convert.ToBase64String(bytes) .</param>
        /// <param name="keyVaultKeyUri">Sample Format: https://{keyvault-name}.vault.azure.net/keys/{key-name}/{key-version}, the /{key-version} is optional.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result including KeyIdentifier and encrypted bytes in base64 string format.</returns>
        public override async Task<KeyVaultWrapResult> WrapKeyAsync(
            string bytesInBase64,
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string keyvaultUri = keyVaultKeyUri + KeyVaultConstants.WrapKeySegment;
            InternalWrapUnwrapResponse internalWrapUnwrapResponse = await this.InternalWrapUnwrapAsync(keyVaultKeyUri, keyvaultUri, bytesInBase64, cancellationToken);

            string responseBytesInBase64 = KeyVaultAccessClient.ConvertBase64UrlToBase64String(internalWrapUnwrapResponse.Value);
            Uri responseKeyVaultKeyUri = new Uri(internalWrapUnwrapResponse.Kid);

            return new KeyVaultWrapResult(
                wrappedKeyBytesInBase64: responseBytesInBase64,
                keyVaultKeyUri: responseKeyVaultKeyUri);
                       
        }

        /// <summary>
        /// Validate the Purge Protection AndSoft Delete Settings.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Whether The Customer has the correct Deletion Level. </returns>
        public override async Task<bool> ValidatePurgeProtectionAndSoftDeleteSettingsAsync(
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            
           InternalGetKeyResponse getKeyResponse = await this.GetKeyVaultKeyResponseAsync(
                                                    keyVaultKeyUri,
                                                    cancellationToken);

            string keyDeletionRecoveryLevel = getKeyResponse?.Properties?.RecoveryLevel;
            DefaultTrace.TraceInformation("ValidatePurgeProtectionAndSoftDeleteSettingsAsync: KeyVaultKey {0} has Deletion Recovery Level {1}.",
            keyVaultKeyUri,
            keyDeletionRecoveryLevel);

            return keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.Recoverable)
                    || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.RecoverableProtectedSubscription)
                    || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.CustomizedRecoverable)
                    || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.CustomizedRecoverableProtectedSubscription);

        }

        /// <summary>
        /// Internal Processing of Wrap/UnWrap requests.
        /// </summary>
        private async Task<InternalWrapUnwrapResponse> InternalWrapUnwrapAsync(
            Uri keyVaultKeyUri,
            string keyvaultUri,
            string bytesInBase64,
            CancellationToken cancellationToken)
        {
            CryptographyClient cryptoClient;

            if (!KeyVaultAccessClient.ValidateKeyVaultKeyUrl(keyVaultKeyUri))
            {
                throw new KeyVaultAccessException(HttpStatusCode.BadRequest, KeyVaultErrorCode.InvalidKeyVaultKeyURI, "Invalid KeyVaultKeyURI");
            }

            if (!KeyVaultAccessClient.ValidateBase64Encoding(bytesInBase64))
            {
                throw new KeyVaultAccessException(HttpStatusCode.BadRequest, KeyVaultErrorCode.InvalidInputBytes, "The Input is not a valid base 64 string");
            }

            ///Get a Crypto Client for Wrap and UnWrap,this gets init per Key ID
            ///Cache it against the Unit KeyUri
            try
            {
                cryptoClient = await this.akvCrytpoClientCache.GetAsync(
                key: keyVaultKeyUri,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    await Task.FromResult(true);
                    cryptoClient = new CryptographyClient(keyVaultKeyUri, this.clientCertificateCredential);
                    return cryptoClient;
                },
                cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("InternalWrapUnwrapAsync: caught exception while trying to acquire token: {0}.", ex.ToString());
                throw new KeyVaultAccessException(HttpStatusCode.ServiceUnavailable, KeyVaultErrorCode.AadServiceUnavailable, ex.ToString());
            }

            return await this.ExecuteKeyVaultRequestAsync(keyvaultUri, cryptoClient, bytesInBase64, cancellationToken);
        }

        /// <summary>
        /// Helper Method for ExecuteKeyVaultRequestAsync.
        /// </summary>
        private async Task<InternalWrapUnwrapResponse> ExecuteKeyVaultRequestAsync(
            string keyvaultUri,
            CryptographyClient cryptoClient,
            string bytesInBase64,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string operationtype = keyvaultUri.Substring(keyvaultUri.LastIndexOf('/') + 1);
            Byte[] key = Convert.FromBase64String(bytesInBase64);

            if(cryptoClient == null)
            {
                DefaultTrace.TraceWarning("ExecuteKeyVaultRequestAsync: CryptoClient not initialized");
                throw new KeyVaultAccessException(
                            HttpStatusCode.BadRequest,
                            KeyVaultErrorCode.KeyVaultWrapUnwrapFailure,
                            "ExecuteKeyVaultRequestAsync Failed with HTTP status BadRequest 400");
            }
            /// gets called from InternalKeyWrapUnwrap requests for either wrapkey or unwrap key
            try
            {
                if (string.Equals(operationtype, "wrapkey"))
                {
                    WrapResult keyOpResult;
                    keyOpResult = await cryptoClient.WrapKeyAsync(KeyVaultConstants.RsaOaep256, key, cancellationToken);
                    return new InternalWrapUnwrapResponse(keyOpResult.KeyId, Convert.ToBase64String(keyOpResult.EncryptedKey));
                }
                else
                {
                    UnwrapResult keyOpResult;
                    keyOpResult = await cryptoClient.UnwrapKeyAsync(KeyVaultConstants.RsaOaep256, key, cancellationToken);
                    return new InternalWrapUnwrapResponse(keyOpResult.KeyId, Convert.ToBase64String(keyOpResult.Key));
                }               
            }
            catch (Exception ex)
            {
                if (ex is ArgumentException || ex is ArgumentNullException || ex is RequestFailedException)
                {
                    throw new KeyVaultAccessException(
                            HttpStatusCode.BadRequest,
                            KeyVaultErrorCode.KeyVaultWrapUnwrapFailure,
                            "ExecuteKeyVaultRequestAsync Failed with HTTP status BadRequest 400");
                }
                throw;
            }
        }        

        private async Task<InternalGetKeyResponse> GetKeyVaultKeyResponseAsync(
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await this.InternalGetKeyAsync(keyVaultKeyUri, cancellationToken);
            
        }

        /// <summary>
        /// Get The Key Information like public key and recovery level.
        /// </summary>
        private async Task<InternalGetKeyResponse> InternalGetKeyAsync(
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string[] source = keyVaultKeyUri.ToString().Split('/');
            string keyname = source.ElementAt(4);

            KeyClient akvClient = null;
            KeyVaultKey kvk = null;

            try
            {
                akvClient = await this.GetAkvClient(keyVaultKeyUri, cancellationToken);
                /// we would probably have caught it by now.
                if (akvClient != null)
                {
                    kvk = await akvClient.GetKeyAsync(keyname, cancellationToken: cancellationToken);
                    goto Success;
                }
                else
                {
                    DefaultTrace.TraceWarning("InternalGetKeyAsync: Key Vault Client not initialized");
                    throw new KeyVaultAccessException(
                            HttpStatusCode.BadRequest,
                            KeyVaultErrorCode.KeyVaultServiceUnavailable,
                            "InternalGetKeyAsync Failed with HTTP status BadRequest 400");

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

            Success:
            return new InternalGetKeyResponse(kvk.Key, kvk.Properties);
        }

        /// <summary>
        /// Obtain the AAD Token to be later used to access KeyVault.
        /// </summary>
        /// <returns>AAD Bearer Token. </returns>
        private async Task<string> GetAadAccessTokenAsync(
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string tenantid = await this.EndPointCache.GetAsync(
                key: keyVaultKeyUri,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    await this.InitializeLoginUrlAndResourceEndpointAsync(keyVaultKeyUri, cancellationToken);
                    return this.aadLoginUrl.Substring(this.aadLoginUrl.LastIndexOf('/') + 1);
                },
                cancellationToken: cancellationToken);
            
            IAADTokenProvider aadTokenProvider = await this.aadTokenProvider.GetAsync(
                key: tenantid,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    await Task.FromResult(true);
                    return new AADTokenProvider(
                        this.keyVaultResourceEndpoint,
                        this.clientCertificateCredential,
                        this.aadRetryInterval,
                        this.aadRetryCount);
                },
                cancellationToken: cancellationToken);

            try
            {
                string cacheToken = await this.aadTokenCache.GetAsync(
                key: aadTokenProvider,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    return await aadTokenProvider.GetAccessTokenAsync(cancellationToken);
                },
                cancellationToken: cancellationToken);

                return cacheToken;
            }
            catch (AuthenticationFailedException ex)
            {
                DefaultTrace.TraceInformation("GetAadAccessTokenAsync: Caught exception while trying to acquire token: {0}.", ex.ToString());
                throw new KeyVaultAccessException(HttpStatusCode.ServiceUnavailable, KeyVaultErrorCode.AadServiceUnavailable, ex.ToString());
            }            
        }

        /// <summary>
        /// Obtains the AAD Token to be used to get a KeyVault Client,caches the Client for each Tenant.
        /// </summary>
        /// <returns>AAD Bearer Token. </returns>
        private async Task<KeyClient> GetAkvClient(Uri keyVaultKeyUri,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string[] source = keyVaultKeyUri.ToString().Split('/');
            string KeyVaultName = source.ElementAt(2);
            Uri keyvaulturi = new Uri($"https://{KeyVaultName}/");

            string tenantid = await this.EndPointCache.GetAsync(
                key: keyVaultKeyUri,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    await this.InitializeLoginUrlAndResourceEndpointAsync(keyVaultKeyUri, cancellationToken);
                    return this.aadLoginUrl.Substring(this.aadLoginUrl.LastIndexOf('/') + 1);
                },
                cancellationToken: cancellationToken);

            IAADTokenProvider aadTokenProvider = await this.aadTokenProvider.GetAsync(
                key: tenantid,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    await Task.FromResult(true);
                    return new AADTokenProvider(
                        this.keyVaultResourceEndpoint,
                        this.clientCertificateCredential,
                        this.aadRetryInterval,
                        this.aadRetryCount);
                },
                cancellationToken: cancellationToken);

            try
            {
                KeyClient akvClient = await this.akvClientCache.GetAsync(
                key: KeyVaultName,
                obsoleteValue: null,
                singleValueInitFunc: async () =>
                {
                    await Task.FromResult(true);
                    return new KeyClient(keyvaulturi, this.clientCertificateCredential);
                },
                cancellationToken: cancellationToken);

                return akvClient;
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceInformation("GetAkvClient: Caught exception while trying to acquire token: {0}.", ex.ToString());
                throw new KeyVaultAccessException(HttpStatusCode.ServiceUnavailable, KeyVaultErrorCode.AadServiceUnavailable, ex.ToString());
            }
        }

        /// <summary>
        /// Initialize the LoginUrl and ResourceEndpoint.
        /// The SDK will send GET request to the key vault url in order to retrieve the AAD authority and resource.
        /// </summary>
        private async Task InitializeLoginUrlAndResourceEndpointAsync(Uri keyVaultKeyUri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using HttpResponseMessage response = await this.keyvaulthttpclient.ExecuteHttpRequestAsync(HttpMethod.Get, keyVaultKeyUri.ToString(), cancellationToken:cancellationToken);
            {
                // authenticationHeaderValue Sample:
                // Bearer authorization="https://login.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47", resource="https://vault.azure.net"
                AuthenticationHeaderValue authenticationHeaderValue = response.Headers.WwwAuthenticate.Single();

                string[] source = authenticationHeaderValue.Parameter.Split('=', ',');
                               
                try
                {
                    // Sample aadLoginUrl: https://login.windows.net/72f988bf-86f1-41af-91ab-2d7cd011db47
                    this.aadLoginUrl = source.ElementAt(1).Trim('"');

                    this.clientCertificateCredential = new ClientCertificateCredential(this.aadLoginUrl.Substring(this.aadLoginUrl.LastIndexOf('/') + 1), this.clientId,this.certificate);

                    // Sample keyVaultResourceEndpoint: https://vault.azure.net
                    this.keyVaultResourceEndpoint = source.ElementAt(3).Trim('"');
                }
                catch(ArgumentOutOfRangeException ex)
                {
                    DefaultTrace.TraceWarning("InitializeLoginUrlAndResourceEndpointAsync: Caught Out of Range ex {0}", ex.ToString());
                }
                                
            }
        }

        /// <summary>
        /// Convert base64 url to base64 string.
        /// </summary>
        private static string ConvertBase64UrlToBase64String(string str)
        {
            string base64EncodedValue = str.Replace('-', '+').Replace('_', '/');

            int count = 3 - ((str.Length + 3) % 4);

            for (int ich = 0; ich < count; ich++)
            {
                base64EncodedValue += "=";
            }

            return base64EncodedValue;
        }

        private static bool ValidateKeyVaultKeyUrl(Uri keyVaultKeyUri)
        {
            string[] segments = keyVaultKeyUri.Segments;
            return (segments.Length == 3 || segments.Length == 4) &&
                string.Equals(segments[1], KeyVaultConstants.KeysSegment, StringComparison.InvariantCultureIgnoreCase);
        }

        private static bool ValidateBase64Encoding(string bytesInBase64)
        {
            if (bytesInBase64 == null || bytesInBase64.Length % 4 != 0)
            {
                return false;
            }

            try
            {
                Convert.FromBase64String(bytesInBase64);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static Uri GetKeyVaultKeyUrlWithNoKeyVersion(Uri keyVaultKeyUri)
        {
            string[] segments = keyVaultKeyUri.Segments;
            if (segments.Length == 3)
            {
                return keyVaultKeyUri;
            }

            string[] newSegments = keyVaultKeyUri.Segments.Take(keyVaultKeyUri.Segments.Length - 1).ToArray();
            newSegments[newSegments.Length - 1] = newSegments[newSegments.Length - 1].TrimEnd('/');

            UriBuilder uriBuilder = new UriBuilder(keyVaultKeyUri);
            uriBuilder.Path = string.Concat(newSegments);
            return uriBuilder.Uri;
        }
    }
}