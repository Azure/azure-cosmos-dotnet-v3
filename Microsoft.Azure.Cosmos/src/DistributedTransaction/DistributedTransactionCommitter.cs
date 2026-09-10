// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
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

                this.ValidateUserSuppliedSessionTokens();

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
                DefaultTrace.TraceError($"Distributed transaction failed: {FormatForLog(ex.Message)}");
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
        private static string FormatForLog(string value)
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
                                this.clientContext.DocumentClient?.sessionContainer);
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
            ISessionContainer sessionContainer)
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
                        headers.Clear();
                        headers[HttpConstants.HttpHeaders.SessionToken] = result.SessionToken;

                        sessionContainer.SetSessionToken(
                            operation.CollectionResourceId,
                            collectionFullName,
                            headers);
                    }
                    else
                    {
                        failureReason = $"{validationFailure} Token: '{FormatForLog(result.SessionToken)}'.";
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
                DefaultTrace.TraceWarning("{0} Session token was not recorded.", FormatForLog(message));

                // Stop at the first failure; later tokens are intentionally not recorded.
                throw new InvalidOperationException(message, failureCause);
            }
        }

        /// <summary>
        /// Rejects malformed caller-supplied session tokens before dispatch.
        /// </summary>
        private void ValidateUserSuppliedSessionTokens()
        {
            foreach (DistributedTransactionOperation operation in this.operations)
            {
                string sessionToken = operation?.SessionToken;
                if (string.IsNullOrEmpty(sessionToken))
                {
                    continue;
                }

                if (DistributedTransactionCommitter.TryValidateSessionToken(sessionToken, out string _))
                {
                    continue;
                }

                throw new ArgumentException(
                    $"Distributed transaction operation index {operation.OperationIndex} was given the session token " +
                    $"'{FormatSessionTokenForMessage(sessionToken)}', which must be a single valid " +
                    "'<partitionKeyRangeId>:<token>' pair. The transaction was not sent.",
                    nameof(DistributedTransactionRequestOptions.SessionToken));
            }
        }

        private static string FormatSessionTokenForMessage(string sessionToken)
        {
            return FormatForLog(sessionToken);
        }

        /// <summary>
        /// Determines whether a session token is usable: it must have one numeric partition key range id
        /// and a simple or vector token whose numeric segments fit their wire types.
        /// </summary>
        /// <param name="sessionToken">The token reported for a single operation.</param>
        /// <param name="failureReason">The reason the token is unusable, or <c>null</c> when it is usable.</param>
        private static bool TryValidateSessionToken(string sessionToken, out string failureReason)
        {
            int colonIndex = sessionToken.IndexOf(':');
            string partitionKeyRangeId = colonIndex < 0
                ? null
                : sessionToken.Substring(0, colonIndex);
            string tokenSegment = colonIndex < 0
                ? sessionToken
                : sessionToken.Substring(colonIndex + 1);

            if (sessionToken.IndexOf(',') >= 0
                || (colonIndex >= 0 && sessionToken.IndexOf(':', colonIndex + 1) >= 0)
                || !DistributedTransactionCommitter.IsValidSessionTokenSegment(tokenSegment))
            {
                failureReason = "the token could not be parsed.";
                return false;
            }

            if (string.IsNullOrEmpty(partitionKeyRangeId))
            {
                failureReason = "the token is missing the partitionKeyRangeId prefix.";
                return false;
            }

            if (!DistributedTransactionCommitter.IsValidPartitionKeyRangeId(partitionKeyRangeId))
            {
                failureReason = "the partitionKeyRangeId prefix is invalid.";
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

        private static bool IsValidSessionTokenSegment(string value)
        {
            string[] segments = value.Split('#');
            if (segments.Length == 1)
            {
                return DistributedTransactionCommitter.IsValidInt64Segment(segments[0]);
            }

            if (segments.Length < 2
                || !DistributedTransactionCommitter.IsValidInt64Segment(segments[0])
                || !DistributedTransactionCommitter.IsValidInt64Segment(segments[1]))
            {
                return false;
            }

            for (int index = 2; index < segments.Length; index++)
            {
                string regionProgress = segments[index];
                int separatorIndex = regionProgress.IndexOf('=');
                if (separatorIndex <= 0
                    || separatorIndex != regionProgress.LastIndexOf('=')
                    || separatorIndex == regionProgress.Length - 1
                    || !DistributedTransactionCommitter.IsValidUInt32Segment(regionProgress.Substring(0, separatorIndex))
                    || !DistributedTransactionCommitter.IsValidInt64Segment(regionProgress.Substring(separatorIndex + 1)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidInt64Segment(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            int index = value[0] == '-' ? 1 : 0;
            if (index == value.Length)
            {
                return false;
            }

            for (; index < value.Length; index++)
            {
                if (value[index] < '0' || value[index] > '9')
                {
                    return false;
                }
            }

            return long.TryParse(
                value,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out long _);
        }

        private static bool IsValidUInt32Segment(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] < '0' || value[index] > '9')
                {
                    return false;
                }
            }

            return uint.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out uint _);
        }
    }
}
