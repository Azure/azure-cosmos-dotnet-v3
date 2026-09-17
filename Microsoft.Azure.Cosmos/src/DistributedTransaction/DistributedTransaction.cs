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
#if PREVIEW
    public
#else
    internal
#endif
    abstract class DistributedTransaction
    {
        /// <summary>
        /// Gets the idempotency token of the latest commit attempt that reached the dispatch boundary.
        /// </summary>
        /// <remarks>
        /// <see cref="Guid.Empty"/> until the first attempt is dispatched. Because the token is published
        /// before the dispatch is awaited, it remains observable even when <see cref="ExecuteTransactionAsync"/>
        /// throws <see cref="OperationCanceledException"/> — including cancellation during an in-flight dispatch.
        /// </remarks>
        internal virtual Guid IdempotencyToken => Guid.Empty;

        /// <summary>
        /// Commits the distributed transaction.
        /// </summary>
        /// <remarks>
        /// Add all operations before calling this method; later additions are not submitted.
        /// An empty transaction is rejected without consuming the instance. Otherwise, the instance is
        /// single-use, even if execution fails or is cancelled. Create a new transaction for further operations.
        /// Before resubmitting writes after an unknown outcome, verify the resulting state to avoid duplicates.
        /// Concurrent use of the transaction builder is not supported.
        /// </remarks>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing a <see cref="DistributedTransactionResponse"/> that represents the result of the transaction.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the transaction has no operations or execution has already started on this instance.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled before or during the commit.</exception>
        public abstract Task<DistributedTransactionResponse> ExecuteTransactionAsync(CancellationToken cancellationToken = default);
    }
}
