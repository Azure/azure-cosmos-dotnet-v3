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
                    cancellationToken);

                // Resolve once per transaction; retries use the same consistency level.
                ConsistencyLevel? effectiveConsistencyLevel = await this.ResolveEffectiveConsistencyLevelAsync();

                return await this.ExecuteCommitWithRetryAsync(serverRequest, effectiveConsistencyLevel, trace, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DefaultTrace.TraceError($"Distributed transaction failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Resolves the effective consistency level for the transaction.
        /// </summary>
        private async Task<ConsistencyLevel?> ResolveEffectiveConsistencyLevelAsync()
        {
            ConsistencyLevel? clientOverride = this.clientContext.ClientOptions?.ConsistencyLevel;
            if (clientOverride.HasValue)
            {
                return clientOverride.Value;
            }

            DocumentClient documentClient = this.clientContext.DocumentClient;
            if (documentClient == null)
            {
                return null;
            }

            try
            {
                return await documentClient.GetDefaultConsistencyLevelAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DefaultTrace.TraceWarning(
                    "Distributed transaction could not resolve the account consistency level ([{0}] {1}); " +
                    "session token failures will be traced rather than surfaced.",
                    ex.GetType().Name,
                    ex.Message);
                return null;
            }
        }

        private async Task<DistributedTransactionResponse> ExecuteCommitWithRetryAsync(
            DistributedTransactionServerRequest serverRequest,
            ConsistencyLevel? effectiveConsistencyLevel,
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

                DistributedTransactionResponse response = await this.ExecuteCommitAsync(serverRequest, effectiveConsistencyLevel, rotateIdempotencyToken, parentTrace, cancellationToken);

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
            ConsistencyLevel? effectiveConsistencyLevel,
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

                        try
                        {
                            DistributedTransactionCommitter.MergeSessionTokens(
                                response,
                                serverRequest,
                                this.clientContext.DocumentClient?.sessionContainer,
                                effectiveConsistencyLevel,
                                this.operationType);
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
            ConsistencyLevel? effectiveConsistencyLevel,
            OperationType operationType)
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

            // Surfacing a token failure ends the transaction with an exception, so it may only happen once
            // the outcome is settled. IsCommittedInFull mirrors the terminal condition ExecuteCommitWithRetryAsync
            // applies after this method returns, and the message below asserts the transaction committed in full,
            // so both conditions are evaluated on the envelope rather than on the sub-operation that carried
            // the bad token. Ordering the consistency test first skips the envelope scan whenever the failure
            // could not be surfaced anyway.
            bool surfaceTokenFailures = effectiveConsistencyLevel == ConsistencyLevel.Session
                && DistributedTransactionCommitter.IsCommittedInFull(response);

            for (int i = 0; i < response.Count; i++)
            {
                DistributedTransactionOperationResult result = response[i];

                string collectionFullName = null;
                string failureReason = null;
                Exception failureCause = null;

                try
                {
                    DistributedTransactionOperation operation = serverRequest.Operations[result.Index];

                    if (string.IsNullOrEmpty(result.SessionToken) || string.IsNullOrEmpty(operation.CollectionResourceId))
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

                    collectionFullName = DistributedTransactionConstants.GetCollectionFullName(operation.Database, operation.Container);

                    if (DistributedTransactionCommitter.TryValidateSessionToken(result.SessionToken, out string validationFailure))
                    {
                        // SetSessionToken acquires a write lock on the session container.
                        headers.Clear();
                        headers[HttpConstants.HttpHeaders.SessionToken] = result.SessionToken;

                        sessionContainer.SetSessionToken(
                            operation.CollectionResourceId,
                            collectionFullName,
                            headers);
                    }
                    else
                    {
                        failureReason = $"{validationFailure} Token: '{TruncateForLog(result.SessionToken)}'.";
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

                // The collection is unknown only when resolving the operation itself threw.
                string collectionScope = collectionFullName == null
                    ? string.Empty
                    : $" for collection '{collectionFullName}'";

                // Apply the same policy to invalid and rejected tokens.
                string message = $"Session token for operation index {result.Index} could not be recorded{collectionScope}: {failureReason}";

                // Keep server-supplied braces out of the format string.
                DefaultTrace.TraceWarning("{0} Session token was not recorded.", message);

                if (surfaceTokenFailures)
                {
                    // Read transactions never commit, so the caller-facing outcome differs even though the
                    // capture path is shared.
                    string outcome = operationType == OperationType.Read
                        ? " The read transaction completed successfully and should not be retried."
                        : " The transaction was committed successfully and should not be retried.";

                    // Stop at the first failure; later tokens are intentionally not recorded.
                    throw new InvalidOperationException(message + outcome, failureCause);
                }
            }
        }

        /// <summary>
        /// Determines whether the response represents a transaction that committed in full: a settled,
        /// non-error envelope in which every sub-operation also carries a non-error status.
        /// </summary>
        /// <remarks>
        /// A MultiStatus envelope is a success status but reports a rolled back transaction through its
        /// sub-operations, so the envelope status alone cannot establish that the transaction committed.
        /// </remarks>
        private static bool IsCommittedInFull(DistributedTransactionResponse response)
        {
            // Mirrors the terminal condition in ExecuteCommitWithRetryAsync. A response that the loop will
            // not retry is the outcome the caller receives, which is the only point a token failure may
            // surface on. Testing IsRetriable alone would leave a success envelope that also reports
            // isRetriable unsettled here even though the loop returns it, silently dropping its token.
            bool outcomeIsSettled = response.IsSuccessStatusCode || !response.IsRetriable;

            if (!outcomeIsSettled || (int)response.StatusCode >= (int)StatusCodes.StartingErrorCode)
            {
                return false;
            }

            for (int i = 0; i < response.Count; i++)
            {
                if ((int)response[i].StatusCode >= (int)StatusCodes.StartingErrorCode)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a session token is usable: it must parse, and it must carry the partition
        /// key range id the progress was recorded against.
        /// </summary>
        /// <param name="sessionToken">The token reported for a single operation.</param>
        /// <param name="failureReason">The reason the token is unusable, or <c>null</c> when it is usable.</param>
        /// <remarks>
        /// The range id is checked separately because
        /// <see cref="SessionTokenHelper.TryParse(string, out string, out ISessionToken)"/> accepts a bare
        /// LSN and reports a null range id for it. Without that check a prefix-less token reaches
        /// <see cref="ISessionContainer.SetSessionToken(string, string, INameValueCollection)"/>
        /// and fails there with an <see cref="IndexOutOfRangeException"/>.
        /// </remarks>
        private static bool TryValidateSessionToken(string sessionToken, out string failureReason)
        {
            if (!SessionTokenHelper.TryParse(sessionToken, out string partitionKeyRangeId, out ISessionToken _))
            {
                failureReason = "the token could not be parsed.";
                return false;
            }

            if (string.IsNullOrEmpty(partitionKeyRangeId))
            {
                failureReason = "the token is missing the partitionKeyRangeId prefix.";
                return false;
            }

            failureReason = null;
            return true;
        }
    }
}
