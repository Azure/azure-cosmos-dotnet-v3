//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal abstract class CosmosAuthorization : ICosmosAuthorizationTokenProvider, IAuthorizationTokenProvider, IDisposable
    {
        public async Task AddSystemAuthorizationHeaderAsync(
            DocumentServiceRequest request, 
            string federationId, 
            string verb, 
            string resourceId)
        {
            request.Headers[HttpConstants.HttpHeaders.XDate] = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);

            request.Headers[HttpConstants.HttpHeaders.Authorization] = (await this.GetUserAuthorizationAsync(
                resourceId ?? request.ResourceAddress,
                PathsHelper.GetResourcePath(request.ResourceType),
                verb,
                request.Headers,
                request.RequestAuthorizationTokenType)).token;
        }

        public abstract ValueTask AddSystemAuthorizationHeaderAsync(
            INameValueCollection headersCollection,
            Uri requestAddress,
            string verb,
            AuthorizationTokenType tokenType);

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
            CosmosDiagnosticsContext diagnosticsContext);

        public abstract void TraceUnauthorized(
            DocumentClientException dce,
            string authorizationToken,
            string payload);

        public static CosmosAuthorization CreateWithResourceTokenOrAuthKey(string authKeyOrResourceToken)
        {
            if (AuthorizationHelper.IsResourceToken(authKeyOrResourceToken))
            {
                return new CosmosAuthorizationResourceToken(authKeyOrResourceToken);
            }
            else
            {
                return new CosmosAuthorizationComputeHash(authKeyOrResourceToken);
            }
        }

        public abstract void Dispose();
    }
}
