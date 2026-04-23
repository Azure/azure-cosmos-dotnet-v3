//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System.Threading.Tasks;

    /// <summary>
    /// The DocumentServiceLeaseStoreManager defines a way to perform operations with <see cref="DocumentServiceLease"/>.
    /// </summary>
    internal abstract class DocumentServiceLeaseStoreManager
    {
        /// <summary>
        /// Represents operations to get leases from lease store.
        /// </summary>
        public abstract DocumentServiceLeaseContainer LeaseContainer { get; }

        /// <summary>
        /// The DocumentServiceLeaseManager defines a way to perform operations with <see cref="DocumentServiceLease"/>.
        /// </summary>
        public abstract DocumentServiceLeaseManager LeaseManager { get; }

        /// <summary>
        /// Used to checkpoint leases.
        /// </summary>
        public abstract DocumentServiceLeaseCheckpointer LeaseCheckpointer { get; }

        /// <summary>
        /// Represents the lease store container to deal with initialiation/cleanup of leases
        /// for particular monitoring collection and lease container prefix.
        /// </summary>
        public abstract DocumentServiceLeaseStore LeaseStore { get; }

        /// <summary>
        /// Called when the processor is stopping. Allows implementations to perform
        /// cleanup or state persistence (e.g., exporting in-memory lease state).
        /// </summary>
        public abstract Task ShutdownAsync();
    }
}
