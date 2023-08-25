//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents operations to get leases from lease store.
    /// </summary>
    internal abstract class DocumentServiceLeaseContainer
    {
        /// <summary>
        /// Get all leases.
        /// </summary>
        /// <returns>List of all leases</returns>
        public abstract Task<IReadOnlyList<DocumentServiceLease>> GetAllLeasesAsync();

        /// <summary>
        /// Get all the leases owned by the current host.
        /// </summary>
        /// <returns>Enumerable of all leases owned by the current host</returns>
        public abstract Task<IEnumerable<DocumentServiceLease>> GetOwnedLeasesAsync();
    }
}
