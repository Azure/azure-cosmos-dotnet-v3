//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Monads;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;

    /// <summary>
    /// AddressCache implementation for client SDK. Supports cross region address routing based on
    /// avaialbility and preference list.
    /// </summary>
    internal sealed class GlobalAddressResolver : IAddressResolverExtension, IDisposable
    {
        private const int MaxBackupReadRegions = 3;

        private readonly GlobalEndpointManager endpointManager;
        private readonly Protocol protocol;
        private readonly IAuthorizationTokenProvider tokenProvider;
        private readonly UserAgentContainer userAgentContainer;
        private readonly CollectionCache collectionCache;
        private readonly PartitionKeyRangeCache routingMapProvider;
        private readonly int maxEndpoints;
        private readonly IServiceConfigurationReader serviceConfigReader;
        private readonly HttpMessageHandler messageHandler;
        private readonly ConcurrentDictionary<Uri, EndpointCache> addressCacheByEndpoint;
        private readonly TimeSpan requestTimeout;
        private readonly ApiType apiType;

        public GlobalAddressResolver(
            GlobalEndpointManager endpointManager,
            Protocol protocol,
            IAuthorizationTokenProvider tokenProvider,
            CollectionCache collectionCache,
            PartitionKeyRangeCache routingMapProvider,
            UserAgentContainer userAgentContainer,
            IServiceConfigurationReader serviceConfigReader,
            HttpMessageHandler messageHandler,
            ConnectionPolicy connectionPolicy,
            ApiType apiType)
        {
            this.endpointManager = endpointManager;
            this.protocol = protocol;
            this.tokenProvider = tokenProvider;
            this.userAgentContainer = userAgentContainer;
            this.collectionCache = collectionCache;
            this.routingMapProvider = routingMapProvider;
            this.serviceConfigReader = serviceConfigReader;
            this.messageHandler = messageHandler;
            this.requestTimeout = connectionPolicy.RequestTimeout;
            this.apiType = apiType;

            int maxBackupReadEndpoints =
                !connectionPolicy.EnableReadRequestsFallback.HasValue || connectionPolicy.EnableReadRequestsFallback.Value
                ? GlobalAddressResolver.MaxBackupReadRegions : 0;

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
            TryCatch<CollectionRoutingMap> tryGetRoutingMap =
                await this.routingMapProvider.TryLookupAsync(collection.ResourceId, null, null, cancellationToken);

            if (tryGetRoutingMap.Failed)
            {
                return;
            }

            List<PartitionKeyRangeIdentity> ranges = tryGetRoutingMap.Result.OrderedPartitionKeyRanges.Select(
                range => new PartitionKeyRangeIdentity(collection.ResourceId, range.Id)).ToList();

            List<Task> tasks = new List<Task>();

            foreach (EndpointCache endpointCache in this.addressCacheByEndpoint.Values)
            {
                tasks.Add(endpointCache.AddressCache.OpenAsync(databaseName, collection, ranges, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        public Task<PartitionAddressInformation> ResolveAsync(
            DocumentServiceRequest request,
            bool forceRefresh,
            CancellationToken cancellationToken)
        {
            IAddressResolver resolver = this.GetAddressResolver(request);
            return resolver.ResolveAsync(request, forceRefresh, cancellationToken);
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
            EndpointCache endpointCache = this.addressCacheByEndpoint.GetOrAdd(
                endpoint,
                (Uri resolvedEndpoint) =>
                {
                    GatewayAddressCache gatewayAddressCache = new GatewayAddressCache(
                        resolvedEndpoint,
                        this.protocol,
                        this.tokenProvider,
                        this.userAgentContainer,
                        this.serviceConfigReader,
                        this.requestTimeout,
                        messageHandler: this.messageHandler,
                        apiType: this.apiType);

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
                        EndpointCache removedEntry;
                        this.addressCacheByEndpoint.TryRemove(endpoints.Dequeue(), out removedEntry);
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
