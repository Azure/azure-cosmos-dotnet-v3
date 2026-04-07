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

    internal class DistributedTransactionCommitter
    {
        private static readonly TimeSpan DefaultRetryBaseDelay = TimeSpan.FromSeconds(1);
        private static readonly string ResourceUri = Paths.OperationsPathSegment + "/" + Paths.Operations_Dtc;

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
            using (ITrace trace = Trace.GetRootTrace("Execute Distributed Transaction Commit", TraceComponent.Batch, TraceLevel.Info))
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
                        trace: trace,
                        cancellationToken: cancellationToken);

                    using (responseMessage)
                    {
                        return await DistributedTransactionResponse.FromResponseMessageAsync(
                            responseMessage,
                            serverRequest,
                            this.clientContext.SerializerCore,
                            trace,
                            cancellationToken);
                    }
                }
            }
        }

        private static void EnrichRequestMessage(RequestMessage requestMessage, DistributedTransactionServerRequest serverRequest)
        {
            // Set DTC-specific headers
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IdempotencyToken, serverRequest.IdempotencyToken.ToString());
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.OperationType, requestMessage.OperationType.ToString());
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ResourceType, requestMessage.ResourceType.ToString());
            requestMessage.UseGatewayMode = true;
        }

        private Task AbortTransactionAsync(CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
