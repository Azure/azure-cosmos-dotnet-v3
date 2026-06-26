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
        // the budget is the binding constraint (~4-5 retries) rather than the attempt-count cap (10).
        // Mirrors ResourceThrottleRetryPolicy's cumulative cap pattern. Overridable via the internal
        // constructor for tests that need to exercise the attempt-count cap with realistic delays.
        internal static readonly TimeSpan MaxCumulativeRetryDelay = TimeSpan.FromSeconds(30);
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

                // Resolve effective consistency before the commit and reuse it for the post-commit
                // token merge. Session-token work only applies under Session consistency. Resolving
                // here means a transient consistency-lookup failure surfaces before the commit rather
                // than failing an already-committed transaction.
                bool isSessionConsistency = await DistributedTransactionCommitterUtils.ResolveCollectionRidsAsync(
                    this.operations,
                    this.clientContext,
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

                        // Merge per-op session tokens into the SessionContainer (Session consistency
                        // only). On success the response is returned to the caller and not disposed
                        // here — it owns no unmanaged resources (the body is a MemoryStream). On a
                        // malformed token MergeSessionTokens throws and the response is discarded,
                        // matching the SDK's point-operation pattern.
                        DistributedTransactionCommitter.MergeSessionTokens(
                            response,
                            serverRequest,
                            this.clientContext.DocumentClient?.sessionContainer,
                            isSessionConsistency);

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

        internal static void MergeSessionTokens(
            DistributedTransactionResponse response,
            DistributedTransactionServerRequest serverRequest,
            ISessionContainer sessionContainer,
            bool isSessionConsistency)
        {
            // Merge each valid per-op token into the SessionContainer. Under Session consistency on a
            // committed response, throw on the first malformed token; otherwise trace best-effort.
            if (response == null || response.Count == 0 || serverRequest == null || sessionContainer == null)
            {
                return;
            }

            bool throwOnMalformed = response.IsSuccessStatusCode && isSessionConsistency;

            RequestNameValueCollection headers = new RequestNameValueCollection();

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

                // Skip absent/whitespace tokens or unresolved collection ids.
                if (string.IsNullOrWhiteSpace(result.SessionToken) || string.IsNullOrEmpty(operation.CollectionResourceId))
                {
                    continue;
                }

                // Shape pre-check: reject tokens lacking a '{pkRangeId}:{lsn}' separator. Confirms a
                // non-empty segment on each side of the colon; full content is validated downstream.
                int colonIndex = result.SessionToken.IndexOf(':');
                if (colonIndex <= 0 || colonIndex == result.SessionToken.Length - 1)
                {
                    DistributedTransactionCommitter.ThrowOrTraceMalformedSessionToken(
                        throwOnMalformed,
                        $"Op {result.Index}: non-canonical session token '{DistributedTransactionCommitter.TruncateForLog(result.SessionToken)}' (expected '{{pkRangeId}}:{{lsn}}')",
                        response);
                    continue;
                }

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
                catch (Exception ex) when (DistributedTransactionCommitterUtils.IsMalformedSessionTokenException(ex))
                {
                    DistributedTransactionCommitter.ThrowOrTraceMalformedSessionToken(
                        throwOnMalformed,
                        $"Op {result.Index}: SetSessionToken rejected '{DistributedTransactionCommitter.TruncateForLog(result.SessionToken)}' ({ex.GetType().Name}: {ex.Message})",
                        response);
                }
            }
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
