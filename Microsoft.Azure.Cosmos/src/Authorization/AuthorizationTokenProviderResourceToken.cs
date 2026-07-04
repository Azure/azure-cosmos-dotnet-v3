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
            this.Dispose(disposing: true);

            // This object will be cleaned up by the Dispose method.
            // Therefore, you should call GC.SuppressFinalize to
            // take this object off the finalization queue
            // and prevent finalization code for this object
            // from executing a second time.
            GC.SuppressFinalize(this);
        }

        // Dispose(bool disposing) executes in two distinct scenarios.
        // If disposing equals true, the method has been called directly
        // or indirectly by a user's code. Managed and unmanaged resources
        // can be disposed.
        // If disposing equals false, the method has been called by the
        // runtime from inside the finalizer and you should not reference
        // other objects. Only unmanaged resources can be disposed.
        private void Dispose(bool disposing)
        {
            // Do nothing
        }

        // Use C# finalizer syntax for finalization code.
        // This finalizer will run only if the Dispose method does not get called.
        // It gives your base class the opportunity to finalize.
        ~AuthorizationTokenProviderResourceToken()
        {
            // Calling Dispose(disposing: false) is optimal in terms of
            // readability and maintainability.
            this.Dispose(disposing: false);
        }
    }
}
