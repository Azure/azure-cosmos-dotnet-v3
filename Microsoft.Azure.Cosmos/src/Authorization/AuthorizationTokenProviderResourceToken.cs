//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    
    internal sealed class AuthorizationTokenProviderResourceToken : AuthorizationTokenProvider
    {
        private readonly string urlEncodedAuthKeyResourceToken;
        private readonly ValueTask<string> urlEncodedAuthKeyResourceTokenValueTask;
        private readonly ValueTask<(string, string)> urlEncodedAuthKeyResourceTokenValueTaskWithPayload;
        private readonly ValueTask defaultValueTask;

        public AuthorizationTokenProviderResourceToken(
            string authKeyResourceToken)
        {
            this.urlEncodedAuthKeyResourceToken = HttpUtility.UrlEncode(authKeyResourceToken);
            this.urlEncodedAuthKeyResourceTokenValueTask = new ValueTask<string>(this.urlEncodedAuthKeyResourceToken);
            this.urlEncodedAuthKeyResourceTokenValueTaskWithPayload = new ValueTask<(string, string)>((this.urlEncodedAuthKeyResourceToken, default));
            this.defaultValueTask = new ValueTask();
        }

        public override ValueTask<(string token, string payload)> GetUserAuthorizationAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType)
        {
            // If the input auth token is a resource token, then use it as a bearer-token.
            return this.urlEncodedAuthKeyResourceTokenValueTaskWithPayload;
        }

        public override ValueTask<string> GetUserAuthorizationTokenAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType,
            ITrace trace)
        {
            // If the input auth token is a resource token, then use it as a bearer-token.
            return this.urlEncodedAuthKeyResourceTokenValueTask;
        }

        public override ValueTask AddAuthorizationHeaderAsync(
            INameValueCollection headersCollection,
            Uri requestAddress,
            string verb,
            AuthorizationTokenType tokenType)
        {
            headersCollection.Add(HttpConstants.HttpHeaders.Authorization, this.urlEncodedAuthKeyResourceToken);
            return this.defaultValueTask;
        }

        public override void TraceUnauthorized(
            DocumentClientException dce,
            string authorizationToken,
            string payload)
        {
            DefaultTrace.TraceError($"Un-expected authorization for resource token. {dce.Message}");
        }

        public override void Dispose()
        {
        }
    }
}
