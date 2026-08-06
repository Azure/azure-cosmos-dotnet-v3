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
    /// Applies each operation's <em>partition-local</em> session token rather than the compound collection
    /// token, mirroring the point-op path in GatewayStoreModel.
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
        /// Returns null when auto-resolution cannot run: consistency is not Session, the client uses a custom
        /// <see cref="ISessionContainer"/> rather than the built-in one, or the PartitionKeyRangeCache is
        /// unavailable. None of these is an error — the commit proceeds without an auto-applied token, and
        /// callers can still supply one via request options.
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

            bool canUseMultipleWriteLocations = DistributedTransactionSessionTokenResolver.CanUseMultipleWriteLocationsForDocumentWrite(
                clientContext);

            return new DistributedTransactionSessionTokenResolver(sessionContainer, partitionKeyRangeCache, canUseMultipleWriteLocations);
        }

        private static bool CanUseMultipleWriteLocationsForDocumentWrite(CosmosClientContext clientContext)
        {
            try
            {
                Routing.GlobalEndpointManager globalEndpointManager = clientContext.DocumentClient?.GlobalEndpointManager;
                if (globalEndpointManager == null)
                {
                    return false;
                }

                // Drives the same per-request gate the point-op path uses. The account-level
                // CanSupportMultipleWriteLocations is deliberately avoided: its ">1 write region" clause would
                // gate off a token the point-op path would keep, weakening read-your-own-writes.
                using (DocumentServiceRequest documentWriteProbe = DocumentServiceRequest.Create(
                    OperationType.Create,
                    ResourceType.Document,
                    AuthorizationTokenType.PrimaryMasterKey))
                {
                    return globalEndpointManager.CanUseMultipleWriteLocations(documentWriteProbe);
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // Single-master is the conservative assumption: a missed token on a multi-master write costs
                // an extra server-side session check, never correctness.
                DefaultTrace.TraceWarning(
                    "DistributedTransaction could not determine multi-master capability; assuming single-master " +
                    "for the session-token write-gate. Exception: {0}",
                    ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Applies a partition-local session token to each operation, skipping any that already carries one
        /// and any write excluded by the multi-master gate. Scoped to a single collection so its routing map
        /// is looked up once and shared.
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
        /// Unlike the usual Try* convention this can throw: when the session container already holds causal
        /// progress for the collection, a metadata failure is surfaced rather than degraded, because
        /// committing tokenless would serve stale reads inside a session that has already promised progress.
        /// Returns null only when nothing is cached and there is no guarantee to lose.
        /// </summary>
        private async Task<Routing.CollectionRoutingMap> TryLookupRoutingMapAsync(
            string collectionPath,
            string containerResourceId)
        {
            Routing.CollectionRoutingMap routingMap;
            try
            {
                routingMap = await this.partitionKeyRangeCache.TryLookupAsync(
                    collectionRid: containerResourceId,
                    previousValue: null,
                    request: null,
                    trace: NoOpTrace.Singleton);
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                // The throw runs inside PrepareOperationsAsync, before ExecuteCommitWithRetryAsync, so the
                // commit retry loop does not swallow it.
                if (this.CollectionHasCachedProgress(collectionPath))
                {
                    DefaultTrace.TraceError(
                        "DistributedTransaction routing-map lookup failed for collection '{0}' while the session " +
                        "container already holds causal progress; failing the commit instead of sending Session " +
                        "operations with no token. Exception: {1}",
                        collectionPath,
                        ex.Message);
                    throw;
                }

                // Nothing cached, so there is no guarantee to violate; degrade to no token.
                DefaultTrace.TraceWarning(
                    "DistributedTransaction routing-map lookup failed for collection '{0}' with no cached progress; " +
                    "operations in this collection will be sent with no session token. Exception: {1}",
                    collectionPath,
                    ex.Message);
                return null;
            }

            // Same reasoning as the catch above: a null map is as token-destroying as a throw.
            if (routingMap == null && this.CollectionHasCachedProgress(collectionPath))
            {
                throw new InvalidOperationException(
                    $"DistributedTransaction could not resolve the routing map for collection '{collectionPath}' " +
                    "while the session container already holds causal progress; failing the commit instead of " +
                    "sending Session operations with no session token.");
            }

            return routingMap;
        }

        private bool CollectionHasCachedProgress(string collectionPath)
        {
            return !string.IsNullOrEmpty(this.sessionContainer.GetSessionToken(collectionPath));
        }

        private void TryApplyResolvedSessionToken(
            DistributedTransactionOperation operation,
            string collectionPath,
            ContainerProperties containerProperties,
            Routing.CollectionRoutingMap routingMap)
        {
            // Resolved ahead of the two early returns below: either may skip applying a token, but neither
            // should skip recording the range id.
            string resolvedToken = this.ResolvePartitionLocalToken(
                collectionPath,
                containerProperties,
                routingMap,
                operation.PartitionKey,
                out string resolvedPartitionKeyRangeId);

            // The DTX analogue of DocumentServiceRequest.RequestContext.ResolvedPartitionKeyRangeId: sub-ops
            // are payload items in one batched commit, so there is no RequestContext to hang it on. Consumed by
            // PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync, which compares it against the
            // range the server actually served to detect a split. It must be captured here at send time, not
            // recomputed after the commit: by then the routing cache may have refreshed past the split and
            // would always agree with the server, silently defeating the detection. The DTX-side capture call
            // lands in a follow-up PR, so this property is write-only for now — not dead code.
            operation.ResolvedPartitionKeyRangeId = resolvedPartitionKeyRangeId;

            if (!string.IsNullOrEmpty(operation.SessionToken))
            {
                return; // A caller-supplied token is authoritative.
            }

            // Write-gate parity with GatewayStoreModel: on a single-master account the write goes to the sole
            // write region, so a token would add a session check with nothing to wait for.
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
        /// Returns the partition's own token, never the compound collection token.
        /// </summary>
        private string ResolvePartitionLocalToken(
            string collectionPath,
            ContainerProperties containerProperties,
            Routing.CollectionRoutingMap routingMap,
            PartitionKey partitionKey,
            out string resolvedPartitionKeyRangeId)
        {
            resolvedPartitionKeyRangeId = null;

            // None is the unroutable sentinel and default(PartitionKey) has a null InternalKey; routing either
            // would stamp another partition's token.
            if (partitionKey.IsNone || partitionKey.InternalKey == null)
            {
                return null;
            }

            if (routingMap != null)
            {
                try
                {
                    // Shared with the point-op path so key-guard, effective-key and range lookup have one
                    // definition. collectionCacheUptoDate: true classifies a partial key as KeyMismatch rather
                    // than sending the caller around a refresh loop the up-to-date map cannot improve on.
                    PartitionKeyRangeResolutionKind resolutionKind = AddressResolver.TryResolvePartitionKeyToRange(
                        partitionKey.InternalKey,
                        containerProperties,
                        routingMap,
                        collectionCacheUptoDate: true,
                        out PartitionKeyRange range);

                    if (resolutionKind == PartitionKeyRangeResolutionKind.Resolved)
                    {
                        resolvedPartitionKeyRangeId = range.Id;

                        // No forced refresh: a full key against a complete map always resolves, during a split
                        // to the covering parent whose token is the correct causal floor. range.Parents lets a
                        // freshly-split child inherit that progress instead of starting from no token.
                        return this.sessionContainer.GetSessionTokenForPartitionKeyRange(collectionPath, range.Id, range.Parents);
                    }

                    // Traced because the operation silently drops to eventual consistency; a genuine key
                    // mismatch still fails server-side on the real write.
                    DefaultTrace.TraceWarning(
                        "DistributedTransaction could not resolve an operation's partition key to a single range in " +
                        "collection '{0}' (outcome: {1}); applying no session token (served at eventual consistency).",
                        collectionPath,
                        resolutionKind);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    DefaultTrace.TraceWarning(
                        "DistributedTransaction per-partition session-token resolution failed for collection '{0}'; " +
                        "applying no token for this operation. Exception: {1}",
                        collectionPath,
                        ex.Message);
                }
            }

            // Degrade to no token rather than throw, matching GatewayStoreModel.TryResolveSessionTokenAsync.
            // Never the compound collection token: it aggregates every partition's LSN, so stamping it here
            // would make this operation wait on unrelated partitions.
            return null;
        }
    }
}
