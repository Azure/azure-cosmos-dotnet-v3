// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class DistributedTransactionCommitter
    {
        // Outer-loop retry parameters. The inner loop (ClientRetryPolicy) handles envelope failures with empty body;
        // the outer loop handles body-bearing semantic failures whose JSON body sets isRetriable: true.
        //
        // Hard ceiling on outer-loop wire requests. With non-trivial retryBaseDelay the cumulative
        // MaxCumulativeRetryDelay budget will typically fire first; this cap only binds when delays
        // are very small (e.g., zero in tests or hypothetical fast-server scenarios) — it guards
        // against unbounded wire-request amplification when delays are degenerate.
        internal const int MaxIsRetriableRetryCount = 10;
        // Default cumulative planned-delay budget. With default 1s base and maxExponent=5 (±25% jitter),
        // the budget is the binding constraint (~7-8 retries) rather than the attempt-count cap (10).
        // Mirrors ResourceThrottleRetryPolicy's cumulative cap pattern. Overridable via the internal
        // constructor for tests that need to exercise the attempt-count cap with realistic delays.
        internal static readonly TimeSpan MaxCumulativeRetryDelay = TimeSpan.FromSeconds(120);
        private const int RetryMaxExponent = 5; // ~32 s max base delay before jitter
        private static readonly TimeSpan DefaultRetryBaseDelay = TimeSpan.FromSeconds(1);
        private static readonly string ResourceUri = Paths.OperationsPathSegment + "/" + Paths.Operations_Dtc;

        private readonly IReadOnlyList<DistributedTransactionOperation> operations;
        private readonly CosmosClientContext clientContext;
        private readonly OperationType operationType;
        private readonly TimeSpan retryBaseDelay;
        private readonly TimeSpan maxCumulativeRetryDelay;
        private readonly Func<TimeSpan, CancellationToken, Task> delayProvider;

        public DistributedTransactionCommitter(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            OperationType operationType)
            : this(operations, clientContext, operationType, DistributedTransactionCommitter.DefaultRetryBaseDelay)
        {
        }

        internal DistributedTransactionCommitter(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            OperationType operationType,
            TimeSpan retryBaseDelay,
            Func<TimeSpan, CancellationToken, Task> delayProvider = null,
            TimeSpan? maxCumulativeRetryDelay = null)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.operationType = operationType;
            this.retryBaseDelay = retryBaseDelay;
            this.delayProvider = delayProvider ?? Task.Delay;
            this.maxCumulativeRetryDelay = maxCumulativeRetryDelay ?? DistributedTransactionCommitter.MaxCumulativeRetryDelay;
        }

        public async Task<DistributedTransactionResponse> CommitTransactionAsync(
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (this.operations.Count == 0)
            {
                throw new InvalidOperationException("Cannot commit a distributed transaction with zero operations. Add at least one operation before committing.");
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Resolve effective consistency once, before the commit: session-token work only applies
                // under Session, and resolving here surfaces a lookup failure before committing, not after.
                bool isSessionConsistency = await DistributedTransactionCommitter.IsEffectiveSessionConsistencyAsync(
                    this.clientContext);

                await DistributedTransactionCommitterUtils.PrepareOperationsAsync(
                    this.operations,
                    this.clientContext,
                    isSessionConsistency,
                    cancellationToken);

                DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                    this.operations,
                    this.clientContext.SerializerCore,
                    cancellationToken);

                return await this.ExecuteCommitWithRetryAsync(serverRequest, isSessionConsistency, trace, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DefaultTrace.TraceError($"Distributed transaction failed: {ex.Message}");
                throw;
            }
        }

        private async Task<DistributedTransactionResponse> ExecuteCommitWithRetryAsync(
            DistributedTransactionServerRequest serverRequest,
            bool isSessionConsistency,
            ITrace parentTrace,
            CancellationToken cancellationToken)
        {
            // Allocate once; the underlying parentTrace tree continues to accumulate per-attempt children.
            CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(parentTrace);

            int attempt = 0;
            TimeSpan cumulativeRetryDelay = TimeSpan.Zero;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DistributedTransactionResponse response = await this.ExecuteCommitAsync(serverRequest, isSessionConsistency, parentTrace, cancellationToken);

                if (response.IsSuccessStatusCode || !response.IsRetriable)
                {
                    response.Diagnostics = diagnostics;
                    return response;
                }

                if (attempt >= DistributedTransactionCommitter.MaxIsRetriableRetryCount)
                {
                    DefaultTrace.TraceWarning(
                        $"Distributed transaction isRetriable retry budget exhausted after {attempt} attempts " +
                            $"(StatusCode={response.StatusCode}, DiagnosticString={TruncateForLog(response.DiagnosticString)}). Returning last response.");
                    response.Diagnostics = diagnostics;
                    return response;
                }

                // Use the maximum of the server hint and the locally-computed exponential backoff
                // to avoid retrying sooner than the server requested.
                TimeSpan computedDelay = DistributedTransactionRetryHelpers.ComputeBackoff(
                    attempt,
                    this.retryBaseDelay,
                    TimeSpan.MaxValue,
                    DistributedTransactionCommitter.RetryMaxExponent);

                TimeSpan delay = response.Headers?.RetryAfter is TimeSpan serverHint && serverHint > computedDelay
                    ? serverHint
                    : computedDelay;

                // Check cumulative delay budget before sleeping. If the next delay would
                // exceed the budget, stop retrying — mirroring ResourceThrottleRetryPolicy.
                cumulativeRetryDelay += delay;
                if (cumulativeRetryDelay > this.maxCumulativeRetryDelay)
                {
                    DefaultTrace.TraceWarning(
                        $"Distributed transaction isRetriable cumulative delay budget exceeded " +
                            $"(cumulativeDelayMs={(int)cumulativeRetryDelay.TotalMilliseconds}, " +
                            $"maxDelayMs={(int)this.maxCumulativeRetryDelay.TotalMilliseconds}, " +
                            $"attempt={attempt}, StatusCode={response.StatusCode}, " +
                            $"DiagnosticString={TruncateForLog(response.DiagnosticString)}). Returning last response.");
                    response.Diagnostics = diagnostics;
                    return response;
                }

                DefaultTrace.TraceWarning(
                    $"Distributed transaction commit retriable (StatusCode={response.StatusCode}, IsRetriable={response.IsRetriable}, DiagnosticString={TruncateForLog(response.DiagnosticString)}, attempt={attempt}, delayMs={(int)delay.TotalMilliseconds}, cumulativeDelayMs={(int)cumulativeRetryDelay.TotalMilliseconds}). Retrying with idempotency token {serverRequest.IdempotencyToken}.");

                response.Dispose();
                attempt++;
                await this.delayProvider(delay, cancellationToken);
            }
        }

        // Caps server-controlled diagnostic strings before they enter SDK trace logs to prevent
        // log bloat and avoid newline-driven log-line interleaving.
        private static string TruncateForLog(string value)
        {
            const int MaxLogLength = 256;
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= MaxLogLength
                ? value
                : value.Substring(0, MaxLogLength) + "...[truncated]";
        }

        private async Task<DistributedTransactionResponse> ExecuteCommitAsync(
            DistributedTransactionServerRequest serverRequest,
            bool isSessionConsistency,
            ITrace parentTrace,
            CancellationToken cancellationToken)
        {
            using (ITrace attemptTrace = parentTrace.StartChild("Execute Distributed Transaction Commit", TraceComponent.Batch, TraceLevel.Info))
            {
                using (MemoryStream bodyStream = serverRequest.CreateBodyStream())
                {
                    ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                        resourceUri: DistributedTransactionCommitter.ResourceUri,
                        resourceType: ResourceType.DistributedTransactionBatch,
                        operationType: this.operationType,
                        requestOptions: null,
                        cosmosContainerCore: null,
                        partitionKey: null,
                        itemId: null,
                        streamPayload: bodyStream,
                        requestEnricher: requestMessage => DistributedTransactionCommitter.EnrichRequestMessage(requestMessage, serverRequest),
                        trace: attemptTrace,
                        cancellationToken: cancellationToken);

                    using (responseMessage)
                    {
                        DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                            responseMessage,
                            serverRequest,
                            this.clientContext.SerializerCore,
                            attemptTrace,
                            cancellationToken);

                        // Merge per-op session tokens into the SessionContainer (Session consistency only).
                        // The response is returned undisposed (its body is a MemoryStream); a malformed token
                        // throws and discards it, matching the point-op pattern.
                        DocumentClient documentClient = this.clientContext.DocumentClient;

                        // Acquire the routing cache for capture-side split detection. Best-effort: unlike the
                        // force-refresh (which propagates), a failure here just skips split detection this commit.
                        Routing.PartitionKeyRangeCache partitionKeyRangeCache = null;
                        if (isSessionConsistency && documentClient != null)
                        {
                            try
                            {
                                partitionKeyRangeCache = await documentClient.GetPartitionKeyRangeCacheAsync(attemptTrace);
                            }
                            catch (Exception ex) when (!(ex is OperationCanceledException))
                            {
                                DefaultTrace.TraceWarning(
                                    "DistributedTransaction could not obtain PartitionKeyRangeCache for capture-side " +
                                    "split detection; skipping it for this commit. Exception: {0}",
                                    ex.Message);
                            }
                        }

                        await DistributedTransactionCommitter.MergeSessionTokensAsync(
                            response,
                            serverRequest,
                            documentClient?.sessionContainer,
                            isSessionConsistency,
                            partitionKeyRangeCache,
                            attemptTrace);

                        return response;
                    }
                }
            }
        }

        private static void EnrichRequestMessage(RequestMessage requestMessage, DistributedTransactionServerRequest serverRequest)
        {
            // Set DTC-specific headers
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IdempotencyToken, serverRequest.IdempotencyToken.ToString());
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.OperationType, requestMessage.OperationType.ToOperationTypeString());
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ResourceType, requestMessage.ResourceType.ToResourceTypeString());
            requestMessage.UseGatewayMode = true;
        }

        internal static async Task MergeSessionTokensAsync(
            DistributedTransactionResponse response,
            DistributedTransactionServerRequest serverRequest,
            ISessionContainer sessionContainer,
            bool isSessionConsistency,
            Routing.PartitionKeyRangeCache partitionKeyRangeCache,
            ITrace trace)
        {
            // Mirror GatewayStoreModel.CaptureSessionTokenAndHandleSplitAsync: store each op's session
            // token so later Session reads on the affected collections avoid ReadSessionNotAvailable.
            // DTX spans collections, so tokens arrive per-op in the JSON body. On a committed response
            // under Session consistency, throw on the first malformed token; otherwise trace best-effort.
            if (response == null || response.Count == 0 || serverRequest == null || sessionContainer == null)
            {
                return;
            }

            bool throwOnMalformed = response.IsSuccessStatusCode && isSessionConsistency;

            RequestNameValueCollection headers = new RequestNameValueCollection();

            // Dedupe force-refreshes so N ops moving to the same range trigger one cache refresh.
            HashSet<string> refreshedRanges = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < response.Count; i++)
            {
                DistributedTransactionOperationResult result = response[i];

                // Defensive crash-safety only; index correctness is owned upstream. Never throws.
                if (result.Index < 0 || result.Index >= serverRequest.Operations.Count)
                {
                    DefaultTrace.TraceWarning(
                        $"Distributed transaction response (StatusCode={response.StatusCode}): skipping result {i} " +
                        $"with out-of-range op Index {result.Index} (request has {serverRequest.Operations.Count} ops).");
                    continue;
                }

                DistributedTransactionOperation operation = serverRequest.Operations[result.Index];

                // Skip non-success sub-ops: a failed op (e.g. 404/1002 ReadSessionNotAvailable) may
                // carry a stale or malformed token that must not be merged or trigger the throw.
                if (!result.IsSuccessStatusCode)
                {
                    continue;
                }

                // Capture-side split/partition-move detection (mirrors CaptureSessionTokenAndHandleSplitAsync):
                // if the server served a different range than the client resolved, force-refresh the stale
                // routing cache once per moved range. Restores point-op recovery parity, since a committed
                // DTX is HTTP 207 and per-op 1002s never reach SessionTokenMismatchRetryPolicy.
                await DistributedTransactionCommitter.RefreshRoutingCacheIfPartitionMovedAsync(
                    operation,
                    result,
                    partitionKeyRangeCache,
                    refreshedRanges,
                    trace);

                // Skip absent/whitespace tokens or unresolved collection ids.
                if (string.IsNullOrWhiteSpace(result.SessionToken) || string.IsNullOrEmpty(operation.CollectionResourceId))
                {
                    continue;
                }

                // Shape pre-check delegated to SessionContainer so DTX shares the core
                // '{pkRangeId}:{lsn}' definition; content is validated by SetSessionToken below.
                if (!SessionContainer.IsCanonicalSessionTokenShape(result.SessionToken))
                {
                    DistributedTransactionCommitter.ThrowOrTraceMalformedSessionToken(
                        throwOnMalformed,
                        $"Op {result.Index}: non-canonical session token '{DistributedTransactionCommitter.TruncateForLog(result.SessionToken)}' (expected '{{pkRangeId}}:{{lsn}}')",
                        response);
                    continue;
                }

                // Each SetSessionToken takes a SessionContainer write lock; a future batch-update API
                // could reduce lock churn when many ops share a collection.
                headers.Clear();
                headers[HttpConstants.HttpHeaders.SessionToken] = result.SessionToken;

                try
                {
                    sessionContainer.SetSessionToken(
                        operation.CollectionResourceId,
                        DistributedTransactionConstants.GetCollectionFullName(operation.Database, operation.Container),
                        headers);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (SessionContainer.IsMalformedSessionTokenException(ex))
                {
                    DistributedTransactionCommitter.ThrowOrTraceMalformedSessionToken(
                        throwOnMalformed,
                        $"Op {result.Index}: SetSessionToken rejected '{DistributedTransactionCommitter.TruncateForLog(result.SessionToken)}' ({ex.GetType().Name}: {ex.Message})",
                        response);
                }
            }
        }

        /// <summary>
        /// Force-refreshes the partition routing cache when the server served a different partition key range
        /// than the client resolved for the operation, deduping so each distinct moved range refreshes once.
        /// Mirrors <see cref="GatewayStoreModel.CaptureSessionTokenAndHandleSplitAsync"/>.
        /// </summary>
        /// <remarks>
        /// Runs post-commit. A refresh failure is intentionally NOT swallowed: it propagates, matching
        /// point operations (the gateway force-refreshes with a bare await). Acquiring the cache is
        /// best-effort (the caller's concern); the refresh itself is not.
        /// </remarks>
        private static async Task RefreshRoutingCacheIfPartitionMovedAsync(
            DistributedTransactionOperation operation,
            DistributedTransactionOperationResult result,
            Routing.PartitionKeyRangeCache partitionKeyRangeCache,
            HashSet<string> refreshedRanges,
            ITrace trace)
        {
            if (partitionKeyRangeCache == null
                || string.IsNullOrEmpty(operation.CollectionResourceId)
                || string.IsNullOrEmpty(operation.ResolvedPartitionKeyRangeId)
                || string.IsNullOrEmpty(result.PartitionKeyRangeId)
                || result.PartitionKeyRangeId.Equals(operation.ResolvedPartitionKeyRangeId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!refreshedRanges.Add(operation.CollectionResourceId + "|" + result.PartitionKeyRangeId))
            {
                return; // Already refreshed this moved range on this commit.
            }

            await partitionKeyRangeCache.TryGetPartitionKeyRangeByIdAsync(
                operation.CollectionResourceId,
                result.PartitionKeyRangeId,
                trace ?? NoOpTrace.Singleton,
                forceRefresh: true);
        }

        /// <summary>
        /// Determines whether the effective consistency level is Session (client override ?? account default).
        /// Session-token bookkeeping only applies under Session consistency.
        /// </summary>
        /// <remarks>
        /// A per-request consistency override is not consulted: DistributedTransactionRequestOptions
        /// exposes none today. If one is added, thread it here and validate via
        /// ValidationHelpers.IsValidConsistencyLevelOverwrite (matching point operations).
        /// </remarks>
        private static async Task<bool> IsEffectiveSessionConsistencyAsync(CosmosClientContext clientContext)
        {
            ConsistencyLevel? clientOverride = clientContext.ClientOptions?.ConsistencyLevel;
            if (clientOverride.HasValue)
            {
                return clientOverride.Value == ConsistencyLevel.Session;
            }

            // Fall back to account consistency; default to Session when the client is unavailable
            // (e.g., minimal test mocks) so session-token bookkeeping stays active.
            CosmosClient client = clientContext.Client;
            if (client == null)
            {
                return true;
            }

            ConsistencyLevel accountLevel = await client.GetAccountConsistencyLevelAsync();
            return accountLevel == ConsistencyLevel.Session;
        }

        /// <summary>
        /// Throws a non-retriable <see cref="CosmosException"/> for the first malformed session token
        /// on a committed transaction under Session consistency; otherwise traces it.
        /// </summary>
        private static void ThrowOrTraceMalformedSessionToken(
            bool throwOnMalformed,
            string detail,
            DistributedTransactionResponse response)
        {
            if (!throwOnMalformed)
            {
                // Non-success response or non-Session consistency: informational, don't throw.
                DefaultTrace.TraceWarning(
                    $"Distributed transaction response (StatusCode={response.StatusCode}) contained a " +
                    $"malformed session token: {detail}");
                return;
            }

            // Transaction committed but session bookkeeping is incomplete; throw and discard the
            // response per the point-operation pattern. InternalServerError because the token is
            // server-originated. Retry idempotency is the caller's responsibility via IfMatch/ETag.
            throw new CosmosException(
                message:
                    $"Distributed transaction committed but a malformed session token was returned; " +
                    $"do not retry. {detail}",
                statusCode: HttpStatusCode.InternalServerError,
                subStatusCode: 0,
                activityId: response.ActivityId,
                requestCharge: response.RequestCharge);
        }
    }
}
