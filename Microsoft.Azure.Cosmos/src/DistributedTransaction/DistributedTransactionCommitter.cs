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
        internal const int MaxIsRetriableRetryCount = 10;
        private const int RetryMaxExponent = 5; // ~32 s max base delay before jitter
        private static readonly TimeSpan DefaultRetryBaseDelay = TimeSpan.FromSeconds(1);
        private static readonly string ResourceUri = Paths.OperationsPathSegment + "/" + Paths.Operations_Dtc;

        private readonly IReadOnlyList<DistributedTransactionOperation> operations;
        private readonly CosmosClientContext clientContext;
        private readonly TimeSpan retryBaseDelay;
        private readonly Func<TimeSpan, CancellationToken, Task> delayProvider;

        public DistributedTransactionCommitter(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext)
            : this(operations, clientContext, DistributedTransactionCommitter.DefaultRetryBaseDelay)
        {
        }

        internal DistributedTransactionCommitter(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext,
            TimeSpan retryBaseDelay,
            Func<TimeSpan, CancellationToken, Task> delayProvider = null)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.retryBaseDelay = retryBaseDelay;
            this.delayProvider = delayProvider ?? Task.Delay;
        }

        public async Task<DistributedTransactionResponse> CommitTransactionAsync(
            ITrace trace,
            CancellationToken cancellationToken)
        {
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

                return await this.ExecuteCommitWithRetryAsync(serverRequest, trace, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Neutral wording: the server-side commit may have succeeded even when this
                // pipeline throws (e.g., post-commit session-token bookkeeping surfacing a
                // coordinator contract violation). Callers should rely on the exception type
                // and message — not this trace — to decide whether to retry.
                DefaultTrace.TraceError($"Distributed transaction commit pipeline threw: {ex.Message}");
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
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DistributedTransactionResponse response = await this.ExecuteCommitAsync(serverRequest, parentTrace, cancellationToken);

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

                DefaultTrace.TraceWarning(
                    $"Distributed transaction commit retriable (StatusCode={response.StatusCode}, IsRetriable={response.IsRetriable}, DiagnosticString={TruncateForLog(response.DiagnosticString)}, attempt {attempt + 1}, delayMs={(int)delay.TotalMilliseconds}). Retrying with idempotency token {serverRequest.IdempotencyToken}.");

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
                        operationType: OperationType.CommitDistributedTransaction,
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

                        // MergeSessionTokens may throw CosmosException for malformed tokens.
                        // Response is not disposed: the transaction committed and the caller
                        // may still need operation results. MemoryStream has no unmanaged resources.
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
        }

        internal static void MergeSessionTokens(
            DistributedTransactionResponse response,
            DistributedTransactionServerRequest serverRequest,
            ISessionContainer sessionContainer)
        {
            // Mirror GatewayStoreModel.CaptureSessionTokenAndHandleSplitAsync -> SessionContainer.SetSessionToken:
            // absent/empty tokens are silently skipped; valid tokens are merged into the SessionContainer.
            // Malformed tokens (anything SessionContainer.SetSessionToken cannot parse) are caught per-op
            // so other ops still get merged, then surfaced via a single CosmosException after the loop.
            if (response == null || response.Count == 0 || serverRequest == null || sessionContainer == null)
            {
                return;
            }

            RequestNameValueCollection headers = new RequestNameValueCollection();
            List<string> malformedErrors = null;

            for (int i = 0; i < response.Count; i++)
            {
                DistributedTransactionOperationResult result = response[i];

                // Defensive: server controls result.Index. A malformed response with an
                // out-of-range index must not crash the loop or skip remaining ops.
                if (result.Index < 0 || result.Index >= serverRequest.Operations.Count)
                {
                    malformedErrors ??= new List<string>();
                    malformedErrors.Add(
                        $"Result {i}: out-of-range op Index {result.Index} (request has {serverRequest.Operations.Count} ops)");
                    continue;
                }

                DistributedTransactionOperation operation = serverRequest.Operations[result.Index];

                // Silently skip empty tokens (same semantics as GatewayStoreModel).
                if (string.IsNullOrEmpty(result.SessionToken))
                {
                    continue;
                }

                // Skip merge if CollectionResourceId was not resolved (e.g., serverless/system resources).
                if (string.IsNullOrEmpty(operation.CollectionResourceId))
                {
                    continue;
                }

                headers.Clear();
                headers[HttpConstants.HttpHeaders.SessionToken] = result.SessionToken;

                // Validate canonical shape up front; SetSessionToken validates content internally.
                // Both failures funnel through the same catch block for uniform error reporting.
                // Catch per-op so one bad token doesn't abort merging the rest of the batch.
                try
                {
                    int colonIndex = result.SessionToken.IndexOf(':');
                    if (colonIndex <= 0 || colonIndex == result.SessionToken.Length - 1)
                    {
                        throw new FormatException(
                            "Expected canonical '{pkRangeId}:{lsn}' format.");
                    }

                    sessionContainer.SetSessionToken(
                        operation.CollectionResourceId,
                        DistributedTransactionConstants.GetCollectionFullName(operation.Database, operation.Container),
                        headers);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    malformedErrors ??= new List<string>();
                    malformedErrors.Add(
                        $"Op {result.Index}: SetSessionToken failed for '{DistributedTransactionCommitter.TruncateForLog(result.SessionToken)}' ({ex.GetType().Name}: {ex.Message})");
                }
            }

            // After merging all valid tokens, surface aggregated failure as CosmosException.
            // The transaction has already committed — this is a post-commit bookkeeping error.
            // Message explicitly states "committed successfully" so callers do not retry.
            if (malformedErrors != null)
            {
                throw new CosmosException(
                    message:
                        $"Distributed transaction committed successfully, but the coordinator returned " +
                        $"{malformedErrors.Count} malformed session token(s); session-token bookkeeping " +
                        "was partial and subsequent session-consistent reads on affected partitions may " +
                        "see ReadSessionNotAvailable until the session container is refreshed. " +
                        "DO NOT retry the transaction. " +
                        "Expected canonical '{{pkRangeId}}:{{lsn}}' format. " +
                        $"Details: [{string.Join("; ", malformedErrors)}]",
                    statusCode: HttpStatusCode.InternalServerError,
                    subStatusCode: 0,
                    activityId: response.ActivityId,
                    requestCharge: response.RequestCharge);
            }
        }
    }
}
