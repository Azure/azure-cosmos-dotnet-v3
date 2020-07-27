//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;

    /// <summary>
    /// Remaining estimated work on the lease.
    /// </summary>
    public class RemainingLeaseWork
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemainingLeaseWork"/> class.
        /// </summary>
        /// <param name="feedRange">The <see cref="Cosmos.FeedRange"/> associated with the current lease.</param>
        /// <param name="remainingWork">The amount of documents remaining to be processed</param>
        internal RemainingLeaseWork(FeedRange feedRange, long remainingWork)
        {
            this.FeedRange = feedRange ?? throw new ArgumentNullException(nameof(feedRange));
            this.RemainingWork = remainingWork;
        }

        /// <summary>
        /// Gets the lease token for which the remaining work is calculated
        /// </summary>
        public virtual FeedRange FeedRange { get; }

        /// <summary>
        /// Gets the amount of documents remaining to be processed.
        /// </summary>
        public virtual long RemainingWork { get; }
    }
}