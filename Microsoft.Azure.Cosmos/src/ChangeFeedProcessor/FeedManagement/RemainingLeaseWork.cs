//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;

    /// <summary>
    /// Remaining estimated work on the lease.
    /// </summary>
    public sealed class RemainingLeaseWork
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemainingLeaseWork"/> class.
        /// </summary>
        /// <param name="leaseToken">The lease token that identifies this lease.</param>
        /// <param name="remainingWork">The amount of documents remaining to be processed</param>
        /// <param name="instanceName">The instance currently owning the lease.</param>
        public RemainingLeaseWork(
            string leaseToken,
            long remainingWork,
            string instanceName)
        {
            this.LeaseToken = leaseToken ?? throw new ArgumentNullException(nameof(leaseToken));
            this.RemainingWork = remainingWork;
            this.InstanceName = instanceName;
        }

        /// <summary>
        /// Gets the lease token for which the remaining work is calculated
        /// </summary>
        public string LeaseToken { get; }

        /// <summary>
        /// Gets the amount of documents remaining to be processed.
        /// </summary>
        public long RemainingWork { get; }

        /// <summary>
        /// Gets the name of the instance owning the lease currently.
        /// </summary>
        /// <remarks>
        /// Leases can be in a released state and not being owned by any instance on a particular moment in time, in which case, this value is null.
        /// </remarks>
        public string InstanceName { get; }
    }
}