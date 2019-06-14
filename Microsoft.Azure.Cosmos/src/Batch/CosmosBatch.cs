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
    /// Represents a batch of requests to Cosmos DB.
    /// </summary>
    public class CosmosBatch
    {
        private readonly PartitionKey partitionKey;

        private readonly ContainerCore container;

        private List<ItemBatchOperation> operations;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosmosBatch"/> class.
        /// </summary>
        /// <param name="container">Container that has items on which batch operations are to be performed.</param>
        /// <param name="partitionKey">The partition key for all items in the batch. <see cref="PartitionKey"/>.</param>
        internal CosmosBatch(ContainerCore container, PartitionKey partitionKey)
        {
            this.container = container;
            this.partitionKey = partitionKey;
            this.operations = new List<ItemBatchOperation>();
        }

        /// <summary>
        /// Adds an operation to create an item into the batch.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property.<see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="itemRequestOptions">(Optional) The options for the item request. <see cref="ItemRequestOptions"/>.</param>
        /// <returns>The <see cref="CosmosBatch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public virtual CosmosBatch CreateItem<T>(T item, ItemRequestOptions itemRequestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation<T>(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    resource: item,
                    requestOptions: itemRequestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to create an item into the batch.
        /// </summary>
        /// <param name="resourceStream">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="itemRequestOptions">(Optional) The options for the item request. <see cref="ItemRequestOptions"/>.</param>
        /// <returns>The <see cref="CosmosBatch"/> instance with the operation added.</returns>
        public virtual CosmosBatch CreateItemStream(Stream resourceStream, ItemRequestOptions itemRequestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Create,
                    operationIndex: this.operations.Count,
                    resourceStream: resourceStream,
                    requestOptions: itemRequestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to read an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="itemRequestOptions">(Optional) The options for the item request. <see cref="ItemRequestOptions"/>.</param>
        /// <returns>The <see cref="CosmosBatch"/> instance with the operation added.</returns>
        public virtual CosmosBatch ReadItem(string id, ItemRequestOptions itemRequestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Read,
                    operationIndex: this.operations.Count,
                    id: id,
                    requestOptions: itemRequestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to upsert an item into the batch.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="itemRequestOptions">(Optional) The options for the item request. <see cref="ItemRequestOptions"/>.</param>
        /// <returns>The <see cref="CosmosBatch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public virtual CosmosBatch UpsertItem<T>(T item, ItemRequestOptions itemRequestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation<T>(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    resource: item,
                    requestOptions: itemRequestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to upsert an item into the batch.
        /// </summary>
        /// <param name="resourceStream">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="itemRequestOptions">(Optional) The options for the item request. <see cref="ItemRequestOptions"/>.</param>
        /// <returns>The <see cref="CosmosBatch"/> instance with the operation added.</returns>
        public virtual CosmosBatch UpsertItemStream(Stream resourceStream, ItemRequestOptions itemRequestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Upsert,
                    operationIndex: this.operations.Count,
                    resourceStream: resourceStream,
                    requestOptions: itemRequestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to replace an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="itemRequestOptions">(Optional) The options for the item request. <see cref="ItemRequestOptions"/>.</param>
        /// <returns>The <see cref="CosmosBatch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public virtual CosmosBatch ReplaceItem<T>(string id, T item, ItemRequestOptions itemRequestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation<T>(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    id: id,
                    resource: item,
                    requestOptions: itemRequestOptions));

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
        /// <param name="itemRequestOptions">(Optional) The options for the item request. <see cref="ItemRequestOptions"/>.</param>
        /// <returns>The <see cref="CosmosBatch"/> instance with the operation added.</returns>
        public virtual CosmosBatch ReplaceItemStream(string id, Stream resourceStream, ItemRequestOptions itemRequestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Replace,
                    operationIndex: this.operations.Count,
                    id: id,
                    resourceStream: resourceStream,
                    requestOptions: itemRequestOptions));

            return this;
        }

        /// <summary>
        /// Adds an operation to delete an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="itemRequestOptions">(Optional) The options for the item request. <see cref="ItemRequestOptions"/>.</param>
        /// <returns>The <see cref="CosmosBatch"/> instance with the operation added.</returns>
        public virtual CosmosBatch DeleteItem(string id, ItemRequestOptions itemRequestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Delete,
                    operationIndex: this.operations.Count,
                    id: id,
                    requestOptions: itemRequestOptions));

            return this;
        }

        /// <summary>
        /// Executes the batch at the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An awaitable <see cref="CosmosBatchResponse"/> which contains the completion status and results of each operation.</returns>
        public virtual Task<CosmosBatchResponse> ExecuteAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ExecuteAsync(requestOptions: null, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Adds an operation to patch an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="patchStream">A <see cref="Stream"/> containing the patch specification.</param>
        /// <param name="itemRequestOptions">(Optional) The options for the item request. <see cref="ItemRequestOptions"/>.</param>
        /// <returns>The <see cref="CosmosBatch"/> instance with the operation added.</returns>
        internal virtual CosmosBatch PatchItemStream(string id, Stream patchStream, ItemRequestOptions itemRequestOptions = null)
        {
            this.operations.Add(new ItemBatchOperation(
                    operationType: OperationType.Patch,
                    operationIndex: this.operations.Count,
                    id: id,
                    resourceStream: patchStream,
                    requestOptions: itemRequestOptions));

            return this;
        }

        /// <summary>
        /// Executes the batch at the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="requestOptions">Options that apply to the batch.</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An awaitable <see cref="CosmosBatchResponse"/> which contains the completion status and results of each operation.</returns>
        internal virtual Task<CosmosBatchResponse> ExecuteAsync(RequestOptions requestOptions, CancellationToken cancellationToken = default(CancellationToken))
        {
            BatchExecUtils.GetServerRequestLimits(out int maxServerRequestBodyLength, out int maxServerRequestOperationCount);
            return this.ExecuteAsync(maxServerRequestBodyLength, maxServerRequestOperationCount, requestOptions, cancellationToken);
        }

        internal Task<CosmosBatchResponse> ExecuteAsync(
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
