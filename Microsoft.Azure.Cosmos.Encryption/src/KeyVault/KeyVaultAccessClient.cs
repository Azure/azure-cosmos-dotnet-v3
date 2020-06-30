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
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Newtonsoft.Json;

    /// <summary>
    /// Implementation of <see cref="IKeyVaultAccessClient"/> that uses the
    /// <see cref="KeyVaultAccessClient"/> client.
    /// </summary>
    internal sealed class KeyVaultAccessClient : IKeyVaultAccessClient
    {
        private const string HttpConstantsHttpHeadersAccept = "Accept";
        private const string RuntimeConstantsMediaTypesJson = "application/json";

        private string aadLoginUrl;
        private string keyVaultResourceEndpoint;

        private readonly TimeSpan aadRetryInterval;
        private readonly int aadRetryCount;

        private readonly ClientAssertionCertificate clientAssertionCertificate;
        // cache the toke provider and Token based on KeyVault URI and Directory/Tenant ID
        private readonly AsyncCache<string, IAADTokenProvider> aadTokenProvider;
        private readonly AsyncCache<IAADTokenProvider, string> aadTokenCache;
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
            this.clientAssertionCertificate = new ClientAssertionCertificate(clientId, certificate);
            this.aadTokenProvider = new AsyncCache<string, IAADTokenProvider>();
            this.aadTokenCache = new AsyncCache<IAADTokenProvider, string>();
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

            if (getKeyResponse != null)
            {
                string keyDeletionRecoveryLevel = getKeyResponse?.Attributes?.RecoveryLevel;
                DefaultTrace.TraceInformation("ValidatePurgeProtectionAndSoftDeleteSettingsAsync: KeyVaultKey {0} has Deletion Recovery Level {1}.",
                keyVaultKeyUri,
                keyDeletionRecoveryLevel);

                return keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.Recoverable)
                        || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.RecoverableProtectedSubscription)
                        || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.CustomizedRecoverable)
                        || keyDeletionRecoveryLevel.Contains(KeyVaultConstants.DeletionRecoveryLevel.CustomizedRecoverableProtectedSubscription);
            }
            else
            {
                DefaultTrace.TraceInformation("ValidatePurgeProtectionAndSoftDeleteSettingsAsync: caught exception while trying to GetKeyVaultKeyResponseAsync");
                throw new KeyVaultAccessException(
                    HttpStatusCode.ServiceUnavailable,
                    KeyVaultErrorCode.KeyVaultServiceUnavailable,
                    "GetKeyVaultKeyResponseAsync failed with bad HTTP response");
            }
            
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
            if (!KeyVaultAccessClient.ValidateKeyVaultKeyUrl(keyVaultKeyUri))
            {
                throw new KeyVaultAccessException(HttpStatusCode.BadRequest, KeyVaultErrorCode.InvalidKeyVaultKeyURI, "Invalid KeyVaultKeyURI");
            }

            if (!KeyVaultAccessClient.ValidateBase64Encoding(bytesInBase64))
            {
                throw new KeyVaultAccessException(HttpStatusCode.BadRequest, KeyVaultErrorCode.InvalidInputBytes, "The Input is not a valid base 64 string");
            }

            string accessToken = await this.GetAadAccessTokenAsync(keyVaultKeyUri, cancellationToken);

            InternalWrapUnwrapResponse IWUresponse = await this.ExecuteKeyVaultRequestAsync(keyvaultUri, accessToken, bytesInBase64, cancellationToken);

            if (IWUresponse != null)
            {
                return IWUresponse;
            }
            else
            {
                DefaultTrace.TraceInformation("InternalWrapUnwrapAsync: caught exception while trying to ExecuteKeyVaultRequestAsync");
                throw new KeyVaultAccessException(
                    HttpStatusCode.ServiceUnavailable,
                    KeyVaultErrorCode.KeyVaultServiceUnavailable,
                    "ExecuteKeyVaultRequestAsync failed with bad HTTP response");
            }

        }
        /// <summary>
        /// Helper Method for ExecuteKeyVaultRequestAsync.
        /// </summary>
        private async Task<InternalWrapUnwrapResponse> ExecuteKeyVaultRequestAsync(
            string keyvaultUri,
            string accessToken,
            string bytesInBase64,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using HttpResponseMessage response = await this.keyvaulthttpclient.ExecuteHttpRequestAsync(HttpMethod.Post,keyvaultUri, accessToken:accessToken, bytesInBase64:bytesInBase64, cancellationToken:cancellationToken);
            {
                
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    jsonResponse = string.IsNullOrEmpty(jsonResponse) ? string.Empty : jsonResponse;
                    return JsonConvert.DeserializeObject<InternalWrapUnwrapResponse>(jsonResponse);
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    DefaultTrace.TraceWarning($"ExecuteKeyVaultRequestAsync: Receive HttpStatusCode {response.StatusCode}, KeyVaultErrorCode {KeyVaultErrorCode.KeyVaultAuthenticationFailure}.");
                    throw new KeyVaultAccessException(
                        response.StatusCode,
                        KeyVaultErrorCode.KeyVaultAuthenticationFailure,
                        "ExecuteKeyVaultRequestAsync Failed with HTTP status Forbidden 403");
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    DefaultTrace.TraceWarning($"ExecuteKeyVaultRequestAsync: Receive HttpStatusCode {response.StatusCode}, KeyVaultErrorCode {KeyVaultErrorCode.KeyVaultKeyNotFound}.");
                    throw new KeyVaultAccessException(
                        response.StatusCode,
                        KeyVaultErrorCode.KeyVaultKeyNotFound,
                        "ExecuteKeyVaultRequestAsync Failed with HTTP status NotFound 404");
                }
                else if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    DefaultTrace.TraceWarning($"ExecuteKeyVaultRequestAsync: Receive HttpStatusCode {response.StatusCode}, KeyVaultErrorCode {KeyVaultErrorCode.KeyVaultWrapUnwrapFailure}.");
                    throw new KeyVaultAccessException(
                        response.StatusCode,
                        KeyVaultErrorCode.KeyVaultWrapUnwrapFailure,
                        "ExecuteKeyVaultRequestAsync Failed with HTTP status BadRequest 400");
                }
                else if (response.StatusCode != HttpStatusCode.OK)
                {
                    DefaultTrace.TraceWarning($"ExecuteKeyVaultRequestAsync: Receive HttpStatusCode { response.StatusCode}, KeyVaultErrorCode {KeyVaultErrorCode.KeyVaultInternalServerError}");
                    throw new KeyVaultAccessException(
                        response.StatusCode,
                        KeyVaultErrorCode.KeyVaultInternalServerError,
                        "ExecuteKeyVaultRequestAsync Failed with Bad HTTP response.");
                }
                else
                {
                    /// when all else fails unlikely
                    return null;
                }                                
            }
        }

        private async Task<InternalGetKeyResponse> GetKeyVaultKeyResponseAsync(
            Uri keyVaultKeyUri,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string accessToken = await this.GetAadAccessTokenAsync(keyVaultKeyUri, cancellationToken);

            using HttpResponseMessage response = await this.InternalGetKeyAsync(keyVaultKeyUri, accessToken, cancellationToken);
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    jsonResponse = string.IsNullOrEmpty(jsonResponse) ? string.Empty : jsonResponse;
                    return JsonConvert.DeserializeObject<InternalGetKeyResponse>(jsonResponse);
                }
                else if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    DefaultTrace.TraceWarning($"GetKeyResponseAsync: Receive HttpStatusCode {response.StatusCode}, KeyVaultErrorCode {KeyVaultErrorCode.KeyVaultAuthenticationFailure}.");
                    throw new KeyVaultAccessException(
                        response.StatusCode,
                        KeyVaultErrorCode.KeyVaultAuthenticationFailure,
                        "GetKeyVaultKeyResponseAsync Failed with HTTP status Forbidden 403");
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    DefaultTrace.TraceWarning($"GetKeyResponseAsync: Receive HttpStatusCode {response.StatusCode}, KeyVaultErrorCode {KeyVaultErrorCode.KeyVaultKeyNotFound}.");
                    throw new KeyVaultAccessException(
                        response.StatusCode,
                        KeyVaultErrorCode.KeyVaultKeyNotFound,
                        "GetKeyVaultKeyResponseAsync Failed with HTTP status NotFound 404");
                }
                else if (response.StatusCode != HttpStatusCode.OK)
                {
                    DefaultTrace.TraceWarning($"GetKeyResponseAsync: Receive HttpStatusCode {response.StatusCode}, KeyVaultErrorCode {KeyVaultErrorCode.KeyVaultInternalServerError}.");
                    throw new KeyVaultAccessException(
                        response.StatusCode,
                        KeyVaultErrorCode.KeyVaultInternalServerError,
                        "GetKeyVaultKeyResponseAsync Failed with Bad HTTP response.");
                }
                else
                {
                    /// when all else fails unlikely
                    return null;
                }
            }
        }

        /// <summary>
        /// Get The Key Information like public key and recovery level.
        /// </summary>
        private async Task<HttpResponseMessage> InternalGetKeyAsync(
            Uri keyVaultKeyUri,
            string accessToken,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return await this.keyvaulthttpclient.ExecuteHttpRequestAsync(HttpMethod.Get,keyVaultKeyUri.ToString(), accessToken:accessToken, cancellationToken:cancellationToken);

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
                        this.aadLoginUrl,
                        this.keyVaultResourceEndpoint,
                        this.clientAssertionCertificate,
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
            catch (AdalException ex)
            {
                DefaultTrace.TraceInformation("GetAadAccessTokenAsync: caught exception while trying to acquire token: {0}.", ex.ToString());
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