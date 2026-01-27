// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal class DistributedTransactionCommitter
    {
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
                    cancellationToken).ConfigureAwait(false);

                DistributedTransactionServerRequest serverRequest = await DistributedTransactionServerRequest.CreateAsync(
                    this.operations,
                    this.clientContext.SerializerCore,
                    cancellationToken).ConfigureAwait(false);

                return await this.ExecuteCommitAsync(serverRequest, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError($"Distributed transaction failed: {ex.Message}");
                await this.AbortTransactionAsync(cancellationToken);
                throw;
            }
        }

        private async Task<DistributedTransactionResponse> ExecuteCommitAsync(
            DistributedTransactionServerRequest serverRequest,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using (ITrace trace = Tracing.Trace.GetRootTrace("Execute Distributed Transaction Commit", TraceComponent.Batch, Tracing.TraceLevel.Info))
            {
                DistributedTransactionRequest transactionRequest = new DistributedTransactionRequest(
                    serverRequest.Operations,
                    OperationType.Batch,
                    ResourceType.Document);

                using (MemoryStream bodyStream = serverRequest.TransferBodyStream())
                {
                    Debug.Assert(bodyStream != null, "Server request payload expected to be non-null");

                    ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                        resourceUri: null,
                        resourceType: ResourceType.Document,
                        operationType: OperationType.Batch,
                        requestOptions: null,
                        cosmosContainerCore: null,
                        partitionKey: null,
                        itemId: null,
                        streamPayload: bodyStream,
                        requestEnricher: requestMessage => this.EnrichRequestMessage(requestMessage, transactionRequest),
                        trace: trace,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();

                    return await DistributedTransactionResponse.FromResponseMessageAsync(
                        responseMessage,
                        serverRequest,
                        this.clientContext.SerializerCore,
                        trace,
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private void EnrichRequestMessage(RequestMessage requestMessage, DistributedTransactionRequest transactionRequest)
        {
            // Set DTC-specific headers
            requestMessage.Headers.Add("x-ms-dtc-version", "0.0");
            requestMessage.Headers.Add("x-ms-dtc-operation-id", transactionRequest.IdempotencyToken.ToString());

            // Indicate this is a distributed transaction request (batch operation)
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchRequest, bool.TrueString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchAtomic, bool.TrueString);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchOrdered, bool.TrueString);
        }

        private Task AbortTransactionAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // TODO: Implement abort logic to clean up any partial state
            // This may involve sending an abort request to the coordinator
            DefaultTrace.TraceWarning("AbortTransactionAsync called but not yet implemented");
            return Task.CompletedTask;
        }
    }
}
