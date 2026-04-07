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
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;

    internal class DistributedTransactionCommitter
    {
        private static readonly TimeSpan DefaultRetryBaseDelay = TimeSpan.FromSeconds(1);

        private readonly IReadOnlyList<DistributedTransactionOperation> operations;
        private readonly CosmosClientContext clientContext;
        private readonly TimeSpan retryBaseDelay;
        private readonly Random jitter = new Random();
        private readonly Func<TimeSpan, CancellationToken, Task> delayProvider;

        public DistributedTransactionCommitter(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext)
            : this(operations, clientContext, DefaultRetryBaseDelay)
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

        public async Task<DistributedTransactionResponse> CommitTransactionAsync(CancellationToken cancellationToken)
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

                return await this.ExecuteCommitWithRetryAsync(serverRequest, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                DefaultTrace.TraceError($"Distributed transaction failed: {ex.Message}");
                // await this.AbortTransactionAsync(cancellationToken);
                throw;
            }
        }

        private async Task<DistributedTransactionResponse> ExecuteCommitWithRetryAsync(
            DistributedTransactionServerRequest serverRequest,
            CancellationToken cancellationToken)
        {
            int attempt = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DistributedTransactionResponse response;
                try
                {
                    response = await this.ExecuteCommitAsync(serverRequest, cancellationToken);
                }
                catch (CosmosException cosmosEx) when (
                    !cancellationToken.IsCancellationRequested
                    && cosmosEx.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    DefaultTrace.TraceWarning(
                        $"Distributed transaction commit timed out (attempt {attempt + 1}). " +
                        $"Retrying with idempotency token {serverRequest.IdempotencyToken}.");
                    await this.delayProvider(this.GetRetryDelay(attempt, retryAfter: null), cancellationToken);
                    attempt++;
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    // Eagerly evaluate IsRetriableByStatusCode so that the out parameter (retryAfter)
                    // is always populated when applicable, regardless of the order of the || operands.
                    bool isRetriableByStatusCode = DistributedTransactionCommitter.IsRetriableByStatusCode(response, out TimeSpan? retryAfter);
                    bool shouldRetry = response.IsRetriable || isRetriableByStatusCode;

                    if (shouldRetry)
                    {
                        DefaultTrace.TraceWarning(
                            $"Distributed transaction commit retriable (StatusCode={response.StatusCode}, " +
                            $"SubStatusCode={response.SubStatusCode}, IsRetriable={response.IsRetriable}, " +
                            $"attempt {attempt + 1}). Retrying with idempotency token {serverRequest.IdempotencyToken}.");
                        response.Dispose();
                        await this.delayProvider(this.GetRetryDelay(attempt, retryAfter), cancellationToken);
                        attempt++;
                        continue;
                    }
                }

                return response;
            }
        }

        /// <summary>
        /// Determines whether the response should be retried based on the envelope status code and
        /// sub-status code defined in the DTx SDK response contract, independently of the JSON body.
        /// The server returns an empty body for 408, 449, 429, and 500 responses, so the
        /// <see cref="DistributedTransactionResponse.IsRetriable"/> flag (which requires a JSON body)
        /// cannot be used for these cases.
        /// </summary>
        private static bool IsRetriableByStatusCode(DistributedTransactionResponse response, out TimeSpan? retryAfter)
        {
            retryAfter = null;
            int statusCode = (int)response.StatusCode;
            int subStatusCode = (int)response.SubStatusCode;

            // 408: coordinator retries exhausted with no terminal state reached ("stuck").
            if (statusCode == (int)HttpStatusCode.RequestTimeout)
            {
                return true;
            }

            // 449/5352: coordinator race conflict — server signals the required backoff via Retry-After.
            if (statusCode == (int)StatusCodes.RetryWith && subStatusCode == DistributedTransactionConstants.DtcCoordinatorRaceConflict)
            {
                retryAfter = response.Headers?.RetryAfter;
                return true;
            }

            // 429/3200: ledger RU throttled — coordinator exhausted its internal retry budget.
            if (statusCode == (int)StatusCodes.TooManyRequests
                && subStatusCode == DistributedTransactionConstants.DtcLedgerThrottled)
            {
                retryAfter = response.Headers?.RetryAfter;
                return true;
            }

            // 500/5411-5413: transient infrastructure failures (ledger, account config, dispatch).
            // Only these specific sub-statuses are retriable; generic 500s are not.
            if (statusCode == (int)HttpStatusCode.InternalServerError
                && (subStatusCode == DistributedTransactionConstants.DtcLedgerFailure
                    || subStatusCode == DistributedTransactionConstants.DtcAccountConfigFailure
                    || subStatusCode == DistributedTransactionConstants.DtcDispatchFailure))
            {
                return true;
            }

            return false;
        }

        private TimeSpan GetRetryDelay(int attempt, TimeSpan? retryAfter)
        {
            if (retryAfter.HasValue)
            {
                return retryAfter.Value;
            }

            const int maxExponent = 5;
            int exponent = Math.Min(attempt, maxExponent);
            double baseDelayMs = this.retryBaseDelay.TotalMilliseconds * Math.Pow(2, exponent);
            // Jitter: uniform random to decorrelate concurrent clients and avoid synchronized retry storms.
            double jitterDelay = baseDelayMs * this.jitter.NextDouble();
            return TimeSpan.FromMilliseconds((baseDelayMs * 0.5) + jitterDelay);
        }

        private async Task<DistributedTransactionResponse> ExecuteCommitAsync(
            DistributedTransactionServerRequest serverRequest,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (ITrace trace = Trace.GetRootTrace("Execute Distributed Transaction Commit", TraceComponent.Batch, TraceLevel.Info))
            {
                using (MemoryStream bodyStream = serverRequest.CreateBodyStream())
                {
                    ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                        resourceUri: DistributedTransactionCommitter.GetResourceUri(),
                        resourceType: ResourceType.DistributedTransactionBatch,
                        operationType: OperationType.CommitDistributedTransaction,
                        requestOptions: null,
                        cosmosContainerCore: null,
                        partitionKey: null,
                        itemId: null,
                        streamPayload: bodyStream,
                        requestEnricher: requestMessage => DistributedTransactionCommitter.EnrichRequestMessage(requestMessage, serverRequest),
                        trace: trace,
                        cancellationToken: cancellationToken);

                    using (responseMessage)
                    {
                        DistributedTransactionResponse response = await DistributedTransactionResponse.FromResponseMessageAsync(
                            responseMessage,
                            serverRequest,
                            this.clientContext.SerializerCore,
                            trace,
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

        private static string GetResourceUri()
        {
            return Paths.OperationsPathSegment + "/" + Paths.Operations_Dtc;
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
            // DTC spans multiple collections so the server embeds per-operation session
            // tokens in the JSON body; those are already parsed into DistributedTransactionOperationResult.SessionToken,
            // but we must explicitly push them into the SessionContainer.

            if (response == null || response.Count == 0 || serverRequest == null || sessionContainer == null)
            {
                return;
            }

            RequestNameValueCollection headers = new RequestNameValueCollection();

            for (int i = 0; i < response.Count; i++)
            {
                DistributedTransactionOperationResult result = response[i];
                DistributedTransactionOperation operation = serverRequest.Operations[result.Index];

                if (string.IsNullOrEmpty(result.SessionToken) || string.IsNullOrEmpty(operation.CollectionResourceId))
                {
                    continue;
                }

                if (result.StatusCode == HttpStatusCode.NotFound
                    && result.SubStatusCode == SubStatusCodes.ReadSessionNotAvailable)
                {
                    continue;
                }

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
        }

        private Task AbortTransactionAsync(CancellationToken cancellationToken)
        {
            // TODO: Implement abort for the two-phase commit path.
            throw new NotImplementedException();
        }
    }
}
