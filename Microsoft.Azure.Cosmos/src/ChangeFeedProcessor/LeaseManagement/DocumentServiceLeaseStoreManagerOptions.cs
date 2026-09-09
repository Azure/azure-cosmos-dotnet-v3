//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;

    internal class DocumentServiceLeaseStoreManagerOptions
    {
        private const string PartitionLeasePrefixSeparator = "..";

        internal string ContainerNamePrefix { get; set; }

        internal string HostName { get; set; }

        internal string GetPartitionLeasePrefix()
        {
            return this.ContainerNamePrefix + PartitionLeasePrefixSeparator;
        }

        internal ChangeFeedMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the start time to persist on leases during acquisition.
        /// When set, this value is stored on every lease during AcquireAsync to ensure
        /// it survives processor restarts even for partitions that never checkpoint.
        /// A null value means "clear the start time from the lease".
        /// </summary>
        internal DateTime? StartTime { get; set; }

        /// <summary>
        /// Gets or sets whether the StartTime was explicitly set by the user.
        /// When false, the start time on the lease is not modified during acquisition.
        /// </summary>
        internal bool IsStartTimeUserExplicit { get; set; }
    }
}
