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
    /// <para>
    /// <b>Isolation semantics:</b> All reads execute under snapshot isolation so the results reflect a
    /// consistent point in time across participating partitions.
    /// </para>
    /// <para>
    /// <b>Result deserialization:</b> Use <see cref="DistributedTransactionResponse.GetOperationResultAtIndex{T}"/>
    /// to deserialize a read operation response body into a typed object.
    /// </para>
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
        /// <param name="container">The <see cref="Container"/> reference where the item exists.</param>
        /// <param name="partitionKey">The partition key for the item.</param>
        /// <param name="id">The unique identifier of the item to read.</param>
        /// <param name="requestOptions">Options for the read operation.</param>
        /// <returns>The current <see cref="DistributedReadTransaction"/> instance for method chaining.</returns>
        /// <remarks>
        /// The distributed transaction bypasses the per-container request pipeline. Only the database and
        /// container identifiers are extracted from <paramref name="container"/>; container-level behaviors
        /// such as custom serializers, client-side encryption policies, or decorator wrappers are not applied.
        /// </remarks>
        public abstract DistributedReadTransaction ReadItem(
            Container container,
            PartitionKey partitionKey,
            string id,
            DistributedTransactionRequestOptions requestOptions = null);
    }
}
