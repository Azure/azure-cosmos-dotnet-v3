//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal sealed class AuthorizationTokenProviderTokenCredential : AuthorizationTokenProvider
    {
        private const string InferenceTokenPrefix = "Bearer ";
        internal readonly TokenCredentialCache tokenCredentialCache;
        private bool isDisposed = false;
       
        internal readonly TokenCredential tokenCredential;

        public AuthorizationTokenProviderTokenCredential(
            TokenCredential tokenCredential,
            Uri accountEndpoint,
            TimeSpan? backgroundTokenCredentialRefreshInterval)
        {
            this.tokenCredential = tokenCredential ?? throw new ArgumentNullException(nameof(tokenCredential));
            this.tokenCredentialCache = new TokenCredentialCache(
                tokenCredential: tokenCredential,
                accountEndpoint: accountEndpoint,
                backgroundTokenCredentialRefreshInterval: backgroundTokenCredentialRefreshInterval);
        }

        public override async ValueTask<(string token, string payload)> GetUserAuthorizationAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType)
        {
            using (Trace trace = Trace.GetRootTrace(nameof(GetUserAuthorizationTokenAsync), TraceComponent.Authorization, TraceLevel.Info))
            {
                string token = AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(
                    await this.tokenCredentialCache.GetTokenAsync(trace));
                return (token, default);
            }
        }

        public override async ValueTask<string> GetUserAuthorizationTokenAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType,
            ITrace trace)
        {
            return AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(
                    await this.tokenCredentialCache.GetTokenAsync(trace));
        }

        public override async ValueTask AddAuthorizationHeaderAsync(
            INameValueCollection headersCollection,
            Uri requestAddress,
            string verb,
            AuthorizationTokenType tokenType)
        {
            using (Trace trace = Trace.GetRootTrace(nameof(GetUserAuthorizationTokenAsync), TraceComponent.Authorization, TraceLevel.Info))
            {
                string token = AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(
                    await this.tokenCredentialCache.GetTokenAsync(trace));

                headersCollection.Add(HttpConstants.HttpHeaders.Authorization, token);
            }
        }

        public override async ValueTask AddInferenceAuthorizationHeaderAsync(
            INameValueCollection headersCollection,
            Uri requestAddress,
            string verb,
            AuthorizationTokenType tokenType)
        {
            using (Trace trace = Trace.GetRootTrace(nameof(GetUserAuthorizationTokenAsync), TraceComponent.Authorization, TraceLevel.Info))
            {
                string token = await this.tokenCredentialCache.GetTokenAsync(trace);

                string inferenceToken = $"{InferenceTokenPrefix}{token}";
                headersCollection.Add(HttpConstants.HttpHeaders.Authorization, inferenceToken);
            }
        }

        public override void TraceUnauthorized(
            DocumentClientException dce,
            string authorizationToken,
            string payload)
        {
            DefaultTrace.TraceError($"Un-expected authorization for token credential. {dce.Message}");
        }

        public static string GenerateAadAuthorizationSignature(string aadToken)
        {
            return HttpUtility.UrlEncode(string.Format(
                CultureInfo.InvariantCulture,
                Constants.Properties.AuthorizationFormat,
                Constants.Properties.AadToken,
                Constants.Properties.TokenVersion,
                aadToken));
        }

        public override void Dispose()
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.tokenCredentialCache.Dispose();
            }
        }

        /// <summary>
        /// Attempts to handle AAD token revocation by checking for claims challenge.
        /// Extracts claims from WWW-Authenticate header and resets cache for retry with fresh token.
        /// </summary>
        /// <param name="statusCode">HTTP status code from the response</param>
        /// <param name="headers">Response headers containing WWW-Authenticate</param>
        /// <returns>True if claims challenge detected and request should be retried; false otherwise</returns>
        internal bool TryHandleTokenRevocation(
            HttpStatusCode statusCode,
            INameValueCollection headers)
        {
            if (statusCode != HttpStatusCode.Unauthorized || headers == null)
            {
                return false;
            }

            string wwwAuth = headers[HttpConstants.HttpHeaders.WwwAuthenticate];
            if (string.IsNullOrEmpty(wwwAuth))
            {
                return false;
            }

            // Check for claims challenge indicators
            bool hasClaimsChallenge = wwwAuth.IndexOf("insufficient_claims", StringComparison.OrdinalIgnoreCase) >= 0
                || wwwAuth.IndexOf("claims=", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!hasClaimsChallenge)
            {
                return false;
            }

            string claimsChallenge = AuthorizationTokenProviderTokenCredential.ExtractClaimsFromWwwAuthenticate(wwwAuth);

            // Reset cache with claims challenge for next token request
            this.tokenCredentialCache.ResetCachedToken(claimsChallenge);

            DefaultTrace.TraceInformation(
                "AAD token revocation detected (claims challenge present). Token cache reset. " +
                "Request will be retried with fresh token including claims. HasClaims={0}",
                claimsChallenge != null);

            return true;
        }

        /// <summary>
        /// Extracts the claims challenge from the WWW-Authenticate header value.
        /// </summary>
        /// <param name="wwwAuthenticateHeader">WWW-Authenticate header value</param>
        /// <returns>Base64-encoded claims string, or null if not present</returns>
        private static string ExtractClaimsFromWwwAuthenticate(string wwwAuthenticateHeader)
        {
            if (string.IsNullOrEmpty(wwwAuthenticateHeader))
            {
                return null;
            }

            const string claimsPrefix = "claims=\"";
            int claimsIndex = wwwAuthenticateHeader.IndexOf(claimsPrefix, StringComparison.OrdinalIgnoreCase);
            if (claimsIndex < 0)
            {
                return null;
            }

            int startIndex = claimsIndex + claimsPrefix.Length;
            int endIndex = wwwAuthenticateHeader.IndexOf("\"", startIndex, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                return null;
            }

            return wwwAuthenticateHeader.Substring(startIndex, endIndex - startIndex);
        }
    }
}
