//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;

    /// <summary>
    /// The DocumentServiceLeaseManager defines a way to perform operations with <see cref="DocumentServiceLease"/>.
    /// </summary>
    internal abstract class DocumentServiceLeaseManager
    {
        /// <summary>
        /// Checks whether the lease exists and creates it if it does not exist.
        /// </summary>
        /// <param name="feedRange">Feed range for the lease.</param>
        /// <param name="continuationToken">Continuation token if it exists.</param>
        public abstract Task<DocumentServiceLease> CreateLeaseIfNotExistAsync(FeedRangeInternal feedRange, string continuationToken);

        /// <summary>
        /// Delete the lease.
        /// </summary>
        /// <param name="lease">Lease to remove.</param>
        public abstract Task DeleteAsync(DocumentServiceLease lease);

        /// <summary>
        /// Acquire ownership of the lease.
        /// </summary>
        /// <param name="lease">Lease to acquire.</param>
        /// <returns>Updated acquired lease.</returns>
        /// <exception cref="LeaseLostException">Thrown if other host acquired concurrently</exception>
        public abstract Task<DocumentServiceLease> AcquireAsync(DocumentServiceLease lease);

        /// <summary>
        /// Release ownership of the lease.
        /// </summary>
        /// <param name="lease">Lease to acquire.</param>
        /// <exception cref="LeaseLostException">Thrown if other host acquired the lease or lease was deleted</exception>
        public abstract Task ReleaseAsync(DocumentServiceLease lease);

        /// <summary>
        /// Renew the lease. Leases are periodically renewed to prevent expiration.
        /// </summary>
        /// <param name="lease">Lease to renew.</param>
        /// <returns>Updated renewed lease.</returns>
        /// <exception cref="LeaseLostException">Thrown if other host acquired the lease or lease was deleted</exception>
        public abstract Task<DocumentServiceLease> RenewAsync(DocumentServiceLease lease);

        /// <summary>
        /// Replace properties from the specified lease.
        /// </summary>
        /// <param name="leaseToUpdatePropertiesFrom">Lease containing new properties</param>
        /// <returns>Updated lease.</returns>
        /// <exception cref="LeaseLostException">Thrown if other host acquired the lease</exception>
        public abstract Task<DocumentServiceLease> UpdatePropertiesAsync(DocumentServiceLease leaseToUpdatePropertiesFrom);
    }
}
