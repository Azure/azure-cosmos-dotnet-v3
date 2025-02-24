//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Newtonsoft.Json;

    /// <summary>
    /// An IStoreModelExtension implementation that routes operations through the ThinClient proxy. 
    /// It applies session tokens, resolves partition key ranges, and delegates requests to ProxyStoreClient.
    /// </summary>
    internal class ThinClientStoreModel : IStoreModelExtension, IDisposable
    {
        private readonly IGlobalEndpointManager endpointManager;
        private readonly DocumentClientEventSource eventSource;
        private readonly ISessionContainer sessionContainer;
        private readonly ConsistencyLevel defaultConsistencyLevel;

        private ProxyStoreClient proxyStoreClient;

        // Caches to resolve the PartitionKeyRange from request. For Session Token Optimization.
        private ClientCollectionCache clientCollectionCache;
        private PartitionKeyRangeCache partitionKeyRangeCache;

        public ThinClientStoreModel(
            IGlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            CosmosHttpClient httpClient,
            Uri proxyEndpoint,
            string globalDatabaseAccountName)
        {
            this.endpointManager = endpointManager;
            this.sessionContainer = sessionContainer;
            this.defaultConsistencyLevel = defaultConsistencyLevel;
            this.eventSource = eventSource;
            this.proxyStoreClient = new ProxyStoreClient(
                httpClient,
                this.eventSource,
                proxyEndpoint,
                globalDatabaseAccountName,
                serializerSettings);
        }

        public virtual async Task<DocumentServiceResponse> ProcessMessageAsync(
            DocumentServiceRequest request,
            CancellationToken cancellationToken = default)
        {
            DefaultTrace.TraceInformation(
                "In {0}, OperationType: {1}, ResourceType: {2}",
                nameof(ThinClientStoreModel),
                request.OperationType,
                request.ResourceType);

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
                Uri physicalAddress = ProxyStoreClient.IsFeedRequest(request.OperationType)
                    ? this.GetFeedUri(request)
                    : this.GetEntityUri(request);

                // Collect region name only for document resources
                if (request.ResourceType.Equals(ResourceType.Document)
                    && this.endpointManager.TryGetLocationForGatewayDiagnostics(
                           request.RequestContext.LocationEndpointToRoute,
                           out string regionName))
                {
                    request.RequestContext.RegionName = regionName;
                }

                response = await this.proxyStoreClient.InvokeAsync(
                    request,
                    request.ResourceType,
                    physicalAddress,
                    cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                if ((!ReplicatedResourceClient.IsMasterResource(request.ResourceType)) &&
                    (exception.StatusCode == HttpStatusCode.PreconditionFailed
                     || exception.StatusCode == HttpStatusCode.Conflict
                     || (exception.StatusCode == HttpStatusCode.NotFound
                         && exception.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
                {
                    await this.CaptureSessionTokenAndHandleSplitAsync(
                        exception.StatusCode,
                        exception.GetSubStatus(),
                        request,
                        exception.Headers);
                }

                throw;
            }

            await this.CaptureSessionTokenAndHandleSplitAsync(
                response.StatusCode,
                response.SubStatusCode,
                request,
                response.Headers);

            return response;
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

        public void SetCaches(
            PartitionKeyRangeCache partitionKeyRangeCache,
            ClientCollectionCache clientCollectionCache)
        {
            this.clientCollectionCache = clientCollectionCache;
            this.partitionKeyRangeCache = partitionKeyRangeCache;
        }

        public void Dispose()
        {
            this.Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.proxyStoreClient != null)
                {
                    try
                    {
                        this.proxyStoreClient.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DefaultTrace.TraceWarning(
                            "Exception {0} thrown during dispose of HttpClient...",
                            exception);
                    }

                    this.proxyStoreClient = null;
                }
            }
        }

        private Uri GetEntityUri(DocumentServiceRequest entity)
        {
            string contentLocation = entity.Headers[HttpConstants.HttpHeaders.ContentLocation];
            if (!string.IsNullOrEmpty(contentLocation))
            {
                return new Uri(
                    this.endpointManager.ResolveServiceEndpoint(entity),
                    new Uri(contentLocation).AbsolutePath);
            }

            return new Uri(
                this.endpointManager.ResolveServiceEndpoint(entity),
                PathsHelper.GeneratePath(entity.ResourceType, entity, false));
        }

        private Uri GetFeedUri(DocumentServiceRequest request)
        {
            return new Uri(
                this.endpointManager.ResolveServiceEndpoint(request),
                PathsHelper.GeneratePath(request.ResourceType, request, true));
        }

        public Task OpenConnectionsToAllReplicasAsync(string databaseName, string containerLinkUri, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}