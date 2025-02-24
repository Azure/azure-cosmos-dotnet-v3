//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;

    // Marking it as non-sealed in order to unit test it using Moq framework
    internal class GatewayStoreModel : IStoreModelExtension, IDisposable
    {
        private readonly GlobalEndpointManager endpointManager;
        private readonly DocumentClientEventSource eventSource;
        private readonly ISessionContainer sessionContainer;
        private readonly ConsistencyLevel defaultConsistencyLevel;

        private GatewayStoreClient gatewayStoreClient;

        // Caches to resolve the PartitionKeyRange from request. For Session Token Optimization.
        private ClientCollectionCache clientCollectionCache;
        private PartitionKeyRangeCache partitionKeyRangeCache;

        public GatewayStoreModel(
            GlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            CosmosHttpClient httpClient)
        {
            this.endpointManager = endpointManager;
            this.sessionContainer = sessionContainer;
            this.defaultConsistencyLevel = defaultConsistencyLevel;
            this.eventSource = eventSource;

            this.gatewayStoreClient = new GatewayStoreClient(
                httpClient,
                this.eventSource,
                serializerSettings);
        }

        public virtual async Task<DocumentServiceResponse> ProcessMessageAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default)
        {
            await StoreModelHelper.ApplySessionTokenAsync(
                request,
                this.defaultConsistencyLevel,
                this.sessionContainer,
                this.partitionKeyRangeCache,
                this.clientCollectionCache,
                this.endpointManager);

            DocumentServiceResponse response;
            try
            {
                Uri physicalAddress = GatewayStoreClient.IsFeedRequest(request.OperationType) ? this.GetFeedUri(request) : this.GetEntityUri(request);
                // Collect region name only for document resources
                if (request.ResourceType.Equals(ResourceType.Document) && this.endpointManager.TryGetLocationForGatewayDiagnostics(request.RequestContext.LocationEndpointToRoute, out string regionName))
                {
                    request.RequestContext.RegionName = regionName;
                }
                response = await this.gatewayStoreClient.InvokeAsync(request, request.ResourceType, physicalAddress, cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                if ((!ReplicatedResourceClient.IsMasterResource(request.ResourceType)) &&
                    (exception.StatusCode == HttpStatusCode.PreconditionFailed || exception.StatusCode == HttpStatusCode.Conflict
                    || (exception.StatusCode == HttpStatusCode.NotFound && exception.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
                {
                    await this.CaptureSessionTokenAndHandleSplitAsync(exception.StatusCode, exception.GetSubStatus(), request, exception.Headers);
                }

                throw;
            }

            await this.CaptureSessionTokenAndHandleSplitAsync(response.StatusCode, response.SubStatusCode, request, response.Headers);
            return response;
        }

        public virtual async Task<AccountProperties> GetDatabaseAccountAsync(Func<ValueTask<HttpRequestMessage>> requestMessage,
                                                        IClientSideRequestStatistics clientSideRequestStatistics,
                                                        CancellationToken cancellationToken = default)
        {
            AccountProperties databaseAccount = null;

            // Get the ServiceDocumentResource from the gateway.
            using (HttpResponseMessage responseMessage = await this.gatewayStoreClient.SendHttpAsync(
                requestMessage,
                ResourceType.DatabaseAccount,
                HttpTimeoutPolicyControlPlaneRead.Instance,
                clientSideRequestStatistics,
                cancellationToken))
            {
                using (DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(responseMessage))
                {
                    databaseAccount = CosmosResource.FromStream<AccountProperties>(documentServiceResponse);
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.MaxMediaStorageUsageInMB, out IEnumerable<string> headerValues))
                {
                    if (long.TryParse(headerValues.First(), out long longValue))
                    {
                        databaseAccount.MaxMediaStorageUsageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.CurrentMediaStorageUsageInMB, out headerValues) &&
                    (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out long longValue))
                    {
                        databaseAccount.MediaStorageUsageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.DatabaseAccountConsumedDocumentStorageInMB, out headerValues) &&
                   (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out long longValue))
                    {
                        databaseAccount.ConsumedDocumentStorageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.DatabaseAccountProvisionedDocumentStorageInMB, out headerValues) &&
                   (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out long longValue))
                    {
                        databaseAccount.ProvisionedDocumentStorageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.DatabaseAccountReservedDocumentStorageInMB, out headerValues) &&
                   (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out long longValue))
                    {
                        databaseAccount.ReservedDocumentStorageInMB = longValue;
                    }
                }
            }

            return databaseAccount;
        }

        public void SetCaches(PartitionKeyRangeCache partitionKeyRangeCache,
            ClientCollectionCache clientCollectionCache)
        {
            this.clientCollectionCache = clientCollectionCache;
            this.partitionKeyRangeCache = partitionKeyRangeCache;
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        private async Task CaptureSessionTokenAndHandleSplitAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes subStatusCode,
            DocumentServiceRequest request,
            INameValueCollection responseHeaders)
        {
            await StoreModelHelper.CaptureSessionTokenAndHandleSplitAsync(
                this.sessionContainer,
                this.partitionKeyRangeCache,
                statusCode,
                subStatusCode,
                request,
                responseHeaders);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.gatewayStoreClient != null)
                {
                    try
                    {
                        this.gatewayStoreClient.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DefaultTrace.TraceWarning("Exception {0} thrown during dispose of HttpClient, this could happen if there are inflight request during the dispose of client",
                          exception);
                    }

                    this.gatewayStoreClient = null;
                }
            }
        }

        private Uri GetEntityUri(DocumentServiceRequest entity)
        {
            string contentLocation = entity.Headers[HttpConstants.HttpHeaders.ContentLocation];
            
            if (!string.IsNullOrEmpty(contentLocation))
            {
                return new Uri(this.endpointManager.ResolveServiceEndpoint(entity), new Uri(contentLocation).AbsolutePath);
            }

            return new Uri(this.endpointManager.ResolveServiceEndpoint(entity), PathsHelper.GeneratePath(entity.ResourceType, entity, false));
        }

        private Uri GetFeedUri(DocumentServiceRequest request)
        {
            return new Uri(this.endpointManager.ResolveServiceEndpoint(request), PathsHelper.GeneratePath(request.ResourceType, request, true));
        }

        public Task OpenConnectionsToAllReplicasAsync(string databaseName, string containerLinkUri, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
