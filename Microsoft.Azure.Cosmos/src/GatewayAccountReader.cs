//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net.Http;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Resource.Settings;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal sealed class GatewayAccountReader
    {
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
                CancellationToken cancellationToken = default)
        {
            this.httpClient = httpClient;
            this.serviceEndpoint = serviceEndpoint;
            this.cosmosAuthorization = cosmosAuthorization ?? throw new ArgumentNullException(nameof(AuthorizationTokenProvider));
            this.connectionPolicy = connectionPolicy;
            this.cancellationToken = cancellationToken;
        }

        private async Task<AccountProperties> GetDatabaseAccountAsync(Uri serviceEndpoint)
        {
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
                    using (HttpResponseMessage responseMessage = await this.httpClient.GetAsync(
                        uri: serviceEndpoint,
                        additionalHeaders: headers,
                        resourceType: ResourceType.DatabaseAccount,
                        timeoutPolicy: HttpTimeoutPolicyControlPlaneRead.Instance,
                        clientSideRequestStatistics: stats,
                        cancellationToken: default))
                    using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                    {
                        return CosmosResource.FromStream<AccountProperties>(documentServiceResponse);
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

        public async Task<TryCatch<AccountClientConfigProperties>> GetDatabaseAccountClientConfigAsync()
        {
            Uri clientConfigEndpoint = new Uri(this.serviceEndpoint + Paths.ClientConfigPathSegment);
            
            INameValueCollection headers = new RequestNameValueCollection();
            await this.cosmosAuthorization.AddAuthorizationHeaderAsync(
                headersCollection: headers,
                clientConfigEndpoint,
                HttpConstants.HttpMethods.Get,
                AuthorizationTokenType.PrimaryMasterKey);

            using (ITrace trace = Trace.GetRootTrace("Account Client Config Read", TraceComponent.Transport, TraceLevel.Info))
            {
                try
                {
                    using (HttpResponseMessage responseMessage = await this.httpClient.GetAsync(
                        uri: clientConfigEndpoint,
                        additionalHeaders: headers,
                        resourceType: ResourceType.DatabaseAccount,
                        timeoutPolicy: HttpTimeoutPolicyControlPlaneRead.Instance,
                        clientSideRequestStatistics: null,
                        cancellationToken: default))
                    using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                    {
                        return TryCatch<AccountClientConfigProperties>.FromResult(CosmosResource.FromStream<AccountClientConfigProperties>(documentServiceResponse));
                    }
                }
                catch (ObjectDisposedException ex) when (this.cancellationToken.IsCancellationRequested)
                {
                    DefaultTrace.TraceWarning($"Client is being disposed for {clientConfigEndpoint} at {DateTime.UtcNow}, cancelling client config call.");
                    return TryCatch<AccountClientConfigProperties>.FromException(ex);
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceWarning($"Exception while calling client config " + ex.StackTrace);
                    return TryCatch<AccountClientConfigProperties>.FromException(ex);
                }
            }
        }

        public async Task<AccountProperties> InitializeReaderAsync()
        {
            AccountProperties databaseAccount = await GlobalEndpointManager.GetDatabaseAccountFromAnyLocationsAsync(
                defaultEndpoint: this.serviceEndpoint,
                locations: this.connectionPolicy.PreferredLocations,
                getDatabaseAccountFn: this.GetDatabaseAccountAsync,
                cancellationToken: this.cancellationToken);

            return databaseAccount;
        }
    }
}
