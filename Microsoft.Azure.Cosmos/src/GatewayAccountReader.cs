//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal sealed class GatewayAccountReader
    {
        private const string RegionProximitySourceRegionQueryParameter = "regionproximitysourceregion";
        private readonly bool isThinClientEnabled;
        private readonly ConnectionPolicy connectionPolicy;
        private readonly AuthorizationTokenProvider cosmosAuthorization;
        private readonly CosmosHttpClient httpClient;
        private readonly Uri serviceEndpoint;
        private readonly CancellationToken cancellationToken;

        // Backlog: Auth abstractions are spilling through. 4 arguments for this CTOR are result of it.
        public GatewayAccountReader(Uri serviceEndpoint,
                AuthorizationTokenProvider cosmosAuthorization,
                ConnectionPolicy connectionPolicy,
                CosmosHttpClient httpClient,
                CancellationToken cancellationToken = default,
                bool isThinClientEnabled = false)
        {
            this.httpClient = httpClient;
            this.serviceEndpoint = serviceEndpoint;
            this.cosmosAuthorization = cosmosAuthorization ?? throw new ArgumentNullException(nameof(AuthorizationTokenProvider));
            this.connectionPolicy = connectionPolicy;
            this.cancellationToken = cancellationToken;
            this.isThinClientEnabled = isThinClientEnabled;
        }

        private async Task<AccountProperties> GetDatabaseAccountAsync(Uri serviceEndpoint)
        {
            serviceEndpoint = GatewayAccountReader.AddRegionProximitySourceRegion(
                serviceEndpoint,
                this.connectionPolicy.CurrentLocation);

            INameValueCollection headers = new RequestNameValueCollection();
            await this.cosmosAuthorization.AddAuthorizationHeaderAsync(
                headersCollection: headers,
                serviceEndpoint,
                HttpConstants.HttpMethods.Get,
                AuthorizationTokenType.PrimaryMasterKey);

            using (ITrace trace = Trace.GetRootTrace("Account Read", TraceComponent.Transport, TraceLevel.Info))
            {
                IClientSideRequestStatistics stats = new ClientSideRequestStatisticsTraceDatum(DateTime.UtcNow, trace);

                try
                {
                    using (DocumentServiceRequest request = DocumentServiceRequest.Create(
                        operationType: OperationType.Read,
                        resourceType: ResourceType.DatabaseAccount,
                        authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey))
                    {
                        if (this.isThinClientEnabled)
                        {
                            headers.Add(
                                ThinClientConstants.EnableThinClientEndpointDiscoveryHeaderName,
                                this.isThinClientEnabled.ToString());
                        }

                        using (HttpResponseMessage responseMessage = await this.httpClient.GetAsync(
                            uri: serviceEndpoint,
                            additionalHeaders: headers,
                            resourceType: ResourceType.DatabaseAccount,
                            timeoutPolicy: HttpTimeoutPolicyControlPlaneRead.Instance,
                            clientSideRequestStatistics: stats,
                            cancellationToken: default,
                            documentServiceRequest: request))
                         using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                         {
                          return CosmosResource.FromStream<AccountProperties>(documentServiceResponse);
                         }
                    }
                }
                catch (ObjectDisposedException) when (this.cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException($"Client is being disposed for {serviceEndpoint} at {DateTime.UtcNow}, cancelling further operations.");
                }
                catch (OperationCanceledException ex)
                {
                    trace.AddDatum("Client Side Request Stats", stats);
                    throw CosmosExceptionFactory.CreateRequestTimeoutException(
                                                message: ex.Data?["Message"]?.ToString() ?? ex.Message,
                                                headers: new Headers()
                                                {
                                                    ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString()
                                                },
                                                innerException: ex,
                                                trace: trace);
                }
            }
        }

        internal static Uri AddRegionProximitySourceRegion(
            Uri serviceEndpoint,
            string currentLocation)
        {
            if (string.IsNullOrEmpty(currentLocation))
            {
                return serviceEndpoint;
            }

            string sanitizedRegion = RegionNameMapper.SanitizeRegionName(currentLocation);
            if (string.IsNullOrEmpty(sanitizedRegion))
            {
                return serviceEndpoint;
            }

            UriBuilder builder = new UriBuilder(serviceEndpoint);
            string existingQuery = builder.Query?.TrimStart('?') ?? string.Empty;
            List<string> queryParameters = new List<string>();
            foreach (string queryParameter in existingQuery.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separatorIndex = queryParameter.IndexOf('=');
                string encodedName = separatorIndex >= 0
                    ? queryParameter.Substring(0, separatorIndex)
                    : queryParameter;
                if (!string.Equals(
                    Uri.UnescapeDataString(encodedName),
                    GatewayAccountReader.RegionProximitySourceRegionQueryParameter,
                    StringComparison.OrdinalIgnoreCase))
                {
                    queryParameters.Add(queryParameter);
                }
            }

            queryParameters.Add(
                $"{GatewayAccountReader.RegionProximitySourceRegionQueryParameter}=" +
                $"{Uri.EscapeDataString(sanitizedRegion)}");
            builder.Query = string.Join("&", queryParameters);
            return builder.Uri;
        }

        public async Task<AccountProperties> InitializeReaderAsync()
        {
            AccountProperties databaseAccount = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                defaultEndpoint: this.serviceEndpoint,
                locations: this.connectionPolicy.PreferredLocations,
                accountInitializationCustomEndpoints: this.connectionPolicy.AccountInitializationCustomEndpoints,
                getDatabaseAccountFn: this.GetDatabaseAccountAsync,
                cancellationToken: this.cancellationToken);

            return databaseAccount;
        }
    }
}
