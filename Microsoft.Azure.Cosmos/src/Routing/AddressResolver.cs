//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Rntbd;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// Abstracts out the logic to resolve physical replica addresses for the given <see cref="DocumentServiceRequest"/>.
    /// 
    /// AddressCache internally maintains CollectionCache, CollectionRoutingMapCache and BackendAddressCache.
    /// Logic in this class mainly joins these 3 caches and deals with potential staleness of the caches.
    /// 
    /// </summary>
    internal sealed class AddressResolver : IAddressResolver
    {
        private readonly IMasterServiceIdentityProvider masterServiceIdentityProvider;

        private readonly IRequestSigner requestSigner;
        private readonly string location;

        private readonly PartitionKeyRangeIdentity masterPartitionKeyRangeIdentity = new PartitionKeyRangeIdentity(PartitionKeyRange.MasterPartitionKeyRangeId);

        private CollectionCache collectionCache;
        private ICollectionRoutingMapCache collectionRoutingMapCache;
        private IAddressCache addressCache;

        public AddressResolver(IMasterServiceIdentityProvider masterServiceIdentityProvider, IRequestSigner requestSigner, string location)
        {
            this.masterServiceIdentityProvider = masterServiceIdentityProvider;
            this.requestSigner = requestSigner;
            this.location = location;
        }

        public void InitializeCaches(
            CollectionCache collectionCache,
            ICollectionRoutingMapCache collectionRoutingMapCache,
            IAddressCache addressCache)
        {
            this.collectionCache = collectionCache;
            this.addressCache = addressCache;
            this.collectionRoutingMapCache = collectionRoutingMapCache;
        }

        public async Task<PartitionAddressInformation> ResolveAsync(
            DocumentServiceRequest request,
            bool forceRefreshPartitionAddresses,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResolutionResult result =
                await this.ResolveAddressesAndIdentityAsync(request, forceRefreshPartitionAddresses, cancellationToken);

            this.ThrowIfTargetChanged(request, result.TargetPartitionKeyRange);
            request.RequestContext.TargetIdentity = result.TargetServiceIdentity;
            request.RequestContext.ResolvedPartitionKeyRange = result.TargetPartitionKeyRange;
            request.RequestContext.RegionName = this.location;
            request.RequestContext.LocalRegionRequest = result.Addresses.IsLocalRegion;

            await this.requestSigner.SignRequestAsync(request, cancellationToken);

            return result.Addresses;
        }

        private static bool IsSameCollection(PartitionKeyRange initiallyResolved, PartitionKeyRange newlyResolved)
        {
            if (initiallyResolved == null)
            {
                throw new ArgumentException("parent");
            }

            if (newlyResolved == null)
            {
                return false;
            }

            if (initiallyResolved.Id == PartitionKeyRange.MasterPartitionKeyRangeId
                && newlyResolved.Id == PartitionKeyRange.MasterPartitionKeyRangeId)
            {
                return true;
            }

            if (initiallyResolved.Id == PartitionKeyRange.MasterPartitionKeyRangeId
                || newlyResolved.Id == PartitionKeyRange.MasterPartitionKeyRangeId)
            {
                string message =
                    "Request was resolved to master partition and then to server partition.";
                Debug.Assert(false, message);
                DefaultTrace.TraceCritical(message);
                return false;
            }

            if (ResourceId.Parse(initiallyResolved.ResourceId).DocumentCollection
                != ResourceId.Parse(newlyResolved.ResourceId).DocumentCollection)
            {
                return false;
            }

            if (initiallyResolved.Id != newlyResolved.Id && !(newlyResolved.Parents != null && newlyResolved.Parents.Contains(initiallyResolved.Id)))
            {
                // the above condition should be always false in current codebase.
                // We don't need to refresh any caches if we resolved to a range which is child of previously resolved range.
                // Quorum reads should be handled transparently as child partitions share LSNs with parent partitions which are gone.
                string message =
                    "Request is targeted at a partition key range which is not child of previously targeted range.";
                Debug.Assert(false, message);
                DefaultTrace.TraceCritical(message);

                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates if the target partition to which the request is being sent has changed during retry.
        /// If that happens, the request is no more valid and need to be retried.
        /// Also has the side-effect that if the target identity is not set, we set it on the request
        /// </summary>
        /// <param name="request">Request in progress</param>
        /// <param name="targetRange">Target partition key range determined by address resolver</param>
        private void ThrowIfTargetChanged(DocumentServiceRequest request, PartitionKeyRange targetRange)
        {
            // If new range is child of previous range, we don't need to throw any exceptions
            // as LSNs are continued on child ranges.
            if (request.RequestContext.ResolvedPartitionKeyRange != null &&
                !IsSameCollection(request.RequestContext.ResolvedPartitionKeyRange, targetRange))
            {
                if (!request.IsNameBased)
                {
                    string message = string.Format(CultureInfo.CurrentCulture,
                        "Target should not change for non name based requests. Previous target {0}, Current {1}",
                        request.RequestContext.ResolvedPartitionKeyRange, targetRange);
                    Debug.Assert(false, message);
                    DefaultTrace.TraceCritical(message);
                }

                request.RequestContext.TargetIdentity = null;
                request.RequestContext.ResolvedPartitionKeyRange = null;
                throw new InvalidPartitionException(RMResources.InvalidTarget) { ResourceAddress = request.ResourceAddress };
            }
        }

        /// <summary>
        /// Resolves the endpoint of the partition for the given request
        /// </summary>
        /// <param name="request">Request for which the partition endpoint resolution is to be performed</param>
        /// <param name="forceRefreshPartitionAddresses">Force refresh the partition's endpoint</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An instance of <see cref="ResolutionResult"/>.</returns>
        private async Task<ResolutionResult> ResolveAddressesAndIdentityAsync(
            DocumentServiceRequest request,
            bool forceRefreshPartitionAddresses,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.ServiceIdentity != null)
            {
                if (request.ServiceIdentity.IsMasterService &&
                    request.ForceMasterRefresh &&
                    this.masterServiceIdentityProvider != null)
                {
                    await this.masterServiceIdentityProvider.RefreshAsync(request.ServiceIdentity, cancellationToken);

                    ServiceIdentity newMasterServiceIdentity = this.masterServiceIdentityProvider.MasterServiceIdentity;

                    bool masterServiceIdentityChanged = newMasterServiceIdentity != null &&
                        !newMasterServiceIdentity.Equals(request.ServiceIdentity);

                    DefaultTrace.TraceInformation(
                        "Refreshed master service identity. masterServiceIdentityChanged = {0}, " +
                        "previousRequestServiceIdentity = {1}, newMasterServiceIdentity = {2}",
                        masterServiceIdentityChanged,
                        request.ServiceIdentity,
                        newMasterServiceIdentity);

                    if (masterServiceIdentityChanged)
                    {
                        request.RouteTo(newMasterServiceIdentity);
                    }
                }

                // In this case we don't populate request.RequestContext.ResolvedPartitionKeyRangeId,
                // which is needed for session token.
                // The assumption is that:
                //     1. Master requests never use session consistency.
                //     2. Service requests (like collection create etc.) don't use session consistency.
                //     3. Requests which target specific partition of an existing collection will use x-ms-documentdb-partitionkeyrangeid header
                //        to send request to specific partition and will not set request.ServiceIdentity
                ServiceIdentity identity = request.ServiceIdentity;
                PartitionAddressInformation addresses = await this.addressCache.TryGetAddressesAsync(request, null, identity, forceRefreshPartitionAddresses, cancellationToken);

                if (addresses == null && identity.IsMasterService && this.masterServiceIdentityProvider != null)
                {
                    DefaultTrace.TraceWarning("Could not get addresses for MasterServiceIdentity {0}. will refresh masterServiceIdentity and retry", identity);
                    await this.masterServiceIdentityProvider.RefreshAsync(identity, cancellationToken);
                    identity = this.masterServiceIdentityProvider.MasterServiceIdentity;
                    addresses = await this.addressCache.TryGetAddressesAsync(request, null, identity, forceRefreshPartitionAddresses, cancellationToken);
                }

                if (addresses == null)
                {
                    DefaultTrace.TraceInformation("Could not get addresses for explicitly specified ServiceIdentity {0}", identity);
                    throw new NotFoundException() { ResourceAddress = request.ResourceAddress };
                }

                return new ResolutionResult(addresses, identity);
            }

            if (ReplicatedResourceClient.IsReadingFromMaster(request.ResourceType, request.OperationType) && request.PartitionKeyRangeIdentity == null)
            {
                DefaultTrace.TraceInformation("Resolving Master service address, forceMasterRefresh: {0}, currentMaster: {1}",
                    request.ForceMasterRefresh,
                    this.masterServiceIdentityProvider?.MasterServiceIdentity);

                // Client implementation, GlobalAddressResolver passes in a null IMasterServiceIdentityProvider, because it doesn't actually use the serviceIdentity
                // in the addressCache.TryGetAddresses method. In GatewayAddressCache.cs, the master address is resolved by making a call to Gateway AddressFeed,
                // not using the serviceIdentity that is passed in
                if (request.ForceMasterRefresh && this.masterServiceIdentityProvider != null)
                {
                    ServiceIdentity previousMasterService = this.masterServiceIdentityProvider.MasterServiceIdentity;
                    await this.masterServiceIdentityProvider.RefreshAsync(previousMasterService, cancellationToken);
                }
                ServiceIdentity serviceIdentity = this.masterServiceIdentityProvider?.MasterServiceIdentity;
                PartitionKeyRangeIdentity partitionKeyRangeIdentity = this.masterPartitionKeyRangeIdentity;
                PartitionAddressInformation addresses = await this.addressCache.TryGetAddressesAsync(
                    request,
                    partitionKeyRangeIdentity,
                    serviceIdentity,
                    forceRefreshPartitionAddresses,
                    cancellationToken);
                if (addresses == null)
                {
                    // This shouldn't really happen.
                    DefaultTrace.TraceCritical("Could not get addresses for master partition {0}", serviceIdentity);
                    throw new NotFoundException() { ResourceAddress = request.ResourceAddress };
                }

                PartitionKeyRange partitionKeyRange = new PartitionKeyRange { Id = PartitionKeyRange.MasterPartitionKeyRangeId };
                return new ResolutionResult(partitionKeyRange, addresses, serviceIdentity);
            }

            bool collectionCacheIsUptoDate = !request.IsNameBased ||
                (request.PartitionKeyRangeIdentity != null && request.PartitionKeyRangeIdentity.CollectionRid != null);

            bool collectionRoutingMapCacheIsUptoDate = false;

            ContainerProperties collection = await this.collectionCache.ResolveCollectionAsync(request, cancellationToken, NoOpTrace.Singleton);
            CollectionRoutingMap routingMap = await this.collectionRoutingMapCache.TryLookupAsync(
                collection.ResourceId, null, request, cancellationToken, NoOpTrace.Singleton);

            if (routingMap != null && request.ForceCollectionRoutingMapRefresh)
            {
                DefaultTrace.TraceInformation(
                    "AddressResolver.ResolveAddressesAndIdentityAsync ForceCollectionRoutingMapRefresh collection.ResourceId = {0}",
                    collection.ResourceId);

                routingMap = await this.collectionRoutingMapCache.TryLookupAsync(collection.ResourceId, routingMap, request, cancellationToken, NoOpTrace.Singleton);
            }

            if (request.ForcePartitionKeyRangeRefresh)
            {
                collectionRoutingMapCacheIsUptoDate = true;
                request.ForcePartitionKeyRangeRefresh = false;
                if (routingMap != null)
                {
                    routingMap = await this.collectionRoutingMapCache.TryLookupAsync(collection.ResourceId, routingMap, request, cancellationToken, NoOpTrace.Singleton);
                }
            }

            if (routingMap == null && !collectionCacheIsUptoDate)
            {
                // Routing map was not found by resolved collection rid. Maybe collection rid is outdated.
                // Refresh collection cache and reresolve routing map.
                request.ForceNameCacheRefresh = true;
                collectionCacheIsUptoDate = true;
                collectionRoutingMapCacheIsUptoDate = false;
                collection = await this.collectionCache.ResolveCollectionAsync(request, cancellationToken, NoOpTrace.Singleton);
                routingMap = await this.collectionRoutingMapCache.TryLookupAsync(
                        collection.ResourceId,
                        previousValue: null,
                        request: request,
                        cancellationToken: cancellationToken,
                        trace: NoOpTrace.Singleton);
            }

            AddressResolver.EnsureRoutingMapPresent(request, routingMap, collection);

            // At this point we have both collection and routingMap.
            ResolutionResult result = await this.TryResolveServerPartitionAsync(
                request,
                collection,
                routingMap,
                collectionCacheIsUptoDate,
                collectionRoutingMapCacheIsUptodate: collectionRoutingMapCacheIsUptoDate,
                forceRefreshPartitionAddresses: forceRefreshPartitionAddresses,
                cancellationToken: cancellationToken);

            if (result == null)
            {
                // Couldn't resolve server partition or its addresses.
                // Either collection cache is outdated or routing map cache is outdated.
                if (!collectionCacheIsUptoDate)
                {
                    request.ForceNameCacheRefresh = true;
                    collectionCacheIsUptoDate = true;
                    collection = await this.collectionCache.ResolveCollectionAsync(request, cancellationToken, NoOpTrace.Singleton);
                    if (collection.ResourceId != routingMap.CollectionUniqueId)
                    {
                        // Collection cache was stale. We resolved to new Rid. routing map cache is potentially stale
                        // for this new collection rid. Mark it as such.
                        collectionRoutingMapCacheIsUptoDate = false;
                        routingMap = await this.collectionRoutingMapCache.TryLookupAsync(
                            collection.ResourceId,
                            previousValue: null,
                            request: request,
                            cancellationToken: cancellationToken,
                            trace: NoOpTrace.Singleton);
                    }
                }

                if (!collectionRoutingMapCacheIsUptoDate)
                {
                    collectionRoutingMapCacheIsUptoDate = true;
                    routingMap = await this.collectionRoutingMapCache.TryLookupAsync(
                        collection.ResourceId,
                        previousValue: routingMap,
                        request: request,
                        cancellationToken: cancellationToken,
                        trace: NoOpTrace.Singleton);
                }

                AddressResolver.EnsureRoutingMapPresent(request, routingMap, collection);

                result = await this.TryResolveServerPartitionAsync(
                    request,
                    collection,
                    routingMap,
                    collectionCacheIsUptodate: true,
                    collectionRoutingMapCacheIsUptodate: true,
                    forceRefreshPartitionAddresses: forceRefreshPartitionAddresses,
                    cancellationToken: cancellationToken);
            }

            if (result == null)
            {
                DefaultTrace.TraceInformation("Couldn't route partitionkeyrange-oblivious request after retry/cache refresh. Collection doesn't exist.");

                // At this point collection cache and routing map caches are refreshed.
                // The only reason we will get here is if collection doesn't exist.
                // Case when partitionkeyrange doesn't exist is handled in the corresponding method.
                throw new NotFoundException() { ResourceAddress = request.ResourceAddress };
            }

            if (request.IsNameBased)
            {
                // Append collection rid.
                // If we resolved collection rid incorrectly because of outdated cache, this can lead 
                // to incorrect routing decisions. But backend will validate collection rid and throw
                // InvalidPartitionException if we reach wrong collection.
                // Also this header will be used by backend to inject collection rid into metrics for
                // throttled requests.
                request.Headers[WFConstants.BackendHeaders.CollectionRid] = collection.ResourceId;
            }

            return result;
        }

        private static void EnsureRoutingMapPresent(
            DocumentServiceRequest request,
            CollectionRoutingMap routingMap,
            ContainerProperties collection)
        {
            if (routingMap == null && request.IsNameBased && request.PartitionKeyRangeIdentity != null
                && request.PartitionKeyRangeIdentity.CollectionRid != null)
            {
                // By design, if partitionkeyrangeid header is present and it contains collectionrid for collection
                // which doesn't exist, we return InvalidPartitionException. Backend does the same.
                // Caller (client SDK or whoever attached the header) supposedly has outdated collection cache and will refresh it.
                // We cannot retry here, as the logic for retry in this case is use-case specific.
                DefaultTrace.TraceInformation(
                    "Routing map for request with partitionkeyrageid {0} was not found",
                    request.PartitionKeyRangeIdentity.ToHeader());
                throw new InvalidPartitionException() { ResourceAddress = request.ResourceAddress };
            }

            if (routingMap == null)
            {
                DefaultTrace.TraceInformation(
                    "Routing map was not found although collection cache is upto date for collection {0}",
                    collection.ResourceId);
                // Routing map not found although collection was resolved correctly.
                throw new NotFoundException() { ResourceAddress = request.ResourceAddress };
            }
        }

        private async Task<ResolutionResult> TryResolveServerPartitionAsync(
            DocumentServiceRequest request,
            ContainerProperties collection,
            CollectionRoutingMap routingMap,
            bool collectionCacheIsUptodate,
            bool collectionRoutingMapCacheIsUptodate,
            bool forceRefreshPartitionAddresses,
            CancellationToken cancellationToken)
        {
            // Check if this request partitionkeyrange-aware routing logic. We cannot retry here in this case
            // and need to bubble up errors.
            if (request.PartitionKeyRangeIdentity != null)
            {
                return await this.TryResolveServerPartitionByPartitionKeyRangeIdAsync(
                    request,
                    collection,
                    routingMap,
                    collectionCacheIsUptodate,
                    collectionRoutingMapCacheIsUptodate,
                    forceRefreshPartitionAddresses,
                    cancellationToken);
            }

            if (!request.ResourceType.IsPartitioned() &&
               !(request.ResourceType == ResourceType.StoredProcedure && request.OperationType == OperationType.ExecuteJavaScript) &&
               // Collection head is sent internally for strong consistency given routing hints from original requst, which is for partitioned resource.
               !(request.ResourceType == ResourceType.Collection && request.OperationType == OperationType.Head))
            {
                DefaultTrace.TraceCritical(
                    "Shouldn't come here for non partitioned resources. resourceType : {0}, operationtype:{1}, resourceaddress:{2}",
                    request.ResourceType,
                    request.OperationType,
                    request.ResourceAddress);
                throw new InternalServerErrorException(RMResources.InternalServerError) { ResourceAddress = request.ResourceAddress };
            }

            PartitionKeyRange range;
            string partitionKeyString = request.Headers[HttpConstants.HttpHeaders.PartitionKey];

            object effectivePartitionKeyStringObject = null;
            if (partitionKeyString != null)
            {
                range = AddressResolver.TryResolveServerPartitionByPartitionKey(
                    request,
                    partitionKeyString,
                    collectionCacheIsUptodate,
                    collection,
                    routingMap);
            }
            else if (request.Properties != null && request.Properties.TryGetValue(
                WFConstants.BackendHeaders.EffectivePartitionKeyString,
                out effectivePartitionKeyStringObject))
            {
                // Allow EPK only for partitioned collection (excluding migrated fixed collections)
                if (!collection.HasPartitionKey || collection.PartitionKey.IsSystemKey.GetValueOrDefault(false))
                {
                    throw new ArgumentOutOfRangeException(nameof(collection));
                }

                string effectivePartitionKeyString = effectivePartitionKeyStringObject as string;
                if (string.IsNullOrEmpty(effectivePartitionKeyString))
                {
                    throw new ArgumentOutOfRangeException(nameof(effectivePartitionKeyString));
                }

                range = routingMap.GetRangeByEffectivePartitionKey(effectivePartitionKeyString);
            }
            else
            {
                range = this.TryResolveSinglePartitionCollection(request, collection, routingMap, collectionCacheIsUptodate);
            }

            if (range == null)
            {
                // Collection cache or routing map cache is potentially outdated. Return null -
                // upper logic will refresh cache and retry.
                return null;
            }

            ServiceIdentity serviceIdentity = routingMap.TryGetInfoByPartitionKeyRangeId(range.Id);

            PartitionAddressInformation addresses = await this.addressCache.TryGetAddressesAsync(
                request,
                new PartitionKeyRangeIdentity(collection.ResourceId, range.Id),
                serviceIdentity,
                forceRefreshPartitionAddresses,
                cancellationToken);

            if (addresses == null)
            {
                DefaultTrace.TraceVerbose(
                    "Could not resolve addresses for identity {0}/{1}. Potentially collection cache or routing map cache is outdated. Return null - upper logic will refresh and retry. ",
                    new PartitionKeyRangeIdentity(collection.ResourceId, range.Id),
                    serviceIdentity);
                return null;
            }

            return new ResolutionResult(range, addresses, serviceIdentity);
        }

        private PartitionKeyRange TryResolveSinglePartitionCollection(
            DocumentServiceRequest request,
            ContainerProperties collection,
            CollectionRoutingMap routingMap,
            bool collectionCacheIsUptoDate)
        {
            // Neither partitionkey nor partitionkeyrangeid is specified.
            // Three options here:
            //    * This is non-partitioned collection and old client SDK which doesn't send partition key. In
            //      this case there's single entry in routing map. But can be multiple entries if before that
            //      existed partitioned collection with same name.
            //    * This is partitioned collection and old client SDK which doesn't send partition key.
            //      In this case there can be multiple ranges in routing map.
            //    * This is partitioned collection and this is custom written REST sdk, which has a bug and doesn't send
            //      partition key.
            // We cannot know for sure whether this is partitioned collection or not, because
            // partition key definition cache can be outdated.
            // So we route request to the first partition. If this is non-partitioned collection - request will succeed.
            // If it is partitioned collection - backend will return bad request as partition key header is required in this case.
            if (routingMap.OrderedPartitionKeyRanges.Count == 1)
            {
                return routingMap.OrderedPartitionKeyRanges.Single();
            }

            if (collectionCacheIsUptoDate)
            {
                // If the current collection is user-partitioned collection
                if (collection.PartitionKey.Paths.Count >= 1 &&
                    !collection.PartitionKey.IsSystemKey.GetValueOrDefault(false))
                {
                    throw new BadRequestException(RMResources.MissingPartitionKeyValue) { ResourceAddress = request.ResourceAddress };
                }
                else if (routingMap.OrderedPartitionKeyRanges.Count > 1)
                {
                    // With migrated-fixed-collection, it is possible to have multiple partition key ranges
                    // due to parallel usage of V3 SDK and a possible storage or throughput split
                    // The current client might be legacy and not aware of this.
                    // In such case route the request to the first partition
                    return AddressResolver.TryResolveServerPartitionByPartitionKey(
                                        request,
                                        "[]", // This corresponds to first partition
                                        collectionCacheIsUptoDate,
                                        collection,
                                        routingMap);
                }
                else
                {
                    // routingMap.OrderedPartitionKeyRanges.Count == 0
                    // Should never come here.
                    DefaultTrace.TraceCritical(
                        "No Partition Key ranges present for the collection {0}", collection.ResourceId);
                    throw new InternalServerErrorException(RMResources.InternalServerError) { ResourceAddress = request.ResourceAddress };

                }
            }
            else
            {
                return null;
            }
        }

        private ResolutionResult HandleRangeAddressResolutionFailure(
            DocumentServiceRequest request,
            bool collectionCacheIsUpToDate,
            bool routingMapCacheIsUpToDate,
            CollectionRoutingMap routingMap)
        {
            // Optimization to not refresh routing map unnecessary. As we keep track of parent child relationships,
            // we can determine that a range is gone just by looking up in the routing map.
            if (collectionCacheIsUpToDate && routingMapCacheIsUpToDate ||
                collectionCacheIsUpToDate && routingMap.IsGone(request.PartitionKeyRangeIdentity.PartitionKeyRangeId))
            {
                string errorMessage = string.Format(
                    CultureInfo.InvariantCulture,
                    RMResources.PartitionKeyRangeNotFound,
                    request.PartitionKeyRangeIdentity.PartitionKeyRangeId,
                    request.PartitionKeyRangeIdentity.CollectionRid);
                throw new PartitionKeyRangeGoneException(errorMessage) { ResourceAddress = request.ResourceAddress };
            }

            return null;
        }

        private async Task<ResolutionResult> TryResolveServerPartitionByPartitionKeyRangeIdAsync(
            DocumentServiceRequest request,
            ContainerProperties collection,
            CollectionRoutingMap routingMap,
            bool collectionCacheIsUpToDate,
            bool routingMapCacheIsUpToDate,
            bool forceRefreshPartitionAddresses,
            CancellationToken cancellationToken)
        {
            PartitionKeyRange partitionKeyRange = routingMap.TryGetRangeByPartitionKeyRangeId(request.PartitionKeyRangeIdentity.PartitionKeyRangeId);
            if (partitionKeyRange == null)
            {
                DefaultTrace.TraceInformation("Cannot resolve range '{0}'", request.PartitionKeyRangeIdentity.ToHeader());

                return this.HandleRangeAddressResolutionFailure(request, collectionCacheIsUpToDate, routingMapCacheIsUpToDate, routingMap);
            }

            ServiceIdentity identity = routingMap.TryGetInfoByPartitionKeyRangeId(request.PartitionKeyRangeIdentity.PartitionKeyRangeId);

            PartitionAddressInformation addresses = await this.addressCache.TryGetAddressesAsync(
                request,
                new PartitionKeyRangeIdentity(collection.ResourceId, request.PartitionKeyRangeIdentity.PartitionKeyRangeId),
                identity,
                forceRefreshPartitionAddresses,
                cancellationToken);

            if (addresses == null)
            {
                DefaultTrace.TraceInformation("Cannot resolve addresses for range '{0}'", request.PartitionKeyRangeIdentity.ToHeader());

                return this.HandleRangeAddressResolutionFailure(request, collectionCacheIsUpToDate, routingMapCacheIsUpToDate, routingMap);
            }

            return new ResolutionResult(partitionKeyRange, addresses, identity);
        }

        internal static PartitionKeyRange TryResolveServerPartitionByPartitionKey(
            DocumentServiceRequest request,
            string partitionKeyString,
            bool collectionCacheUptoDate,
            ContainerProperties collection,
            CollectionRoutingMap routingMap)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (partitionKeyString == null)
            {
                throw new ArgumentNullException("partitionKeyString");
            }

            if (collection == null)
            {
                throw new ArgumentNullException("collection");
            }

            if (routingMap == null)
            {
                throw new ArgumentNullException("routingMap");
            }

            PartitionKeyInternal partitionKey;

            try
            {
                partitionKey = PartitionKeyInternal.FromJsonString(partitionKeyString);
            }
            catch (JsonException ex)
            {
                throw new BadRequestException(
                    string.Format(CultureInfo.InvariantCulture, RMResources.InvalidPartitionKey, partitionKeyString),
                    ex) { ResourceAddress = request.ResourceAddress };
            }

            if (partitionKey == null)
            {
                throw new InternalServerErrorException(string.Format(CultureInfo.InvariantCulture, "partition key is null '{0}'", partitionKeyString));
            }

            if (partitionKey.Equals(PartitionKeyInternal.Empty) || partitionKey.Components.Count == collection.PartitionKey.Paths.Count)
            {
                // Although we can compute effective partition key here, in general case this Gateway can have outdated
                // partition key definition cached - like if collection with same name but with Range partitioning is created.
                // In this case server will not pass x-ms-documentdb-collection-rid check and will return back InvalidPartitionException.
                // Gateway will refresh its cache and retry.

                string effectivePartitionKey = partitionKey.GetEffectivePartitionKeyString(collection.PartitionKey);

                // There should be exactly one range which contains a partition key. Always.
                return routingMap.GetRangeByEffectivePartitionKey(effectivePartitionKey);
            }

            if (collectionCacheUptoDate)
            {
                BadRequestException badRequestException = new BadRequestException(RMResources.PartitionKeyMismatch) { ResourceAddress = request.ResourceAddress };
                badRequestException.Headers[WFConstants.BackendHeaders.SubStatus] =
                    ((uint)SubStatusCodes.PartitionKeyMismatch).ToString(CultureInfo.InvariantCulture);

                throw badRequestException;
            }

            // Partition key supplied has different number paths than locally cached partition key definition.
            // Three things can happen:
            //    1. User supplied wrong partition key.
            //    2. Client SDK has outdated partition key definition cache and extracted wrong value from the document.
            //    3. Gateway's cache is outdated.
            //
            // What we will do is append x-ms-documentdb-collection-rid header and forward it to random collection partition.
            // * If collection rid matches, server will send back 400.1001, because it also will not be able to compute
            // effective partition key. Gateway will forward this status code to client - client will handle it.
            // * If collection rid doesn't match, server will send back InvalidPartiitonException and Gateway will
            //   refresh name routing cache - this will refresh partition key definition as well, and retry.

            DefaultTrace.TraceInformation(
                "Cannot compute effective partition key. Definition has '{0}' paths, values supplied has '{1}' paths. Will refresh cache and retry.",
                collection.PartitionKey.Paths.Count,
                partitionKey.Components.Count);

            return null;
        }

        public Task UpdateAsync(ServerKey serverKey, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(IReadOnlyList<AddressCacheToken> addressCacheTokens, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        private class ResolutionResult
        {
            public PartitionKeyRange TargetPartitionKeyRange { get; private set; }

            public PartitionAddressInformation Addresses { get; private set; }

            public ServiceIdentity TargetServiceIdentity { get; private set; }

            public ResolutionResult(
                PartitionAddressInformation addresses,
                ServiceIdentity serviceIdentity)
            {
                if (addresses == null)
                {
                    throw new ArgumentNullException("addresses");
                }

                if (serviceIdentity == null)
                {
                    throw new ArgumentNullException("serviceIdentity");
                }

                this.Addresses = addresses;
                this.TargetServiceIdentity = serviceIdentity;
            }

            public ResolutionResult(
                PartitionKeyRange targetPartitionKeyRange,
                PartitionAddressInformation addresses,
                ServiceIdentity serviceIdentity)
            {
                if (targetPartitionKeyRange == null)
                {
                    throw new ArgumentNullException("targetPartitionKeyRange");
                }

                if (addresses == null)
                {
                    throw new ArgumentNullException("addresses");
                }

                this.TargetPartitionKeyRange = targetPartitionKeyRange;
                this.Addresses = addresses;
                this.TargetServiceIdentity = serviceIdentity;
            }
        }
    }
}