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
    using Microsoft.Azure.Documents.FaultInjection;
    using Newtonsoft.Json;

    /// <summary>
    /// An <see cref="IStoreModelExtension"/> implementation that routes operations through the
    /// ThinClient proxy. It applies session tokens, resolves partition key ranges and delegates
    /// requests to <see cref="ThinClientStoreClient"/>. When a request is not eligible for the
    /// thin-client path (operation type not supported, or the service has withdrawn the thin-client
    /// endpoints mid-flight) the model transparently falls back to the regular gateway HTTP path
    /// via the inherited <see cref="GatewayStoreClient"/>, without requiring a client restart.
    /// This dispatch decision is taken per request (see <see cref="IsThinClientRoutable"/>) so the
    /// model can switch direction in either way (thin-client → gateway, or gateway → thin-client)
    /// as soon as the next <see cref="LocationCache"/> refresh updates the availability signals.
    /// </summary>
    internal class ThinClientStoreModel : GatewayStoreModel
    {
        private ThinClientStoreClient thinClientStoreClient;

        public ThinClientStoreModel(
            GlobalEndpointManager endpointManager,
            ISessionContainer sessionContainer,
            ConsistencyLevel defaultConsistencyLevel,
            DocumentClientEventSource eventSource,
            JsonSerializerSettings serializerSettings,
            CosmosHttpClient httpClient,
            GlobalPartitionEndpointManager globalPartitionEndpointManager,
            UserAgentContainer userAgentContainer,
            IChaosInterceptor chaosInterceptor = null)
            : base(
                  endpointManager,
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
                serializerSettings,
                chaosInterceptor);
        }

        public override async Task<DocumentServiceResponse> ProcessMessageAsync(
            DocumentServiceRequest request,
            CancellationToken cancellationToken = default)
        {
            // ThinClientStoreModel pre-resolves the PartitionKeyRange for all
            // thin-client-enabled, non-master, partitioned (or stored-procedure) requests so that:
            //   * the thin-client invoke can populate ProxyStartEpk / ProxyEndEpk on the proxy
            //     request,
            //   * the gateway fall-back still has the resolved PKR available for split
            //     detection in CaptureSessionTokenAndHandleSplitAsync.
            // The dispatch decision is taken AFTER PKR resolution: routable requests go through
            // the thin-client store client, the rest fall back to the gateway store client on the
            // same instance — exactly matching the prior merged behavior.
            DocumentServiceResponse response;

            await GatewayStoreModel.ApplySessionTokenAsync(
                request,
                this.defaultConsistencyLevel,
                this.sessionContainer,
                this.partitionKeyRangeCache,
                this.clientCollectionCache,
                this.endpointManager);
            try
            {
                if (request.ResourceType.Equals(ResourceType.Document) &&
                    this.endpointManager.TryGetLocationForGatewayDiagnostics(
                        request.RequestContext.LocationEndpointToRoute,
                        out string regionName))
                {
                    request.RequestContext.RegionName = regionName;
                }

                bool isPPAFEnabled = this.IsPartitionLevelFailoverEnabled();
                if (!ReplicatedResourceClient.IsMasterResource(request.ResourceType)
                    && (request.ResourceType.IsPartitioned() || request.ResourceType == ResourceType.StoredProcedure))
                {
                    (bool isSuccess, PartitionKeyRange partitionKeyRange) = await GatewayStoreModel.TryResolvePartitionKeyRangeAsync(
                        request: request,
                        sessionContainer: this.sessionContainer,
                        partitionKeyRangeCache: this.partitionKeyRangeCache,
                        clientCollectionCache: this.clientCollectionCache,
                        refreshCache: false);

                    request.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;

                    if (isPPAFEnabled)
                    {
                        this.globalPartitionEndpointManager.TryAddPartitionLevelLocationOverride(request, false);
                    }
                }

                bool canUseThinClient = this.thinClientStoreClient != null
                    && ThinClientStoreModel.IsThinClientRoutable(this.endpointManager, request);

                Uri physicalAddress = ThinClientStoreClient.IsFeedRequest(request.OperationType)
                    ? this.GetFeedUri(request)
                    : this.GetEntityUri(request);

                if (canUseThinClient)
                {
                    Uri thinClientEndpoint = this.endpointManager.ResolveThinClientEndpoint(request);
                    AccountProperties account = await this.GetDatabaseAccountPropertiesAsync();

                    response = await this.thinClientStoreClient.InvokeAsync(
                        request,
                        request.ResourceType,
                        physicalAddress,
                        thinClientEndpoint,
                        account.Id,
                        this.clientCollectionCache,
                        cancellationToken);
                }
                else
                {
                    response = await this.gatewayStoreClient.InvokeAsync(
                        request,
                        request.ResourceType,
                        physicalAddress,
                        cancellationToken);
                }
            }
            catch (DocumentClientException exception)
            {
                if ((!ReplicatedResourceClient.IsMasterResource(request.ResourceType)) &&
                    (exception.StatusCode == HttpStatusCode.PreconditionFailed
                     || exception.StatusCode == HttpStatusCode.Conflict
                     || (exception.StatusCode == HttpStatusCode.NotFound && exception.GetSubStatus() != SubStatusCodes.ReadSessionNotAvailable)))
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

        internal static bool IsOperationSupportedByThinClient(DocumentServiceRequest request)
        {
            // Document operations
            if (request.ResourceType == ResourceType.Document
                && (request.OperationType == OperationType.Batch
                || request.OperationType == OperationType.Patch
                || request.OperationType == OperationType.Create
                || request.OperationType == OperationType.Read
                || request.OperationType == OperationType.Upsert
                || request.OperationType == OperationType.Replace
                || request.OperationType == OperationType.Delete
                || request.OperationType == OperationType.Query
                || request.OperationType == OperationType.QueryPlan))
            {
                return true;
            }

            // LatestVersion (Incremental) ChangeFeed on documents.
            // AllVersionsAndDeletes (FullFidelity) is excluded because it requires
            // split-handling logic in Compute Gateway (UseGatewayMode is set by ChangeFeedModeFullFidelity).
            if (request.ResourceType == ResourceType.Document
                && request.OperationType == OperationType.ReadFeed
                && ThinClientStoreModel.IsLatestVersionChangeFeedRequest(request))
            {
                return true;
            }

            // Stored Procedure execution
            if (request.ResourceType == ResourceType.StoredProcedure
                && request.OperationType == OperationType.ExecuteJavaScript)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the request is currently eligible for thin-client dispatch:
        /// the operation type is supported AND the service is still advertising thin-client
        /// endpoints for the request's direction. When either condition is false the dispatch
        /// falls back to the regular gateway path on the very next request without a client
        /// restart.
        /// </summary>
        internal static bool IsThinClientRoutable(IGlobalEndpointManager endpointManager, DocumentServiceRequest request)
        {
            return IsOperationSupportedByThinClient(request)
                && (request.IsReadOnlyRequest
                    ? endpointManager.HasThinClientReadLocations
                    : endpointManager.HasThinClientWriteLocations);
        }

        /// <summary>
        /// Read-direction variant of <see cref="IsThinClientRoutable"/> for failover walks
        /// (PPCB / PPAF) that traverse thin-client READ endpoints regardless of the original
        /// request direction.
        /// </summary>
        internal static bool IsThinClientReadRoutable(IGlobalEndpointManager endpointManager, DocumentServiceRequest request)
        {
            return IsOperationSupportedByThinClient(request)
                && endpointManager.HasThinClientReadLocations;
        }

        /// <summary>
        /// Determines if the request is a LatestVersion (Incremental) change feed request that can
        /// be routed to the thin client. Returns true only when the A-IM header is exactly
        /// <c>HttpConstants.A_IMHeaderValues.IncrementalFeed</c>. Any other value — including
        /// Full-Fidelity Feed (AllVersionsAndDeletes) or an unknown future mode — falls back to
        /// Compute Gateway so that new modes are not accidentally routed to the thin client.
        /// </summary>
        internal static bool IsLatestVersionChangeFeedRequest(DocumentServiceRequest request)
        {
            string aImHeaderValue = request.Headers[HttpConstants.HttpHeaders.A_IM];
            return string.Equals(aImHeaderValue, HttpConstants.A_IMHeaderValues.IncrementalFeed, StringComparison.OrdinalIgnoreCase);
        }

        private async Task<AccountProperties> GetDatabaseAccountPropertiesAsync()
        {
            AccountProperties accountProperties = await this.endpointManager.GetDatabaseAccountAsync();
            if (accountProperties != null)
            {
                return accountProperties;
            }

            throw new InvalidOperationException("Failed to retrieve AccountProperties. The response was null.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && this.thinClientStoreClient != null)
            {
                try
                {
                    this.thinClientStoreClient.Dispose();
                }
                catch (Exception exception)
                {
                    DefaultTrace.TraceWarning(
                        "Exception {0} thrown during dispose of HttpClient, this could happen if there are inflight request during the dispose of client",
                        exception.Message);
                }

                this.thinClientStoreClient = null;
            }

            base.Dispose(disposing);
        }
    }
}
