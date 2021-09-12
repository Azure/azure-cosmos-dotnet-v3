//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class BatchExecutor
    {
        private readonly ContainerInternal container;

        private readonly CosmosClientContext clientContext;

        private readonly IReadOnlyList<ItemBatchOperation> inputOperations;

        private readonly PartitionKey partitionKey;

        private readonly RequestOptions batchOptions;

        public BatchExecutor(
            ContainerInternal container,
            PartitionKey partitionKey,
            IReadOnlyList<ItemBatchOperation> operations,
            RequestOptions batchOptions)
        {
            this.container = container;
            this.clientContext = this.container.ClientContext;
            this.inputOperations = operations;
            this.partitionKey = partitionKey;
            this.batchOptions = batchOptions;
        }

        public async Task<TransactionalBatchResponse> ExecuteAsync(ITrace trace, CancellationToken cancellationToken)
        {
            using (ITrace executeNextBatchTrace = trace.StartChild("Execute Next Batch", TraceComponent.Batch, Tracing.TraceLevel.Info))
            {
                BatchExecUtils.EnsureValid(this.inputOperations, this.batchOptions);

                PartitionKey? serverRequestPartitionKey = this.partitionKey;
                if (this.batchOptions != null && this.batchOptions.IsEffectivePartitionKeyRouting)
                {
                    serverRequestPartitionKey = null;
                }

                SinglePartitionKeyServerBatchRequest serverRequest;
                serverRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                    serverRequestPartitionKey,
                    new ArraySegment<ItemBatchOperation>(this.inputOperations.ToArray()),
                    this.clientContext.SerializerCore,
                    executeNextBatchTrace,
                    cancellationToken);

                return await this.ExecuteServerRequestAsync( 
                    serverRequest,
                    executeNextBatchTrace,
                    cancellationToken);
            }
        }

        /// <summary>
        /// Makes a single batch request to the server.
        /// </summary>
        /// <param name="serverRequest">A server request with a set of operations on items.</param>
        /// <param name="trace">The trace.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>Response from the server.</returns>
        private async Task<TransactionalBatchResponse> ExecuteServerRequestAsync(
            SinglePartitionKeyServerBatchRequest serverRequest,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            using (ITrace executeBatchTrace = trace.StartChild("Execute Batch Request", TraceComponent.Batch, Tracing.TraceLevel.Info))
            {
                using (Stream serverRequestPayload = serverRequest.TransferBodyStream())
                {
                    Debug.Assert(serverRequestPayload != null, "Server request payload expected to be non-null");
                    ResponseMessage responseMessage = await this.clientContext.ProcessResourceOperationStreamAsync(
                        this.container.LinkUri,
                        ResourceType.Document,
                        OperationType.Batch,
                        this.batchOptions,
                        this.container,
                        serverRequest.PartitionKey.HasValue ? new FeedRangePartitionKey(serverRequest.PartitionKey.Value) : null,
                        serverRequestPayload,
                        requestMessage =>
                        {
                            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchRequest, bool.TrueString);
                            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchAtomic, bool.TrueString);
                            requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchOrdered, bool.TrueString);
                        },
                        executeBatchTrace,
                        cancellationToken);

                    return await TransactionalBatchResponse.FromResponseMessageAsync(
                        responseMessage,
                        serverRequest,
                        this.clientContext.SerializerCore,
                        shouldPromoteOperationStatus: true,
                        executeBatchTrace,
                        cancellationToken);
                }
            }
        }
    }
}
