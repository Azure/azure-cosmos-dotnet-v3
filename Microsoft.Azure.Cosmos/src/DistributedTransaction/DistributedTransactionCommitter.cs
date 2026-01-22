//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
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
        private readonly CosmosSerializerCore serializerCore;

        public DistributedTransactionCommitter(
            IReadOnlyList<DistributedTransactionOperation> operations,
            CosmosClientContext clientContext)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
            this.serializerCore = this.clientContext.SerializerCore;
        }

        public async Task<DistributedTransactionResponse> CommitTransactionAsync(
            CancellationToken cancellationToken = default)
        {
            try
            {
                DistributedTransactionCommitterUtils.ValidateTransaction(this.operations);

                await DistributedTransactionCommitterUtils.ResolveCollectionRidsAsync(
                    this.operations,
                    this.clientContext,
                    cancellationToken);

                DistributedTransactionRequest transactionRequest = this.BuildTransactionRequest(this.operations);

                return await this.ExecuteRequestAsync(transactionRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceError($"Distributed transaction failed: {ex.Message}");
                await this.AbortTransactionAsync(cancellationToken);
                throw;
            }
        }

        private DistributedTransactionRequest BuildTransactionRequest(
            IReadOnlyList<DistributedTransactionOperation> operations)
        {
            DistributedTransactionRequest request = new DistributedTransactionRequest(
                operations: operations,
                operationType: OperationType.Batch,
                resourceType: ResourceType.Document);

            // TODO : Serialize resource body and build DTS request payload

            return request;
        }

        private async Task<DistributedTransactionResponse> ExecuteRequestAsync(
            DistributedTransactionRequest transactionRequest,
            CancellationToken cancellationToken)
        {
            using (Stream requestPayload = this.SerializeTransactionRequest(transactionRequest))
            {
                ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                    resourceUri: string.Empty, // DTC endpoint - to be configured
                    resourceType: ResourceType.Document,
                    operationType: OperationType.Batch,
                    requestOptions: null,
                    cosmosContainerCore: null,
                    feedRange: null,
                    streamPayload: requestPayload,
                    requestEnricher: requestMessage =>
                    {
                        // TODO: update HttpHeaders with required headers and populate them here
                    },
                    trace: NoOpTrace.Singleton,
                    cancellationToken: cancellationToken);

                return await this.CreateResponseFromMessageAsync(
                    responseMessage,
                    transactionRequest,
                    cancellationToken);
            }
        }

        private Stream SerializeTransactionRequest(DistributedTransactionRequest transactionRequest)
        {
            return this.serializerCore.ToStream(transactionRequest);
        }

        private async Task<DistributedTransactionResponse> CreateResponseFromMessageAsync(
            ResponseMessage responseMessage,
            DistributedTransactionRequest transactionRequest,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

#pragma warning disable IDE0060 // Remove unused parameter
        private async Task AbortTransactionAsync(CancellationToken cancellationToken)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            // TODO: Implement transaction abort logic
            await Task.CompletedTask;
        }
    }
}