// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    internal class DistributedTransactionCommitterUtils
    {
        /// <summary>
        /// Determines whether an exception thrown by SessionContainer.SetSessionToken indicates a
        /// malformed (content-invalid) session token. Only this case is caught per-op in
        /// MergeSessionTokens and (under Session consistency on a committed response) surfaced as a
        /// non-retriable error; every other exception propagates unchanged.
        /// </summary>
        /// <remarks>
        /// SetSessionToken parses the token via SessionTokenHelper.Parse, which throws
        /// <see cref="BadRequestException"/> — and only that — on unparseable content. Broader types
        /// must not be treated as malformed: SessionContainer throws
        /// <c>InternalServerErrorException</c> on a benign concurrent-add race and ResourceId.Parse
        /// can throw on a bad collection RID, and misclassifying either would surface a spurious 500
        /// for a committed transaction.
        /// </remarks>
        internal static bool IsMalformedSessionTokenException(Exception ex)
        {
            return ex is BadRequestException;
        }

        public static async Task<bool> ResolveCollectionRidsAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            CancellationToken cancellationToken)
        {
            IEnumerable<IGrouping<string, DistributedTransactionOperation>> groupedOperations = operations
                .GroupBy(op => DistributedTransactionConstants.GetCollectionFullName(op.Database, op.Container));

            ISessionContainer sessionContainer = clientContext.DocumentClient?.sessionContainer;

            // Auto-resolution of session tokens is only meaningful under Session consistency
            // (mirrors GatewayStoreModel.ApplySessionTokenAsync which gates on Session).
            bool isSessionConsistency = await DistributedTransactionCommitterUtils.IsEffectiveSessionConsistencyAsync(clientContext);

            // Auto-resolution requires SessionContainer.GetSessionTokenForPartitionKeyRange, which is
            // not on the ISessionContainer interface, so a custom container disables it. Detect the
            // unsupported case once per commit and trace it. Tokens can still be passed explicitly via
            // DistributedTransactionRequestOptions.SessionToken.
            SessionContainer concreteSessionContainer = isSessionConsistency
                ? sessionContainer as SessionContainer
                : null;

            if (isSessionConsistency && sessionContainer != null && concreteSessionContainer == null)
            {
                DefaultTrace.TraceWarning(
                    "DistributedTransaction auto session-token resolution is disabled: " +
                    "ISessionContainer implementation is '{0}', not the built-in SessionContainer. " +
                    "Pass session tokens explicitly via DistributedTransactionRequestOptions.SessionToken " +
                    "to enforce session consistency.",
                    sessionContainer.GetType().FullName);
            }

            // Obtain the PartitionKeyRangeCache for per-partition token resolution. If unavailable,
            // resolution falls back to the compound token rather than failing the commit — token
            // resolution must stay best-effort.
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = null;
            if (concreteSessionContainer != null)
            {
                try
                {
                    partitionKeyRangeCache = await clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    DefaultTrace.TraceWarning(
                        "DistributedTransaction could not obtain PartitionKeyRangeCache; per-partition " +
                        "session-token resolution will fall back to the compound token. Exception: {0}",
                        ex.Message);
                }
            }

            foreach (IGrouping<string, DistributedTransactionOperation> group in groupedOperations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string collectionPath = group.Key;
                ContainerProperties containerProperties = await clientContext.GetCachedContainerPropertiesAsync(
                    collectionPath,
                    NoOpTrace.Singleton,
                    cancellationToken);

                string containerResourceId = containerProperties.ResourceId;
                ResourceId resourceId = ResourceId.Parse(containerResourceId);
                string databaseResourceId = resourceId.DatabaseId.ToString();

                // Resolve per-partition session tokens from SessionContainer for operations
                // without an explicit user-provided token (mirrors GatewayStoreModel.TryResolveSessionTokenAsync).
                // Falls back to the compound (collection-wide) token only if the routing map is unavailable.
                Routing.CollectionRoutingMap routingMap = null;
                if (partitionKeyRangeCache != null)
                {
                    try
                    {
                        routingMap = await partitionKeyRangeCache.TryLookupAsync(
                            collectionRid: containerResourceId,
                            previousValue: null,
                            request: null,
                            trace: NoOpTrace.Singleton);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        DefaultTrace.TraceWarning(
                            "DistributedTransaction routing-map lookup failed for collection '{0}'; per-partition " +
                            "session-token resolution will fall back to the compound token. Exception: {1}",
                            collectionPath,
                            ex.Message);
                    }
                }

                foreach (DistributedTransactionOperation operation in group)
                {
                    operation.CollectionResourceId = containerResourceId;
                    operation.DatabaseResourceId = databaseResourceId;

                    if (!string.IsNullOrEmpty(operation.SessionToken))
                    {
                        continue; // User explicitly provided a token — don't override.
                    }

                    if (concreteSessionContainer == null)
                    {
                        continue; // No session container or not Session consistency.
                    }

                    string resolvedToken = DistributedTransactionCommitterUtils.ResolvePartitionLocalToken(
                        concreteSessionContainer,
                        collectionPath,
                        containerProperties,
                        routingMap,
                        operation.PartitionKey);

                    if (!string.IsNullOrEmpty(resolvedToken))
                    {
                        operation.SessionToken = resolvedToken;
                    }
                }
            }

            // Return the resolved consistency so the caller (CommitTransactionAsync) can reuse it
            // for the post-commit token merge without re-resolving account consistency.
            return isSessionConsistency;
        }

        /// <summary>
        /// Resolves the partition-local session token for a specific operation's partition key,
        /// mirroring GatewayStoreModel.TryResolveSessionTokenAsync.
        /// </summary>
        /// <remarks>
        /// Returns the resolved partition's per-partition token (or null if that partition has no
        /// token yet, in which case none is applied). The compound collection-wide token is used only
        /// when the partition cannot be resolved at all; substituting it after a partition was
        /// resolved would stamp other partitions' tokens onto this operation.
        /// </remarks>
        private static string ResolvePartitionLocalToken(
            SessionContainer sessionContainer,
            string collectionPath,
            ContainerProperties containerProperties,
            Routing.CollectionRoutingMap routingMap,
            PartitionKey partitionKey)
        {
            if (routingMap != null)
            {
                try
                {
                    PartitionKeyInternal partitionKeyInternal = partitionKey.InternalKey;
                    string effectivePartitionKey = partitionKeyInternal.GetEffectivePartitionKeyString(containerProperties.PartitionKey);
                    PartitionKeyRange range = routingMap.GetRangeByEffectivePartitionKey(effectivePartitionKey);

                    if (range != null)
                    {
                        // Partition resolved: return its per-partition token (may be null → no token
                        // applied). Do not substitute the compound token here. For a freshly-split
                        // child with no token, range.Parents lets it inherit the parent's progress.
                        return sessionContainer.GetSessionTokenForPartitionKeyRange(collectionPath, range.Id, range.Parents);
                    }
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    // Routing resolution failed (e.g., stale cache, partial/None partition key).
                    // Fall through to the compound-token fallback below.
                    DefaultTrace.TraceWarning(
                        "DistributedTransaction per-partition session-token resolution failed for collection '{0}'; " +
                        "falling back to the compound token. Exception: {1}",
                        collectionPath,
                        ex.Message);
                }
            }

            // Fallback: the operation's partition could not be resolved (routing map unavailable or
            // range not found). Use the compound collection-wide token; the coordinator extracts the
            // relevant per-partition token from the partition key carried in each op's request body.
            return sessionContainer.GetSessionToken(collectionPath);
        }

        /// <summary>
        /// Determines whether the effective consistency level is Session.
        /// Effective = client override ?? account default.
        /// </summary>
        internal static async Task<bool> IsEffectiveSessionConsistencyAsync(CosmosClientContext clientContext)
        {
            ConsistencyLevel? clientOverride = clientContext.ClientOptions?.ConsistencyLevel;
            if (clientOverride.HasValue)
            {
                return clientOverride.Value == ConsistencyLevel.Session;
            }

            // Fall back to account-level consistency. If the client is not available
            // (e.g., in unit tests with minimal mocks), default to Session (safest:
            // ensures session-token bookkeeping is active).
            CosmosClient client = clientContext.Client;
            if (client == null)
            {
                return true;
            }

            ConsistencyLevel accountLevel = await client.GetAccountConsistencyLevelAsync();
            return accountLevel == ConsistencyLevel.Session;
        }
    }
}
