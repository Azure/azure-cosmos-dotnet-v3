//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class StoreModelHelper
    {
        private static readonly string sessionConsistencyAsString = ConsistencyLevel.Session.ToString();

        public static async Task CaptureSessionTokenAndHandleSplitAsync(
            ISessionContainer sessionContainer,
            PartitionKeyRangeCache partitionKeyRangeCache,
            HttpStatusCode? statusCode,
            SubStatusCodes subStatusCode,
            DocumentServiceRequest request,
            INameValueCollection responseHeaders)
        {
            if (request.IsValidStatusCodeForExceptionlessRetry((int)statusCode, subStatusCode))
            {
                if (ReplicatedResourceClient.IsMasterResource(request.ResourceType))
                {
                    return;
                }

                if (statusCode != HttpStatusCode.PreconditionFailed
                    && statusCode != HttpStatusCode.Conflict
                    && (statusCode != HttpStatusCode.NotFound || subStatusCode == SubStatusCodes.ReadSessionNotAvailable))
                {
                    return;
                }
            }

            if (request.ResourceType == ResourceType.Collection && request.OperationType == OperationType.Delete)
            {
                string resourceId = request.IsNameBased
                    ? responseHeaders[HttpConstants.HttpHeaders.OwnerId]
                    : request.ResourceId;

                sessionContainer.ClearTokenByResourceId(resourceId);
            }
            else
            {
                sessionContainer.SetSessionToken(request, responseHeaders);

                PartitionKeyRange detectedPartitionKeyRange = request.RequestContext.ResolvedPartitionKeyRange;
                string partitionKeyRangeInResponse = responseHeaders[HttpConstants.HttpHeaders.PartitionKeyRangeId];
                if (detectedPartitionKeyRange != null
                    && !string.IsNullOrEmpty(partitionKeyRangeInResponse)
                    && !string.IsNullOrEmpty(request.RequestContext.ResolvedCollectionRid)
                    && !partitionKeyRangeInResponse.Equals(detectedPartitionKeyRange.Id, StringComparison.OrdinalIgnoreCase))
                {
                    // The request ended up being on a different partition unknown to the client, so we refresh the caches
                    await partitionKeyRangeCache.TryGetPartitionKeyRangeByIdAsync(
                        request.RequestContext.ResolvedCollectionRid,
                        partitionKeyRangeInResponse,
                        NoOpTrace.Singleton,
                        forceRefresh: true);
                }
            }
        }

        public static async Task ApplySessionTokenAsync(
            DocumentServiceRequest request,
            ConsistencyLevel defaultConsistencyLevel,
            ISessionContainer sessionContainer,
            PartitionKeyRangeCache partitionKeyRangeCache,
            CollectionCache clientCollectionCache,
            IGlobalEndpointManager globalEndpointManager)
        {
            if (request.Headers == null)
            {
                throw new InvalidOperationException("DocumentServiceRequest does not have headers.");
            }

            // Master resource operations don't require session token.
            if (IsMasterOperation(request.ResourceType, request.OperationType))
            {
                // Make sure to remove any existing session token if present
                if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.SessionToken]))
                {
                    request.Headers.Remove(HttpConstants.HttpHeaders.SessionToken);
                }

                return;
            }

            // If user explicitly set a session token, do not override it
            if (!string.IsNullOrEmpty(request.Headers[HttpConstants.HttpHeaders.SessionToken]))
            {
                return;
            }

            // Check consistency
            string requestConsistencyLevel = request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel];
            bool isReadOrBatchRequest = request.IsReadOnlyRequest || request.OperationType == OperationType.Batch;
            bool requestHasConsistencySet = !string.IsNullOrEmpty(requestConsistencyLevel) && isReadOrBatchRequest;

            bool sessionConsistencyApplies =
                (!requestHasConsistencySet && defaultConsistencyLevel == ConsistencyLevel.Session) ||
                (requestHasConsistencySet
                    && string.Equals(requestConsistencyLevel, sessionConsistencyAsString, StringComparison.OrdinalIgnoreCase));

            bool isMultiMasterEnabledForRequest = globalEndpointManager.CanUseMultipleWriteLocations(request);

            // Only apply session token if session consistency is relevant and:
            //   - it's a read (or batch) OR 
            //   - it's a write with MultiMaster enabled
            if (!sessionConsistencyApplies
                || (!isReadOrBatchRequest && !isMultiMasterEnabledForRequest))
            {
                return;
            }

            (bool isSuccess, string sessionToken) = await TryResolveSessionTokenAsync(
                request,
                sessionContainer,
                partitionKeyRangeCache,
                clientCollectionCache);

            if (isSuccess && !string.IsNullOrEmpty(sessionToken))
            {
                request.Headers[HttpConstants.HttpHeaders.SessionToken] = sessionToken;
            }
        }

        public static async Task<(bool isSuccess, string sessionToken)> TryResolveSessionTokenAsync(
            DocumentServiceRequest request,
            ISessionContainer sessionContainer,
            PartitionKeyRangeCache partitionKeyRangeCache,
            CollectionCache clientCollectionCache)
        {
            if (request.ResourceType.IsPartitioned())
            {
                (bool isSuccess, PartitionKeyRange partitionKeyRange) = await TryResolvePartitionKeyRangeAsync(
                    request,
                    sessionContainer,
                    partitionKeyRangeCache,
                    clientCollectionCache,
                    refreshCache: false);

                if (isSuccess && sessionContainer is SessionContainer gatewaySessionContainer)
                {
                    request.RequestContext.ResolvedPartitionKeyRange = partitionKeyRange;
                    string localSessionToken =
                        gatewaySessionContainer.ResolvePartitionLocalSessionTokenForGateway(request, partitionKeyRange.Id);

                    if (!string.IsNullOrEmpty(localSessionToken))
                    {
                        return (true, localSessionToken);
                    }
                }
            }

            return (false, null);
        }

        private static async Task<(bool isSuccess, PartitionKeyRange partitionKeyRange)> TryResolvePartitionKeyRangeAsync(
            DocumentServiceRequest request,
            ISessionContainer sessionContainer,
            PartitionKeyRangeCache partitionKeyRangeCache,
            CollectionCache clientCollectionCache,
            bool refreshCache)
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
                    collection.ResourceId,
                    previousValue: null,
                    request: request,
                    NoOpTrace.Singleton);

                if (refreshCache && collectionRoutingMap != null)
                {
                    collectionRoutingMap = await partitionKeyRangeCache.TryLookupAsync(
                        collection.ResourceId,
                        collectionRoutingMap,
                        request,
                        NoOpTrace.Singleton);
                }

                if (collectionRoutingMap != null)
                {
                    partitonKeyRange = AddressResolver.TryResolveServerPartitionByPartitionKey(
                        request,
                        partitionKeyString,
                        collectionCacheUptoDate: false,
                        collection,
                        collectionRoutingMap);
                }
            }
            else if (request.PartitionKeyRangeIdentity != null)
            {
                PartitionKeyRangeIdentity pkRangeId = request.PartitionKeyRangeIdentity;
                partitonKeyRange = await partitionKeyRangeCache.TryGetPartitionKeyRangeByIdAsync(
                    collection.ResourceId,
                    pkRangeId.PartitionKeyRangeId,
                    NoOpTrace.Singleton,
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
                    return (false, null);
                }

                // Retry with refresh
                return await TryResolvePartitionKeyRangeAsync(
                    request,
                    sessionContainer,
                    partitionKeyRangeCache,
                    clientCollectionCache,
                    refreshCache: true);
            }

            return (true, partitonKeyRange);
        }

        internal static bool IsMasterOperation(ResourceType resourceType, OperationType operationType)
        {
            return ReplicatedResourceClient.IsMasterResource(resourceType)
                || IsStoredProcedureCrudOperation(resourceType, operationType)
                || resourceType == ResourceType.Trigger
                || resourceType == ResourceType.UserDefinedFunction
                || operationType == OperationType.QueryPlan;
        }

        internal static bool IsStoredProcedureCrudOperation(ResourceType resourceType, OperationType operationType)
        {
            return resourceType == ResourceType.StoredProcedure
                   && operationType != OperationType.ExecuteJavaScript;
        }
    }
}
