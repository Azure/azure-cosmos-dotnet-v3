//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
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

        /// <summary>
        /// The thin-client path always needs the resolved <see cref="PartitionKeyRange"/> so that
        /// the proxy request can be populated with ProxyStartEpk / ProxyEndEpk, and so the gateway
        /// fall-back still has the PKR available for split detection in
        /// <see cref="GatewayStoreModel.CaptureSessionTokenAndHandleSplitAsync"/>. It is therefore
        /// resolved for every non-master, partitioned (or stored-procedure) request, regardless of
        /// whether per partition automatic failover / circuit breaker is enabled.
        /// </summary>
        protected override bool ShouldResolvePartitionKeyRange()
        {
            return true;
        }

        /// <summary>
        /// Routes the request through the thin-client store client when it is currently
        /// thin-client-routable, otherwise transparently falls back to the inherited gateway HTTP
        /// path on the same instance. The decision is taken per request so the model can switch
        /// direction (thin-client ↔ gateway) as soon as the next <see cref="LocationCache"/> refresh
        /// updates the availability signals, without requiring a client restart.
        /// </summary>
        protected override async Task<DocumentServiceResponse> DispatchAsync(
            DocumentServiceRequest request,
            Uri physicalAddress,
            CancellationToken cancellationToken)
        {
            bool canUseThinClient = this.thinClientStoreClient != null
                && ThinClientStoreModel.IsThinClientRoutable(this.endpointManager, request);

            if (!canUseThinClient)
            {
                return await base.DispatchAsync(request, physicalAddress, cancellationToken);
            }

            Uri thinClientEndpoint = this.endpointManager.ResolveThinClientEndpoint(request);

            // Per-region probe gate: route to the proxy only when this request's resolved regional endpoint has
            // been confirmed healthy. An un-probed or failed region resolves to its gateway endpoint, which fails
            // this check and transparently falls back to Gateway V1.
            if (!this.endpointManager.IsProxyEndpointHealthy(thinClientEndpoint))
            {
                return await base.DispatchAsync(request, physicalAddress, cancellationToken);
            }

            AccountProperties account = await this.GetDatabaseAccountPropertiesAsync();

            return await this.thinClientStoreClient.InvokeAsync(
                request,
                request.ResourceType,
                physicalAddress,
                thinClientEndpoint,
                account.Id,
                this.clientCollectionCache,
                cancellationToken);
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
                || request.OperationType == OperationType.Query))
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
        /// Returns true if the request is eligible for thin-client dispatch: the operation type is supported AND
        /// the service is advertising thin-client endpoints for the request's direction. This is the capability +
        /// topology gate only; per-region probe health is applied separately at dispatch time
        /// (<see cref="DispatchAsync"/>), so an unhealthy region falls back to the gateway path.
        /// </summary>
        internal static bool IsThinClientRoutable(IGlobalEndpointManager endpointManager, DocumentServiceRequest request)
        {
            return IsOperationSupportedByThinClient(request)
                && (request.IsReadOnlyRequest
                    ? endpointManager.HasThinClientReadLocations
                    : endpointManager.HasThinClientWriteLocations);
        }

        /// <summary>
        /// Read-direction variant of <see cref="IsThinClientRoutable"/> for failover walks (PPCB / PPAF) that
        /// traverse thin-client READ endpoints regardless of the original request direction. Because the walk
        /// selects the whole read-endpoint list rather than a single endpoint, it requires every read region to
        /// be probe-healthy (<see cref="IGlobalEndpointManager.AreAllThinClientReadEndpointsHealthy"/>);
        /// otherwise it routes through the gateway read endpoints.
        /// </summary>
        internal static bool IsThinClientReadRoutable(IGlobalEndpointManager endpointManager, DocumentServiceRequest request)
        {
            return IsOperationSupportedByThinClient(request)
                && endpointManager.AreAllThinClientReadEndpointsHealthy
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
