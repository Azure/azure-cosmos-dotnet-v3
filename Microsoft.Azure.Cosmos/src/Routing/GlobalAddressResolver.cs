//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// AddressCache implementation for client SDK. Supports cross region address routing based on
    /// avaialbility and preference list.
    /// </summary>
    internal sealed class GlobalAddressResolver : IAddressResolverExtension, IDisposable
    {
        private const int MaxBackupReadRegions = 3;
        private readonly GlobalEndpointManager endpointManager;
        private readonly GlobalPartitionEndpointManager partitionKeyRangeLocationCache;
        private readonly Protocol protocol;
        private readonly ICosmosAuthorizationTokenProvider tokenProvider;
        private readonly CollectionCache collectionCache;
        private readonly PartitionKeyRangeCache routingMapProvider;
        private readonly int maxEndpoints;
        private readonly IServiceConfigurationReader serviceConfigReader;
        private readonly CosmosHttpClient httpClient;
        private readonly ConcurrentDictionary<Uri, EndpointCache> addressCacheByEndpoint;
        private readonly bool enableTcpConnectionEndpointRediscovery;
        private readonly bool isReplicaAddressValidationEnabled;
        private readonly bool enableAsyncCacheExceptionNoSharing;
        private readonly IConnectionStateListener connectionStateListener;
        private IOpenConnectionsHandler openConnectionsHandler;

        public GlobalAddressResolver(
            GlobalEndpointManager endpointManager,
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache,
            Protocol protocol,
            ICosmosAuthorizationTokenProvider tokenProvider,
            CollectionCache collectionCache,
            PartitionKeyRangeCache routingMapProvider,
            IServiceConfigurationReader serviceConfigReader,
            ConnectionPolicy connectionPolicy,
            CosmosHttpClient httpClient,
            IConnectionStateListener connectionStateListener,
            bool enableAsyncCacheExceptionNoSharing = true)
        {
            this.endpointManager = endpointManager;
            this.partitionKeyRangeLocationCache = partitionKeyRangeLocationCache;
            this.protocol = protocol;
            this.tokenProvider = tokenProvider;
            this.collectionCache = collectionCache;
            this.routingMapProvider = routingMapProvider;
            this.serviceConfigReader = serviceConfigReader;
            this.httpClient = httpClient;
            this.connectionStateListener = connectionStateListener;

            int maxBackupReadEndpoints =
                !connectionPolicy.EnableReadRequestsFallback.HasValue || connectionPolicy.EnableReadRequestsFallback.Value
                ? GlobalAddressResolver.MaxBackupReadRegions : 0;

            this.enableTcpConnectionEndpointRediscovery = connectionPolicy.EnableTcpConnectionEndpointRediscovery;

            this.isReplicaAddressValidationEnabled = ConfigurationManager.IsReplicaAddressValidationEnabled(connectionPolicy);

            this.enableAsyncCacheExceptionNoSharing = enableAsyncCacheExceptionNoSharing;

            this.maxEndpoints = maxBackupReadEndpoints + 2; // for write and alternate write endpoint (during failover)

            this.addressCacheByEndpoint = new ConcurrentDictionary<Uri, EndpointCache>();

            foreach (Uri endpoint in endpointManager.WriteEndpoints)
            {
                this.GetOrAddEndpoint(endpoint);
            }

            foreach (Uri endpoint in endpointManager.ReadEndpoints)
            {
                this.GetOrAddEndpoint(endpoint);
            }

            this.partitionKeyRangeLocationCache.SetBackgroundConnectionPeriodicRefreshTask(
                this.TryOpenConnectionToUnhealthyEndpointsAsync);
        }

        public async Task OpenAsync(
            string databaseName,
            ContainerProperties collection,
            CancellationToken cancellationToken)
        {
            CollectionRoutingMap routingMap = await this.routingMapProvider.TryLookupAsync(
                    collectionRid: collection.ResourceId,
                    previousValue: null,
                    request: null,
                    trace: NoOpTrace.Singleton);

            if (routingMap == null)
            {
                return;
            }

            List<PartitionKeyRangeIdentity> ranges = routingMap.OrderedPartitionKeyRanges.Select(
                range => new PartitionKeyRangeIdentity(collection.ResourceId, range.Id)).ToList();

            List<Task> tasks = new List<Task>();

            foreach (EndpointCache endpointCache in this.addressCacheByEndpoint.Values)
            {
                tasks.Add(endpointCache.AddressCache.OpenConnectionsAsync(
                    databaseName: databaseName,
                    collection: collection,
                    partitionKeyRangeIdentities: ranges,
                    shouldOpenRntbdChannels: false,
                    cancellationToken: cancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Invokes the gateway address cache to open the rntbd connections to the backend replicas.
        /// </summary>
        /// <param name="databaseName">A string containing the name of the database.</param>
        /// <param name="containerLinkUri">A string containing the container's link uri.</param>
        /// <param name="cancellationToken">An Instance of the <see cref="CancellationToken"/>.</param>
        public async Task OpenConnectionsToAllReplicasAsync(
            string databaseName,
            string containerLinkUri,
            CancellationToken cancellationToken = default)
        {
            try
            {
                ContainerProperties collection = await this.collectionCache.ResolveByNameAsync(
                        apiVersion: HttpConstants.Versions.CurrentVersion,
                        resourceAddress: containerLinkUri,
                        forceRefesh: false,
                        trace: NoOpTrace.Singleton,
                        clientSideRequestStatistics: null,
                        cancellationToken: cancellationToken);

                if (collection == null)
                {
                    throw CosmosExceptionFactory.Create(
                        statusCode: HttpStatusCode.NotFound,
                        message: $"Could not resolve the collection: {containerLinkUri} for database: {databaseName}.",
                        stackTrace: default,
                        headers: new Headers(),
                        trace: NoOpTrace.Singleton,
                        error: null,
                        innerException: default);
                }

                IReadOnlyList<PartitionKeyRange> partitionKeyRanges = await this.routingMapProvider?.TryGetOverlappingRangesAsync(
                        collectionRid: collection.ResourceId,
                        range: FeedRangeEpk.FullRange.Range,
                        trace: NoOpTrace.Singleton);

                IReadOnlyList<PartitionKeyRangeIdentity> partitionKeyRangeIdentities = partitionKeyRanges?.Select(
                    range => new PartitionKeyRangeIdentity(
                        collection.ResourceId,
                        range.Id))
                    .ToList();

                Uri firstPreferredReadRegion = this.endpointManager
                    .ReadEndpoints
                    .First();

                if (!this.addressCacheByEndpoint.ContainsKey(firstPreferredReadRegion))
                {
                    DefaultTrace.TraceWarning("The Address Cache doesn't contain a value for the first preferred read region: {0} under the database: {1}. '{2}'",
                        firstPreferredReadRegion,
                        databaseName,
                        System.Diagnostics.Trace.CorrelationManager.ActivityId);
                    return;
                }

                await this.addressCacheByEndpoint[firstPreferredReadRegion]
                    .AddressCache
                    .OpenConnectionsAsync(
                        databaseName: databaseName,
                        collection: collection,
                        partitionKeyRangeIdentities: partitionKeyRangeIdentities,
                        shouldOpenRntbdChannels: true,
                        cancellationToken: cancellationToken);

            }
            catch (Exception ex)
            {
                throw ex switch
                {
                    DocumentClientException dce => CosmosExceptionFactory.Create(
                        dce,
                        NoOpTrace.Singleton),

                    _ => ex,
                };
            }
        }

        /// <inheritdoc/>
        public void SetOpenConnectionsHandler(IOpenConnectionsHandler openConnectionsHandler)
        {
            this.openConnectionsHandler = openConnectionsHandler;

            // Sets the openConnectionsHandler for the existing address cache.
            // For the new address caches added later, the openConnectionsHandler
            // will be set through the constructor.
            foreach (EndpointCache endpointCache in this.addressCacheByEndpoint.Values)
            {
                endpointCache.AddressCache.SetOpenConnectionsHandler(openConnectionsHandler);
            }
        }

        public async Task<PartitionAddressInformation> ResolveAsync(
            DocumentServiceRequest request,
            bool forceRefresh,
            CancellationToken cancellationToken)
        {
            IAddressResolver resolver = this.GetAddressResolver(request);
            PartitionAddressInformation partitionAddressInformation = await resolver.ResolveAsync(request, forceRefresh, cancellationToken);

            if (!this.partitionKeyRangeLocationCache.TryAddPartitionLevelLocationOverride(request))
            {
                return partitionAddressInformation;
            }

            resolver = this.GetAddressResolver(request);
            return await resolver.ResolveAsync(request, forceRefresh, cancellationToken);
        }

        /// <summary>
        /// Attempts to open connections to unhealthy endpoints by validating and opening Rntbd connections
        /// to the backend replicas. Updates the health status of the endpoints if the connection is successful.
        /// </summary>
        /// <param name="pkRangeUriMappings">A dictionary mapping partition key ranges to their corresponding collection resource ID, original failed location, and health status.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task TryOpenConnectionToUnhealthyEndpointsAsync(
            Dictionary<PartitionKeyRange, Tuple<string, Uri, TransportAddressHealthState.HealthStatus>> pkRangeUriMappings)
        {
            foreach (PartitionKeyRange pkRange in pkRangeUriMappings?.Keys)
            {
                string collectionRid = pkRangeUriMappings[pkRange].Item1;
                Uri originalFailedLocation = pkRangeUriMappings[pkRange].Item2;

                DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                    OperationType.Read,
                    collectionRid,
                    ResourceType.Collection,
                    AuthorizationTokenType.PrimaryMasterKey);

                try
                {
                    PartitionAddressInformation addresses = await this.addressCacheByEndpoint[originalFailedLocation]
                        .AddressCache
                        .TryGetAddressesAsync(
                            request,
                            new PartitionKeyRangeIdentity(collectionRid, pkRange.Id),
                            request.ServiceIdentity,
                            false,
                            CancellationToken.None);

                    PerProtocolPartitionAddressInformation currentAddressInfo = addresses.Get(Protocol.Tcp);
                    IReadOnlyList<TransportAddressUri> transportAddressUris = currentAddressInfo.ReplicaTransportAddressUris;

                    DefaultTrace.TraceVerbose("Trying to open connection to all the replica addresses for the PkRange: {0}, collectionRid: {1} and originalFailedLocation: {2}",
                        pkRange.Id,
                        collectionRid,
                        originalFailedLocation);

                    await this.openConnectionsHandler.TryOpenRntbdChannelsAsync(transportAddressUris);

                    foreach (TransportAddressUri transportAddressUri in transportAddressUris)
                    {
                        if (transportAddressUri.GetCurrentHealthState().GetHealthStatus() == TransportAddressHealthState.HealthStatus.Connected)
                        {
                            DefaultTrace.TraceVerbose("Opened connection to replica addresses: {0}, for the PkRange: {1}, collectionRid: {2} and and current health: {3}",
                                transportAddressUri.Uri,
                                pkRange.Id,
                                collectionRid,
                                transportAddressUri.GetCurrentHealthState().GetHealthStatus());

                            pkRangeUriMappings[pkRange] = new Tuple<string, Uri, TransportAddressHealthState.HealthStatus>(collectionRid, originalFailedLocation, TransportAddressHealthState.HealthStatus.Connected);
                        }
                    }
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceWarning("Failed to open connection to all the replica addresses for the PkRange: {0}, collectionRid: {1} and originalFailedLocation: {2}, with exception: {3}",
                        pkRange.Id,
                        collectionRid,
                        originalFailedLocation,
                        ex.Message);
                }
            }
        }

        /// <summary>
        /// ReplicatedResourceClient will use this API to get the direct connectivity AddressCache for given request.
        /// </summary>
        /// <param name="request"></param>
        private IAddressResolver GetAddressResolver(DocumentServiceRequest request)
        {
            Uri endpoint = this.endpointManager.ResolveServiceEndpoint(request);

            return this.GetOrAddEndpoint(endpoint).AddressResolver;
        }

        public void Dispose()
        {
            foreach (EndpointCache endpointCache in this.addressCacheByEndpoint.Values)
            {
                endpointCache.AddressCache.Dispose();
            }
        }

        private EndpointCache GetOrAddEndpoint(Uri endpoint)
        {
            // The GetorAdd is followed by a call to .Count which in a ConcurrentDictionary
            // will acquire all locks for all buckets. This is really expensive. Since the check
            // there is only to see if we've exceeded the count of endpoints, we can simply
            // avoid that check altogether if we are not adding any more endpoints.
            if (this.addressCacheByEndpoint.TryGetValue(endpoint, out EndpointCache existingCache))
            {
                return existingCache;
            }

            EndpointCache endpointCache = this.addressCacheByEndpoint.GetOrAdd(
                endpoint,
                (Uri resolvedEndpoint) =>
                {
                    GatewayAddressCache gatewayAddressCache = new GatewayAddressCache(
                        resolvedEndpoint,
                        this.protocol,
                        this.tokenProvider,
                        this.serviceConfigReader,
                        this.httpClient,
                        this.openConnectionsHandler,
                        this.connectionStateListener,
                        enableTcpConnectionEndpointRediscovery: this.enableTcpConnectionEndpointRediscovery,
                        replicaAddressValidationEnabled: this.isReplicaAddressValidationEnabled,
                        enableAsyncCacheExceptionNoSharing: this.enableAsyncCacheExceptionNoSharing);

                    string location = this.endpointManager.GetLocation(endpoint);
                    AddressResolver addressResolver = new AddressResolver(null, new NullRequestSigner(), location);
                    addressResolver.InitializeCaches(this.collectionCache, this.routingMapProvider, gatewayAddressCache);

                    return new EndpointCache()
                    {
                        AddressCache = gatewayAddressCache,
                        AddressResolver = addressResolver,
                    };
                });

            if (this.addressCacheByEndpoint.Count > this.maxEndpoints)
            {
                IEnumerable<Uri> allEndpoints = this.endpointManager.WriteEndpoints.Union(this.endpointManager.ReadEndpoints);
                Queue<Uri> endpoints = new Queue<Uri>(allEndpoints.Reverse());

                while (this.addressCacheByEndpoint.Count > this.maxEndpoints)
                {
                    if (endpoints.Count > 0)
                    {
                        this.addressCacheByEndpoint.TryRemove(endpoints.Dequeue(), out EndpointCache removedEntry);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return endpointCache;
        }

        private sealed class EndpointCache
        {
            public GatewayAddressCache AddressCache { get; set; }
            public AddressResolver AddressResolver { get; set; }
        }
    }
}
