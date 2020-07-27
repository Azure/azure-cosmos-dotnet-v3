//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Used to estimate the pending work remaining to be read in the Change Feed. Calculates the sum of pending work based on the difference between the latest status of the feed and the status of each existing lease.
    /// </summary>
    internal abstract class RemainingWorkEstimator
    {
        /// <summary>
        /// Calculates an estimate of the pending work remaining to be read in the Change Feed in amount of documents in the whole collection.
        /// </summary>
        /// <returns>An estimation of pending work in amount of documents.</returns>
        public abstract Task<long> GetEstimatedRemainingWorkAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Calculates an estimate of the pending work remaining to be read in the Change Feed in amount of documents per distributed lease token.
        /// </summary>
        /// <returns>An array of an estimation of pending work in amount of documents per distributed lease token.</returns>
        public abstract Task<IReadOnlyList<RemainingLeaseWork>> GetEstimatedRemainingWorkPerLeaseTokenAsync(CancellationToken cancellationToken);
    }
}
