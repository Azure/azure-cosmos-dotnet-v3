//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Authorization
{
    using System;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class AzureKeyCredentialAuthorizationTokenProvider : AuthorizationTokenProvider
    {
        private readonly AzureKeyCredential azureKeyCredential;
        
        // keyObject is used to check for refresh 
        private string currentKeyObject = null;
        private AuthorizationTokenProvider authorizationTokenProvider;

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
            if (!object.ReferenceEquals(this.currentKeyObject, this.azureKeyCredential.Key))
            {
                this.currentKeyObject = this.azureKeyCredential.Key ?? throw new ArgumentNullException($"{nameof(AzureKeyCredential)} has null Key");

                // Credentials changed:
                // 1. Dispose current token provider
                // 2. Refresh the provider
                using (this.authorizationTokenProvider) 
                { 
                }

                this.authorizationTokenProvider = AuthorizationTokenProvider.CreateWithResourceTokenOrAuthKey(this.currentKeyObject);
            }
        }
    }
}
