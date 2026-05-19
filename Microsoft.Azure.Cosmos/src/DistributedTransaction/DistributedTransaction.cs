// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a distributed transaction that will be performed across partitions and/or collections. 
    /// </summary>
#if INTERNAL
    public
#else
    internal
#endif
    abstract class DistributedTransaction
    {
        /// <summary>
        /// Commits the distributed transaction.
        /// </summary>
        /// <remarks>
        /// This method is single-use: it can only be called once per transaction instance.
        /// Each call generates a unique idempotency token that the server uses for duplicate
        /// detection during the SDK's internal retries. A second call would generate a new
        /// token and bypass that server-side duplicate detection, risking a double-commit.
        /// <para>
        /// If the call fails for any reason (including transient network failures, cancellation,
        /// or non-retriable server errors), the transaction instance is permanently consumed.
        /// To retry, construct a new transaction with the same operations. When the previous
        /// commit's outcome is unknown, verify the resulting state before retrying to avoid
        /// duplicate writes.
        /// </para>
        /// </remarks>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing a <see cref="DistributedTransactionResponse"/> that represents the result of the transaction.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="CommitTransactionAsync"/> has already been called on this instance.</exception>
        public abstract Task<DistributedTransactionResponse> CommitTransactionAsync(CancellationToken cancellationToken = default);
    }
}
