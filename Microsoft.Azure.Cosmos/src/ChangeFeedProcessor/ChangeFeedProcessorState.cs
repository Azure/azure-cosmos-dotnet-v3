//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Change Feed processor state for a particular range of partition keys.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    sealed class ChangeFeedProcessorState
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
        /// Gets an approximation of the difference between the last processed item in the feed container and the latest change recorded.
        /// </summary>
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