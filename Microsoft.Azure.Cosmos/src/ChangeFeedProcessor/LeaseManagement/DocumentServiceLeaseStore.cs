//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the lease store container to deal with initialiation/cleanup of leases
    /// for particular monitoring collection and lease container prefix.
    /// </summary>
    public abstract class DocumentServiceLeaseStore
    {
        /// <summary>
        /// Checks if the lease store is initialized.
        /// </summary>
        public abstract Task<bool> IsInitializedAsync();

        /// <summary>
        /// Mark the store as initialized.
        /// </summary>
        public abstract Task MarkInitializedAsync();

        /// <summary>
        /// Places a lock on the lease store for initialization. Only one process may own the store for the lock time.
        /// </summary>
        /// <param name="lockExpirationTime">The time for the lock to expire.</param>
        /// <returns>True if the lock was acquired, false otherwise.</returns>
        /// <remarks>In order for expiration time work, lease colection needs to have TTL enabled.</remarks>
        public abstract Task<bool> AcquireInitializationLockAsync(TimeSpan lockExpirationTime);

        /// <summary>
        /// Releases the lock one the lease store for initialization.
        /// </summary>
        /// <returns>True if the lock was acquired and was relesed, false if the lock was not acquired.</returns>
        public abstract Task<bool> ReleaseInitializationLockAsync();
    }
}