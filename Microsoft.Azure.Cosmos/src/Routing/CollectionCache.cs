//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Common
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;

#if !NETSTANDARD16
    using System.Diagnostics;
    using Microsoft.Azure.Documents;
#endif

    /// <summary>
    /// Cache to provide resource id lookup based on resource name
    /// </summary>
    internal abstract class CollectionCache
    {
        /// <summary>
        /// Master Service returns collection definition based on API Version and may not be always same for all API Versions.
        /// Here the InternalCache stores collection information related to a particular API Version
        /// </summary>
        protected class InternalCache
        {
            internal InternalCache()
            {
                this.collectionInfoByName = new AsyncCache<string, ContainerProperties>(new CollectionRidComparer());
                this.collectionInfoById = new AsyncCache<string, ContainerProperties>(new CollectionRidComparer());
                this.collectionInfoByNameLastRefreshTime = new ConcurrentDictionary<string, DateTime>();
                this.collectionInfoByIdLastRefreshTime = new ConcurrentDictionary<string, DateTime>();
            }

            internal readonly AsyncCache<string, ContainerProperties> collectionInfoByName;
            internal readonly AsyncCache<string, ContainerProperties> collectionInfoById;
            internal readonly ConcurrentDictionary<string, DateTime> collectionInfoByNameLastRefreshTime;
            internal readonly ConcurrentDictionary<string, DateTime> collectionInfoByIdLastRefreshTime;
        }

        /// <summary>
        /// cacheByApiList caches the collection information by API Version. In general it is expected that only a single version is populated
        /// for a collection, but this handles the situation if customer is using multiple API versions from different applications
        /// </summary>
        protected readonly InternalCache[] cacheByApiList;

        protected CollectionCache()
        {
            this.cacheByApiList = new InternalCache[2];
            this.cacheByApiList[0] = new InternalCache(); // for API version < 2018-12-31
            this.cacheByApiList[1] = new InternalCache(); // for API version >= 2018-12-31
        }

        /// <summary>
        /// Resolve the ContainerProperties object from the cache. If the collection was read before "refreshAfter" Timespan, force a cache refresh by reading from the backend.
        /// </summary>
        /// <param name="request">Request to resolve.</param>
        /// <param name="refreshAfter"> Time duration to refresh</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Instance of <see cref="ContainerProperties"/>.</returns>
        public virtual Task<ContainerProperties> ResolveCollectionAsync(
            DocumentServiceRequest request,
            TimeSpan refreshAfter,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            InternalCache cache = this.GetCache(request.Headers[HttpConstants.HttpHeaders.Version]);
#if !NETSTANDARD16
            Debug.Assert(request.ForceNameCacheRefresh == false);
#endif 
            DateTime currentTime = DateTime.UtcNow;
            DateTime lastRefreshTime = DateTime.MinValue;
            if (request.IsNameBased)
            {
                string resourceFullName = PathsHelper.GetCollectionPath(request.ResourceAddress);

                if (cache.collectionInfoByNameLastRefreshTime.TryGetValue(resourceFullName, out lastRefreshTime))
                {
                    TimeSpan cachedItemStaleness = currentTime - lastRefreshTime;

                    if (cachedItemStaleness > refreshAfter)
                    {
                        cache.collectionInfoByName.TryRemoveIfCompleted(resourceFullName);
                    }
                }
            }
            else
            {
                ResourceId resourceIdParsed = ResourceId.Parse(request.ResourceId);
                string collectionResourceId = resourceIdParsed.DocumentCollectionId.ToString();

                if (cache.collectionInfoByIdLastRefreshTime.TryGetValue(collectionResourceId, out lastRefreshTime))
                {
                    TimeSpan cachedItemStaleness = currentTime - lastRefreshTime;

                    if (cachedItemStaleness > refreshAfter)
                    {
                        cache.collectionInfoById.TryRemoveIfCompleted(request.ResourceId);
                    }
                }
            }

            return this.ResolveCollectionAsync(request, cancellationToken);
        }

        /// <summary>
        /// Resolves a request to a collection in a sticky manner.
        /// Unless request.ForceNameCacheRefresh is equal to true, it will return the same collection.
        /// </summary>
        /// <param name="request">Request to resolve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Instance of <see cref="ContainerProperties"/>.</returns>
        public virtual async Task<ContainerProperties> ResolveCollectionAsync(
            DocumentServiceRequest request,
            CancellationToken cancellationToken)
        {
            if (request.IsNameBased)
            {
                if (request.ForceNameCacheRefresh)
                {
                    await this.RefreshAsync(request, cancellationToken);
                    request.ForceNameCacheRefresh = false;
                }

                ContainerProperties collectionInfo = await this.ResolveByPartitionKeyRangeIdentityAsync(
                    request.Headers[HttpConstants.HttpHeaders.Version],
                    request.PartitionKeyRangeIdentity,
                    cancellationToken);
                if (collectionInfo != null)
                {
                    return collectionInfo;
                }

                if (request.RequestContext.ResolvedCollectionRid == null)
                {
                    collectionInfo =
                        await this.ResolveByNameAsync(
                            apiVersion: request.Headers[HttpConstants.HttpHeaders.Version],
                            resourceAddress: request.ResourceAddress,
                            forceRefesh: false,
                            cancellationToken: cancellationToken);

                    if (collectionInfo != null)
                    {
                        DefaultTrace.TraceVerbose(
                            "Mapped resourceName {0} to resourceId {1}. '{2}'",
                            request.ResourceAddress,
                            collectionInfo.ResourceId,
                            System.Diagnostics.Trace.CorrelationManager.ActivityId);

                        request.ResourceId = collectionInfo.ResourceId;
                        request.RequestContext.ResolvedCollectionRid = collectionInfo.ResourceId;
                    }
                    else
                    {
                        DefaultTrace.TraceVerbose(
                            "Collection with resourceName {0} not found. '{1}'",
                            request.ResourceAddress,
                            System.Diagnostics.Trace.CorrelationManager.ActivityId);
                    }

                    return collectionInfo;
                }
                else
                {
                    return await this.ResolveByRidAsync(request.Headers[HttpConstants.HttpHeaders.Version], request.RequestContext.ResolvedCollectionRid, cancellationToken);
                }
            }
            else
            {
                return await this.ResolveByPartitionKeyRangeIdentityAsync(request.Headers[HttpConstants.HttpHeaders.Version], request.PartitionKeyRangeIdentity, cancellationToken) ??
                    await this.ResolveByRidAsync(request.Headers[HttpConstants.HttpHeaders.Version], request.ResourceAddress, cancellationToken);
            }
        }

        /// <summary>
        /// This method is only used in client SDK in retry policy as it doesn't have request handy.
        /// </summary>
        public void Refresh(string resourceAddress, string apiVersion = null)
        {
            InternalCache cache = this.GetCache(apiVersion);
            if (PathsHelper.IsNameBased(resourceAddress))
            {
                string resourceFullName = PathsHelper.GetCollectionPath(resourceAddress);

                cache.collectionInfoByName.TryRemoveIfCompleted(resourceFullName);
            }
        }

        protected abstract Task<ContainerProperties> GetByRidAsync(string apiVersion, string collectionRid, CancellationToken cancellationToken);

        protected abstract Task<ContainerProperties> GetByNameAsync(string apiVersion, string resourceAddress, CancellationToken cancellationToken);

        private async Task<ContainerProperties> ResolveByPartitionKeyRangeIdentityAsync(string apiVersion, PartitionKeyRangeIdentity partitionKeyRangeIdentity, CancellationToken cancellationToken)
        {
            // if request is targeted at specific partition using x-ms-documentd-partitionkeyrangeid header,
            // which contains value "<collectionrid>,<partitionkeyrangeid>", then resolve to collection rid in this header.
            if (partitionKeyRangeIdentity != null && partitionKeyRangeIdentity.CollectionRid != null)
            {
                try
                {
                    return await this.ResolveByRidAsync(apiVersion, partitionKeyRangeIdentity.CollectionRid, cancellationToken);
                }
                catch (NotFoundException)
                {
                    // This is signal to the upper logic either in Gateway or client SDK to refresh
                    // collection cache and retry.
                    throw new InvalidPartitionException(RMResources.InvalidDocumentCollection);
                }
            }

            return null;
        }

        private Task<ContainerProperties> ResolveByRidAsync(
            string apiVersion,
            string resourceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResourceId resourceIdParsed = ResourceId.Parse(resourceId);
            string collectionResourceId = resourceIdParsed.DocumentCollectionId.ToString();
            InternalCache cache = this.GetCache(apiVersion);
            return cache.collectionInfoById.GetAsync(
                collectionResourceId,
                null,
                async () =>
                {
                    DateTime currentTime = DateTime.UtcNow;
                    ContainerProperties collection = await this.GetByRidAsync(apiVersion, collectionResourceId, cancellationToken);
                    cache.collectionInfoByIdLastRefreshTime.AddOrUpdate(collectionResourceId, currentTime,
                             (string currentKey, DateTime currentValue) => currentTime);
                    return collection;
                },
                cancellationToken);
        }

        internal virtual async Task<ContainerProperties> ResolveByNameAsync(
            string apiVersion,
            string resourceAddress,
            bool forceRefesh,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string resourceFullName = PathsHelper.GetCollectionPath(resourceAddress);
            InternalCache cache = this.GetCache(apiVersion);

            if (forceRefesh)
            {
                cache.collectionInfoByName.TryRemoveIfCompleted(resourceFullName);
            }

            return await cache.collectionInfoByName.GetAsync(
                resourceFullName,
                null,
                async () =>
                {
                    DateTime currentTime = DateTime.UtcNow;
                    ContainerProperties collection = await this.GetByNameAsync(apiVersion, resourceFullName, cancellationToken);
                    cache.collectionInfoById.Set(collection.ResourceId, collection);
                    cache.collectionInfoByNameLastRefreshTime.AddOrUpdate(resourceFullName, currentTime,
                        (string currentKey, DateTime currentValue) => currentTime);
                    cache.collectionInfoByIdLastRefreshTime.AddOrUpdate(collection.ResourceId, currentTime,
                             (string currentKey, DateTime currentValue) => currentTime);
                    return collection;
                },
                cancellationToken);
        }

        private async Task RefreshAsync(DocumentServiceRequest request, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.Assert(request.IsNameBased);
            InternalCache cache = this.GetCache(request.Headers[HttpConstants.HttpHeaders.Version]);
            string resourceFullName = PathsHelper.GetCollectionPath(request.ResourceAddress);

            if (request.RequestContext.ResolvedCollectionRid != null)
            {
                // Here we will issue backend call only if cache wasn't already refreshed (if whatever is there corresponds to presiously resolved collection rid).
                await cache.collectionInfoByName.GetAsync(
                   resourceFullName,
                   ContainerProperties.CreateWithResourceId(request.RequestContext.ResolvedCollectionRid),
                   async () =>
                   {
                       DateTime currentTime = DateTime.UtcNow;
                       ContainerProperties collection = await this.GetByNameAsync(request.Headers[HttpConstants.HttpHeaders.Version], resourceFullName, cancellationToken);
                       cache.collectionInfoById.Set(collection.ResourceId, collection);
                       cache.collectionInfoByNameLastRefreshTime.AddOrUpdate(resourceFullName, currentTime,
                       (string currentKey, DateTime currentValue) => currentTime);
                       cache.collectionInfoByIdLastRefreshTime.AddOrUpdate(collection.ResourceId, currentTime,
                                (string currentKey, DateTime currentValue) => currentTime);
                       return collection;
                   },
                   cancellationToken);
            }
            else
            {
                // In case of ForceRefresh directive coming from client, there will be no ResolvedCollectionRid, so we 
                // need to refresh unconditionally.
                this.Refresh(request.ResourceAddress, request.Headers[HttpConstants.HttpHeaders.Version]);
            }

            request.RequestContext.ResolvedCollectionRid = null;
        }

        /// <summary>
        /// The function selects the right cache based on apiVersion. 
        /// </summary>
        protected InternalCache GetCache(string apiVersion)
        {
            // Non Partitioned Migration Version. Need this to flight V3 SDK till we make this the Current Version
            if (string.IsNullOrEmpty(apiVersion) || VersionUtility.IsLaterThan(apiVersion, HttpConstants.VersionDates.v2018_12_31))
            {
                return this.cacheByApiList[1];
            }

            return this.cacheByApiList[0];
        }

        private sealed class CollectionRidComparer : IEqualityComparer<ContainerProperties>
        {
            public bool Equals(ContainerProperties left, ContainerProperties right)
            {
                if (left == null && right == null)
                {
                    return true;
                }

                if ((left == null) ^ (right == null))
                {
                    return false;
                }

                return StringComparer.Ordinal.Compare(left.ResourceId, right.ResourceId) == 0;
            }

            public int GetHashCode(ContainerProperties collection)
            {
                return collection.ResourceId.GetHashCode();
            }
        }

    }
}