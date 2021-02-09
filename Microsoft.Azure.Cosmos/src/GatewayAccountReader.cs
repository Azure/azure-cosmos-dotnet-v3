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
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal sealed class GatewayAccountReader
    {
        private readonly ConnectionPolicy connectionPolicy;
        private readonly AuthorizationTokenProvider cosmosAuthorization;
        private readonly CosmosHttpClient httpClient;
        private readonly Uri serviceEndpoint;

        // Backlog: Auth abstractions are spilling through. 4 arguments for this CTOR are result of it.
        public GatewayAccountReader(Uri serviceEndpoint,
                AuthorizationTokenProvider cosmosAuthorization,
                ConnectionPolicy connectionPolicy,
                CosmosHttpClient httpClient)
        {
            this.httpClient = httpClient;
            this.serviceEndpoint = serviceEndpoint;
            this.cosmosAuthorization = cosmosAuthorization ?? throw new ArgumentNullException(nameof(AuthorizationTokenProvider));
            this.connectionPolicy = connectionPolicy;
        }

        private async Task<AccountProperties> GetDatabaseAccountAsync(Uri serviceEndpoint)
        {
            INameValueCollection headers = new StoreRequestNameValueCollection();
            await this.cosmosAuthorization.AddAuthorizationHeaderAsync(
                headersCollection: headers,
                serviceEndpoint,
                HttpConstants.HttpMethods.Get,
                AuthorizationTokenType.PrimaryMasterKey);

            using (HttpResponseMessage responseMessage = await this.httpClient.GetAsync(
                uri: serviceEndpoint,
                additionalHeaders: headers,
                resourceType: ResourceType.DatabaseAccount,
                timeoutPolicy: HttpTimeoutPolicyControlPlaneRead.Instance,
                trace: NoOpTrace.Singleton,
                cancellationToken: default))
            {
                using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                {
                    return CosmosResource.FromStream<AccountProperties>(documentServiceResponse);
                }
            }
        }

        public async Task<AccountProperties> InitializeReaderAsync()
        {
            AccountProperties databaseAccount = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                this.serviceEndpoint, this.connectionPolicy.PreferredLocations, this.GetDatabaseAccountAsync);

            return databaseAccount;
        }
    }
}
