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
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Session token policy for distributed transactions: validating caller-supplied tokens, resolving
    /// cached tokens for eligible operations, and recording tokens from coordinator responses.
    /// </summary>
    internal static class DistributedTransactionSessionTokenHelper
    {
        /// <summary>
        /// Resolves the account's default consistency level for automatic session-token resolution.
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
        /// Resolves ranges and cached session tokens for operations without explicit tokens when the account
        /// uses Session consistency.
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
            if (documentClient == null
                || accountConsistencyLevel != ConsistencyLevel.Session
                || operations.All(operation => !string.IsNullOrEmpty(operation.SessionToken)))
            {
                return null;
            }

            SessionContainer sessionContainer = documentClient.sessionContainer as SessionContainer;
            PartitionKeyRangeCache partitionKeyRangeCache = await documentClient.GetPartitionKeyRangeCacheAsync(effectiveTrace);
            if (partitionKeyRangeCache == null)
            {
                return null;
            }

            IEnumerable<IGrouping<string, DistributedTransactionOperation>> groupedOperations = operations
                .GroupBy(op => DistributedTransactionConstants.GetCollectionFullName(op.Database, op.Container));

            foreach (IGrouping<string, DistributedTransactionOperation> group in groupedOperations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string collectionPath = group.Key;
                ContainerProperties containerProperties = containerPropertiesByCollection[collectionPath];

                foreach (DistributedTransactionOperation operation in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await DistributedTransactionSessionTokenHelper.ResolveOperationSessionTokenAsync(
                        operation,
                        containerProperties,
                        sessionContainer,
                        partitionKeyRangeCache,
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

            List<(int operationIndex, string collectionResourceId, string collectionFullName, string sessionToken)> tokensToMerge =
                new List<(int operationIndex, string collectionResourceId, string collectionFullName, string sessionToken)>();
            List<string> validationFailures = new List<string>();
            HashSet<(string CollectionResourceId, string PartitionKeyRangeId)> refreshedRanges = new HashSet<(string, string)>();

            for (int i = 0; i < response.Count; i++)
            {
                DistributedTransactionOperationResult result = response[i];

                string collectionFullName = null;
                string malformedTokenDetails = null;

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
                        tokensToMerge.Add((
                            result.Index,
                            operation.CollectionResourceId,
                            collectionFullName,
                            result.SessionToken));
                    }
                    else
                    {
                        malformedTokenDetails = $"{validationFailure} Token: '{DistributedTransactionCommitter.FormatForLog(result.SessionToken)}'.";
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    string message = DistributedTransactionSessionTokenHelper.TraceSessionTokenFailure(
                        result.Index,
                        collectionFullName,
                        ex.Message);

                    throw new InvalidOperationException(message, ex);
                }

                if (malformedTokenDetails != null)
                {
                    validationFailures.Add(DistributedTransactionSessionTokenHelper.TraceSessionTokenFailure(
                        result.Index,
                        collectionFullName,
                        malformedTokenDetails));
                }
            }

            if (validationFailures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Session tokens could not be recorded because one or more tokens were malformed: {string.Join(" ", validationFailures)}");
            }

            RequestNameValueCollection headers = new RequestNameValueCollection();

            foreach ((int operationIndex, string collectionResourceId, string collectionFullName, string sessionToken) token in tokensToMerge)
            {
                try
                {
                    headers.Clear();
                    headers[HttpConstants.HttpHeaders.SessionToken] = token.sessionToken;

                    sessionContainer.SetSessionToken(
                        token.collectionResourceId,
                        token.collectionFullName,
                        headers);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    string message = DistributedTransactionSessionTokenHelper.TraceSessionTokenFailure(
                        token.operationIndex,
                        token.collectionFullName,
                        ex.Message);

                    throw new InvalidOperationException(message, ex);
                }
            }
        }

        private static string TraceSessionTokenFailure(
            int operationIndex,
            string collectionFullName,
            string failureDetails)
        {
            string collectionScope = collectionFullName == null
                ? string.Empty
                : $" for collection '{collectionFullName}'";

            string message = $"Session token for operation index {operationIndex} could not be recorded{collectionScope}: {failureDetails}";

            // Keep server-supplied braces out of the format string.
            DefaultTrace.TraceWarning("{0} Session token was not recorded.", DistributedTransactionCommitter.FormatForLog(message));
            return message;
        }

        /// <summary>
        /// Refreshes the routing cache when the partition that served an operation is not the one the
        /// client routed it to.
        /// </summary>
        /// <remarks>
        /// Detects a split or move that happened between routing resolution and the server commit, which
        /// the transaction path could not previously see because it recorded no client-side range. Runs on
        /// each distinct collection and serving range the status filter admitted for operations with a
        /// recorded range. Operations without automatic-token routing, including caller-supplied tokens
        /// and non-Session accounts, record no range and do not trigger this refresh.
        /// <para>
        /// Failures, including refresh cancellation, are traced and swallowed so optional cache maintenance
        /// cannot hide the coordinator response or prevent session-token capture. No caller cancellation
        /// token is passed to this post-response refresh; the routing cache may remain stale on failure.
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
            catch (Exception ex)
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
            SessionContainer sessionContainer,
            PartitionKeyRangeCache partitionKeyRangeCache,
            ITrace trace)
        {
            if (!string.IsNullOrEmpty(operation.SessionToken))
            {
                // The caller is explicitly controlling the session, matching the early return in
                // GatewayStoreModel.ApplySessionTokenAsync. Pre-flight validation already ran.
                return;
            }

            PartitionKeyRange range = await DistributedTransactionSessionTokenHelper.TryResolvePartitionKeyRangeAsync(
                operation,
                containerProperties,
                partitionKeyRangeCache,
                trace);
            operation.ResolvedPartitionKeyRangeId = range?.Id;

            operation.ResolvedSessionToken = sessionContainer?.ResolvePartitionLocalSessionTokenForGateway(
                operation.CollectionResourceId, range);
        }

        private static async Task<PartitionKeyRange> TryResolvePartitionKeyRangeAsync(
            DistributedTransactionOperation operation,
            ContainerProperties containerProperties,
            PartitionKeyRangeCache partitionKeyRangeCache,
            ITrace trace)
        {
            if ((operation.PartitionKey.IsNone
                    && containerProperties.PartitionKey?.Paths.Count > 0
                    && containerProperties.PartitionKey.IsSystemKey != true)
                || operation.PartitionKey.InternalKey == null
                || containerProperties.PartitionKey == null)
            {
                return null;
            }

            PartitionKeyInternal partitionKey = operation.PartitionKey.IsNone
                ? containerProperties.GetNoneValue()
                : operation.PartitionKey.InternalKey;
            string effectivePartitionKey = partitionKey.GetEffectivePartitionKeyString(
                containerProperties.PartitionKey);

            PartitionKeyRange range;
            try
            {
                range = await partitionKeyRangeCache.TryGetRangeByEffectivePartitionKeyAsync(
                    containerProperties.ResourceId,
                    effectivePartitionKey,
                    trace);
            }
            catch (DocumentClientException ex)
            {
                throw CosmosExceptionFactory.Create(ex, trace);
            }

            // A failed required lookup must not silently discard the operation's cached session progress.
            return range ?? throw CosmosExceptionFactory.CreateNotFoundException(
                message: $"Could not resolve a partition key range for distributed transaction operation {operation.OperationIndex}.",
                headers: null,
                trace: trace);
        }

        /// <summary>
        /// Determines whether a session token is usable: it must have one numeric partition key range id
        /// and a vector token accepted by the current protocol's shared session-token parser.
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

            if (!SessionTokenHelper.TryParse(sessionToken, out ISessionToken parsedToken))
            {
                failureReason = "the token could not be parsed.";
                return false;
            }

            if (parsedToken is not VectorSessionToken)
            {
                failureReason = "the token payload is not valid for the current protocol version.";
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
