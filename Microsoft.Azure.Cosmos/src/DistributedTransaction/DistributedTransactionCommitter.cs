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
        private const int MaxRetryAttempts = 3;
        private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(1);
        private static readonly string ResourceUri = Paths.OperationsPathSegment + "/" + Paths.Operations_Dtc;

        private readonly IReadOnlyList<DistributedTransactionOperation> operations;
        private readonly CosmosClientContext clientContext;

        public DistributedTransactionCommitter(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
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
            for (int attempt = 0; attempt <= MaxRetryAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool canRetry = attempt < MaxRetryAttempts;

                DistributedTransactionResponse response;
                try
                {
                    response = await this.ExecuteCommitAsync(serverRequest, cancellationToken);
                }
                catch (CosmosException cosmosEx) when (
                    !cancellationToken.IsCancellationRequested
                    && canRetry
                    && cosmosEx.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    DefaultTrace.TraceWarning(
                        $"Distributed transaction commit timed out (attempt {attempt + 1}/{MaxRetryAttempts + 1}). " +
                        $"Retrying with idempotency token {serverRequest.IdempotencyToken}.");
                    await Task.Delay(TimeSpan.FromTicks((long)(RetryBaseDelay.Ticks * Math.Pow(2, attempt))), cancellationToken);
                    continue;
                }

                if (canRetry
                    && !response.IsSuccessStatusCode
                    && (response.IsRetriable || response.StatusCode == HttpStatusCode.RequestTimeout))
                {
                    DefaultTrace.TraceWarning(
                        $"Distributed transaction commit retriable (StatusCode={response.StatusCode}, IsRetriable={response.IsRetriable}, " +
                        $"attempt {attempt + 1}/{MaxRetryAttempts + 1}). Retrying with idempotency token {serverRequest.IdempotencyToken}.");
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromTicks((long)(RetryBaseDelay.Ticks * Math.Pow(2, attempt))), cancellationToken);
                    continue;
                }

                return response;
            }
            throw new InvalidOperationException("Unexpected state: retry loop exhausted without returning.");
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
