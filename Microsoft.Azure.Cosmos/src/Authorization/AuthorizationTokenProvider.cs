//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal abstract class AuthorizationTokenProvider : ICosmosAuthorizationTokenProvider, IAuthorizationTokenProvider, IDisposable
    {
        private readonly DateTime creationTime = DateTime.UtcNow;

        public async Task AddSystemAuthorizationHeaderAsync(
            DocumentServiceRequest request, 
            string federationId, 
            string verb, 
            string resourceId)
        {
            request.Headers[HttpConstants.HttpHeaders.XDate] = Rfc1123DateTimeCache.UtcNow();

            request.Headers[HttpConstants.HttpHeaders.Authorization] = (await this.GetUserAuthorizationAsync(
                resourceId ?? request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                verb,
                request.Headers,
                request.RequestAuthorizationTokenType)).token;
        }

        public abstract ValueTask AddAuthorizationHeaderAsync(
            INameValueCollection headersCollection,
            Uri requestAddress,
            string verb,
            AuthorizationTokenType tokenType);

        /// <summary>
        /// Helper method for Microsoft.Azure.Cosmos.AI so that it doesn't require internal types from the Direct package.
        /// </summary>
        /// <remarks>
        /// Virtual for mocking purposes.
        /// </remarks>
        public virtual async ValueTask AddAadAuthorizationHeadersAsync(
            IDictionary<string, string> headersCollection,
            Uri requestAddress,
            string verb)
        {
            INameValueCollection newHeaders = new RequestNameValueCollection();
            await this.AddAuthorizationHeaderAsync(
                newHeaders,
                requestAddress,
                verb,
                AuthorizationTokenType.AadToken);

            foreach (KeyValuePair<string, string> kvp in newHeaders)
            {
                headersCollection.Add(kvp.Key, kvp.Value);
            }
        }

        public abstract ValueTask<(string token, string payload)> GetUserAuthorizationAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType);

        public abstract ValueTask<string> GetUserAuthorizationTokenAsync(
            string resourceAddress,
            string resourceType,
            string requestVerb,
            INameValueCollection headers,
            AuthorizationTokenType tokenType,
            ITrace trace);

        public abstract void TraceUnauthorized(
            DocumentClientException dce,
            string authorizationToken,
            string payload);

        public virtual TimeSpan GetAge()
        {
            return DateTime.UtcNow.Subtract(this.creationTime);
        }

        public static AuthorizationTokenProvider CreateWithResourceTokenOrAuthKey(string authKeyOrResourceToken)
        {
            if (string.IsNullOrEmpty(authKeyOrResourceToken))
            {
                throw new ArgumentNullException(nameof(authKeyOrResourceToken));
            }

            if (AuthorizationHelper.IsResourceToken(authKeyOrResourceToken))
            {
                return new AuthorizationTokenProviderResourceToken(authKeyOrResourceToken);
            }
            else
            {
                return new AuthorizationTokenProviderMasterKey(authKeyOrResourceToken);
            }
        }

        public abstract void Dispose();
    }
}
