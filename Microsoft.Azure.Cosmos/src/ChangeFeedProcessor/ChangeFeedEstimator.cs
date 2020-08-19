//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;

    /// <summary>
    /// Used to estimate the pending work remaining to be read by a <see cref="ChangeFeedProcessor"/> deployment.
    /// </summary>
    /// <remarks>
    /// The estimator is meant to monitor an existing deployment of <see cref="ChangeFeedProcessor"/> instances that are currently running.
    /// </remarks>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/change-feed-processor"/>
    public abstract class ChangeFeedEstimator
    {
        /// <summary>
        /// Calculates an estimate of the pending work remaining to be read in the Change Feed in amount of documents in the whole container.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An estimation of pending work in amount of documents.</returns>
        public abstract Task<long> GetEstimatedRemainingWorkAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Calculates an estimate of the pending work remaining to be read in the Change Feed in amount of documents per distributed lease token.
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An array of an estimation of pending work in amount of documents per distributed lease token.</returns>
        public abstract Task<IReadOnlyList<RemainingLeaseWork>> GetEstimatedRemainingWorkPerLeaseTokenAsync(CancellationToken cancellationToken);
    }
}
