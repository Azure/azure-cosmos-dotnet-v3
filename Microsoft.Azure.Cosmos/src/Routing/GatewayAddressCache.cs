﻿//------------------------------------------------------------
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

        private readonly CosmosHttpClient httpClient;

        private Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> masterPartitionAddressCache;
        private DateTime suboptimalMasterPartitionTimestamp;
        private bool disposedValue;

        public GatewayAddressCache(
            Uri serviceEndpoint,
            Protocol protocol,
            ICosmosAuthorizationTokenProvider tokenProvider,
            IServiceConfigurationReader serviceConfigReader,
            CosmosHttpClient httpClient,
            long suboptimalPartitionForceRefreshIntervalInSeconds = 600,
            bool enableTcpConnectionEndpointRediscovery = false)
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

            this.suboptimalPartitionForceRefreshIntervalInSeconds = suboptimalPartitionForceRefreshIntervalInSeconds;

            this.httpClient = httpClient;

            this.protocolFilter =
                string.Format(CultureInfo.InvariantCulture,
                GatewayAddressCache.protocolFilterFormat,
                Constants.Properties.Protocol,
                GatewayAddressCache.ProtocolString(this.protocol));
        }

        public Uri ServiceEndpoint => this.serviceEndpoint;

        public async Task OpenConnectionsAsync(
            string databaseName,
            ContainerProperties collection,
            IReadOnlyList<PartitionKeyRangeIdentity> partitionKeyRangeIdentities,
            Func<Uri, Task> openConnectionHandler,
            CancellationToken cancellationToken)
        {
            List<Task<TryCatch<DocumentServiceResponse>>> tasks = new ();
            int batchSize = GatewayAddressCache.DefaultBatchSize;

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
                    tasks
                        .Add(this.GetAddressesAsync(
                            request: request,
                            collectionRid: collection.ResourceId,
                            partitionKeyRangeIds: partitionKeyRangeIdentities.Skip(i).Take(batchSize).Select(range => range.PartitionKeyRangeId)));
                }
            }

            foreach (TryCatch<DocumentServiceResponse> task in await Task.WhenAll(tasks))
            {
                if (task.Failed)
                {
                    continue;
                }

                using (DocumentServiceResponse response = task.Result)
                {
                    FeedResource<Address> addressFeed = response.GetResource<FeedResource<Address>>();

                    bool inNetworkRequest = this.IsInNetworkRequest(response);

                    IEnumerable<Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation>> addressInfos =
                        addressFeed.Where(addressInfo => ProtocolFromString(addressInfo.Protocol) == this.protocol)
                            .GroupBy(address => address.PartitionKeyRangeId, StringComparer.Ordinal)
                            .Select(group => this.ToPartitionAddressAndRange(collection.ResourceId, @group.ToList(), inNetworkRequest));

                    foreach (Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> addressInfo in addressInfos)
                    {
                        this.serverPartitionAddressCache.Set(
                            new PartitionKeyRangeIdentity(collection.ResourceId, addressInfo.Item1.PartitionKeyRangeId),
                            addressInfo.Item2);

                        if (openConnectionHandler != null)
                        {
                            await this.OpenRntbdChannelsAsync(
                                addressInfo,
                                openConnectionHandler);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Invokes the transport client delegate to open the Rntbd connection
        /// and establish Rntbd context negotiation to the backend replica nodes.
        /// </summary>
        /// <param name="addressInfo">An instance of <see cref="Tuple{T1, T2}"/> containing the partition key id
        /// and it's corresponding address information.</param>
        /// <param name="openConnectionHandlerAsync">The transport client callback delegate to be invoked at a
        /// later point of time.</param>
        private async Task OpenRntbdChannelsAsync(
             Tuple<PartitionKeyRangeIdentity, PartitionAddressInformation> addressInfo,
             Func<Uri, Task> openConnectionHandlerAsync)
        {
            foreach (AddressInformation address in addressInfo.Item2.AllAddresses)
            {
                DefaultTrace.TraceVerbose("Attempting to open Rntbd connection to backend uri: {0}. '{1}'",
                    address.PhysicalUri,
                    System.Diagnostics.Trace.CorrelationManager.ActivityId);
                try
                {
                    await openConnectionHandlerAsync(
                        new Uri(address.PhysicalUri));
                }
                catch (Exception ex)
                {
                    DefaultTrace.TraceWarning("Failed to open Rntbd connection to backend uri: {0} with exception: {1}. '{2}'",
                        address.PhysicalUri,
                        ex,
                        System.Diagnostics.Trace.CorrelationManager.ActivityId);
                }
            }
        }

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

        public void TryRemoveAddresses(
            ServerKey serverKey)
        {
            if (serverKey == null)
            {
                throw new ArgumentNullException(nameof(serverKey));
            }

            if (this.serverPartitionAddressToPkRangeIdMap.TryRemove(serverKey, out HashSet<PartitionKeyRangeIdentity> pkRangeIds))
            {
                PartitionKeyRangeIdentity[] pkRangeIdsCopy;
                lock (pkRangeIds)
                {
                    pkRangeIdsCopy = pkRangeIds.ToArray();
                }

                foreach (PartitionKeyRangeIdentity pkRangeId in pkRangeIdsCopy)
                {
                    DefaultTrace.TraceInformation("Remove addresses for collectionRid :{0}, pkRangeId: {1}, serviceEndpoint: {2}",
                       pkRangeId.CollectionRid,
                       pkRangeId.PartitionKeyRangeId,
                       this.serviceEndpoint);

                    this.serverPartitionAddressCache.TryRemove(pkRangeId);
                }
            }
        }

        public async Task<PartitionAddressInformation> UpdateAsync(
            PartitionKeyRangeIdentity partitionKeyRangeIdentity,
            CancellationToken cancellationToken)
        {
            if (partitionKeyRangeIdentity == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyRangeIdentity));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return await this.serverPartitionAddressCache.GetAsync(
                       key: partitionKeyRangeIdentity,
                       singleValueInitFunc: (_) => this.GetAddressesForRangeIdAsync(
                           null,
                           partitionKeyRangeIdentity.CollectionRid,
                           partitionKeyRangeIdentity.PartitionKeyRangeId,
                           forceRefresh: true),
                       forceRefresh: (_) => true);
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

                    HashSet<PartitionKeyRangeIdentity> pkRangeIdSet = this.serverPartitionAddressToPkRangeIdMap.GetOrAdd(
                        new ServerKey(new Uri(addressInfo.PhysicalUri)),
                        (_) => new HashSet<PartitionKeyRangeIdentity>());
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
            return (protocol.ToLowerInvariant()) switch
            {
                RuntimeConstants.Protocols.HTTPS => Protocol.Https,
                RuntimeConstants.Protocols.RNTBD => Protocol.Tcp,
                _ => throw new ArgumentOutOfRangeException("protocol"),
            };
        }

        private static string ProtocolString(Protocol protocol)
        {
            return ((int)protocol) switch
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

        protected virtual void Dispose(bool disposing)
        {
            if (this.disposedValue)
            {
                return;
            }

            if (disposing)
            {
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
