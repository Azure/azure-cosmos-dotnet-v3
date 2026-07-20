// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Owns the distributed-transaction per-partition session-token resolution: it resolves each operation's
    /// partition and applies that partition's token. Created once per commit via <see cref="TryCreateAsync"/>,
    /// holding the resolved SessionContainer and PartitionKeyRangeCache so the commit's single pass over
    /// operations can apply tokens without re-resolving infrastructure.
    /// </summary>
    internal sealed class DistributedTransactionSessionTokenResolver
    {
        private readonly SessionContainer sessionContainer;
        private readonly Routing.PartitionKeyRangeCache partitionKeyRangeCache;
        private readonly bool canUseMultipleWriteLocations;

        internal DistributedTransactionSessionTokenResolver(
            SessionContainer sessionContainer,
            Routing.PartitionKeyRangeCache partitionKeyRangeCache,
            bool canUseMultipleWriteLocations)
        {
            this.sessionContainer = sessionContainer;
            this.partitionKeyRangeCache = partitionKeyRangeCache;
            this.canUseMultipleWriteLocations = canUseMultipleWriteLocations;
        }

        /// <summary>
        /// Creates a resolver for auto per-partition token resolution, or returns null when it can't run —
        /// non-Session consistency, a custom ISessionContainer, or the PartitionKeyRangeCache is unavailable
        /// (tokens can still be passed explicitly via request options). Resolves the built-in SessionContainer
        /// and PartitionKeyRangeCache once so the commit's single pass can apply tokens without re-resolving.
        /// </summary>
        internal static async Task<DistributedTransactionSessionTokenResolver> TryCreateAsync(
            CosmosClientContext clientContext,
            bool isSessionConsistency)
        {
            if (!isSessionConsistency)
            {
                return null;
            }

            ISessionContainer clientSessionContainer = clientContext.DocumentClient?.sessionContainer;
            SessionContainer sessionContainer = clientSessionContainer as SessionContainer;
            if (sessionContainer == null)
            {
                if (clientSessionContainer != null)
                {
                    DefaultTrace.TraceWarning(
                        "DistributedTransaction auto session-token resolution is disabled: " +
                        "ISessionContainer implementation is '{0}', not the built-in SessionContainer. " +
                        "Pass session tokens explicitly via DistributedTransactionRequestOptions.SessionToken " +
                        "to enforce session consistency.",
                        clientSessionContainer.GetType().FullName);
                }

                return null;
            }

            // Best-effort: no cache means no partition resolves (no token applied anyway), so disable
            // auto-resolution for this commit and let Utils skip token application.
            Routing.PartitionKeyRangeCache partitionKeyRangeCache;
            try
            {
                partitionKeyRangeCache = await clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                DefaultTrace.TraceWarning(
                    "DistributedTransaction could not obtain PartitionKeyRangeCache; auto session-token " +
                    "resolution is disabled for this commit. Operations get no auto-resolved token. Exception: {0}",
                    ex.Message);
                return null;
            }

            if (partitionKeyRangeCache == null)
            {
                return null;
            }

            // Capture multi-master capability once (point ops skip the token on a single-master write; DTX
            // mirrors that). A throwaway Document/Create request drives the request-based
            // CanUseMultipleWriteLocations(request) gate — the exact point-op predicate. The account-level
            // CanSupportMultipleWriteLocations accessor is avoided: its ">1 write region" clause breaks that
            // parity and could gate off a token the point-op path would keep (weaker read-your-own-writes).
            bool canUseMultipleWriteLocations = false;
            try
            {
                Routing.GlobalEndpointManager globalEndpointManager = clientContext.DocumentClient?.GlobalEndpointManager;
                if (globalEndpointManager != null)
                {
                    using (DocumentServiceRequest documentWriteProbe = DocumentServiceRequest.Create(
                        OperationType.Create,
                        ResourceType.Document,
                        AuthorizationTokenType.PrimaryMasterKey))
                    {
                        canUseMultipleWriteLocations = globalEndpointManager.CanUseMultipleWriteLocations(documentWriteProbe);
                    }
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // Best-effort: on failure assume single-master (the conservative gate) — a missed token on a
                // multi-master write only costs an extra server-side session check, never correctness.
                DefaultTrace.TraceWarning(
                    "DistributedTransaction could not determine multi-master capability; assuming single-master " +
                    "for the session-token write-gate. Exception: {0}",
                    ex.Message);
            }

            return new DistributedTransactionSessionTokenResolver(sessionContainer, partitionKeyRangeCache, canUseMultipleWriteLocations);
        }

        /// <summary>
        /// Resolves and applies a per-partition session token to every operation in the collection that has
        /// no explicit token. The routing map is looked up once per collection and reused across its ops.
        /// </summary>
        internal async Task ApplyTokensAsync(
            IEnumerable<DistributedTransactionOperation> operations,
            string collectionPath,
            ContainerProperties containerProperties)
        {
            Routing.CollectionRoutingMap routingMap = await this.TryLookupRoutingMapAsync(
                collectionPath,
                containerProperties.ResourceId);

            foreach (DistributedTransactionOperation operation in operations)
            {
                this.TryApplyResolvedSessionToken(operation, collectionPath, containerProperties, routingMap);
            }
        }

        /// <summary>
        /// Looks up the collection routing map that maps a partition key to a physical partition. Returns
        /// null when the cache is unavailable or the lookup fails, so those ops are sent with no token.
        /// </summary>
        private async Task<Routing.CollectionRoutingMap> TryLookupRoutingMapAsync(
            string collectionPath,
            string containerResourceId)
        {
            try
            {
                return await this.partitionKeyRangeCache.TryLookupAsync(
                    collectionRid: containerResourceId,
                    previousValue: null,
                    request: null,
                    trace: NoOpTrace.Singleton);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                DefaultTrace.TraceWarning(
                    "DistributedTransaction routing-map lookup failed for collection '{0}'; operations " +
                    "in this collection will be sent with no session token. Exception: {1}",
                    collectionPath,
                    ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Resolves the operation's partition range and applies a per-partition session token, unless the
        /// caller already provided one (never overridden). The resolved range is recorded even for
        /// user-supplied-token ops so the post-commit capture pass can detect a partition move for them,
        /// matching the point-op path. An operation whose partition can't be resolved gets no token.
        /// </summary>
        private void TryApplyResolvedSessionToken(
            DistributedTransactionOperation operation,
            string collectionPath,
            ContainerProperties containerProperties,
            Routing.CollectionRoutingMap routingMap)
        {
            // Resolve and record the range up front, regardless of token source, so the commit's capture
            // pass can detect a partition move even for user-supplied-token ops — matching the point-op path.
            string resolvedToken = this.ResolvePartitionLocalToken(
                collectionPath,
                containerProperties,
                routingMap,
                operation.PartitionKey,
                out string resolvedPartitionKeyRangeId);

            operation.ResolvedPartitionKeyRangeId = resolvedPartitionKeyRangeId;

            if (!string.IsNullOrEmpty(operation.SessionToken))
            {
                return; // User explicitly provided a token — don't override (range still recorded above).
            }

            // Write-gate parity with point ops (GatewayStoreModel): a single-master write gets no token;
            // reads and multi-master writes still do. The range is already recorded above for split detection.
            if (!OperationTypeExtensions.IsReadOperation(operation.OperationType) && !this.canUseMultipleWriteLocations)
            {
                return;
            }

            if (!string.IsNullOrEmpty(resolvedToken))
            {
                operation.SessionToken = resolvedToken;
            }
        }

        /// <summary>
        /// Resolves the partition-local session token for an operation's partition key, mirroring
        /// GatewayStoreModel.TryResolveSessionTokenAsync.
        /// </summary>
        /// <remarks>
        /// Returns the partition's token, or null when it has no token yet or can't be resolved (routing map
        /// unavailable, range not found, or a None partition key). The compound collection-wide token is
        /// never substituted: it aggregates every partition's LSN, so stamping it on one op would attach
        /// other partitions' progress to it.
        /// </remarks>
        private string ResolvePartitionLocalToken(
            string collectionPath,
            ContainerProperties containerProperties,
            Routing.CollectionRoutingMap routingMap,
            PartitionKey partitionKey,
            out string resolvedPartitionKeyRangeId)
        {
            resolvedPartitionKeyRangeId = null;

            // A None or default(PartitionKey) can't be routed; both lack a routable key (None is the sentinel,
            // default has a null InternalKey). Mapping either would stamp a wrong-partition token.
            if (partitionKey.IsNone || partitionKey.InternalKey == null)
            {
                return null;
            }

            if (routingMap != null)
            {
                try
                {
                    // Delegate key-guard, effective-key and range lookup to the shared core so DTX and the
                    // point-op path share one definition. collectionCacheUptoDate is moot here: KeyMismatch
                    // and StaleMetadata both degrade to "no token".
                    PartitionKeyRangeResolutionKind resolutionKind = AddressResolver.TryResolvePartitionKeyToRange(
                        partitionKey.InternalKey,
                        containerProperties,
                        routingMap,
                        collectionCacheUptoDate: true,
                        out PartitionKeyRange range);

                    if (resolutionKind == PartitionKeyRangeResolutionKind.Resolved)
                    {
                        // Record the range for split detection, then return the partition's token (may be
                        // null). range.Parents lets a freshly-split child inherit the parent's progress.
                        resolvedPartitionKeyRangeId = range.Id;
                        return this.sessionContainer.GetSessionTokenForPartitionKeyRange(collectionPath, range.Id, range.Parents);
                    }

                    // KeyMismatch or StaleMetadata: apply no token and trace (observable degrade-to-eventual).
                    // Never throw — real routing surfaces any key mismatch server-side for the actual write.
                    DefaultTrace.TraceWarning(
                        "DistributedTransaction could not resolve an operation's partition key to a single range in " +
                        "collection '{0}' (outcome: {1}); applying no session token (served at eventual consistency).",
                        collectionPath,
                        resolutionKind);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    // Routing resolution failed (e.g., stale cache); apply no token.
                    DefaultTrace.TraceWarning(
                        "DistributedTransaction per-partition session-token resolution failed for collection '{0}'; " +
                        "applying no token for this operation. Exception: {1}",
                        collectionPath,
                        ex.Message);
                }
            }

            // Partition unresolved (no routing map or range not found): apply no token, never the compound token.
            return null;
        }
    }
}
