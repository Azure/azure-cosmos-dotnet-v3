//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Change Feed processor state for a particular range of partition keys.
    /// </summary>
    public sealed class ChangeFeedProcessorState
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeFeedProcessorState"/> class.
        /// </summary>
        /// <param name="leaseToken">The lease token that identifies this lease.</param>
        /// <param name="estimatedLag">The amount of documents remaining to be processed</param>
        /// <param name="instanceName">The instance currently owning the lease.</param>
        public ChangeFeedProcessorState(
            string leaseToken,
            long estimatedLag,
            string instanceName)
        {
            this.LeaseToken = leaseToken ?? throw new ArgumentNullException(nameof(leaseToken));
            this.EstimatedLag = estimatedLag;
            this.InstanceName = instanceName;
        }

        /// <summary>
        /// Gets the lease token for which the state is calculated
        /// </summary>
        public string LeaseToken { get; }

        /// <summary>
        /// Gets an approximation of the difference between the last processed transaction in the feed container and the latest transaction recorded.
        /// </summary>
        /// <remarks>
        /// The estimation over the Change Feed identifies volumes of transactions. If operations in the container are performed through stored procedures, transactional batch or bulk, a group of operations may share the same <see href="https://docs.microsoft.com/azure/cosmos-db/stored-procedures-triggers-udfs#transactions">transaction scope</see> and represented by a single transaction. 
        /// In those cases, the estimation might not exactly represent number of items, but it is still valid to understand if the pending volume is increasing, decreasing, or on a steady state.
        /// </remarks>
        public long EstimatedLag { get; }

        /// <summary>
        /// Gets the name of the instance currently owning the lease.
        /// </summary>
        /// <remarks>
        /// Leases can be in a released state and not being owned by any instance on a particular moment in time, in which case, this value is null.
        /// </remarks>
        public string InstanceName { get; }
    }
}