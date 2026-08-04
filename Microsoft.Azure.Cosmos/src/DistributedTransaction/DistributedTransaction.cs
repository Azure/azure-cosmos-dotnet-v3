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
        /// Commits the distributed transaction.
        /// </summary>
        /// <remarks>
        /// This method is single-use: it can only be called once per transaction instance.
        /// If the call fails for any reason (including transient network failures or cancellation),
        /// the transaction instance is permanently consumed. To retry, construct a new transaction
        /// with the same operations.
        /// </remarks>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing a <see cref="DistributedTransactionResponse"/> that represents the result of the transaction.</returns>
        /// <exception cref="InvalidOperationException">Thrown if <see cref="ExecuteTransactionAsync"/> has already been called on this instance.</exception>
        /// <exception cref="OperationCanceledException">Thrown if <paramref name="cancellationToken"/> is cancelled before or during the commit.</exception>
        public abstract Task<DistributedTransactionResponse> ExecuteTransactionAsync(CancellationToken cancellationToken = default);
    }
}
