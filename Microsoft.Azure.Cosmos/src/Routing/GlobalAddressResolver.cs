//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Rntbd;

    /// <summary>
    /// AddressCache implementation for client SDK. Supports cross region address routing based on
    /// avaialbility and preference list.
    /// </summary>
    internal sealed class GlobalAddressResolver : IAddressResolver
    {
        private const int MaxBackupReadRegions = 3;

        private readonly GlobalEndpointManager endpointManager;
        private readonly GlobalPartitionEndpointManager partitionKeyRangeLocationCache;
        private readonly Protocol protocol;
        private readonly IAuthorizationTokenProvider tokenProvider;
        private readonly CollectionCache collectionCache;
        private readonly PartitionKeyRangeCache routingMapProvider;
        private readonly int maxEndpoints;
        private readonly IServiceConfigurationReader serviceConfigReader;
        private readonly CosmosHttpClient httpClient;
        private readonly ConcurrentDictionary<Uri, EndpointCache> addressCacheByEndpoint;
        private readonly bool enableTcpConnectionEndpointRediscovery;

        public GlobalAddressResolver(
            GlobalEndpointManager endpointManager,
            GlobalPartitionEndpointManager partitionKeyRangeLocationCache,
            Protocol protocol,
            IAuthorizationTokenProvider tokenProvider,
            CollectionCache collectionCache,
            PartitionKeyRangeCache routingMapProvider,
            IServiceConfigurationReader serviceConfigReader,
            ConnectionPolicy connectionPolicy,
            CosmosHttpClient httpClient)
        {
            this.endpointManager = endpointManager;
            this.partitionKeyRangeLocationCache = partitionKeyRangeLocationCache;
            this.protocol = protocol;
            this.tokenProvider = tokenProvider;
            this.collectionCache = collectionCache;
            this.routingMapProvider = routingMapProvider;
            this.serviceConfigReader = serviceConfigReader;
            this.httpClient = httpClient;

            int maxBackupReadEndpoints =
                !connectionPolicy.EnableReadRequestsFallback.HasValue || connectionPolicy.EnableReadRequestsFallback.Value
                ? GlobalAddressResolver.MaxBackupReadRegions : 0;

            this.enableTcpConnectionEndpointRediscovery = connectionPolicy.EnableTcpConnectionEndpointRediscovery;

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
        }

        public async Task OpenAsync(
            string databaseName,
            ContainerProperties collection,
            CancellationToken cancellationToken)
        {
            CollectionRoutingMap routingMap =
                await this.routingMapProvider.TryLookupAsync(collection.ResourceId, null, null, cancellationToken, NoOpTrace.Singleton);

            if (routingMap == null)
            {
                return;
            }

            List<PartitionKeyRangeIdentity> ranges = routingMap.OrderedPartitionKeyRanges.Select(
                range => new PartitionKeyRangeIdentity(collection.ResourceId, range.Id)).ToList();

            List<Task> tasks = new List<Task>();

            foreach (EndpointCache endpointCache in this.addressCacheByEndpoint.Values)
            {
                tasks.Add(endpointCache.AddressCache.OpenAsync(databaseName, collection, ranges, cancellationToken));
            }

            await Task.WhenAll(tasks);
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

        public async Task UpdateAsync(
            IReadOnlyList<AddressCacheToken> addressCacheTokens,
            CancellationToken cancellationToken)
        {
            List<Task> tasks = new List<Task>();

            foreach (AddressCacheToken cacheToken in addressCacheTokens)
            {
                if (this.addressCacheByEndpoint.TryGetValue(cacheToken.ServiceEndpoint, out EndpointCache endpointCache))
                {
                    tasks.Add(endpointCache.AddressCache.UpdateAsync(cacheToken.PartitionKeyRangeIdentity, cancellationToken));
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task UpdateAsync(
            ServerKey serverKey,
            CancellationToken cancellationToken)
        {
            List<Task> tasks = new List<Task>();

            foreach (KeyValuePair<Uri, EndpointCache> addressCache in this.addressCacheByEndpoint)
            {
                // since we don't know which address cache contains the pkRanges mapped to this node, we do a tryRemove on all AddressCaches of all regions
                tasks.Add(addressCache.Value.AddressCache.TryRemoveAddressesAsync(serverKey, cancellationToken));
            }

            await Task.WhenAll(tasks);
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
                        enableTcpConnectionEndpointRediscovery: this.enableTcpConnectionEndpointRediscovery);

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
