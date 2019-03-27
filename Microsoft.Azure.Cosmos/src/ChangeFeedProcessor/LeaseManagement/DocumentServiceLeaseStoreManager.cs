//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement
{
    /// <summary>
    /// The DocumentServiceLease defines a way to perform operations with <see cref="DocumentServiceLease"/>.
    /// </summary>
    public abstract class DocumentServiceLeaseStoreManager
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
    }
}
