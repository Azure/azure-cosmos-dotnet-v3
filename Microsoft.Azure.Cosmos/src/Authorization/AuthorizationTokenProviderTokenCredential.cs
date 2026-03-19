//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Threading;
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

        // Cache the URL-encoded AAD authorization signature to avoid redundant
        // HttpUtility.UrlEncode calls for the same token. The AAD token is refreshed
        // in the background every ~30 minutes, but GenerateAadAuthorizationSignature
        // is called on every Cosmos DB request. Caching eliminates per-request
        // URL-encoding overhead (~8.9μs and 6KB allocation per call).
        private string cachedAadToken;
        private string cachedAadAuthorizationSignature;

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
                string token = this.GetOrCreateAadAuthorizationSignature(
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
            return this.GetOrCreateAadAuthorizationSignature(
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
                string token = this.GetOrCreateAadAuthorizationSignature(
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

        internal string GetOrCreateAadAuthorizationSignature(string aadToken)
        {
            string currentCachedToken = Volatile.Read(ref this.cachedAadToken);
            if (ReferenceEquals(aadToken, currentCachedToken))
            {
                return Volatile.Read(ref this.cachedAadAuthorizationSignature);
            }

            string signature = AuthorizationTokenProviderTokenCredential.GenerateAadAuthorizationSignature(aadToken);
            Volatile.Write(ref this.cachedAadAuthorizationSignature, signature);
            Volatile.Write(ref this.cachedAadToken, aadToken);
            return signature;
        }

        public override void Dispose()
        {
            if (!this.isDisposed)
            {
                this.isDisposed = true;
                this.tokenCredentialCache.Dispose();
            }
        }
    }
}
