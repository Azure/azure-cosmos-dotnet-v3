//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Common
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

#if !NETSTANDARD16
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Internal;
#endif

    /// <summary>
    /// Cache to provide resource id lookup based on resource name
    /// </summary>
    internal abstract class CollectionCache
    {
        private readonly AsyncCache<string, CosmosContainerSettings> collectionInfoByNameCache;
        private readonly AsyncCache<string, CosmosContainerSettings> collectionInfoByIdCache;

        protected CollectionCache()
        {
            this.collectionInfoByNameCache = new AsyncCache<string, CosmosContainerSettings>(new CollectionRidComparer());
            this.collectionInfoByIdCache = new AsyncCache<string, CosmosContainerSettings>(new CollectionRidComparer());
        }

        /// <summary>
        /// Resolves a request to a collection in a sticky manner.
        /// Unless request.ForceNameCacheRefresh is equal to true, it will return the same collection.
        /// </summary>
        /// <param name="request">Request to resolve.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Instance of <see cref="CosmosContainerSettings"/>.</returns>
        public virtual async Task<CosmosContainerSettings> ResolveCollectionAsync(
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

                CosmosContainerSettings collectionInfo = await this.ResolveByPartitionKeyRangeIdentityAsync(
                    request.PartitionKeyRangeIdentity,
                    cancellationToken);
                if (collectionInfo != null)
                {
                    return collectionInfo;
                }

                if (request.RequestContext.ResolvedCollectionRid == null)
                {
                    collectionInfo =
                        await this.ResolveByNameAsync(request.ResourceAddress, cancellationToken);
                    if (collectionInfo != null)
                    {
                        DefaultTrace.TraceVerbose(
                            "Mapped resourceName {0} to resourceId {1}. '{2}'",
                            request.ResourceAddress,
                            collectionInfo.ResourceId,
                            Trace.CorrelationManager.ActivityId);

                        request.ResourceId = collectionInfo.ResourceId;
                        request.RequestContext.ResolvedCollectionRid = collectionInfo.ResourceId;
                    }
                    else
                    {
                        DefaultTrace.TraceVerbose(
                            "Collection with resourceName {0} not found. '{1}'",
                            request.ResourceAddress,
                            Trace.CorrelationManager.ActivityId);
                    }

                    return collectionInfo;
                }
                else
                {
                    return await this.ResolveByRidAsync(request.RequestContext.ResolvedCollectionRid, cancellationToken);
                }
            }
            else
            {
                return await this.ResolveByPartitionKeyRangeIdentityAsync(request.PartitionKeyRangeIdentity, cancellationToken) ??
                    await this.ResolveByRidAsync(request.ResourceAddress, cancellationToken);
            }
        }

        /// <summary>
        /// This method is only used in client SDK in retry policy as it doesn't have request handy.
        /// </summary>
        public void Refresh(string resourceAddress)
        {
            if (PathsHelper.IsNameBased(resourceAddress))
            {
                string resourceFullName = PathsHelper.GetCollectionPath(resourceAddress);

                this.collectionInfoByNameCache.Refresh(
                        resourceFullName,
                         async () =>
                         {
                             CosmosContainerSettings collection = await this.GetByNameAsync(resourceFullName, CancellationToken.None);
                             if (collection != null)
                             {
                                 this.collectionInfoByIdCache.Set(collection.ResourceId, collection);
                             }

                             return collection;
                         },
                        CancellationToken.None);
            }
        }

        protected abstract Task<CosmosContainerSettings> GetByRidAsync(string collectionRid, CancellationToken cancellationToken);

        protected abstract Task<CosmosContainerSettings> GetByNameAsync(string resourceAddress, CancellationToken cancellationToken);

        private async Task<CosmosContainerSettings> ResolveByPartitionKeyRangeIdentityAsync(PartitionKeyRangeIdentity partitionKeyRangeIdentity, CancellationToken cancellationToken)
        {
            // if request is targeted at specific partition using x-ms-documentd-partitionkeyrangeid header,
            // which contains value "<collectionrid>,<partitionkeyrangeid>", then resolve to collection rid in this header.
            if (partitionKeyRangeIdentity != null && partitionKeyRangeIdentity.CollectionRid != null)
            {
                try
                {
                    CosmosContainerSettings containerSettings = await this.ResolveByRidAsync(partitionKeyRangeIdentity.CollectionRid, cancellationToken);
                    if (containerSettings == null)
                    {
                        // This is signal to the upper logic either in Gateway or client SDK to refresh
                        // collection cache and retry.
                        throw new InvalidPartitionException(RMResources.InvalidDocumentCollection);
                    }

                    return containerSettings;
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

        private Task<CosmosContainerSettings> ResolveByRidAsync(
            string resourceId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResourceId resourceIdParsed = ResourceId.Parse(resourceId);
            string collectionResourceId = resourceIdParsed.DocumentCollectionId.ToString();

            return this.collectionInfoByIdCache.GetAsync(
                collectionResourceId,
                null,
                () => this.GetByRidAsync(collectionResourceId, cancellationToken),
                cancellationToken);
        }

        internal Task<CosmosContainerSettings> ResolveByNameAsync(
            string resourceAddress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string resourceFullName = PathsHelper.GetCollectionPath(resourceAddress);

            return this.collectionInfoByNameCache.GetAsync(
                resourceFullName,
                null,
                async () =>
                {
                    CosmosContainerSettings collection = await this.GetByNameAsync(resourceFullName, cancellationToken);
                    if (collection != null)
                    {
                        this.collectionInfoByIdCache.Set(collection.ResourceId, collection);
                    }

                    return collection;
                },
               cancellationToken);
        }

        private async Task RefreshAsync(DocumentServiceRequest request, CancellationToken cancellationToken)
        {
            System.Diagnostics.Debug.Assert(request.IsNameBased);

            string resourceFullName = PathsHelper.GetCollectionPath(request.ResourceAddress);

            if (request.RequestContext.ResolvedCollectionRid != null)
            {
                // Here we will issue backend call only if cache wasn't already refreshed (if whatever is there corresponds to presiously resolved collection rid).
                await this.collectionInfoByNameCache.GetAsync(
                    resourceFullName,
                    new CosmosContainerSettings { ResourceId = request.RequestContext.ResolvedCollectionRid },
                    async () =>
                    {
                        CosmosContainerSettings collection = await this.GetByNameAsync(resourceFullName, cancellationToken);
                        if (collection != null)
                        {
                            this.collectionInfoByIdCache.Set(collection.ResourceId, collection);
                        }

                        return collection;
                    },
                    cancellationToken);
            }
            else
            {
                // In case of ForceRefresh directive coming from client, there will be no ResolvedCollectionRid, so we 
                // need to refresh unconditionally.
                this.Refresh(request.ResourceAddress);
            }

            request.RequestContext.ResolvedCollectionRid = null;
        }

        private sealed class CollectionRidComparer : IEqualityComparer<CosmosContainerSettings>
        {
            public bool Equals(CosmosContainerSettings left, CosmosContainerSettings right)
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

            public int GetHashCode(CosmosContainerSettings collection)
            {
                return collection.ResourceId.GetHashCode();
            }
        }

    }
}