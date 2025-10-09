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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.FaultInjection;
    using Newtonsoft.Json;

    // Marking it as non-sealed in order to unit test it using Moq framework
    internal class GatewayStoreModel : IStoreModelExtension, IDisposable
    {
        private readonly bool isThinClientEnabled;
        private static readonly string sessionConsistencyAsString = ConsistencyLevel.Session.ToString();
        private readonly GlobalPartitionEndpointManager globalPartitionEndpointManager;
        private readonly ISessionContainer sessionContainer;
        private readonly DocumentClientEventSource eventSource;
        private readonly IChaosInterceptor chaosInterceptor;

        internal readonly GlobalEndpointManager endpointManager;
        internal readonly ConsistencyLevel defaultConsistencyLevel;

        // Store Clients to send requests to the gateway and/ or thin client endpoints.
        private ThinClientStoreClient thinClientStoreClient;
        private GatewayStoreClient gatewayStoreClient;

        // Caches to resolve the PartitionKeyRange from request. For Session Token Optimization.
        private PartitionKeyRangeCache partitionKeyRangeCache;
        private ClientCollectionCache clientCollectionCache;

        public GatewayStoreModel(
             GlobalEndpointManager endpointManager,
             ISessionContainer sessionContainer,
             ConsistencyLevel defaultConsistencyLevel,
             DocumentClientEventSource eventSource,
             JsonSerializerSettings serializerSettings,
             CosmosHttpClient httpClient,
             GlobalPartitionEndpointManager globalPartitionEndpointManager,
             bool isThinClientEnabled,
             UserAgentContainer userAgentContainer = null,
             IChaosInterceptor chaosInterceptor = null)
        {
            this.endpointManager = endpointManager;
            this.sessionContainer = sessionContainer;
            this.defaultConsistencyLevel = defaultConsistencyLevel;
            this.eventSource = eventSource;
            this.globalPartitionEndpointManager = globalPartitionEndpointManager;
            this.chaosInterceptor = chaosInterceptor;
            this.gatewayStoreClient = new GatewayStoreClient(
                httpClient,
                this.eventSource,
                globalPartitionEndpointManager,
                serializerSettings);
            this.isThinClientEnabled = isThinClientEnabled;

            if (isThinClientEnabled)
            {
                this.thinClientStoreClient = new ThinClientStoreClient(
                    httpClient,
                    userAgentContainer,
                    this.eventSource,
                    globalPartitionEndpointManager,
                    serializerSettings,
                    this.chaosInterceptor);
            }

            this.globalPartitionEndpointManager.SetBackgroundConnectionPeriodicRefreshTask(
                this.MarkEndpointsToHealthyAsync);
        }

        public virtual async Task<DocumentServiceResponse> ProcessMessageAsync(DocumentServiceRequest request, CancellationToken cancellationToken = default)
        {
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
                this.endpointManager.TryGetLocationForGatewayDiagnostics(request.RequestContext.LocationEndpointToRoute, out string regionName))
                {
                    request.RequestContext.RegionName = regionName;
                }

                bool isPPAFEnabled = this.IsPartitionLevelFailoverEnabled();
                // This is applicable for both per partition automatic failover and per partition circuit breaker.
                if ((isPPAFEnabled || this.isThinClientEnabled)
                    && !ReplicatedResourceClient.IsMasterResource(request.ResourceType)
                    && request.ResourceType.IsPartitioned())
                {
                    ContainerProperties collection = await this.clientCollectionCache.ResolveCollectionAsync(
                        request,
                        CancellationToken.None,
                        NoOpTrace.Singleton);

                    PartitionKeyDefinition partitionKeyDefinition = null;
                    if (collection != null)
                    {
                        partitionKeyDefinition = collection.PartitionKey;
                    }
                    (bool isSuccess, PartitionKeyRange partitionKeyRange) = await TryResolvePartitionKeyRangeAsync(
                        request: request,
                        sessionContainer: this.sessionContainer,
                        partitionKeyRangeCache: this.partitionKeyRangeCache,
                        clientCollectionCache: this.clientCollectionCache,
                        partitionKeyDefinition: partitionKeyDefinition,
                        refreshCache: false);

                    request.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;

                    if (isPPAFEnabled)
                    {
                        this.globalPartitionEndpointManager.TryAddPartitionLevelLocationOverride(request);
                    }
                }

                bool canUseThinClient =
                    this.thinClientStoreClient != null &&
                    GatewayStoreModel.IsOperationSupportedByThinClient(request);

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

                long longValue;
                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.MaxMediaStorageUsageInMB, out IEnumerable<string> headerValues) &&
                    (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out longValue))
                    {
                        databaseAccount.MaxMediaStorageUsageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.CurrentMediaStorageUsageInMB, out headerValues) &&
                    (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out longValue))
                    {
                        databaseAccount.MediaStorageUsageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.DatabaseAccountConsumedDocumentStorageInMB, out headerValues) &&
                   (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out longValue))
                    {
                        databaseAccount.ConsumedDocumentStorageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.DatabaseAccountProvisionedDocumentStorageInMB, out headerValues) &&
                   (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out longValue))
                    {
                        databaseAccount.ProvisionedDocumentStorageInMB = longValue;
                    }
                }

                if (responseMessage.Headers.TryGetValues(HttpConstants.HttpHeaders.DatabaseAccountReservedDocumentStorageInMB, out headerValues) &&
                   (headerValues.Count() != 0))
                {
                    if (long.TryParse(headerValues.First(), out longValue))
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

        internal async Task CaptureSessionTokenAndHandleSplitAsync(
            HttpStatusCode? statusCode,
            SubStatusCodes subStatusCode,
            DocumentServiceRequest request,
            INameValueCollection responseHeaders)
        {
            // Exceptionless can try to capture session token from CompleteResponse
            if (request.IsValidStatusCodeForExceptionlessRetry((int)statusCode, subStatusCode))
            {
                // Not capturing on master resources
                if (ReplicatedResourceClient.IsMasterResource(request.ResourceType))
                {
                    return;
                }

                // Only capturing on 409, 412, 404 && !1002
                if (statusCode != HttpStatusCode.PreconditionFailed
                    && statusCode != HttpStatusCode.Conflict
                        && (statusCode != HttpStatusCode.NotFound || subStatusCode == SubStatusCodes.ReadSessionNotAvailable))
                {
                    return;
                }
            }

            if (request.ResourceType == ResourceType.Collection && request.OperationType == OperationType.Delete)
            {
                string resourceId;

                if (request.IsNameBased)
                {
                    resourceId = responseHeaders[HttpConstants.HttpHeaders.OwnerId];
                }
                else
                {
                    resourceId = request.ResourceId;
                }

                this.sessionContainer.ClearTokenByResourceId(resourceId);
            }
            else
            {
                this.sessionContainer.SetSessionToken(request, responseHeaders);
                PartitionKeyRange detectedPartitionKeyRange = request.RequestContext.ResolvedPartitionKeyRange;
                string partitionKeyRangeInResponse = responseHeaders[HttpConstants.HttpHeaders.PartitionKeyRangeId];
                if (detectedPartitionKeyRange != null
                    && !string.IsNullOrEmpty(partitionKeyRangeInResponse)
                    && !string.IsNullOrEmpty(request.RequestContext.ResolvedCollectionRid)
                    && !partitionKeyRangeInResponse.Equals(detectedPartitionKeyRange.Id, StringComparison.OrdinalIgnoreCase))
                {
                    // The request ended up being on a different partition unknown to the client, so we better refresh the caches

                    ContainerProperties collection = await this.clientCollectionCache.ResolveCollectionAsync(
                        request,
                        CancellationToken.None,
                        NoOpTrace.Singleton);

                    PartitionKeyDefinition partitionKeyDefinition = null;
                    if (collection != null)
                    {
                        partitionKeyDefinition = collection.PartitionKey;
                    }

                    await this.partitionKeyRangeCache.TryGetPartitionKeyRangeByIdAsync(
                        request.RequestContext.ResolvedCollectionRid,
                        partitionKeyRangeInResponse,
                        NoOpTrace.Singleton,
                        partitionKeyDefinition,
                        forceRefresh: true);
                }
            }
        }

        internal static async Task ApplySessionTokenAsync(
            DocumentServiceRequest request,
            ConsistencyLevel defaultConsistencyLevel,
            ISessionContainer sessionContainer,
            PartitionKeyRangeCache partitionKeyRangeCache,
            CollectionCache clientCollectionCache,
            IGlobalEndpointManager globalEndpointManager)
        {
            if (request.Headers == null)
            {
                Debug.Fail("DocumentServiceRequest does not have headers.");
                return;
            }

            // Master resource operations don't require session token.
            if (GatewayStoreModel.IsMasterOperation(request.ResourceType, request.OperationType))
            {
                if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.SessionToken]))
                {
                    request.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
                }

                return;
            }

            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.SessionToken]))
            {
                return; // User is explicitly controlling the session.
            }

            string requestConsistencyLevel = request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel];
            bool isReadOrBatchRequest = request.IsReadOnlyRequest || request.OperationType == OperationType.Batch;
            bool requestHasConsistencySet = !string.IsNullOrEmpty(requestConsistencyLevel) && isReadOrBatchRequest; // Only read requests can have their consistency modified
            
            bool sessionConsistencyApplies =
                (!requestHasConsistencySet && defaultConsistencyLevel == ConsistencyLevel.Session) ||
                (requestHasConsistencySet
                    && string.Equals(requestConsistencyLevel, GatewayStoreModel.sessionConsistencyAsString, StringComparison.OrdinalIgnoreCase));

            bool isMultiMasterEnabledForRequest = globalEndpointManager.CanUseMultipleWriteLocations(request);

            if (!sessionConsistencyApplies
                || (!isReadOrBatchRequest
                    && !isMultiMasterEnabledForRequest))
            {
                return; // Only apply the session token in case of session consistency and the request is read only or read/write on multimaster
            }

            (bool isSuccess, string sessionToken) = await GatewayStoreModel.TryResolveSessionTokenAsync(
                request,
                sessionContainer,
                partitionKeyRangeCache,
                clientCollectionCache);

            if (isSuccess && !string.IsNullOrEmpty(sessionToken))
            {
                request.Headers[HttpConstants.HttpHeaders.SessionToken] = sessionToken;
            }
        }

        internal static async Task<Tuple<bool, string>> TryResolveSessionTokenAsync(
            DocumentServiceRequest request,
            ISessionContainer sessionContainer,
            PartitionKeyRangeCache partitionKeyRangeCache,
            CollectionCache clientCollectionCache)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (sessionContainer == null)
            {
                throw new ArgumentNullException(nameof(sessionContainer));
            }

            if (partitionKeyRangeCache == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyRangeCache));
            }

            if (clientCollectionCache == null)
            {
                throw new ArgumentNullException(nameof(clientCollectionCache));
            }

            if (request.ResourceType.IsPartitioned())
            {
                ContainerProperties collection = await clientCollectionCache.ResolveCollectionAsync(
                    request,
                    CancellationToken.None,
                    NoOpTrace.Singleton);

                PartitionKeyDefinition partitionKeyDefinition = null;
                if (collection != null)
                {
                    partitionKeyDefinition = collection.PartitionKey;
                }

                (bool isSuccess, PartitionKeyRange partitionKeyRange) = await TryResolvePartitionKeyRangeAsync(
                    request: request,
                    sessionContainer: sessionContainer,
                    partitionKeyRangeCache: partitionKeyRangeCache,
                    clientCollectionCache: clientCollectionCache,
                    partitionKeyDefinition: partitionKeyDefinition,
                    refreshCache: false);

                if (isSuccess && sessionContainer is SessionContainer gatewaySessionContainer)
                {
                    request.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
                    string localSessionToken = gatewaySessionContainer.ResolvePartitionLocalSessionTokenForGateway(request, partitionKeyRange.Id);
                    if (!string.IsNullOrEmpty(localSessionToken))
                    {
                        return new Tuple<bool, string>(true, localSessionToken);
                    }
                }
            }

            return new Tuple<bool, string>(false, null);
        }

        private bool IsPartitionLevelFailoverEnabled()
        {
            return this.globalPartitionEndpointManager.IsPartitionLevelCircuitBreakerEnabled()
                || this.globalPartitionEndpointManager.IsPartitionLevelAutomaticFailoverEnabled();
        }

        private static async Task<Tuple<bool, PartitionKeyRange>> TryResolvePartitionKeyRangeAsync(
            DocumentServiceRequest request,
            ISessionContainer sessionContainer,
            PartitionKeyRangeCache partitionKeyRangeCache,
            CollectionCache clientCollectionCache,
            bool refreshCache,
            PartitionKeyDefinition partitionKeyDefinition)
        {
            if (refreshCache)
            {
                request.ForceMasterRefresh = true;
                request.ForceNameCacheRefresh = true;
            }

            PartitionKeyRange partitonKeyRange = null;
            ContainerProperties collection = await clientCollectionCache.ResolveCollectionAsync(
                request,
                CancellationToken.None,
                NoOpTrace.Singleton);

            string partitionKeyString = request.Headers[HttpConstants.HttpHeaders.PartitionKey];
            if (partitionKeyString != null)
            {
                CollectionRoutingMap collectionRoutingMap = await partitionKeyRangeCache.TryLookupAsync(
                    collectionRid: collection.ResourceId,
                    previousValue: null,
                    request: request,
                    NoOpTrace.Singleton,
                    partitionKeyDefinition);

                if (refreshCache && collectionRoutingMap != null)
                {
                    collectionRoutingMap = await partitionKeyRangeCache.TryLookupAsync(
                        collectionRid: collection.ResourceId,
                        previousValue: collectionRoutingMap,
                        request: request,
                        NoOpTrace.Singleton,
                        partitionKeyDefinition);
                }

                if (collectionRoutingMap != null)
                {
                    partitonKeyRange = AddressResolver.TryResolveServerPartitionByPartitionKey(
                        request: request,
                        partitionKeyString: partitionKeyString,
                        collectionCacheUptoDate: false,
                        collection: collection,
                        routingMap: collectionRoutingMap);
                }
            }
            else if (request.PartitionKeyRangeIdentity != null)
            {
                PartitionKeyRangeIdentity partitionKeyRangeId = request.PartitionKeyRangeIdentity;
                partitonKeyRange = await partitionKeyRangeCache.TryGetPartitionKeyRangeByIdAsync(
                    collection.ResourceId,
                    partitionKeyRangeId.PartitionKeyRangeId,
                    NoOpTrace.Singleton,
                    partitionKeyDefinition,
                    refreshCache);
            }
            else if (request.RequestContext.ResolvedPartitionKeyRange != null)
            {
                partitonKeyRange = request.RequestContext.ResolvedPartitionKeyRange;
            }

            if (partitonKeyRange == null)
            {
                if (refreshCache)
                {
                    return new Tuple<bool, PartitionKeyRange>(false, null);
                }

                // need to refresh cache. Maybe split happened.
                return await GatewayStoreModel.TryResolvePartitionKeyRangeAsync(
                    request: request,
                    sessionContainer: sessionContainer,
                    partitionKeyRangeCache: partitionKeyRangeCache,
                    clientCollectionCache: clientCollectionCache,
                    partitionKeyDefinition: partitionKeyDefinition,
                    refreshCache: true);
            }

            return new Tuple<bool, PartitionKeyRange>(true, partitonKeyRange);
        }

        /// <summary>
        /// Attempts to mark the unhealthy endpoints for a faulty partition to healthy state, un-deterministically. This is done
        /// specifically for the gateway mode to get the faulty partition failed back to the original location.
        /// </summary>
        /// <param name="pkRangeUriMappings">A dictionary mapping partition key ranges to their corresponding collection resource ID, original failed location, and health status.</param>
        public Task MarkEndpointsToHealthyAsync(
            Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>> pkRangeUriMappings)
        {
            foreach (PartitionKeyRange pkRange in pkRangeUriMappings?.Keys)
            {
                string collectionRid = pkRangeUriMappings[pkRange].Item1;
                Uri originalFailedLocation = pkRangeUriMappings[pkRange].Item2;

                DefaultTrace.TraceVerbose("Un-deterministically marking the original failed endpoint: {0}, for the PkRange: {1}, collectionRid: {2} back to healthy.",
                    originalFailedLocation,
                    pkRange.Id,
                    collectionRid);

                pkRangeUriMappings[pkRange] = new Tuple<string, Uri, TransportAddressHealthState.HealthStatus>(collectionRid, originalFailedLocation, TransportAddressHealthState.HealthStatus.Connected);
            }

            return Task.CompletedTask;
        }

        // DEVNOTE: This can be replace with ReplicatedResourceClient.IsMasterOperation on next Direct sync
        internal static bool IsMasterOperation(
            ResourceType resourceType,
            OperationType operationType)
        {
            // Stored procedures, trigger, and user defined functions CRUD operations are done on
            // master so they do not require the session token. Stored procedures execute is not a master operation
            return ReplicatedResourceClient.IsMasterResource(resourceType) ||
                   GatewayStoreModel.IsStoredProcedureCrudOperation(resourceType, operationType) ||
                   resourceType == ResourceType.Trigger ||
                   resourceType == ResourceType.UserDefinedFunction ||
                   operationType == OperationType.QueryPlan;
        }

        internal static bool IsStoredProcedureCrudOperation(
            ResourceType resourceType,
            OperationType operationType)
        {
            return resourceType == ResourceType.StoredProcedure &&
                   operationType != Documents.OperationType.ExecuteJavaScript;
        }

        internal static bool IsOperationSupportedByThinClient(DocumentServiceRequest request)
        {
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
            AccountProperties accountProperties = await this.endpointManager.GetDatabaseAccountAsync();
            if (accountProperties != null)
            {
                return accountProperties;
            }

            throw new InvalidOperationException("Failed to retrieve AccountProperties. The response was null.");
        }

        protected virtual void Dispose(bool disposing)
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
                if (this.gatewayStoreClient != null)
                {
                    try
                    {
                        this.gatewayStoreClient.Dispose();
                    }
                    catch (Exception exception)
                    {
                        DefaultTrace.TraceWarning("Exception {0} thrown during dispose of HttpClient, this could happen if there are inflight request during the dispose of client",
                            exception.Message);
                    }

                    this.gatewayStoreClient = null;
                }
            }
        }

        internal Uri GetEntityUri(DocumentServiceRequest entity)
        {
            string contentLocation = entity.Headers[HttpConstants.HttpHeaders.ContentLocation];

            if (!string.IsNullOrEmpty(contentLocation))
            {
                return new Uri(this.endpointManager.ResolveServiceEndpoint(entity), new Uri(contentLocation).AbsolutePath);
            }

            return new Uri(this.endpointManager.ResolveServiceEndpoint(entity), PathsHelper.GeneratePath(entity.ResourceType, entity, false));
        }

        internal Uri GetFeedUri(DocumentServiceRequest request)
        {
            return new Uri(this.endpointManager.ResolveServiceEndpoint(request), PathsHelper.GeneratePath(request.ResourceType, request, true));
        }

        public Task OpenConnectionsToAllReplicasAsync(string databaseName, string containerLinkUri, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
