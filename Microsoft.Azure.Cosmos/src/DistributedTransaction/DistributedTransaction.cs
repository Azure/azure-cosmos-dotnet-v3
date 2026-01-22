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
        public abstract Task<DistributedTransactionResponse> CommitTransactionAsync(CancellationToken cancellationToken);
    }
}
