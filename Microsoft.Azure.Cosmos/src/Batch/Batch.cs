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
#if PREVIEW
    public
#else
    internal
#endif
    abstract class Batch
    {
        /// <summary>
        /// Adds an operation to create an item into the batch.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property.<see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public abstract Batch CreateItem<T>(
            T item,
            BatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to create an item into the batch.
        /// </summary>
        /// <param name="streamPayload">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public abstract Batch CreateItemStream(
            Stream streamPayload,
            BatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to read an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public abstract Batch ReadItem(
            string id,
            BatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to upsert an item into the batch.
        /// </summary>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public abstract Batch UpsertItem<T>(
            T item,
            BatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to upsert an item into the batch.
        /// </summary>
        /// <param name="streamPayload">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public abstract Batch UpsertItemStream(
            Stream streamPayload,
            BatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to replace an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="item">A JSON serializable object that must contain an id property. <see cref="CosmosSerializer"/> to implement a custom serializer.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        /// <typeparam name="T">The type of item to be created.</typeparam>
        public abstract Batch ReplaceItem<T>(
            string id,
            T item,
            BatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to replace an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="streamPayload">
        /// A <see cref="Stream"/> containing the payload of the item.
        /// The stream must have a UTF-8 encoded JSON object which contains an id property.
        /// </param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public abstract Batch ReplaceItemStream(
            string id,
            Stream streamPayload,
            BatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an operation to delete an item into the batch.
        /// </summary>
        /// <param name="id">The cosmos item id.</param>
        /// <param name="requestOptions">(Optional) The options for the item request. <see cref="BatchItemRequestOptions"/>.</param>
        /// <returns>The <see cref="Batch"/> instance with the operation added.</returns>
        public abstract Batch DeleteItem(
            string id,
            BatchItemRequestOptions requestOptions = null);

        /// <summary>
        /// Executes the batch at the Azure Cosmos service as an asynchronous operation.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An awaitable <see cref="BatchResponse"/> which contains the completion status and results of each operation.</returns>
        public abstract Task<BatchResponse> ExecuteAsync(
            CancellationToken cancellationToken = default(CancellationToken));
    }
}
