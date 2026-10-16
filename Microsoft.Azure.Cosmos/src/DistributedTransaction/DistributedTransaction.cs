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
        /// This method is single-use: it can only be called once per transaction instance.
        /// Once execution starts, the transaction instance is permanently consumed, even if the call
        /// fails or is canceled.
        /// <para>
        /// Cancellation stops client-side processing; it does not roll back writes that may have already
        /// reached the service. Cancellation or a network failure can leave the transaction's outcome unknown.
        /// Reconcile that outcome before resubmitting: a new transaction uses a new idempotency token and
        /// can apply the same writes twice.
        /// </para>
        /// </remarks>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe during client-side processing. Cancellation does not guarantee that a write transaction has not committed.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing a <see cref="DistributedTransactionResponse"/> that represents the result of the transaction.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="ExecuteTransactionAsync"/> has already been called on this instance.</exception>
        /// <exception cref="OperationCanceledException">Thrown when cancellation is observed before or during execution. A write transaction may still have committed.</exception>
        public abstract Task<DistributedTransactionResponse> ExecuteTransactionAsync(CancellationToken cancellationToken = default);
    }
}
