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
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Session token policy for distributed transactions: validating caller-supplied tokens, assigning a
    /// partition-local token to each sub-operation, and recording the tokens the coordinator returns.
    /// </summary>
    internal static class DistributedTransactionSessionTokenHelper
    {
        /// <summary>
        /// Resolves the account's default consistency level, which point operations pass to
        /// <see cref="GatewayStoreModel.IsSessionTokenApplicable"/> as the default.
        /// </summary>
        public static async Task<ConsistencyLevel?> ResolveAccountConsistencyLevelAsync(CosmosClientContext clientContext)
        {
            DocumentClient documentClient = clientContext.DocumentClient;
            if (documentClient == null)
            {
                return null;
            }

            return await documentClient.GetDefaultConsistencyLevelAsync();
        }

        /// <summary>
        /// Rejects malformed caller-supplied session tokens before dispatch.
        /// </summary>
        public static void ValidateUserSuppliedSessionTokens(IReadOnlyList<DistributedTransactionOperation> operations)
        {
            foreach (DistributedTransactionOperation operation in operations)
            {
                string sessionToken = operation?.SessionToken;
                if (string.IsNullOrEmpty(sessionToken))
                {
                    continue;
                }

                if (DistributedTransactionSessionTokenHelper.TryValidateSessionToken(sessionToken, out string _))
                {
                    continue;
                }

                throw new ArgumentException(
                    $"Distributed transaction operation index {operation.OperationIndex} was given the session token " +
                    $"'{DistributedTransactionCommitter.FormatForLog(sessionToken)}', which must be a single valid " +
                    "'<partitionKeyRangeId>:<token>' pair. The transaction was not sent.",
                    nameof(DistributedTransactionRequestOptions.SessionToken));
            }
        }

        /// <summary>
        /// Resolves each operation's target partition key range and assigns the partition-local session
        /// token to the operations that warrant one.
        /// </summary>
        public static async Task<PartitionKeyRangeCache> ResolveOperationSessionTokensAsync(
            IReadOnlyList<DistributedTransactionOperation> operations,
            IReadOnlyDictionary<string, ContainerProperties> containerPropertiesByCollection,
            CosmosClientContext clientContext,
            ConsistencyLevel? accountConsistencyLevel,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ITrace effectiveTrace = trace ?? NoOpTrace.Singleton;
            DocumentClient documentClient = clientContext.DocumentClient;
            if (documentClient == null)
            {
                return null;
            }

            // Point operations carry the client override as a request header, which only reads may honor.
            string clientConsistencyLevel = clientContext.ClientOptions?.ConsistencyLevel?.ToString();

            // Matches GatewayStoreModel.TryResolveSessionTokenAsync, which also resolves against the
            // concrete container only and simply assigns no token otherwise.
            SessionContainer sessionContainer = documentClient.sessionContainer as SessionContainer;
            PartitionKeyRangeCache partitionKeyRangeCache = await documentClient.GetPartitionKeyRangeCacheAsync(effectiveTrace);
            if (partitionKeyRangeCache == null)
            {
                return null;
            }

            bool isMultiMasterEnabled = documentClient.CanUseMultipleWriteLocations();

            IEnumerable<IGrouping<string, DistributedTransactionOperation>> groupedOperations = operations
                .GroupBy(op => DistributedTransactionConstants.GetCollectionFullName(op.Database, op.Container));

            foreach (IGrouping<string, DistributedTransactionOperation> group in groupedOperations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string collectionPath = group.Key;
                ContainerProperties containerProperties = containerPropertiesByCollection[collectionPath];

                // Read once per collection: every operation in the group narrows the same compound token.
                string collectionSessionToken = sessionContainer?.GetSessionToken(collectionPath);

                foreach (DistributedTransactionOperation operation in group)
                {
                    await DistributedTransactionSessionTokenHelper.ResolveOperationSessionTokenAsync(
                        operation,
                        containerProperties,
                        collectionSessionToken,
                        partitionKeyRangeCache,
                        accountConsistencyLevel,
                        clientConsistencyLevel,
                        isMultiMasterEnabled,
                        effectiveTrace);
                }
            }

            return partitionKeyRangeCache;
        }

        /// <summary>
        /// Records the session tokens the coordinator returned for each sub-operation.
        /// </summary>
        public static async Task MergeSessionTokensAsync(
            DistributedTransactionResponse response,
            DistributedTransactionServerRequest serverRequest,
            ISessionContainer sessionContainer,
            PartitionKeyRangeCache partitionKeyRangeCache,
            ITrace trace)
        {
            // Mirror the pattern used by GatewayStoreModel.CaptureSessionTokenAndHandleSplitAsync.
            // after a response is received, store each operation's session token in the SessionContainer
            // so that subsequent Session-consistency reads on the affected collections can use the latest token
            // without getting ReadSessionNotAvailable.
            //
            // DTC spans multiple collections so the server embeds per-operation session tokens in the JSON body.
            // Capture is gated per sub-operation on the same statuses point operations capture on.
            if (response == null || response.Count == 0 || serverRequest == null || sessionContainer == null)
            {
                return;
            }

            RequestNameValueCollection headers = new RequestNameValueCollection();
            HashSet<(string CollectionResourceId, string PartitionKeyRangeId)> refreshedRanges = new HashSet<(string, string)>();

            for (int i = 0; i < response.Count; i++)
            {
                DistributedTransactionOperationResult result = response[i];

                string collectionFullName = null;
                string failureReason = null;
                Exception failureCause = null;

                try
                {
                    DistributedTransactionOperation operation = serverRequest.Operations[result.Index];

                    if (string.IsNullOrEmpty(operation.CollectionResourceId))
                    {
                        continue;
                    }

                    // Gated per sub-operation rather than on the envelope: status promotion only happens
                    // for MultiStatus, so a partially failed transaction carries meaningful individual
                    // statuses. Every status below 400 is captured, which is what the gateway does: it
                    // reaches its capture call for any such response, 304 included. A rolled back
                    // FailedDependency sub-operation left no durable write to read back.
                    if ((int)result.StatusCode >= (int)StatusCodes.StartingErrorCode
                        && !GatewayStoreModel.IsSessionTokenCapturableErrorStatus(result.StatusCode, result.SubStatusCode))
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(operation.ResolvedPartitionKeyRangeId)
                        && !string.IsNullOrWhiteSpace(result.PartitionKeyRangeId)
                        && !string.Equals(operation.ResolvedPartitionKeyRangeId, result.PartitionKeyRangeId, StringComparison.OrdinalIgnoreCase)
                        && refreshedRanges.Add((operation.CollectionResourceId, result.PartitionKeyRangeId)))
                    {
                        await DistributedTransactionSessionTokenHelper.RefreshRoutingCacheIfPartitionMovedAsync(
                            operation,
                            result,
                            partitionKeyRangeCache,
                            trace);
                    }

                    if (string.IsNullOrEmpty(result.SessionToken))
                    {
                        continue;
                    }

                    collectionFullName = DistributedTransactionConstants.GetCollectionFullName(operation.Database, operation.Container);

                    if (DistributedTransactionSessionTokenHelper.TryValidateSessionToken(result.SessionToken, out string validationFailure))
                    {
                        headers.Clear();
                        headers[HttpConstants.HttpHeaders.SessionToken] = result.SessionToken;

                        sessionContainer.SetSessionToken(
                            operation.CollectionResourceId,
                            collectionFullName,
                            headers);
                    }
                    else
                    {
                        failureReason = $"{validationFailure} Token: '{DistributedTransactionCommitter.FormatForLog(result.SessionToken)}'.";
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failureCause = ex;
                    failureReason = ex.Message;
                }

                if (failureReason == null)
                {
                    continue;
                }

                string collectionScope = collectionFullName == null
                    ? string.Empty
                    : $" for collection '{collectionFullName}'";

                string message = $"Session token for operation index {result.Index} could not be recorded{collectionScope}: {failureReason}";

                // Keep server-supplied braces out of the format string.
                DefaultTrace.TraceWarning("{0} Session token was not recorded.", DistributedTransactionCommitter.FormatForLog(message));

                // Stop at the first failure; later tokens are intentionally not recorded.
                throw new InvalidOperationException(message, failureCause);
            }
        }

        /// <summary>
        /// Refreshes the routing cache when the partition that served an operation is not the one the
        /// client routed it to.
        /// </summary>
        /// <remarks>
        /// Detects a split or move that happened between routing resolution and the server commit, which
        /// the transaction path could not previously see because it recorded no client-side range. Runs on
        /// each distinct collection and serving range the status filter admitted, including operations the
        /// send gate withheld a session token from, since the recorded range alone can detect the move.
        /// <para>
        /// Failures are traced and swallowed. This runs after the transaction has already committed, so a
        /// stale cache costs one extra round trip on a later request while throwing would report a
        /// committed transaction as failed. Pre-flight resolution and post-commit bookkeeping are
        /// deliberately asymmetric for that reason.
        /// </para>
        /// </remarks>
        private static async Task RefreshRoutingCacheIfPartitionMovedAsync(
            DistributedTransactionOperation operation,
            DistributedTransactionOperationResult result,
            PartitionKeyRangeCache partitionKeyRangeCache,
            ITrace trace)
        {
            try
            {
                await PartitionKeyRangeCache.RefreshRoutingCacheIfPartitionMovedAsync(
                    partitionKeyRangeCache,
                    operation.CollectionResourceId,
                    operation.ResolvedPartitionKeyRangeId,
                    result.PartitionKeyRangeId,
                    trace ?? NoOpTrace.Singleton);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DistributedTransactionSessionTokenHelper.TraceToleratedFailure(
                    $"Distributed transaction operation index {result.Index}: the routing cache could not be refreshed after the partition moved " +
                    $"from '{operation.ResolvedPartitionKeyRangeId}' to '{result.PartitionKeyRangeId}'.",
                    ex);
            }
        }

        private static async Task ResolveOperationSessionTokenAsync(
            DistributedTransactionOperation operation,
            ContainerProperties containerProperties,
            string collectionSessionToken,
            PartitionKeyRangeCache partitionKeyRangeCache,
            ConsistencyLevel? accountConsistencyLevel,
            string clientConsistencyLevel,
            bool isMultiMasterEnabled,
            ITrace trace)
        {
            // Recorded before the gate runs so that comparing it against the range the server reports back
            // still detects a split or move for an operation the gate withheld a token from.
            operation.ResolvedPartitionKeyRangeId = await DistributedTransactionSessionTokenHelper.TryResolvePartitionKeyRangeIdAsync(
                operation,
                containerProperties,
                partitionKeyRangeCache,
                trace);

            if (!string.IsNullOrEmpty(operation.SessionToken))
            {
                // The caller is explicitly controlling the session, matching the early return in
                // GatewayStoreModel.ApplySessionTokenAsync. Pre-flight validation already ran.
                return;
            }

            // A write against a single-master account is served only by the write region, so it carries no
            // session token; reads and multi-master writes still do. Same policy point operations apply.
            if (accountConsistencyLevel == null
                || !GatewayStoreModel.IsSessionTokenApplicable(
                    defaultConsistencyLevel: accountConsistencyLevel.Value,
                    requestConsistencyLevel: clientConsistencyLevel,
                    isReadOrBatchRequest: OperationTypeExtensions.IsReadOperation(operation.OperationType),
                    isMultiMasterEnabled: isMultiMasterEnabled))
            {
                return;
            }

            operation.SessionToken = DistributedTransactionSessionTokenHelper.TryFindPartitionLocalSessionToken(
                collectionSessionToken,
                operation.ResolvedPartitionKeyRangeId);
        }

        /// <summary>
        /// Returns the <c>partitionKeyRangeId:token</c> segment recorded for the requested range, or null
        /// when the token holds no segment for it.
        /// </summary>
        /// <remarks>
        /// Deliberately an exact match, with no walk to parent ranges. A point operation re-labels an
        /// ancestor's vector under the child range id, which is safe there because the same client resolved
        /// both. The coordinator instead drops a token whose prefix it cannot match, so a re-labelled
        /// ancestor token would claim progress against a range that never reported it. Sending no token
        /// immediately after a split costs one transaction's worth of session guarantee and self-heals as
        /// soon as a transaction commits against the child range.
        /// </remarks>
        internal static string TryFindPartitionLocalSessionToken(string collectionSessionToken, string partitionKeyRangeId)
        {
            if (string.IsNullOrEmpty(partitionKeyRangeId)
                || !SessionTokenHelper.TryFindPartitionLocalSessionToken(collectionSessionToken, partitionKeyRangeId, out string sessionToken))
            {
                return null;
            }

            return partitionKeyRangeId + ":" + sessionToken;
        }

        private static async Task<string> TryResolvePartitionKeyRangeIdAsync(
            DistributedTransactionOperation operation,
            ContainerProperties containerProperties,
            PartitionKeyRangeCache partitionKeyRangeCache,
            ITrace trace)
        {
            if ((operation.PartitionKey.IsNone && containerProperties.PartitionKey?.Paths.Count > 0)
                || operation.PartitionKey.InternalKey == null
                || containerProperties.PartitionKey == null)
            {
                return null;
            }

            // Lookup failures propagate, as in GatewayStoreModel.TryResolveSessionTokenAsync: sending the
            // transaction without the operation's cached token would weaken session consistency.
            PartitionKeyInternal partitionKey = operation.PartitionKey.IsNone
                ? containerProperties.GetNoneValue()
                : operation.PartitionKey.InternalKey;
            string effectivePartitionKey = partitionKey.GetEffectivePartitionKeyString(
                containerProperties.PartitionKey);

            PartitionKeyRange range = await partitionKeyRangeCache.TryGetRangeByEffectivePartitionKeyAsync(
                containerProperties.ResourceId,
                effectivePartitionKey,
                trace);

            return range?.Id;
        }

        /// <summary>
        /// Determines whether a session token is usable: it must have one numeric partition key range id
        /// and a simple or vector token accepted by the shared session-token parser.
        /// </summary>
        /// <param name="sessionToken">The token reported for a single operation.</param>
        /// <param name="failureReason">The reason the token is unusable, or <c>null</c> when it is usable.</param>
        private static bool TryValidateSessionToken(string sessionToken, out string failureReason)
        {
            int colonIndex = sessionToken.IndexOf(':');
            if (colonIndex <= 0)
            {
                failureReason = "the token is missing the partitionKeyRangeId prefix.";
                return false;
            }

            if (sessionToken.IndexOf(',') >= 0
                || sessionToken.IndexOf(':', colonIndex + 1) >= 0)
            {
                failureReason = "the token must contain one partition-local pair.";
                return false;
            }

            string partitionKeyRangeId = sessionToken.Substring(0, colonIndex);
            if (!DistributedTransactionSessionTokenHelper.IsValidPartitionKeyRangeId(partitionKeyRangeId))
            {
                failureReason = "the partitionKeyRangeId prefix is invalid.";
                return false;
            }

            // The shared parser traces malformed values before returning false.
            if (sessionToken.IndexOf('\r') >= 0 || sessionToken.IndexOf('\n') >= 0)
            {
                failureReason = "the token contains an invalid line break.";
                return false;
            }

            if (!SessionTokenHelper.TryParse(sessionToken, out ISessionToken _))
            {
                failureReason = "the token could not be parsed.";
                return false;
            }

            failureReason = null;
            return true;
        }

        private static bool IsValidPartitionKeyRangeId(string value)
        {
            int index = 0;
            while (index < value.Length && value[index] == ' ')
            {
                index++;
            }

            if (index == value.Length)
            {
                return false;
            }

            int parsedValue = 0;
            for (; index < value.Length; index++)
            {
                int digit = value[index] - '0';
                if (digit < 0
                    || digit > 9
                    || parsedValue > (int.MaxValue - digit) / 10)
                {
                    return false;
                }

                parsedValue = (parsedValue * 10) + digit;
            }

            return true;
        }

        private static void TraceToleratedFailure(string context, Exception exception)
        {
            // Exception text can echo server or caller data, so it is bounded and kept out of the format string.
            DefaultTrace.TraceWarning(
                "{0}",
                DistributedTransactionCommitter.FormatForLog($"{context} [{exception.GetType().Name}] {exception.Message}"));
        }
    }
}
