//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a batch of requests that will be performed atomically against the Azure Cosmos DB service.
    /// </summary>
    public abstract class TransactionalBatch
    {
        /// <summary>
        /// Adds an operation to create an item into the batch.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property.<see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public abstract TransactionalBatch CreateItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to create an item into the batch.
        /// </summary>
        /// <param name="streamPayload">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
        public abstract TransactionalBatch CreateItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to read an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
        public abstract TransactionalBatch ReadItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to upsert an item into the batch.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public abstract TransactionalBatch UpsertItem<T>(
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to upsert an item into the batch.
        /// </summary>
        /// <param name="streamPayload">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
        public abstract TransactionalBatch UpsertItemStream(
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to replace an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public abstract TransactionalBatch ReplaceItem<T>(
            string id,
            T item,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to replace an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="streamPayload">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
        public abstract TransactionalBatch ReplaceItemStream(
            string id,
            Stream streamPayload,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to delete an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="TransactionalBatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="TransactionalBatch"/> instance with the operation added.</returns>
        public abstract TransactionalBatch DeleteItem(
            string id,
            TransactionalBatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Executes the batch at the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An awaitable <see cref="TransactionalBatchResponse"/> which contains the completion status and results of each operation.</returns>
        public abstract Task<TransactionalBatchResponse> ExecuteAsync(
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
