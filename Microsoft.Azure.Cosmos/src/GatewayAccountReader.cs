//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal sealed class GatewayAccountReader
    {
        private readonly ConnectionPolicy connectionPolicy;
        private readonly IComputeHash authKeyHashFunction;
        private readonly bool hasAuthKeyResourceToken = false;
        private readonly string authKeyResourceToken = string.Empty;
        private readonly CosmosHttpClient httpClient;
        private readonly Uri serviceEndpoint;

        public GatewayAccountReader(Uri serviceEndpoint,
                IComputeHash stringHMACSHA256Helper,
                bool hasResourceToken,
                string resourceToken,
                ConnectionPolicy connectionPolicy,
                CosmosHttpClient httpClient)
        {
            this.httpClient = httpClient;
            this.serviceEndpoint = serviceEndpoint;
            this.authKeyHashFunction = stringHMACSHA256Helper;
            this.hasAuthKeyResourceToken = hasResourceToken;
            this.authKeyResourceToken = resourceToken;
            this.connectionPolicy = connectionPolicy;
        }

        private async Task<AccountProperties> GetDatabaseAccountAsync(Uri serviceEndpoint)
        {
            INameValueCollection headers = new DictionaryNameValueCollection(StringComparer.Ordinal);
            string authorizationToken = string.Empty;
            if (this.hasAuthKeyResourceToken)
            {
                authorizationToken = HttpUtility.UrlEncode(this.authKeyResourceToken);
            }
            else
            {
                // Retrieve the document service properties.
                string xDate = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
                headers.Set(HttpConstants.HttpHeaders.XDate, xDate);

                authorizationToken = AuthorizationHelper.GenerateKeyAuthorizationSignature(
                    HttpConstants.HttpMethods.Get,
                    serviceEndpoint,
                    headers,
                    this.authKeyHashFunction);
            }

            headers.Set(HttpConstants.HttpHeaders.Authorization, authorizationToken);
            using (HttpResponseMessage responseMessage = await this.httpClient.GetAsync(
                uri: serviceEndpoint,
                additionalHeaders: headers,
                resourceType: ResourceType.DatabaseAccount,
                diagnosticsContext: null,
                cancellationToken: default))
            {
                using (DocumentServiceResponse documentServiceResponse = await GatewayStoreClient.ParseResponseAsync(responseMessage))
                {
                    return CosmosResource.FromStream<AccountProperties>(documentServiceResponse);
                }
            }
        }

        public Task<AccountProperties> InitializeReaderAsync()
        {
            return GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                this.serviceEndpoint, this.connectionPolicy.PreferredLocations, this.GetDatabaseAccountAsync);
        }
    }
}
