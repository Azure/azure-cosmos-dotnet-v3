//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Represents a batch of requests that will be performed atomically against the Azure Cosmos DB service.
    /// </summary>
    public class Batch
    {
        private readonly PartitionKey partitionKey;

        private readonly ContainerCore container;

        private List<ItemBatchOperation> operations;

        /// <summary>
        /// Initializes a new instance of the <see cref="Batch"/> class.
        /// </summary>
        /// <param name="container">Container that has items on which batch operations are to be performed.</param>
        /// <param name="partitionKey">The partition key for all items in the batch. <see cref="PartitionKey"/>.</param>
        internal Batch(ContainerCore container, PartitionKey partitionKey)
        {
            this.container = container;
            this.partitionKey = partitionKey;
            this.operations = new List<ItemBatchOperation>();
        }

        /// <summary>
        /// Adds an operation to create an item into the batch.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property.<see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public virtual Batch CreateItem<T>(T item, BatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation<T>(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    resource: item,
                    requestOptions: requestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to create an item into the batch.
        /// </summary>
        /// <param name="resourceStream">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public virtual Batch CreateItemStream(Stream resourceStream, BatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    resourceStream: resourceStream,
                    requestOptions: requestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to read an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public virtual Batch ReadItem(string id, BatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Read,
                    operationIndex: this.operations.Count,
                    id: id,
                    requestOptions: requestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to upsert an item into the batch.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public virtual Batch UpsertItem<T>(T item, BatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation<T>(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    resource: item,
                    requestOptions: requestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to upsert an item into the batch.
        /// </summary>
        /// <param name="resourceStream">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public virtual Batch UpsertItemStream(Stream resourceStream, BatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    resourceStream: resourceStream,
                    requestOptions: requestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to replace an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public virtual Batch ReplaceItem<T>(string id, T item, BatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation<T>(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    id: id,
                    resource: item,
                    requestOptions: requestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to replace an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="resourceStream">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public virtual Batch ReplaceItemStream(string id, Stream resourceStream, BatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    id: id,
                    resourceStream: resourceStream,
                    requestOptions: requestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to delete an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public virtual Batch DeleteItem(string id, BatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Delete,
                    operationIndex: this.operations.Count,
                    id: id,
                    requestOptions: requestOptions));

            return this;
        }

        /// <summary>
        /// Executes the batch at the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An awaitable <see cref="BatchResponse"/> which contains the completion status and results of each operation.</returns>
        public virtual Task<BatchResponse> ExecuteAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ExecuteAsync(requestOptions: null, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Adds an operation to patch an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="patchStream">A <see cref="Stream"/> containing the patch specification.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        internal virtual Batch PatchItemStream(string id, Stream patchStream, BatchItemRequestOptions requestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Patch,
                    operationIndex: this.operations.Count,
                    id: id,
                    resourceStream: patchStream,
                    requestOptions: requestOptions));

            return this;
        }

        /// <summary>
        /// Executes the batch at the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">Options that apply to the batch.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An awaitable <see cref="BatchResponse"/> which contains the completion status and results of each operation.</returns>
        internal virtual Task<BatchResponse> ExecuteAsync(RequestOptions requestOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            BatchExecUtils.GetServerRequestLimits(out int maxServerRequestBodyLength, out int maxServerRequestOperationCount);
            return this.ExecuteAsync(maxServerRequestBodyLength, maxServerRequestOperationCount, requestOptions, cancellationToken);
        }

        internal Task<BatchResponse> ExecuteAsync(
            int maxServerRequestBodyLength,
            int maxServerRequestOperationCount,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            BatchExecutor executor = new BatchExecutor(this.container, this.partitionKey, this.operations, requestOptions, maxServerRequestBodyLength, maxServerRequestOperationCount);
            this.operations = new List<ItemBatchOperation>();
            return executor.ExecuteAsync(cancellationToken);
        }
    }
}
