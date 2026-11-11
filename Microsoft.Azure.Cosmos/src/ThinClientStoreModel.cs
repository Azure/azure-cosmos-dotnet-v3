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
    using Microsoft.Azure.Cosmos.Tracing;
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
    /// A caller-supplied compound session token is scoped to the attempt's partition key range (and its parents)
    /// before it is sent to the proxy, refreshing the routing map once if the token names ranges the cache doesn't know.
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
        /// Eagerly resolves the <see cref="PartitionKeyRange"/> whenever the account currently
        /// advertises thin-client endpoints, so the proxy request can carry ProxyStartEpk /
        /// ProxyEndEpk and split detection has the PKR available on the gateway fall-back. When no
        /// thin-client endpoints are advertised, defers to the base PPAF/PPCB-gated decision so the
        /// overhead matches plain <see cref="GatewayStoreModel"/>. Re-evaluated per request off the
        /// live <see cref="LocationCache"/> flags.
        /// </summary>
        protected override bool ShouldResolvePartitionKeyRange()
        {
            return base.ShouldResolvePartitionKeyRange()
                || this.endpointManager.HasThinClientReadLocations
                || this.endpointManager.HasThinClientWriteLocations;
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
            if (!this.TryGetHealthyThinClientEndpoint(request, out Uri thinClientEndpoint))
            {
                return await base.DispatchAsync(request, physicalAddress, cancellationToken);
            }

            string sessionToken = request.Headers[HttpConstants.HttpHeaders.SessionToken];
            if (string.IsNullOrEmpty(sessionToken) || sessionToken.IndexOf(',') < 0)
            {
                return await this.InvokeThinClientAsync(request, physicalAddress, thinClientEndpoint, cancellationToken);
            }

            // The proxy accepts only the token of the partition it targets, so a caller-supplied compound token is
            // scoped per attempt and the caller's token is restored afterwards, letting retries (449, split,
            // cross-region) rescope it against the range they resolve.
            try
            {
                await this.ScopeCompoundSessionTokenAsync(request, sessionToken, cancellationToken);
                return await this.InvokeThinClientAsync(request, physicalAddress, thinClientEndpoint, cancellationToken);
            }
            finally
            {
                SessionTokenHelper.SetOriginalSessionToken(request, sessionToken);
            }
        }

        /// <summary>
        /// Replaces a compound session token with the progress of the request's target partition key range (merged
        /// with its parents' progress), as the gateway does for the token it tracks. If the token names a range the
        /// cached routing map doesn't know (e.g. a split the client hasn't seen), the map is refreshed once so child
        /// progress isn't silently dropped in favour of a stale parent. A token that still can't be scoped is omitted
        /// with a warning; a malformed token is rejected with <see cref="BadRequestException"/>.
        /// </summary>
        private async Task ScopeCompoundSessionTokenAsync(
            DocumentServiceRequest request,
            string compoundSessionToken,
            CancellationToken cancellationToken)
        {
            if (!GatewayStoreModel.IsSessionTokenApplicable(request, this.defaultConsistencyLevel, this.endpointManager))
            {
                request.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
                return;
            }

            ContainerProperties collection = await this.clientCollectionCache.ResolveCollectionAsync(
                request,
                cancellationToken,
                NoOpTrace.Singleton);
            CollectionRoutingMap routingMap = await this.partitionKeyRangeCache.TryLookupAsync(
                collection.ResourceId,
                previousValue: null,
                request,
                NoOpTrace.Singleton)
                ?? throw new NotFoundException($"Routing map for collection {collection.ResourceId} could not be resolved.");
            if (ThinClientStoreModel.TryApplyScopedSessionToken(request, compoundSessionToken, collection, routingMap, out _))
            {
                return;
            }

            // The token names a range the cached map doesn't know, so the map may predate a split the token has seen.
            routingMap = await this.partitionKeyRangeCache.TryLookupAsync(
                collection.ResourceId,
                previousValue: routingMap,
                request,
                NoOpTrace.Singleton)
                ?? throw new NotFoundException($"Routing map for collection {collection.ResourceId} could not be refreshed.");
            if (ThinClientStoreModel.TryApplyScopedSessionToken(request, compoundSessionToken, collection, routingMap, out PartitionKeyRange targetRange))
            {
                return;
            }

            if (targetRange == null && request.PartitionKeyRangeIdentity != null)
            {
                throw new Documents.Routing.PartitionKeyRangeGoneException(
                    $"Partition key range {request.PartitionKeyRangeIdentity.PartitionKeyRangeId} of collection {collection.ResourceId} no longer exists.");
            }

            request.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
            DefaultTrace.TraceWarning(
                "compound_session_token_scope_unresolved_after_refresh: the session token names partition key ranges that are unknown even after refreshing partition key range metadata; "
                    + "the request is sent through the thin client proxy without a session token, so the supplied session progress is not enforced. "
                    + "Verify that the token belongs to this container. Reason={0}; CollectionRid={1}; PartitionKeyRangeId={2}; ActivityId={3}",
                targetRange == null ? "unresolved_target" : "unknown_range_or_ancestry",
                collection.ResourceId,
                targetRange?.Id ?? "unresolved",
                System.Diagnostics.Trace.CorrelationManager.ActivityId);
        }

        /// <summary>
        /// Sets the session token header to the target range's progress (merged with its parents') when the target
        /// range resolves against <paramref name="routingMap"/> and every range the token names is known to it.
        /// Every segment is validated, so a malformed token throws <see cref="BadRequestException"/>.
        /// </summary>
        private static bool TryApplyScopedSessionToken(
            DocumentServiceRequest request,
            string compoundSessionToken,
            ContainerProperties collection,
            CollectionRoutingMap routingMap,
            out PartitionKeyRange targetRange)
        {
            targetRange = ThinClientStoreModel.ResolveTargetRange(request, collection, routingMap);
            if (targetRange != null)
            {
                // Keeps the proxy's EPK routing on the range the token is scoped to; SessionTokenHelper also reads
                // the range's parents from here.
                request.RequestContext.ResolvedPartitionKeyRange = targetRange;
            }

            ISessionToken scopedToken = SessionTokenHelper.GetLocalSessionToken(request, compoundSessionToken, targetRange?.Id ?? string.Empty);
            if (targetRange == null || !ThinClientStoreModel.AreSessionTokenRangesKnown(compoundSessionToken, routingMap))
            {
                return false;
            }

            if (scopedToken == null)
            {
                request.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
            }
            else
            {
                request.Headers[HttpConstants.HttpHeaders.SessionToken] = SessionTokenHelper.SerializeSessionToken(targetRange.Id, scopedToken);
            }

            return true;
        }

        /// <summary>
        /// Resolves the request's target range against <paramref name="routingMap"/>: the explicit range identity if
        /// present, otherwise the already-resolved range if it is still current, otherwise the partition key's range.
        /// </summary>
        private static PartitionKeyRange ResolveTargetRange(
            DocumentServiceRequest request,
            ContainerProperties collection,
            CollectionRoutingMap routingMap)
        {
            if (request.PartitionKeyRangeIdentity != null)
            {
                return routingMap.TryGetRangeByPartitionKeyRangeId(request.PartitionKeyRangeIdentity.PartitionKeyRangeId);
            }

            string resolvedRangeId = request.RequestContext.ResolvedPartitionKeyRange?.Id;
            PartitionKeyRange currentRange = resolvedRangeId == null ? null : routingMap.TryGetRangeByPartitionKeyRangeId(resolvedRangeId);
            if (currentRange != null)
            {
                return currentRange;
            }

            string partitionKeyString = request.Headers[HttpConstants.HttpHeaders.PartitionKey];
            return partitionKeyString == null
                ? null
                : AddressResolver.TryResolveServerPartitionByPartitionKey(
                    request,
                    partitionKeyString,
                    collectionCacheUptoDate: false,
                    collection,
                    routingMap);
        }

        /// <summary>
        /// True when every segment of the compound token names a current range of <paramref name="routingMap"/> or one
        /// the map knows has split. An unknown range means the cached map may be older than the token (or the token
        /// belongs to another container).
        /// </summary>
        private static bool AreSessionTokenRangesKnown(string compoundSessionToken, CollectionRoutingMap routingMap)
        {
            foreach (string segment in compoundSessionToken.Split(SessionTokenHelper.CharArrayWithComma, StringSplitOptions.RemoveEmptyEntries))
            {
                string rangeId = segment.Split(SessionTokenHelper.CharArrayWithColon, StringSplitOptions.RemoveEmptyEntries)[0];
                if (routingMap.TryGetRangeByPartitionKeyRangeId(rangeId) == null && !routingMap.IsGone(rangeId))
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<DocumentServiceResponse> InvokeThinClientAsync(
            DocumentServiceRequest request,
            Uri physicalAddress,
            Uri thinClientEndpoint,
            CancellationToken cancellationToken)
        {
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

        /// <summary>
        /// Single source of truth for the per-request thin-client-vs-gateway routing decision. Returns true, and
        /// the resolved healthy thin-client endpoint, only when the request will actually be dispatched through the
        /// thin-client proxy; otherwise the request transparently falls back to the inherited gateway HTTP path.
        /// Both <see cref="DispatchAsync"/> and the transport diagnostics label consult this method so the reported
        /// store model always matches the path actually taken. Side-effect-free: only reads live routing state.
        /// </summary>
        internal bool TryGetHealthyThinClientEndpoint(DocumentServiceRequest request, out Uri thinClientEndpoint)
        {
            thinClientEndpoint = null;

            if (this.thinClientStoreClient == null
                || !ThinClientStoreModel.IsThinClientRoutable(this.endpointManager, request))
            {
                return false;
            }

            // Compute the candidate endpoint without pinning it onto the request (RouteToLocation). Pinning is
            // owned by ClientRetryPolicy.OnBeforeSendRequest, which validates probe health before it commits an
            // endpoint. Keeping this method side-effect-free means a failed probe here can never leave a proxy
            // endpoint pinned for a request that then falls back to Gateway V1, and the diagnostics-label call
            // (WillRouteToThinClient) can safely evaluate routability without mutating the request.
            Uri candidate = this.endpointManager.GetThinClientEndpointCandidate(request);

            // Per-region probe gate: route to the proxy only when this request's candidate regional endpoint has
            // been confirmed healthy. An un-probed or failed region falls back to Gateway V1.
            if (!this.endpointManager.IsProxyEndpointHealthy(candidate))
            {
                return false;
            }

            thinClientEndpoint = candidate;
            return true;
        }

        /// <summary>
        /// True when the given request will be dispatched through the thin-client proxy on this instance, false when
        /// it will transparently fall back to the inherited gateway HTTP path. Used by the transport layer to label
        /// diagnostics with the store model that actually serves the request.
        /// </summary>
        internal bool WillRouteToThinClient(DocumentServiceRequest request)
        {
            return this.TryGetHealthyThinClientEndpoint(request, out _);
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
