//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    /// <summary>
    /// Used to estimate the pending work remaining to be read by a <see cref="ChangeFeedProcessor"/> deployment.
    /// </summary>
    /// <remarks>
    /// The estimator is meant to monitor an existing deployment of <see cref="ChangeFeedProcessor"/> instances that are currently running.
    /// </remarks>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/change-feed-processor"/>
    /// <seealso href="https://docs.microsoft.com/azure/cosmos-db/how-to-use-change-feed-estimator"/>
    public abstract class ChangeFeedEstimator
    {
        /// <summary>
        /// Gets the estimation per lease in the lease container.
        /// </summary>
        /// <param name="changeFeedEstimatorRequestOptions">(Optional) Customize the estimation iterator.</param>
        /// <returns>An iterator that yields an estimation of pending work in amount of transactions per distributed lease token.</returns>
        /// <remarks>
        /// The estimation over the Change Feed identifies volumes of transactions. If operations in the container are performed through stored procedures, transactional batch or bulk, a group of operations may share the same <see href="https://docs.microsoft.com/azure/cosmos-db/stored-procedures-triggers-udfs#transactions">transaction scope</see> and represented by a single transaction. 
        /// In those cases, the estimation might not exactly represent number of items, but it is still valid to understand if the pending volume is increasing, decreasing, or on a steady state.
        /// </remarks>
        public abstract FeedIterator<ChangeFeedProcessorState> GetCurrentStateIterator(ChangeFeedEstimatorRequestOptions changeFeedEstimatorRequestOptions = null);
    }
}
