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
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A <see cref="Task{TResult}"/> containing a <see cref="DistributedTransactionResponse"/> that represents the result of the transaction.</returns>
        public abstract Task<DistributedTransactionResponse> CommitTransactionAsync(CancellationToken cancellationToken);
    }
}
