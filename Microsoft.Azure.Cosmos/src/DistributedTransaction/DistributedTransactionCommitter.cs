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
        // Default cap on outer-loop retries (retries only — the initial attempt is not counted, so the
        // loop dispatches at most MaxIsRetriableRetryCount + 1 wire requests). With non-trivial
        // retryBaseDelay the cumulative MaxCumulativeRetryDelay budget will typically fire first; this cap
        // only binds when delays are very small (e.g., zero in tests or hypothetical fast-server scenarios)
        // — it guards against unbounded wire-request amplification when delays are degenerate. Applied as
        // the default when CosmosClientOptions.MaxRetryAttemptsOnAbortedTransactions is unset.
        internal const int MaxIsRetriableRetryCount = 10;
        // Default cumulative planned-delay budget. With default 1s base and maxExponent=5 (±25% jitter),
        // the budget is the binding constraint (~4-5 retries) rather than the attempt-count cap (10).
        // Mirrors ResourceThrottleRetryPolicy's cumulative cap pattern. Applied as the default when
        // CosmosClientOptions.MaxRetryWaitTimeOnAbortedTransactions is unset; overridable via the internal
        // constructor for tests that need to exercise the attempt-count cap with realistic delays.
        internal static readonly TimeSpan MaxCumulativeRetryDelay = TimeSpan.FromSeconds(30);
        private const int RetryMaxExponent = 5; // ~32 s max base delay before jitter
        private static readonly TimeSpan DefaultRetryBaseDelay = TimeSpan.FromSeconds(1);
        private static readonly string ResourceUri = Paths.OperationsPathSegment + "/" + Paths.Operations_Dtc;

        private readonly IReadOnlyList<DistributedTransactionOperation> operations;
        private readonly CosmosClientContext clientContext;
        private readonly OperationType operationType;
        private readonly TimeSpan retryBaseDelay;
        private readonly int maxIsRetriableRetryCount;
        private readonly TimeSpan maxCumulativeRetryDelay;
        private readonly Func<TimeSpan, CancellationToken, Task> delayProvider;
        private readonly Action<Guid> onDispatch;

        public DistributedTransactionCommitter(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            OperationType operationType,
            Action<Guid> onDispatch = null)
            : this(operations, clientContext, operationType, DistributedTransactionCommitter.DefaultRetryBaseDelay, onDispatch: onDispatch)
        {
        }

        internal DistributedTransactionCommitter(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            OperationType operationType,
            TimeSpan retryBaseDelay,
            Func<TimeSpan, CancellationToken, Task> delayProvider = null,
            TimeSpan? maxCumulativeRetryDelay = null,
            int? maxIsRetriableRetryCount = null,
            Action<Guid> onDispatch = null)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.operationType = operationType;
            this.retryBaseDelay = retryBaseDelay;
            this.delayProvider = delayProvider ?? Task.Delay;

            CosmosClientOptions clientOptions = clientContext?.ClientOptions;

            // Explicit test overrides win; otherwise derive from the client options; otherwise fall back to defaults.
            this.maxIsRetriableRetryCount = maxIsRetriableRetryCount
                ?? clientOptions?.MaxRetryAttemptsOnAbortedTransactions
                ?? DistributedTransactionCommitter.MaxIsRetriableRetryCount;
            this.maxCumulativeRetryDelay = maxCumulativeRetryDelay
                ?? clientOptions?.MaxRetryWaitTimeOnAbortedTransactions
                ?? DistributedTransactionCommitter.MaxCumulativeRetryDelay;
            this.onDispatch = onDispatch;
        }

        public async Task<DistributedTransactionResponse> ExecuteTransactionAsync(
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
                await DistributedTransactionCommitterUtils.ResolveCollectionRidsAsync(
                    this.operations,
                    this.clientContext,
                    cancellationToken);

                DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                    this.operations,
                    this.clientContext.SerializerCore,
                    cancellationToken,
                    // Read transactions hold no commit state, so replaying one is harmless and both
                    // signals are omitted for them.
                    this.operationType == OperationType.CommitDistributedTransaction
                        ? new DistributedTransactionDispatchTracker()
                        : null);

                return await this.ExecuteCommitWithRetryAsync(serverRequest, trace, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DefaultTrace.TraceError($"Distributed transaction failed: {ex.Message}");
                throw;
            }
        }

        private async Task<DistributedTransactionResponse> ExecuteCommitWithRetryAsync(
            DistributedTransactionServerRequest serverRequest,
            ITrace parentTrace,
            CancellationToken cancellationToken)
        {
            // Allocate once; the underlying parentTrace tree continues to accumulate per-attempt children.
            CosmosTraceDiagnostics diagnostics = new CosmosTraceDiagnostics(parentTrace);

            int attempt = 0;
            TimeSpan cumulativeRetryDelay = TimeSpan.Zero;

            // First attempt dispatches under a freshly rotated token; after a retriable response the
            // next attempt's token strategy is decided below.
            bool rotateIdempotencyToken = true;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DistributedTransactionResponse response = await this.ExecuteCommitAsync(serverRequest, rotateIdempotencyToken, parentTrace, cancellationToken);

                if (response.IsSuccessStatusCode || !response.IsRetriable)
                {
                    response.Diagnostics = diagnostics;
                    return response;
                }

                if (attempt >= this.maxIsRetriableRetryCount)
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

                // Durable Abort (HTTP 452) → rotate to a new token (the prior token is terminally
                // aborted); any other retriable status → replay the same token to stay idempotent.
                rotateIdempotencyToken = response.IsTransactionAborted;

                DefaultTrace.TraceWarning(
                    "Distributed transaction commit retriable (StatusCode={0}, IsTransactionAborted={1}, " +
                        "attempt={2}, delayMs={3}, cumulativeDelayMs={4}, token={5}, DiagnosticString={6}).",
                    response.StatusCode,
                    response.IsTransactionAborted,
                    attempt,
                    (int)delay.TotalMilliseconds,
                    (int)cumulativeRetryDelay.TotalMilliseconds,
                    serverRequest.IdempotencyToken,
                    TruncateForLog(response.DiagnosticString));

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
            bool rotateIdempotencyToken,
            ITrace parentTrace,
            CancellationToken cancellationToken)
        {
            using (ITrace attemptTrace = parentTrace.StartChild("Execute Distributed Transaction Commit", TraceComponent.Batch, TraceLevel.Info))
            {
                // Rotate only for a new logical attempt (first attempt or post-Abort resubmission); a
                // non-aborted retriable replays the current token. The serialized body is reused either way.
                if (rotateIdempotencyToken)
                {
                    serverRequest.RotateIdempotencyToken();
                }

                // Publish the dispatched token (spec §4.4) so the transaction exposes the latest attempt's
                // token even after cancellation.
                this.onDispatch?.Invoke(serverRequest.IdempotencyToken);

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

                        DistributedTransactionCommitter.MergeSessionTokens(
                            response,
                            serverRequest,
                            this.clientContext.DocumentClient?.sessionContainer);

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

            // ClientRetryPolicy can re-dispatch this message to another write region without returning
            // here, so the tracker rides along and the headers are stamped per dispatch.
            if (serverRequest.DispatchTracker != null)
            {
                requestMessage.Properties[DistributedTransactionDispatchTracker.PropertyKey] = serverRequest.DispatchTracker;
            }
        }

        internal static void MergeSessionTokens(
            DistributedTransactionResponse response,
            DistributedTransactionServerRequest serverRequest,
            ISessionContainer sessionContainer)
        {
            // Mirror the pattern used by GatewayStoreModel.CaptureSessionTokenAndHandleSplitAsync.
            // after a response is received, store each operation's session token in the SessionContainer
            // so that subsequent Session-consistency reads on the affected collections can use the latest token
            // without getting ReadSessionNotAvailable.
            //
            // DTC spans multiple collections so the server embeds per-operation session tokens in the JSON body.
            // DistributedTransactionOperationResult.FromJson assembles each token into canonical SDK session-token
            if (response == null || response.Count == 0 || serverRequest == null || sessionContainer == null)
            {
                return;
            }

            RequestNameValueCollection headers = new RequestNameValueCollection();

            for (int i = 0; i < response.Count; i++)
            {
                DistributedTransactionOperationResult result = response[i];

                DistributedTransactionOperation operation = null;
                try
                {
                    operation = serverRequest.Operations[result.Index];

                    if (string.IsNullOrEmpty(result.SessionToken) || string.IsNullOrEmpty(operation.CollectionResourceId))
                    {
                        continue;
                    }

                    // SessionToken is already in canonical SDK session-token format, assembled by FromJson.
                    // Note: each SetSessionToken call acquires a write lock on the SessionContainer.
                    // For a future optimization, consider a batch-update API on ISessionContainer to
                    // reduce lock acquisitions when multiple operations target the same collection.
                    headers.Clear();
                    headers[HttpConstants.HttpHeaders.SessionToken] = result.SessionToken;

                    sessionContainer.SetSessionToken(
                        operation.CollectionResourceId,
                        DistributedTransactionConstants.GetCollectionFullName(operation.Database, operation.Container),
                        headers);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Session-token bookkeeping must never fail a transaction the server already committed.
                    // Log and continue so the remaining operations' tokens are still attempted.
                    DefaultTrace.TraceWarning(
                        "DTC session token merge failed for operation index {0} (collection {1}): [{2}] {3}",
                        result.Index,
                        operation?.CollectionResourceId ?? "<unknown>",
                        ex.GetType().Name,
                        ex.Message);
                }
            }
        }
    }
}
