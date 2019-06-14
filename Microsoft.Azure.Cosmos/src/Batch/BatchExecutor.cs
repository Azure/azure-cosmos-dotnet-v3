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
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal sealed class BatchExecutor
    {
        private readonly ContainerCore container;

        private readonly CosmosClient client;

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
            this.client = this.container.ClientContext.Client;
            this.inputOperations = operations;
            this.partitionKey = partitionKey;
            this.batchOptions = batchOptions;
            this.maxServerRequestBodyLength = maxServerRequestBodyLength;
            this.maxServerRequestOperationCount = maxServerRequestOperationCount;
        }

        public async Task<CosmosBatchResponse> ExecuteAsync(CancellationToken cancellationToken)
        {
            CosmosResponseMessage validationResult = BatchExecUtils.Validate(
                this.inputOperations,
                this.batchOptions,
                this.client,
                this.maxServerRequestOperationCount);

            if (!validationResult.IsSuccessStatusCode)
            {
                return new CosmosBatchResponse(
                    validationResult.StatusCode,
                    validationResult.Headers.SubStatusCode,
                    validationResult.ErrorMessage,
                    this.inputOperations);
            }

            SinglePartitionKeyServerBatchRequest serverRequest;
            try
            {
                serverRequest = await SinglePartitionKeyServerBatchRequest.CreateAsync(
                      this.partitionKey,
                      new ArraySegment<ItemBatchOperation>(this.inputOperations.ToArray()),
                      this.maxServerRequestBodyLength,
                      this.maxServerRequestOperationCount,
                      serializer: this.client.ClientOptions.CosmosSerializerWithWrapperOrDefault,
                      cancellationToken: cancellationToken);
            }
            catch (RequestEntityTooLargeException ex)
            {
                return new CosmosBatchResponse(ex.StatusCode ?? HttpStatusCode.RequestEntityTooLarge, ex.GetSubStatus(), ClientResources.BatchOperationTooLarge, this.inputOperations);
            }

            if (serverRequest.Operations.Count != this.inputOperations.Count)
            {
                // todo: should this be PayloadTooLarge
                return new CosmosBatchResponse(HttpStatusCode.RequestEntityTooLarge, SubStatusCodes.Unknown, ClientResources.BatchTooLarge, this.inputOperations);
            }

            return await this.ExecuteServerRequestAsync(serverRequest, cancellationToken);
        }

        /// <summary>
        /// Makes a single batch request to the server.
        /// </summary>
        /// <param name="serverRequest">A server request with a set of operations on items.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>Response from the server or ServiceUnavailable response in case of exceptions.</returns>
        private async Task<CosmosBatchResponse> ExecuteServerRequestAsync(SinglePartitionKeyServerBatchRequest serverRequest, CancellationToken cancellationToken)
        {
            try
            {
                using (Stream serverRequestPayload = serverRequest.TransferBodyStream())
                {
                    Debug.Assert(serverRequestPayload != null, "Server request payload expected to be non-null");
                    CosmosResponseMessage cosmosResponseMessage = await ExecUtils.ProcessResourceOperationAsync(
                        this.client,
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
                        responseMessage => responseMessage, // response creator
                        cancellationToken);

                    return await CosmosBatchResponse.FromResponseMessageAsync(
                        cosmosResponseMessage,
                        serverRequest,
                        this.client.ClientOptions.CosmosSerializerWithWrapperOrDefault);
                }
            }
            catch (CosmosException ex)
            {
                return new CosmosBatchResponse(
                    HttpStatusCode.ServiceUnavailable,
                    SubStatusCodes.Unknown,
                    ex.Message, 
                    serverRequest.Operations);
            }
        }
    }
}
