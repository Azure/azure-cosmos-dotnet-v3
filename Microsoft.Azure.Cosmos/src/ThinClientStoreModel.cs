//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;

    /// <summary>
    /// An IStoreModelExtension implementation that routes operations through the ThinClient proxy. 
    /// It applies session tokens, resolves partition key ranges, and delegates requests to ThinClientStoreClient.
    /// </summary>
    internal class ThinClientStoreModel : GatewayStoreModel
    {
        private readonly GlobalPartitionEndpointManager globalPartitionEndpointManager;
        private ThinClientStoreClient thinClientStoreClient;

        public ThinClientStoreModel(
            GlobalEndpointManager endpointManager,
            GlobalPartitionEndpointManager globalPartitionEndpointManager,
            ISessionContainer sessionContainer,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            CosmosHttpClient httpClient,
            UserAgentContainer userAgentContainer)
            : base(endpointManager,
                  sessionContainer,
                  defaultConsistencyLevel,
                  eventSource,
                  serializerSettings,
                  httpClient,
                  globalPartitionEndpointManager)
        {
            this.thinClientStoreClient = new ThinClientStoreClient(
                httpClient,
                userAgentContainer,
                eventSource,
                globalPartitionEndpointManager,
                serializerSettings);

            this.globalPartitionEndpointManager = globalPartitionEndpointManager;
            this.globalPartitionEndpointManager.SetBackgroundConnectionPeriodicRefreshTask(
               base.MarkEndpointsToHealthyAsync);
        }

        public override async Task<DocumentServiceResponse> ProcessMessageAsync(
            DocumentServiceRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!ThinClientStoreModel.IsOperationSupportedByThinClient(request))
            {
                return await base.ProcessMessageAsync(request, cancellationToken);
            }

            await GatewayStoreModel.ApplySessionTokenAsync(
                request,
                base.defaultConsistencyLevel,
                base.sessionContainer,
                base.partitionKeyRangeCache,
                base.clientCollectionCache,
                base.endpointManager);

            DocumentServiceResponse response;
            try
            {
                if (request.ResourceType.Equals(ResourceType.Document) && base.endpointManager.TryGetLocationForGatewayDiagnostics(
                    request.RequestContext.LocationEndpointToRoute,
                    out string regionName))
                {
                    request.RequestContext.RegionName = regionName;
                }

                // This is applicable for both per partition automatic failover and per partition circuit breaker.
                if (this.globalPartitionEndpointManager.IsPartitionLevelFailoverEnabled()
                    && !ReplicatedResourceClient.IsMasterResource(request.ResourceType)
                    && request.ResourceType.IsPartitioned())
                {
                    (bool isSuccess, PartitionKeyRange partitionKeyRange) = await GatewayStoreModel.TryResolvePartitionKeyRangeAsync(
                        request: request,
                        sessionContainer: this.sessionContainer,
                        partitionKeyRangeCache: this.partitionKeyRangeCache,
                        clientCollectionCache: this.clientCollectionCache,
                        refreshCache: false);

                    request.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
                    this.globalPartitionEndpointManager.TryAddPartitionLevelLocationOverride(request);
                }

                Uri physicalAddress = ThinClientStoreClient.IsFeedRequest(request.OperationType) ? base.GetFeedUri(request) : base.GetEntityUri(request);
                AccountProperties properties = await this.GetDatabaseAccountPropertiesAsync();
                response = await this.thinClientStoreClient.InvokeAsync(
                    request,
                    request.ResourceType,
                    physicalAddress,
                    this.endpointManager.ResolveThinClientEndpoint(request),
                    properties.Id,
                    base.clientCollectionCache,
                    cancellationToken);
            }
            catch (DocumentClientException exception)
            {
                if ((!ReplicatedResourceClient.IsMasterResource(request.ResourceType)) &&
                    (exception.StatusCode == HttpStatusCode.PreconditionFailed || exception.StatusCode == HttpStatusCode.Conflict
                    || (exception.StatusCode == HttpStatusCode.NotFound && exception.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
                {
                    await base.CaptureSessionTokenAndHandleSplitAsync(
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

        public static bool IsOperationSupportedByThinClient(
            DocumentServiceRequest request)
        {
            // Thin proxy supports the following operations for Document resources.
            return request.ResourceType == ResourceType.Document
                   && (request.OperationType == OperationType.Batch
                   || request.OperationType == OperationType.Patch
                   || request.OperationType == OperationType.Create
                   || request.OperationType == OperationType.Read
                   || request.OperationType == OperationType.Upsert
                   || request.OperationType == OperationType.Replace
                   || request.OperationType == OperationType.Delete
                   || request.OperationType == OperationType.Query);
        }

        private async Task<AccountProperties> GetDatabaseAccountPropertiesAsync()
        {
            try
            {
                AccountProperties accountProperties = await this.endpointManager.GetDatabaseAccountAsync();

                if (accountProperties != null)
                {
                    return accountProperties;
                }

                throw new InvalidOperationException("Failed to retrieve AccountProperties. The response was null.");
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError("Exception while retrieving database account information: {0}", ex.Message);
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.thinClientStoreClient != null)
                {
                    try
                    {
                        this.thinClientStoreClient.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DefaultTrace.TraceWarning("Exception {0} thrown during dispose of HttpClient, this could happen if there are inflight request during the dispose of client",
                            exception.Message);
                    }
                    this.thinClientStoreClient = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}