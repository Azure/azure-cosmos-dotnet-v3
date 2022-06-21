//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Authorization
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class AzureKeyCredentialAuthorizationTokenProvider : AuthorizationTokenProvider
    {
        private readonly object refreshLock = new object();
        private readonly AzureKeyCredential azureKeyCredential;
        
        // keyObject is used to check for refresh 
        private string currentKeyObject = null;

        // Internal for unit testing
        internal AuthorizationTokenProvider authorizationTokenProvider;

        // Internal for unit testing
        internal AuthorizationTokenProvider authorizationTokenProvider;

        public AzureKeyCredentialAuthorizationTokenProvider(
                AzureKeyCredential azureKeyCredential)
        {
            this.azureKeyCredential = azureKeyCredential ?? throw new ArgumentNullException(nameof(azureKeyCredential));
            this.CheckAndRefreshTokenProvider();
        }

        public override ValueTask AddAuthorizationHeaderAsync(
                INameValueCollection headersCollection, 
                Uri requestAddress, 
                string verb, 
                AuthorizationTokenType tokenType)
        {
            this.CheckAndRefreshTokenProvider();
            return this.authorizationTokenProvider.AddAuthorizationHeaderAsync(
                headersCollection, 
                requestAddress, 
                verb, 
                tokenType);
        }

        public override void Dispose()
        {
            if (this.authorizationTokenProvider != null)
            {
                this.authorizationTokenProvider.Dispose();
                this.authorizationTokenProvider = null;
            }
        }

        public override ValueTask<(string token, string payload)> GetUserAuthorizationAsync(
                string resourceAddress, 
                string resourceType, 
                string requestVerb, 
                INameValueCollection headers, 
                AuthorizationTokenType tokenType)
        {
            this.CheckAndRefreshTokenProvider();
            return this.authorizationTokenProvider.GetUserAuthorizationAsync(
                    resourceAddress, 
                    resourceType, 
                    requestVerb, 
                    headers, 
                    tokenType);
        }

        public override ValueTask<string> GetUserAuthorizationTokenAsync(
                string resourceAddress, 
                string resourceType, 
                string requestVerb, 
                INameValueCollection headers, 
                AuthorizationTokenType tokenType, 
                ITrace trace)
        {
            this.CheckAndRefreshTokenProvider();
            return this.authorizationTokenProvider.GetUserAuthorizationTokenAsync(
                    resourceAddress, 
                    resourceType, 
                    requestVerb, 
                    headers, 
                    tokenType, 
                    trace);
        }

        public override void TraceUnauthorized(
                DocumentClientException dce, 
                string authorizationToken, 
                string payload)
        {
            this.authorizationTokenProvider.TraceUnauthorized(
                    dce, 
                    authorizationToken, 
                    payload);
        }

        private void CheckAndRefreshTokenProvider()
        {
            if (!Object.ReferenceEquals(this.currentKeyObject, this.azureKeyCredential.Key))
            {
                // Change is immediate for all new reqeust flows (pure compute work and should be very very quick)
                // With-out lock possibility of concurrent updates (== #inflight reqeust's) but eventually only one will win
                lock (this.refreshLock)
                {
                    // Process only if the authProvider is not yet exchanged
                    if (!Object.ReferenceEquals(this.currentKeyObject, this.azureKeyCredential.Key))
                    {
                        AuthorizationTokenProvider newAuthProvider = AuthorizationTokenProvider.CreateWithResourceTokenOrAuthKey(this.azureKeyCredential.Key);
                        AuthorizationTokenProvider currentAuthProvider = Interlocked.Exchange(ref this.authorizationTokenProvider, newAuthProvider);

                        AuthorizationTokenProvider toDispose = newAuthProvider; 
                        if (!Object.ReferenceEquals(newAuthProvider, currentAuthProvider))
                { 
                            // NewAuthProvider =>
                            // 1. Credentials changed
                            // 2. Dispose current token provider
                            this.currentKeyObject = this.azureKeyCredential.Key;

                            toDispose = currentAuthProvider;
                }

                        toDispose?.Dispose();
                    }
                }
            }
        }
    }
}
