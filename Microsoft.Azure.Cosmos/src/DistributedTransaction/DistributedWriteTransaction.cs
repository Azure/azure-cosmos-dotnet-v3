// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Represents a distributed transaction that supports write operations across multiple partitions and containers.
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract class DistributedWriteTransaction : DistributedTransaction
    {
        /// <summary>
        /// Adds a create operation to the distributed transaction.
        /// </summary>
        /// <typeparam name="T">The type of the resource to create.</typeparam>
        /// <param name="database">The name of the database containing the container.</param>
        /// <param name="collection">The name of the container where the item will be created.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to create.</param>
        /// <param name="resource">The resource to create.</param>
        /// <param name="requestOptions">Options for the create operation.</param>
        /// <returns>The current <see cref="DistributedWriteTransaction"/> instance for method chaining.</returns>
        public abstract DistributedWriteTransaction CreateItem<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null);

        /// <summary>
        /// Adds a create operation to the distributed transaction using a pre-serialized JSON stream.
        /// </summary>
        /// <param name="database">The name of the database containing the container.</param>
        /// <param name="collection">The name of the container where the item will be created.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to create.</param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the JSON serialization of the item.</param>
        /// <param name="requestOptions">Options for the create operation.</param>
        /// <returns>The current <see cref="DistributedWriteTransaction"/> instance for method chaining.</returns>
        public abstract DistributedWriteTransaction CreateItemStream(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null);

        /// <summary>
        /// Adds a replace operation to the distributed transaction.
        /// </summary>
        /// <typeparam name="T">The type of the resource to replace.</typeparam>
        /// <param name="database">The name of the database containing the container.</param>
        /// <param name="collection">The name of the container where the item exists.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to replace.</param>
        /// <param name="resource">The resource with updated values.</param>
        /// <param name="requestOptions">Options for the replace operation.</param>
        /// <returns>The current <see cref="DistributedWriteTransaction"/> instance for method chaining.</returns>
        public abstract DistributedWriteTransaction ReplaceItem<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null);

        /// <summary>
        /// Adds a replace operation to the distributed transaction using a pre-serialized JSON stream.
        /// </summary>
        /// <param name="database">The name of the database containing the container.</param>
        /// <param name="collection">The name of the container where the item exists.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to replace.</param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the JSON serialization of the replacement item.</param>
        /// <param name="requestOptions">Options for the replace operation.</param>
        /// <returns>The current <see cref="DistributedWriteTransaction"/> instance for method chaining.</returns>
        public abstract DistributedWriteTransaction ReplaceItemStream(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null);

        /// <summary>
        /// Adds a delete operation to the distributed transaction.
        /// </summary>
        /// <param name="database">The name of the database containing the container.</param>
        /// <param name="collection">The name of the container where the item exists.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to delete.</param>
        /// <param name="requestOptions">Options for the delete operation.</param>
        /// <returns>The current <see cref="DistributedWriteTransaction"/> instance for method chaining.</returns>
        public abstract DistributedWriteTransaction DeleteItem(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null);

        /// <summary>
        /// Adds a patch operation to the distributed transaction.
        /// </summary>
        /// <param name="database">The name of the database containing the container.</param>
        /// <param name="collection">The name of the container where the item exists.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to patch.</param>
        /// <param name="patchOperations">The list of <see cref="PatchOperation"/> to apply to the item.</param>
        /// <param name="requestOptions">Options for the patch operation</param>
        /// <returns>The current <see cref="DistributedWriteTransaction"/> instance for method chaining.</returns>
        public abstract DistributedWriteTransaction PatchItem(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            IReadOnlyList<PatchOperation> patchOperations,
            DistributedTransactionRequestOptions requestOptions = null);

        /// <summary>
        /// Adds a patch operation to the distributed transaction using a pre-serialized JSON patch stream.
        /// </summary>
        /// <param name="database">The name of the database containing the container.</param>
        /// <param name="collection">The name of the container where the item exists.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to patch.</param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the JSON serialization of the patch operations.</param>
        /// <param name="requestOptions">Options for the patch operation.</param>
        /// <returns>The current <see cref="DistributedWriteTransaction"/> instance for method chaining.</returns>
        public abstract DistributedWriteTransaction PatchItemStream(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an upsert operation to the distributed transaction.
        /// If the item exists, it will be replaced; otherwise, it will be created.
        /// </summary>
        /// <typeparam name="T">The type of the resource to upsert.</typeparam>
        /// <param name="database">The name of the database containing the container.</param>
        /// <param name="collection">The name of the container where the item will be upserted.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to upsert.</param>
        /// <param name="resource">The resource to upsert.</param>
        /// <param name="requestOptions">Options for the upsert operation.</param>
        /// <returns>The current <see cref="DistributedWriteTransaction"/> instance for method chaining.</returns>
        public abstract DistributedWriteTransaction UpsertItem<T>(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            T resource,
            DistributedTransactionRequestOptions requestOptions = null);

        /// <summary>
        /// Adds an upsert operation to the distributed transaction using a pre-serialized JSON stream.
        /// If the item exists, it will be replaced; otherwise, it will be created.
        /// </summary>
        /// <param name="database">The name of the database containing the container.</param>
        /// <param name="collection">The name of the container where the item will be upserted.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to upsert.</param>
        /// <param name="streamPayload">A <see cref="Stream"/> containing the JSON serialization of the item.</param>
        /// <param name="requestOptions">Options for the upsert operation.</param>
        /// <returns>The current <see cref="DistributedWriteTransaction"/> instance for method chaining.</returns>
        public abstract DistributedWriteTransaction UpsertItemStream(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            Stream streamPayload,
            DistributedTransactionRequestOptions requestOptions = null);
    }
}
