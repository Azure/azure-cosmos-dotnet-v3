//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents.Routing;

    internal class GatewayAddressCache : IAddressCache, IDisposable
    {
        private const string protocolFilterFormat = "{0} eq {1}";

        private const string AddressResolutionBatchSize = "AddressResolutionBatchSize";
        private const int DefaultBatchSize = 50;

        // This warmup cache and connection timeout is meant to mimic an indefinite timeframe till which
        // a delay task will run, until a cancellation token is requested to cancel the task. The default
        // value for this timeout is 45 minutes at the moment.
        private static readonly TimeSpan WarmupCacheAndOpenConnectionTimeout = TimeSpan.FromMinutes(45);

        private readonly Uri serviceEndpoint;
        private readonly Uri addressEndpoint;

        private readonly AsyncCacheNonBlocking<PartitionKeyRangeIdentity, PartitionAddressInformation> serverPartitionAddressCache;
        private readonly ConcurrentDictionary<PartitionKeyRangeIdentity, DateTime> suboptimalServerPartitionTimestamps;
        private readonly ConcurrentDictionary<ServerKey, HashSet<PartitionKeyRangeIdentity>> serverPartitionAddressToPkRangeIdMap;
        private readonly IServiceConfigurationReader serviceConfigReader;
        private readonly long suboptimalPartitionForceRefreshIntervalInSeconds;

        private readonly Protocol protocol;
        private readonly string protocolFilter;
        private readonly ICosmosAuthorizationTokenProvider tokenProvider;
        private readonly bool enableTcpConnectionEndpointRediscovery;

        private readonly SemaphoreSlim semaphore;
        private readonly CosmosHttpClient httpClient;
        private readonly bool isReplicaAddressValidationEnabled;
        private readonly IConnectionStateListener connectionStateListener;

        private Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> masterPartitionAddressCache;
        private DateTime suboptimalMasterPartitionTimestamp;
        private bool disposedValue;
        private bool validateUnknownReplicas;
        private IOpenConnectionsHandler openConnectionsHandler;

        public GatewayAddressCache(
            Uri serviceEndpoint,
            Protocol protocol,
            ICosmosAuthorizationTokenProvider tokenProvider,
            IServiceConfigurationReader serviceConfigReader,
            CosmosHttpClient httpClient,
            IOpenConnectionsHandler openConnectionsHandler,
            IConnectionStateListener connectionStateListener,
            long suboptimalPartitionForceRefreshIntervalInSeconds = 600,
            bool enableTcpConnectionEndpointRediscovery = false,
            bool replicaAddressValidationEnabled = false)
        {
            this.addressEndpoint = new Uri(serviceEndpoint + "/" + Paths.AddressPathSegment);
            this.protocol = protocol;
            this.tokenProvider = tokenProvider;
            this.serviceEndpoint = serviceEndpoint;
            this.serviceConfigReader = serviceConfigReader;
            this.serverPartitionAddressCache = new AsyncCacheNonBlocking<PartitionKeyRangeIdentity, PartitionAddressInformation>();
            this.suboptimalServerPartitionTimestamps = new ConcurrentDictionary<PartitionKeyRangeIdentity, DateTime>();
            this.serverPartitionAddressToPkRangeIdMap = new ConcurrentDictionary<ServerKey, HashSet<PartitionKeyRangeIdentity>>();
            this.suboptimalMasterPartitionTimestamp = DateTime.MaxValue;
            this.enableTcpConnectionEndpointRediscovery = enableTcpConnectionEndpointRediscovery;
            this.connectionStateListener = connectionStateListener;

            this.suboptimalPartitionForceRefreshIntervalInSeconds = suboptimalPartitionForceRefreshIntervalInSeconds;

            this.httpClient = httpClient;

            this.protocolFilter =
                string.Format(CultureInfo.InvariantCulture,
                GatewayAddressCache.protocolFilterFormat,
                Constants.Properties.Protocol,
                GatewayAddressCache.ProtocolString(this.protocol));

            this.semaphore = new SemaphoreSlim(1, 1);
            this.openConnectionsHandler = openConnectionsHandler;
            this.isReplicaAddressValidationEnabled = replicaAddressValidationEnabled;
            this.validateUnknownReplicas = false;
        }

        public Uri ServiceEndpoint => this.serviceEndpoint;

        /// <summary>
        /// Gets the address information from the gateway and sets them into the async non blocking cache for later lookup.
        /// Additionally attempts to establish Rntbd connections to the backend replicas based on `shouldOpenRntbdChannels`
        /// boolean flag.
        /// </summary>
        /// <param name="databaseName">A string containing the database name.</param>
        /// <param name="collection">An instance of <see cref="ContainerProperties"/> containing the collection properties.</param>
        /// <param name="partitionKeyRangeIdentities">A read only list containing the partition key range identities.</param>
        /// <param name="shouldOpenRntbdChannels">A boolean flag indicating whether Rntbd connections are required to be established
        /// to the backend replica nodes. For cosmos client initialization and cache warmups, the Rntbd connection are needed to be
        /// openned deterministically to the backend replicas to reduce latency, thus the <paramref name="shouldOpenRntbdChannels"/>
        /// should be set to `true` during cosmos client initialization and cache warmups. The OpenAsync flow from DocumentClient
        /// doesn't require the connections to be opened deterministically thus should set the parameter to `false`.</param>
        /// <param name="cancellationToken">An instance of <see cref="CancellationToken"/>.</param>
        public async Task OpenConnectionsAsync(
            string databaseName,
            ContainerProperties collection,
            IReadOnlyList<PartitionKeyRangeIdentity> partitionKeyRangeIdentities,
            bool shouldOpenRntbdChannels,
            CancellationToken cancellationToken)
        {
            List<Task> tasks = new ();
            int batchSize = GatewayAddressCache.DefaultBatchSize;

            // By design, the Unknown replicas are validated only when the following two conditions meet:
            // 1) The CosmosClient is initiated using the CreateAndInitializaAsync() flow.
            // 2) The advanced replica selection feature enabled.
            if (shouldOpenRntbdChannels)
            {
                this.validateUnknownReplicas = true;
            }

#if !(NETSTANDARD15 || NETSTANDARD16)
#if NETSTANDARD20
            // GetEntryAssembly returns null when loaded from native netstandard2.0
            if (System.Reflection.Assembly.GetEntryAssembly() != null)
            {
#endif
                if (int.TryParse(System.Configuration.ConfigurationManager.AppSettings[GatewayAddressCache.AddressResolutionBatchSize], out int userSpecifiedBatchSize))
                {
                    batchSize = userSpecifiedBatchSize;
                }
#if NETSTANDARD20
            }
#endif  
#endif

            string collectionAltLink = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/{1}/{2}/{3}",
                Paths.DatabasesPathSegment,
                Uri.EscapeUriString(databaseName),
                Paths.CollectionsPathSegment,
                Uri.EscapeUriString(collection.Id));

            using (DocumentServiceRequest request = DocumentServiceRequest.CreateFromName(
                OperationType.Read,
                collectionAltLink,
                ResourceType.Collection,
                AuthorizationTokenType.PrimaryMasterKey))
            {
                for (int i = 0; i < partitionKeyRangeIdentities.Count; i += batchSize)
                {
                    tasks.Add(
                        this.WarmupCachesAndOpenConnectionsAsync(
                                request: request,
                                collectionRid: collection.ResourceId,
                                partitionKeyRangeIds: partitionKeyRangeIdentities.Skip(i).Take(batchSize).Select(range => range.PartitionKeyRangeId),
                                containerProperties: collection,
                                shouldOpenRntbdChannels: shouldOpenRntbdChannels,
                                cancellationToken: cancellationToken));
                }
            }

            using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // The `timeoutTask` is a background task which adds a delay for a period of WarmupCacheAndOpenConnectionTimeout. The task will
            // be cancelled either by - a) when `linkedTokenSource` expires, which means the original `cancellationToken` expires or
            // b) the the `linkedTokenSource.Cancel()` is called.
            Task timeoutTask = Task.Delay(GatewayAddressCache.WarmupCacheAndOpenConnectionTimeout, linkedTokenSource.Token);
            Task resultTask = await Task.WhenAny(Task.WhenAll(tasks), timeoutTask);

            if (resultTask == timeoutTask)
            {
                // Operation has been cancelled.
                DefaultTrace.TraceWarning("The open connection task was cancelled because the cancellation token was expired. '{0}'",
                    System.Diagnostics.Trace.CorrelationManager.ActivityId);
            }
            else
            {
                linkedTokenSource.Cancel();
            }
        }

        /// <inheritdoc/>
        public void SetOpenConnectionsHandler(IOpenConnectionsHandler openConnectionsHandler)
        {
            this.openConnectionsHandler = openConnectionsHandler;
        }

        /// <inheritdoc/>
        public async Task<PartitionAddressInformation> TryGetAddressesAsync(
            DocumentServiceRequest request,
            PartitionKeyRangeIdentity partitionKeyRangeIdentity,
            ServiceIdentity serviceIdentity,
            bool forceRefreshPartitionAddresses,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (partitionKeyRangeIdentity == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyRangeIdentity));
            }

            try
            {
                if (partitionKeyRangeIdentity.PartitionKeyRangeId == PartitionKeyRange.MasterPartitionKeyRangeId)
                {
                    return (await this.ResolveMasterAsync(request, forceRefreshPartitionAddresses)).Item2;
                }

                if (this.suboptimalServerPartitionTimestamps.TryGetValue(partitionKeyRangeIdentity, out DateTime suboptimalServerPartitionTimestamp))
                {
                    bool forceRefreshDueToSuboptimalPartitionReplicaSet =
                        DateTime.UtcNow.Subtract(suboptimalServerPartitionTimestamp) > TimeSpan.FromSeconds(this.suboptimalPartitionForceRefreshIntervalInSeconds);

                    if (forceRefreshDueToSuboptimalPartitionReplicaSet && this.suboptimalServerPartitionTimestamps.TryUpdate(partitionKeyRangeIdentity, DateTime.MaxValue, suboptimalServerPartitionTimestamp))
                    {
                        forceRefreshPartitionAddresses = true;
                    }
                }

                PartitionAddressInformation addresses;
                PartitionAddressInformation staleAddressInfo = null;
                if (forceRefreshPartitionAddresses || request.ForceCollectionRoutingMapRefresh)
                {
                    addresses = await this.serverPartitionAddressCache.GetAsync(
                        key: partitionKeyRangeIdentity,
                        singleValueInitFunc: (currentCachedValue) =>
                        {
                            staleAddressInfo = currentCachedValue;

                            GatewayAddressCache.SetTransportAddressUrisToUnhealthy(
                               currentCachedValue,
                               request?.RequestContext?.FailedEndpoints);

                            return this.GetAddressesForRangeIdAsync(
                                request,
                                cachedAddresses: currentCachedValue,
                                partitionKeyRangeIdentity.CollectionRid,
                                partitionKeyRangeIdentity.PartitionKeyRangeId,
                                forceRefresh: forceRefreshPartitionAddresses);
                        },
                        forceRefresh: (currentCachedValue) =>
                        {
                            int cachedHashCode = request?.RequestContext?.LastPartitionAddressInformationHashCode ?? 0;
                            if (cachedHashCode == 0)
                            {
                                return true;
                            }

                            // The cached value is different then the previous access hash then assume
                            // another request already updated the cache since there is a new value in the cache
                            return currentCachedValue.GetHashCode() == cachedHashCode;
                        });

                    if (staleAddressInfo != null)
                    {
                        GatewayAddressCache.LogPartitionCacheRefresh(request.RequestContext.ClientRequestStatistics, staleAddressInfo, addresses);
                    }

                    this.suboptimalServerPartitionTimestamps.TryRemove(partitionKeyRangeIdentity, out DateTime ignoreDateTime);
                }
                else
                {
                    addresses = await this.serverPartitionAddressCache.GetAsync(
                        key: partitionKeyRangeIdentity,
                        singleValueInitFunc: (_) => this.GetAddressesForRangeIdAsync(
                            request,
                            cachedAddresses: null,
                            partitionKeyRangeIdentity.CollectionRid,
                            partitionKeyRangeIdentity.PartitionKeyRangeId,
                            forceRefresh: false),
                        forceRefresh: (_) => false);
                }

                // Always save the hash code. This is used to determine if another request already updated the cache.
                // This helps reduce latency by avoiding uncessary cache refreshes.
                if (request?.RequestContext != null)
                {
                    request.RequestContext.LastPartitionAddressInformationHashCode = addresses.GetHashCode();
                }

                int targetReplicaSetSize = this.serviceConfigReader.UserReplicationPolicy.MaxReplicaSetSize;
                if (addresses.AllAddresses.Count() < targetReplicaSetSize)
                {
                    this.suboptimalServerPartitionTimestamps.TryAdd(partitionKeyRangeIdentity, DateTime.UtcNow);
                }

                // Refresh the cache on-demand, if there were some address that remained as unhealthy long enough (more than 1 minute)
                // and need to revalidate its status. The reason it is not dependent on 410 to force refresh the addresses, is being:
                // When an address is marked as unhealthy, then the address enumerator will deprioritize it and move it back to the
                // end of the transport uris list. Therefore, it could happen that no request will land on the unhealthy address for
                // an extended period of time therefore, the chances of 410 (Gone Exception) to trigger the forceRefresh workflow may
                // not happen for that particular replica.
                if (addresses
                    .Get(Protocol.Tcp)
                    .ReplicaTransportAddressUris
                    .Any(x => x.ShouldRefreshHealthStatus()))
                {
                    bool slimAcquired = await this.semaphore.WaitAsync(0);
                    try
                    {
                        if (slimAcquired)
                        {
                            this.serverPartitionAddressCache.Refresh(
                                key: partitionKeyRangeIdentity,
                                singleValueInitFunc: (currentCachedValue) => this.GetAddressesForRangeIdAsync(
                                    request,
                                    cachedAddresses: currentCachedValue,
                                    partitionKeyRangeIdentity.CollectionRid,
                                    partitionKeyRangeIdentity.PartitionKeyRangeId,
                                    forceRefresh: true));
                        }
                        else
                        {
                            DefaultTrace.TraceVerbose("Failed to refresh addresses in the background for the collection rid: {0}, partition key range id: {1}, because the semaphore is already acquired. '{2}'",
                                partitionKeyRangeIdentity.CollectionRid,
                                partitionKeyRangeIdentity.PartitionKeyRangeId,
                                System.Diagnostics.Trace.CorrelationManager.ActivityId);
                        }
                    }
                    finally
                    {
                        if (slimAcquired)
                        {
                            this.semaphore.Release();
                        }
                    }
                }

                return addresses;
            }
            catch (DocumentClientException ex)
            {
                if ((ex.StatusCode == HttpStatusCode.NotFound) ||
                    (ex.StatusCode == HttpStatusCode.Gone && ex.GetSubStatus() == SubStatusCodes.PartitionKeyRangeGone))
                {
                    //remove from suboptimal cache in case the the collection+pKeyRangeId combo is gone.
                    this.suboptimalServerPartitionTimestamps.TryRemove(partitionKeyRangeIdentity, out _);

                    return null;
                }

                throw;
            }
            catch (Exception)
            {
                if (forceRefreshPartitionAddresses)
                {
                    this.suboptimalServerPartitionTimestamps.TryRemove(partitionKeyRangeIdentity, out _);
                }

                throw;
            }
        }

        /// <summary>
        /// Gets the address information from the gateway using the partition key range ids, and warms up the async non blocking cache
        /// by inserting them as a key value pair for later lookup. Additionally attempts to establish Rntbd connections to the backend
        /// replicas based on `shouldOpenRntbdChannels` boolean flag.
        /// </summary>
        /// <param name="request">An instance of <see cref="DocumentServiceRequest"/> containing the request payload.</param>
        /// <param name="collectionRid">A string containing the collection ids.</param>
        /// <param name="partitionKeyRangeIds">An instance of <see cref="IEnumerable{T}"/> containing the list of partition key range ids.</param>
        /// <param name="containerProperties">An instance of <see cref="ContainerProperties"/> containing the collection properties.</param>
        /// <param name="shouldOpenRntbdChannels">A boolean flag indicating whether Rntbd connections are required to be established to the backend replica nodes.</param>
        /// <param name="cancellationToken">An instance of <see cref="CancellationToken"/>.</param>
        private async Task WarmupCachesAndOpenConnectionsAsync(
            DocumentServiceRequest request,
            string collectionRid,
            IEnumerable<string> partitionKeyRangeIds,
            ContainerProperties containerProperties,
            bool shouldOpenRntbdChannels,
            CancellationToken cancellationToken)
        {
            TryCatch<DocumentServiceResponse> documentServiceResponseWrapper = await this.GetAddressesAsync(
                                request: request,
                                collectionRid: collectionRid,
                                partitionKeyRangeIds: partitionKeyRangeIds);

            if (documentServiceResponseWrapper.Failed)
            {
                return;
            }

            try
            {
                using (DocumentServiceResponse response = documentServiceResponseWrapper.Result)
                {
                    FeedResource<Address> addressFeed = response.GetResource<FeedResource<Address>>();

                    bool inNetworkRequest = this.IsInNetworkRequest(response);

                    IEnumerable<Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation>> addressInfos =
                        addressFeed.Where(addressInfo => ProtocolFromString(addressInfo.Protocol) == this.protocol)
                            .GroupBy(address => address.PartitionKeyRangeId, StringComparer.Ordinal)
                            .Select(group => this.ToPartitionAddressAndRange(containerProperties.ResourceId, @group.ToList(), inNetworkRequest));

                    List<Task> openConnectionTasks = new ();
                    foreach (Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> addressInfo in addressInfos)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        this.serverPartitionAddressCache.Set(
                            new PartitionKeyRangeIdentity(containerProperties.ResourceId, addressInfo.Item1.PartitionKeyRangeId),
                            addressInfo.Item2);

                        // The `shouldOpenRntbdChannels` boolean flag indicates whether the SDK should establish Rntbd connections to the
                        // backend replica nodes. For the `CosmosClient.CreateAndInitializeAsync()` flow, the flag should be passed as
                        // `true` so that the Rntbd connections to the backend replicas could be established deterministically. For any
                        // other flow, the flag should be passed as `false`.
                        if (this.openConnectionsHandler != null && shouldOpenRntbdChannels)
                        {
                            openConnectionTasks
                                .Add(this.openConnectionsHandler
                                    .TryOpenRntbdChannelsAsync(
                                        addresses: addressInfo.Item2.Get(Protocol.Tcp)?.ReplicaTransportAddressUris));
                        }
                    }

                    await Task.WhenAll(openConnectionTasks);
                }
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("Failed to warm-up caches and open connections for the server addresses: {0} with exception: {1}. '{2}'",
                    collectionRid,
                    ex,
                    System.Diagnostics.Trace.CorrelationManager.ActivityId);
            }
        }

        private static void SetTransportAddressUrisToUnhealthy(
            PartitionAddressInformation stalePartitionAddressInformation,
            Lazy<HashSet<TransportAddressUri>> failedEndpoints)
        {
            if (stalePartitionAddressInformation == null ||
                failedEndpoints == null ||
                !failedEndpoints.IsValueCreated)
            {
                return;
            }

            IReadOnlyList<TransportAddressUri> perProtocolPartitionAddressInformation = stalePartitionAddressInformation.Get(Protocol.Tcp)?.ReplicaTransportAddressUris;
            if (perProtocolPartitionAddressInformation == null)
            {
                return;
            }

            foreach (TransportAddressUri failed in perProtocolPartitionAddressInformation)
            {
                if (failedEndpoints.Value.Contains(failed))
                {
                    failed.SetUnhealthy();
                }
            }
        }

        private static void LogPartitionCacheRefresh(
            IClientSideRequestStatistics clientSideRequestStatistics,
            PartitionAddressInformation old,
            PartitionAddressInformation updated)
        {
            if (clientSideRequestStatistics is ClientSideRequestStatisticsTraceDatum traceDatum)
            {
                traceDatum.RecordAddressCachRefreshContent(old, updated);
            }
        }

        /// <summary>
        /// Marks the <see cref="TransportAddressUri"/> to Unhealthy that matches with the faulted
        /// server key.
        /// </summary>
        /// <param name="serverKey">An instance of <see cref="ServerKey"/> that contains the host and
        /// port of the backend replica.</param>
        public async Task MarkAddressesToUnhealthyAsync(
            ServerKey serverKey)
        {
            if (this.disposedValue)
            {
                // Will enable Listener to un-register in-case of un-graceful dispose
                // <see cref="ConnectionStateMuxListener.NotifyAsync(ServerKey, ConcurrentDictionary{Func{ServerKey, Task}, object})"/>
                throw new ObjectDisposedException(nameof(GatewayAddressCache));
            }

            if (serverKey == null)
            {
                throw new ArgumentNullException(nameof(serverKey));
            }

            if (this.serverPartitionAddressToPkRangeIdMap.TryGetValue(serverKey, out HashSet<PartitionKeyRangeIdentity> pkRangeIds))
            {
                PartitionKeyRangeIdentity[] pkRangeIdsCopy;
                lock (pkRangeIds)
                {
                    pkRangeIdsCopy = pkRangeIds.ToArray();
                }

                foreach (PartitionKeyRangeIdentity pkRangeId in pkRangeIdsCopy)
                {
                    // The forceRefresh flag is set to true for the callback delegate is because, if the GetAsync() from the async
                    // non-blocking cache fails to look up the pkRangeId, then there are some inconsistency present in the cache, and it is
                    // more safe to do a force refresh to fetch the addresses from the gateway, instead of fetching it from the cache itself.
                    // Please note that, the chances of encountering such scenario is highly unlikely.
                    PartitionAddressInformation addressInfo = await this.serverPartitionAddressCache.GetAsync(
                       key: pkRangeId,
                       singleValueInitFunc: (_) => this.GetAddressesForRangeIdAsync(
                           null,
                           cachedAddresses: null,
                           pkRangeId.CollectionRid,
                           pkRangeId.PartitionKeyRangeId,
                           forceRefresh: true),
                       forceRefresh: (_) => false);

                    IReadOnlyList<TransportAddressUri> transportAddresses = addressInfo.Get(Protocol.Tcp)?.ReplicaTransportAddressUris;
                    foreach (TransportAddressUri address in from TransportAddressUri transportAddress in transportAddresses
                                                            where serverKey.Equals(transportAddress.ReplicaServerKey)
                                                            select transportAddress)
                    {
                        DefaultTrace.TraceInformation("Marking a backend replica to Unhealthy for collectionRid :{0}, pkRangeId: {1}, serviceEndpoint: {2}, transportAddress: {3}",
                           pkRangeId.CollectionRid,
                           pkRangeId.PartitionKeyRangeId,
                           this.serviceEndpoint,
                           address.ToString());

                        address.SetUnhealthy();
                    }

                    // Update the health status
                    this.CaptureTransportAddressUriHealthStates(addressInfo, transportAddresses);
                }
            }
        }

        private async Task<Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation>> ResolveMasterAsync(DocumentServiceRequest request, bool forceRefresh)
        {
            Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> masterAddressAndRange = this.masterPartitionAddressCache;

            int targetReplicaSetSize = this.serviceConfigReader.SystemReplicationPolicy.MaxReplicaSetSize;

            forceRefresh = forceRefresh ||
                (masterAddressAndRange != null &&
                masterAddressAndRange.Item2.AllAddresses.Count() < targetReplicaSetSize &&
                DateTime.UtcNow.Subtract(this.suboptimalMasterPartitionTimestamp) > TimeSpan.FromSeconds(this.suboptimalPartitionForceRefreshIntervalInSeconds));

            if (forceRefresh || request.ForceCollectionRoutingMapRefresh || this.masterPartitionAddressCache == null)
            {
                string entryUrl = PathsHelper.GeneratePath(
                   ResourceType.Database,
                   string.Empty,
                   true);

                try
                {
                    using (DocumentServiceResponse response = await this.GetMasterAddressesViaGatewayAsync(
                        request,
                        ResourceType.Database,
                        null,
                        entryUrl,
                        forceRefresh,
                        false))
                    {
                        FeedResource<Address> masterAddresses = response.GetResource<FeedResource<Address>>();

                        bool inNetworkRequest = this.IsInNetworkRequest(response);

                        masterAddressAndRange = this.ToPartitionAddressAndRange(string.Empty, masterAddresses.ToList(), inNetworkRequest);
                        this.masterPartitionAddressCache = masterAddressAndRange;
                        this.suboptimalMasterPartitionTimestamp = DateTime.MaxValue;
                    }
                }
                catch (Exception)
                {
                    this.suboptimalMasterPartitionTimestamp = DateTime.MaxValue;
                    throw;
                }
            }

            if (masterAddressAndRange.Item2.AllAddresses.Count() < targetReplicaSetSize && this.suboptimalMasterPartitionTimestamp.Equals(DateTime.MaxValue))
            {
                this.suboptimalMasterPartitionTimestamp = DateTime.UtcNow;
            }

            return masterAddressAndRange;
        }

        private async Task<PartitionAddressInformation> GetAddressesForRangeIdAsync(
            DocumentServiceRequest request,
            PartitionAddressInformation cachedAddresses,
            string collectionRid,
            string partitionKeyRangeId,
            bool forceRefresh)
        {
            using (DocumentServiceResponse response =
                await this.GetServerAddressesViaGatewayAsync(request, collectionRid, new[] { partitionKeyRangeId }, forceRefresh))
            {
                FeedResource<Address> addressFeed = response.GetResource<FeedResource<Address>>();

                bool inNetworkRequest = this.IsInNetworkRequest(response);

                IEnumerable<Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation>> addressInfos =
                    addressFeed.Where(addressInfo => ProtocolFromString(addressInfo.Protocol) == this.protocol)
                        .GroupBy(address => address.PartitionKeyRangeId, StringComparer.Ordinal)
                        .Select(group => this.ToPartitionAddressAndRange(collectionRid, @group.ToList(), inNetworkRequest));

                Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> result =
                    addressInfos.SingleOrDefault(
                        addressInfo => StringComparer.Ordinal.Equals(addressInfo.Item1.PartitionKeyRangeId, partitionKeyRangeId));

                if (result == null)
                {
                    string errorMessage = string.Format(
                        CultureInfo.InvariantCulture,
                        RMResources.PartitionKeyRangeNotFound,
                        partitionKeyRangeId,
                        collectionRid);

                    throw new PartitionKeyRangeGoneException(errorMessage) { ResourceAddress = collectionRid };
                }

                if (this.isReplicaAddressValidationEnabled)
                {
                    // The purpose of this step is to merge the new transport addresses with the old one. What this means is -
                    // 1. If a newly returned address from gateway is already a part of the cache, then restore the health state
                    // of the new address with that of the cached one.
                    // 2. If a newly returned address from gateway doesn't exist in the cache, then keep using the new address
                    // with `Unknown` (initial) status.
                    PartitionAddressInformation mergedAddresses = GatewayAddressCache.MergeAddresses(result.Item2, cachedAddresses);
                    IReadOnlyList<TransportAddressUri> transportAddressUris = mergedAddresses.Get(Protocol.Tcp)?.ReplicaTransportAddressUris;

                    // If cachedAddresses are null, that would mean that the returned address from gateway would remain in Unknown
                    // status and there is no cached state that could transition them into Unhealthy.
                    if (cachedAddresses != null)
                    {
                        foreach (TransportAddressUri address in transportAddressUris)
                        {
                            // The main purpose for this step is to move address health status from Unhealthy to UnhealthyPending.
                            address.SetRefreshedIfUnhealthy();
                        }
                    }

                    this.ValidateReplicaAddresses(transportAddressUris);
                    this.CaptureTransportAddressUriHealthStates(
                        partitionAddressInformation: mergedAddresses,
                        transportAddressUris: transportAddressUris);

                    return mergedAddresses;
                }

                this.CaptureTransportAddressUriHealthStates(
                    partitionAddressInformation: result.Item2,
                    transportAddressUris: result.Item2.Get(Protocol.Tcp)?.ReplicaTransportAddressUris);

                return result.Item2;
            }
        }

        private async Task<DocumentServiceResponse> GetMasterAddressesViaGatewayAsync(
            DocumentServiceRequest request,
            ResourceType resourceType,
            string resourceAddress,
            string entryUrl,
            bool forceRefresh,
            bool useMasterCollectionResolver)
        {
            INameValueCollection addressQuery = new RequestNameValueCollection
            {
                { HttpConstants.QueryStrings.Url, HttpUtility.UrlEncode(entryUrl) }
            };

            INameValueCollection headers = new RequestNameValueCollection();
            if (forceRefresh)
            {
                headers.Set(HttpConstants.HttpHeaders.ForceRefresh, bool.TrueString);
            }

            if (useMasterCollectionResolver)
            {
                headers.Set(HttpConstants.HttpHeaders.UseMasterCollectionResolver, bool.TrueString);
            }

            if (request.ForceCollectionRoutingMapRefresh)
            {
                headers.Set(HttpConstants.HttpHeaders.ForceCollectionRoutingMapRefresh, bool.TrueString);
            }

            addressQuery.Add(HttpConstants.QueryStrings.Filter, this.protocolFilter);

            string resourceTypeToSign = PathsHelper.GetResourcePath(resourceType);

            headers.Set(HttpConstants.HttpHeaders.XDate, Rfc1123DateTimeCache.UtcNow());
            using (ITrace trace = Trace.GetRootTrace(nameof(GetMasterAddressesViaGatewayAsync), TraceComponent.Authorization, TraceLevel.Info))
            {
                string token = await this.tokenProvider.GetUserAuthorizationTokenAsync(
                    resourceAddress,
                    resourceTypeToSign,
                    HttpConstants.HttpMethods.Get,
                    headers,
                    AuthorizationTokenType.PrimaryMasterKey,
                    trace);

                headers.Set(HttpConstants.HttpHeaders.Authorization, token);

                Uri targetEndpoint = UrlUtility.SetQuery(this.addressEndpoint, UrlUtility.CreateQuery(addressQuery));

                string identifier = GatewayAddressCache.LogAddressResolutionStart(request, targetEndpoint);

                if (this.httpClient.IsFaultInjectionClient)
                {
                    using (DocumentServiceRequest faultInjectionRequest = DocumentServiceRequest.Create(
                        operationType: OperationType.Read,
                        resourceType: ResourceType.Address,
                        authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey))
                    {
                        faultInjectionRequest.RequestContext = request.RequestContext;
                        using (HttpResponseMessage httpResponseMessage = await this.httpClient.GetAsync(
                            uri: targetEndpoint,
                            additionalHeaders: headers,
                            resourceType: resourceType,
                            timeoutPolicy: HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance,
                            clientSideRequestStatistics: request.RequestContext?.ClientRequestStatistics,
                            cancellationToken: default,
                            documentServiceRequest: faultInjectionRequest))
                        {
                            DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(httpResponseMessage);
                            GatewayAddressCache.LogAddressResolutionEnd(request, identifier);
                            return documentServiceResponse;
                        }
                    }
                }

                using (HttpResponseMessage httpResponseMessage = await this.httpClient.GetAsync(
                    uri: targetEndpoint,
                    additionalHeaders: headers,
                    resourceType: resourceType,
                    timeoutPolicy: HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance,
                    clientSideRequestStatistics: request.RequestContext?.ClientRequestStatistics,
                    cancellationToken: default))
                {
                    DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(httpResponseMessage);
                    GatewayAddressCache.LogAddressResolutionEnd(request, identifier);
                    return documentServiceResponse;
                }
            }
        }

        private async Task<DocumentServiceResponse> GetServerAddressesViaGatewayAsync(
            DocumentServiceRequest request,
            string collectionRid,
            IEnumerable<string> partitionKeyRangeIds,
            bool forceRefresh)
        {
            string entryUrl = PathsHelper.GeneratePath(ResourceType.Document, collectionRid, true);

            INameValueCollection addressQuery = new RequestNameValueCollection
            {
                { HttpConstants.QueryStrings.Url, HttpUtility.UrlEncode(entryUrl) }
            };

            INameValueCollection headers = new RequestNameValueCollection();
            if (forceRefresh)
            {
                headers.Set(HttpConstants.HttpHeaders.ForceRefresh, bool.TrueString);
            }

            if (request != null && request.ForceCollectionRoutingMapRefresh)
            {
                headers.Set(HttpConstants.HttpHeaders.ForceCollectionRoutingMapRefresh, bool.TrueString);
            }

            addressQuery.Add(HttpConstants.QueryStrings.Filter, this.protocolFilter);
            addressQuery.Add(HttpConstants.QueryStrings.PartitionKeyRangeIds, string.Join(",", partitionKeyRangeIds));

            string resourceTypeToSign = PathsHelper.GetResourcePath(ResourceType.Document);

            headers.Set(HttpConstants.HttpHeaders.XDate, Rfc1123DateTimeCache.UtcNow());
            string token = null;

            using (ITrace trace = Trace.GetRootTrace(nameof(GetMasterAddressesViaGatewayAsync), TraceComponent.Authorization, TraceLevel.Info))
            {
                try
                {
                    token = await this.tokenProvider.GetUserAuthorizationTokenAsync(
                        collectionRid,
                        resourceTypeToSign,
                        HttpConstants.HttpMethods.Get,
                        headers,
                        AuthorizationTokenType.PrimaryMasterKey,
                        trace);
                }
                catch (UnauthorizedException)
                {
                }

                if (token == null && request != null && request.IsNameBased)
                {
                    // User doesn't have rid based resource token. Maybe he has name based.
                    string collectionAltLink = PathsHelper.GetCollectionPath(request.ResourceAddress);
                    token = await this.tokenProvider.GetUserAuthorizationTokenAsync(
                            collectionAltLink,
                            resourceTypeToSign,
                            HttpConstants.HttpMethods.Get,
                            headers,
                            AuthorizationTokenType.PrimaryMasterKey,
                            trace);
                }

                headers.Set(HttpConstants.HttpHeaders.Authorization, token);

                Uri targetEndpoint = UrlUtility.SetQuery(this.addressEndpoint, UrlUtility.CreateQuery(addressQuery));

                string identifier = GatewayAddressCache.LogAddressResolutionStart(request, targetEndpoint);
                
                if (this.httpClient.IsFaultInjectionClient)
                {
                    using (DocumentServiceRequest faultInjectionRequest = DocumentServiceRequest.Create(
                        operationType: OperationType.Read,
                        resourceType: ResourceType.Address,
                        authorizationTokenType: AuthorizationTokenType.PrimaryMasterKey))
                    {
                        faultInjectionRequest.RequestContext = request.RequestContext;
                        using (HttpResponseMessage httpResponseMessage = await this.httpClient.GetAsync(
                            uri: targetEndpoint,
                            additionalHeaders: headers,
                            resourceType: ResourceType.Document,
                            timeoutPolicy: HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance,
                            clientSideRequestStatistics: request.RequestContext?.ClientRequestStatistics,
                            cancellationToken: default,
                            documentServiceRequest: faultInjectionRequest))
                        {
                            DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(httpResponseMessage);
                            GatewayAddressCache.LogAddressResolutionEnd(request, identifier);
                            return documentServiceResponse;
                        }
                    }
                }

                using (HttpResponseMessage httpResponseMessage = await this.httpClient.GetAsync(
                    uri: targetEndpoint,
                    additionalHeaders: headers,
                    resourceType: ResourceType.Document,
                    timeoutPolicy: HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance,
                    clientSideRequestStatistics: request.RequestContext?.ClientRequestStatistics,
                    cancellationToken: default))
                {
                    DocumentServiceResponse documentServiceResponse = await ClientExtensions.ParseResponseAsync(httpResponseMessage);
                    GatewayAddressCache.LogAddressResolutionEnd(request, identifier);
                    return documentServiceResponse;
                }
            }
        }

        internal Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> ToPartitionAddressAndRange(string collectionRid, IList<Address> addresses, bool inNetworkRequest)
        {
            Address address = addresses.First();

            IReadOnlyList<AddressInformation> addressInfosSorted = GatewayAddressCache.GetSortedAddressInformation(addresses);

            PartitionKeyRangeIdentity partitionKeyRangeIdentity = new PartitionKeyRangeIdentity(collectionRid, address.PartitionKeyRangeId);

            if (this.enableTcpConnectionEndpointRediscovery && partitionKeyRangeIdentity.PartitionKeyRangeId != PartitionKeyRange.MasterPartitionKeyRangeId)
            {
                // add serverKey-pkRangeIdentity mapping only for addresses retrieved from gateway
                foreach (AddressInformation addressInfo in addressInfosSorted)
                {
                    DefaultTrace.TraceInformation("Added address to serverPartitionAddressToPkRangeIdMap, collectionRid :{0}, pkRangeId: {1}, address: {2}",
                       partitionKeyRangeIdentity.CollectionRid,
                       partitionKeyRangeIdentity.PartitionKeyRangeId,
                       addressInfo.PhysicalUri);

                    HashSet<PartitionKeyRangeIdentity> createdValue = null;
                    ServerKey serverKey = new ServerKey(new Uri(addressInfo.PhysicalUri));
                    HashSet<PartitionKeyRangeIdentity> pkRangeIdSet = this.serverPartitionAddressToPkRangeIdMap.GetOrAdd(
                        serverKey,
                        (_) =>
                        {
                            createdValue = new HashSet<PartitionKeyRangeIdentity>();
                            return createdValue;
                        });

                    if (object.ReferenceEquals(pkRangeIdSet, createdValue))
                    {
                        this.connectionStateListener.Register(serverKey, this.MarkAddressesToUnhealthyAsync);
                    }

                    lock (pkRangeIdSet)
                    {
                        pkRangeIdSet.Add(partitionKeyRangeIdentity);
                    }
                }
            }

            return Tuple.Create(
                partitionKeyRangeIdentity,
                new PartitionAddressInformation(addressInfosSorted, inNetworkRequest));
        }

        private static IReadOnlyList<AddressInformation> GetSortedAddressInformation(IList<Address> addresses)
        {
            AddressInformation[] addressInformationArray = new AddressInformation[addresses.Count];
            for (int i = 0; i < addresses.Count; i++)
            {
                Address addr = addresses[i];
                addressInformationArray[i] = new AddressInformation(
                    isPrimary: addr.IsPrimary,
                    physicalUri: addr.PhysicalUri,
                    protocol: ProtocolFromString(addr.Protocol),
                    isPublic: true);
            }

            Array.Sort(addressInformationArray);
            return addressInformationArray;
        }

        private bool IsInNetworkRequest(DocumentServiceResponse documentServiceResponse)
        {
            bool inNetworkRequest = false;
            string inNetworkHeader = documentServiceResponse.ResponseHeaders.Get(HttpConstants.HttpHeaders.LocalRegionRequest);
            if (!string.IsNullOrEmpty(inNetworkHeader))
            {
                bool.TryParse(inNetworkHeader, out inNetworkRequest);
            }

            return inNetworkRequest;
        }

        private static string LogAddressResolutionStart(DocumentServiceRequest request, Uri targetEndpoint)
        {
            string identifier = null;
            if (request != null && request.RequestContext.ClientRequestStatistics != null)
            {
                identifier = request.RequestContext.ClientRequestStatistics.RecordAddressResolutionStart(targetEndpoint);
            }

            return identifier;
        }

        private static void LogAddressResolutionEnd(DocumentServiceRequest request, string identifier)
        {
            if (request != null && request.RequestContext.ClientRequestStatistics != null)
            {
                request.RequestContext.ClientRequestStatistics.RecordAddressResolutionEnd(identifier);
            }
        }

        private static Protocol ProtocolFromString(string protocol)
        {
            return protocol.ToLowerInvariant() switch
            {
                RuntimeConstants.Protocols.HTTPS => Protocol.Https,
                RuntimeConstants.Protocols.RNTBD => Protocol.Tcp,
                _ => throw new ArgumentOutOfRangeException("protocol"),
            };
        }

        private static string ProtocolString(Protocol protocol)
        {
            return (int)protocol switch
            {
                (int)Protocol.Https => RuntimeConstants.Protocols.HTTPS,
                (int)Protocol.Tcp => RuntimeConstants.Protocols.RNTBD,
                _ => throw new ArgumentOutOfRangeException("protocol"),
            };
        }

        /// <summary>
        /// Utilizes the <see cref="TryCatch{TResult}"/> to get the server addresses. If an
        /// exception is thrown during the invocation, it handles it gracefully and returns
        /// a <see cref="TryCatch{TResult}"/> Task containing the exception.
        /// </summary>
        /// <param name="request">An instance of <see cref="DocumentServiceRequest"/> containing the request payload.</param>
        /// <param name="collectionRid">A string containing the collection ids.</param>
        /// <param name="partitionKeyRangeIds">An instance of <see cref="IEnumerable{T}"/> containing the list of partition key range ids.</param>
        /// <returns>A task of <see cref="TryCatch{TResult}"/> containing the result.</returns>
        private async Task<TryCatch<DocumentServiceResponse>> GetAddressesAsync(
            DocumentServiceRequest request,
            string collectionRid,
            IEnumerable<string> partitionKeyRangeIds)
        {
            try
            {
                return TryCatch<DocumentServiceResponse>
                    .FromResult(
                        await this.GetServerAddressesViaGatewayAsync(
                            request: request,
                            collectionRid: collectionRid,
                            partitionKeyRangeIds: partitionKeyRangeIds,
                            forceRefresh: false));
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning("Failed to fetch the server addresses for: {0} with exception: {1}. '{2}'",
                    collectionRid,
                    ex,
                    System.Diagnostics.Trace.CorrelationManager.ActivityId);

                return TryCatch<DocumentServiceResponse>.FromException(ex);
            }
        }

        /// <summary>
        /// Validates the unknown or unhealthy-pending replicas by attempting to open the Rntbd connection. This operation
        /// will eventually marks the unknown or unhealthy-pending replicas to healthy, if the rntbd connection attempt made was
        /// successful or unhealthy otherwise.
        /// </summary>
        /// <param name="addresses">A read-only list of <see cref="TransportAddressUri"/> needs to be validated.</param>
        private void ValidateReplicaAddresses(
            IReadOnlyList<TransportAddressUri> addresses)
        {
            if (addresses == null)
            {
                throw new ArgumentNullException(nameof(addresses));
            }

            IEnumerable<TransportAddressUri> addressesNeedToValidateStatus = this.GetAddressesNeededToValidateStatus(
                    transportAddresses: addresses);

            if (addressesNeedToValidateStatus.Any())
            {
                Task openConnectionsInBackgroundTask = Task.Run(async () => await this.openConnectionsHandler.TryOpenRntbdChannelsAsync(
                    addresses: addressesNeedToValidateStatus));
            }
        }

        /// <summary>
        /// Merge the new addresses returned from gateway service with that of the cached addresses. If the returned
        /// new addresses list contains some of the addresses, which are already cached, then reset the health state
        /// of the new address to that of the cached one. If the the new addresses doesn't contain any of the cached
        /// addresses, then keep using the health state of the new addresses, which should be `unknown`.
        /// </summary>
        /// <param name="newAddresses">A list of <see cref="PartitionAddressInformation"/> containing the latest
        /// addresses being returned from gateway.</param>
        /// <param name="cachedAddresses">A list of <see cref="PartitionAddressInformation"/> containing the cached
        /// addresses from the async non blocking cache.</param>
        /// <returns>A list of <see cref="PartitionAddressInformation"/> containing the merged addresses.</returns>
        private static PartitionAddressInformation MergeAddresses(
            PartitionAddressInformation newAddresses,
            PartitionAddressInformation cachedAddresses)
        {
            if (newAddresses == null)
            {
                throw new ArgumentNullException(nameof(newAddresses));
            }

            if (cachedAddresses == null)
            {
                return newAddresses;
            }

            PerProtocolPartitionAddressInformation currentAddressInfo = newAddresses.Get(Protocol.Tcp);
            PerProtocolPartitionAddressInformation cachedAddressInfo = cachedAddresses.Get(Protocol.Tcp);
            Dictionary<string, TransportAddressUri> cachedAddressDict = new ();

            foreach (TransportAddressUri transportAddressUri in cachedAddressInfo.ReplicaTransportAddressUris)
            {
                cachedAddressDict[transportAddressUri.ToString()] = transportAddressUri;
            }

            foreach (TransportAddressUri transportAddressUri in currentAddressInfo.ReplicaTransportAddressUris)
            {
                if (cachedAddressDict.ContainsKey(transportAddressUri.ToString()))
                {
                    TransportAddressUri cachedTransportAddressUri = cachedAddressDict[transportAddressUri.ToString()];
                    transportAddressUri.ResetHealthStatus(
                        status: cachedTransportAddressUri.GetCurrentHealthState().GetHealthStatus(),
                        lastUnknownTimestamp: cachedTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(
                            healthStatus: TransportAddressHealthState.HealthStatus.Unknown),
                        lastUnhealthyPendingTimestamp: cachedTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(
                            healthStatus: TransportAddressHealthState.HealthStatus.UnhealthyPending),
                        lastUnhealthyTimestamp: cachedTransportAddressUri.GetCurrentHealthState().GetLastKnownTimestampByHealthStatus(
                            healthStatus: TransportAddressHealthState.HealthStatus.Unhealthy));

                }
            }

            return newAddresses;
        }

        /// <summary>
        /// Returns a list of <see cref="TransportAddressUri"/> needed to validate their health status. Validating
        /// a uri is done by opening Rntbd connection to the backend replica, which is a costly operation by nature. Therefore
        /// vaidating both Unhealthy and Unknown replicas at the same time could impose a high CPU utilization. To avoid this
        /// situation, the RntbdOpenConnectionHandler has good concurrency control mechanism to open the connections gracefully.
        /// By default, this method only returns the Unhealthy replicas that requires to validate it's connectivity status. The
        /// Unknown replicas are validated only when the CosmosClient is initiated using the CreateAndInitializaAsync() flow.
        /// </summary>
        /// <param name="transportAddresses">A read only list of <see cref="TransportAddressUri"/>s.</param>
        /// <returns>A list of <see cref="TransportAddressUri"/> that needs to validate their status.</returns>
        private IEnumerable<TransportAddressUri> GetAddressesNeededToValidateStatus(
            IReadOnlyList<TransportAddressUri> transportAddresses)
        {
            return this.validateUnknownReplicas
                ? transportAddresses
                    .Where(address => address
                        .GetCurrentHealthState()
                        .GetHealthStatus() is
                            TransportAddressHealthState.HealthStatus.UnhealthyPending or
                            TransportAddressHealthState.HealthStatus.Unknown)
                : transportAddresses
                    .Where(address => address
                        .GetCurrentHealthState()
                        .GetHealthStatus() is
                            TransportAddressHealthState.HealthStatus.UnhealthyPending);
        }

        /// <summary>
        /// The replica health status of the transport address uri will change eventually with the motonically increasing time.
        /// However, the purpose of this method is to capture the health status snapshot at this moment.
        /// </summary>
        /// <param name="partitionAddressInformation">An instance of <see cref="PartitionAddressInformation"/>.</param>
        /// <param name="transportAddressUris">A read-only list of <see cref="TransportAddressUri"/>.</param>
        private void CaptureTransportAddressUriHealthStates(
            PartitionAddressInformation partitionAddressInformation,
            IReadOnlyList<TransportAddressUri> transportAddressUris)
        {
            partitionAddressInformation
                .Get(Protocol.Tcp)?
                .SetTransportAddressUrisHealthState(
                    replicaHealthStates: transportAddressUris.Select(x => x.GetCurrentHealthState().GetHealthStatusDiagnosticString()).ToList());
        }

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposedValue)
            {
                DefaultTrace.TraceInformation("GatewayAddressCache is already disposed {0}", this.GetHashCode());
                return;
            }

            if (disposing)
            {
                // Unregister the server-key
                foreach (ServerKey serverKey in this.serverPartitionAddressToPkRangeIdMap.Keys)
                {
                    this.connectionStateListener.UnRegister(serverKey, this.MarkAddressesToUnhealthyAsync);
                }

                this.serverPartitionAddressCache?.Dispose();
            }

            this.disposedValue = true;
        }

        public void Dispose()
        {
            this.Dispose(disposing: true);
        }
    }
}
