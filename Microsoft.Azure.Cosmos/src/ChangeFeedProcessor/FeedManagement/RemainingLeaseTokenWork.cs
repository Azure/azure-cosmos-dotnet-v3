//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;

    /// <summary>
    /// Remaining estimated work on the lease token
    /// </summary>
    public class RemainingLeaseTokenWork
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemainingLeaseTokenWork"/> class.
        /// </summary>
        /// <param name="leaseToken">The lease token for which the remaining work is calculated</param>
        /// <param name="remainingWork">The amount of documents remaining to be processed</param>
        internal RemainingLeaseTokenWork(FeedToken leaseToken, long remainingWork)
        {
            if (leaseToken == null) throw new ArgumentNullException(nameof(leaseToken));

            this.LeaseToken = leaseToken;
            this.RemainingWork = remainingWork;
        }

        /// <summary>
        /// Gets the lease token for which the remaining work is calculated
        /// </summary>
        public virtual FeedToken LeaseToken { get; }

        /// <summary>
        /// Gets the amount of documents remaining to be processed.
        /// </summary>
        public virtual long RemainingWork { get; }
    }
}