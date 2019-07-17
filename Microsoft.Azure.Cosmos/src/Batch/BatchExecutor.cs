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
    using Microsoft.Azure.Documents;

    internal sealed class BatchExecutor
    {
        private readonly ContainerCore container;

        private readonly CosmosClientContext clientContext;

        private readonly IReadOnlyList<ItemBatchOperation> inputOperations;

        private readonly PartitionKey partitionKey;

        private readonly RequestOptions batchOptions;

        private readonly int maxServerRequestBodyLength;

        private readonly int maxServerRequestOperationCount;

        public BatchExecutor(
            ContainerCore container,
            PartitionKey partitionKey,
            IReadOnlyList<ItemBatchOperation> operations,
            RequestOptions batchOptions,
            int maxServerRequestBodyLength,
            int maxServerRequestOperationCount)
        {
            this.container = container;
            this.clientContext = this.container.ClientContext;
            this.inputOperations = operations;
            this.partitionKey = partitionKey;
            this.batchOptions = batchOptions;
            this.maxServerRequestBodyLength = maxServerRequestBodyLength;
            this.maxServerRequestOperationCount = maxServerRequestOperationCount;
        }

        public async Task<BatchResponse> ExecuteAsync(CancellationToken cancellationToken)
        {
            BatchExecUtils.EnsureValid(this.inputOperations, this.batchOptions, this.maxServerRequestOperationCount);

            PartitionKey? serverRequestPartitionKey = this.partitionKey;
            if (this.batchOptions != null && this.batchOptions.IsEffectivePartitionKeyRouting)
            {
                serverRequestPartitionKey = null;
            }

            SinglePartitionKeyServerBatchRequest serverRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                      serverRequestPartitionKey,
                      new ArraySegment<ItemBatchOperation>(this.inputOperations.ToArray()),
                      this.maxServerRequestBodyLength,
                      this.maxServerRequestOperationCount,
                      serializer: this.clientContext.CosmosSerializer,
                      cancellationToken: cancellationToken);

            if (serverRequest.Operations.Count != this.inputOperations.Count)
            {
                throw new RequestEntityTooLargeException(ClientResources.BatchTooLarge);
            }

            return await this.ExecuteServerRequestAsync(serverRequest, cancellationToken);
        }

        /// <summary>
        /// Makes a single batch request to the server.
        /// </summary>
        /// <param name="serverRequest">A server request with a set of operations on items.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>Response from the server.</returns>
        private async Task<BatchResponse> ExecuteServerRequestAsync(
            SinglePartitionKeyServerBatchRequest serverRequest,
            CancellationToken cancellationToken)
        {
            using (Stream serverRequestPayload = serverRequest.TransferBodyStream())
            {
                Debug.Assert(serverRequestPayload != null, "Server request payload expected to be non-null");
                ResponseMessage responseMessage = await clientContext.ProcessResourceOperationStreamAsync(
                    this.container.LinkUri,
                    ResourceType.Document,
                    OperationType.Batch,
                    this.batchOptions,
                    this.container,
                    serverRequest.PartitionKey,
                    serverRequestPayload,
                    requestMessage =>
                    {
                        requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchRequest, bool.TrueString);
                        requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchAtomic, bool.TrueString);
                        requestMessage.Headers.Add(HttpConstants.HttpHeaders.IsBatchOrdered, bool.TrueString);
                    },
                    cancellationToken);

                return await BatchResponse.FromResponseMessageAsync(
                    responseMessage,
                    serverRequest,
                    this.clientContext.CosmosSerializer);
            }
        }
    }
}
