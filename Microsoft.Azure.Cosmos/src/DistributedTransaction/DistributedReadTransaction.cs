// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.IO;

    /// <summary>
    /// Represents a distributed transaction that supports read operations across multiple partitions and containers.
    /// </summary>
    /// <remarks>
    /// Use <see cref="CosmosClient.CreateDistributedReadTransaction"/> to obtain an instance.
    /// Add read operations using <see cref="ReadItem"/> then call
    /// <see cref="DistributedTransaction.CommitTransactionAsync"/> to execute all reads atomically.
    /// </remarks>
#if INTERNAL
    public
#else
    internal
#endif
    abstract class DistributedReadTransaction : DistributedTransaction
    {
        /// <summary>
        /// Adds a read operation to the distributed transaction.
        /// </summary>
        /// <param name="database">The name of the database containing the container.</param>
        /// <param name="collection">The name of the container where the item exists.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to read.</param>
        /// <param name="requestOptions">Options for the read operation.</param>
        /// <returns>The current <see cref="DistributedReadTransaction"/> instance for method chaining.</returns>
        public abstract DistributedReadTransaction ReadItem(
            string database,
            string collection,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null);
    }
}
