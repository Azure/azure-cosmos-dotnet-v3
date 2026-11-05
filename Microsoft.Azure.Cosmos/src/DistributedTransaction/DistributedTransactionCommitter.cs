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
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

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

                // Validation and routing must use the same option values that will be serialized.
                foreach (DistributedTransactionOperation operation in this.operations)
                {
                    operation.CaptureRequestOptions();
                }

                DistributedTransactionSessionTokenHelper.ValidateUserSuppliedSessionTokens(this.operations);

                // Resolved before routing to decide which sub-operations receive an automatic session token.
                ConsistencyLevel? accountConsistencyLevel = await DistributedTransactionSessionTokenHelper.ResolveAccountConsistencyLevelAsync(
                    this.clientContext);

                IReadOnlyDictionary<string, ContainerProperties> containerPropertiesByCollection = await DistributedTransactionCommitterUtils.ResolveCollectionRidsAsync(
                    this.operations,
                    this.clientContext,
                    cancellationToken);

                // Reuse this cache after dispatch; reacquiring it then could turn a committed result into a failure.
                PartitionKeyRangeCache partitionKeyRangeCache = await DistributedTransactionSessionTokenHelper.ResolveOperationSessionTokensAsync(
                    this.operations,
                    containerPropertiesByCollection,
                    this.clientContext,
                    accountConsistencyLevel,
                    trace,
                    cancellationToken);

                DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                    this.operations,
                    this.clientContext.SerializerCore,
                    cancellationToken,
                    // Read transactions have no commit state requiring write-dispatch signals.
                    tracksDispatch: this.operationType == OperationType.CommitDistributedTransaction);

                return await this.ExecuteCommitWithRetryAsync(serverRequest, partitionKeyRangeCache, trace, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DefaultTrace.TraceError($"Distributed transaction failed: {FormatForLog(ex.Message)}");
                throw;
            }
        }

        private async Task<DistributedTransactionResponse> ExecuteCommitWithRetryAsync(
            DistributedTransactionServerRequest serverRequest,
            PartitionKeyRangeCache partitionKeyRangeCache,
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

                DistributedTransactionResponse response = await this.ExecuteCommitAsync(
                    serverRequest, partitionKeyRangeCache, rotateIdempotencyToken, parentTrace, cancellationToken);

                if (response.IsSuccessStatusCode || !response.IsRetriable)
                {
                    response.Diagnostics = diagnostics;
                    return response;
                }

                if (attempt >= this.maxIsRetriableRetryCount)
                {
                    DefaultTrace.TraceWarning(
                        $"Distributed transaction isRetriable retry budget exhausted after {attempt} attempts " +
                            $"(StatusCode={response.StatusCode}, DiagnosticString={FormatForLog(response.DiagnosticString)}). Returning last response.");
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
                                $"DiagnosticString={FormatForLog(response.DiagnosticString)}). Returning last response.");
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
                    FormatForLog(response.DiagnosticString));

                response.Dispose();
                attempt++;
                await this.delayProvider(delay, cancellationToken);
            }
        }

        // Bounds and escapes external text before it enters SDK trace logs.
        internal static string FormatForLog(string value)
        {
            const int MaxLogLength = 256;
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            string boundedValue = value.Length <= MaxLogLength
                ? value
                : value.Substring(0, MaxLogLength) + "...[truncated]";

            return boundedValue
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private async Task<DistributedTransactionResponse> ExecuteCommitAsync(
            DistributedTransactionServerRequest serverRequest,
            PartitionKeyRangeCache partitionKeyRangeCache,
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
                    serverRequest.RotateIdempotencyToken(Guid.NewGuid());
                }

                DistributedTransactionDispatchTracker dispatchTracker = serverRequest.DispatchTracker;
                Guid idempotencyToken = dispatchTracker?.IdempotencyToken ?? serverRequest.IdempotencyToken;

                // Publish the dispatched token (spec §4.4) so the transaction exposes the latest attempt's
                // token even after cancellation.
                this.onDispatch?.Invoke(idempotencyToken);

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
                        requestEnricher: requestMessage => DistributedTransactionCommitter.EnrichRequestMessage(
                            requestMessage,
                            idempotencyToken,
                            dispatchTracker),
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

                        try
                        {
                            await DistributedTransactionSessionTokenHelper.MergeSessionTokensAsync(
                                response,
                                serverRequest,
                                this.clientContext.DocumentClient?.sessionContainer,
                                partitionKeyRangeCache,
                                attemptTrace);
                        }
                        catch
                        {
                            // Ownership of the response transfers to the caller only on the return path.
                            // When bookkeeping throws, nothing else can reach it, so it is disposed here
                            // rather than left to the finalizer.
                            response.Dispose();
                            throw;
                        }

                        return response;
                    }
                }
            }
        }

        private static void EnrichRequestMessage(
            RequestMessage requestMessage,
            Guid idempotencyToken,
            DistributedTransactionDispatchTracker dispatchTracker)
        {
            // Set DTC-specific headers
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IdempotencyToken, idempotencyToken.ToString());
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.OperationType, requestMessage.OperationType.ToOperationTypeString());
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ResourceType, requestMessage.ResourceType.ToResourceTypeString());
            requestMessage.UseGatewayMode = true;

            requestMessage.DistributedTransactionDispatchTracker = dispatchTracker;
        }

    }
}
