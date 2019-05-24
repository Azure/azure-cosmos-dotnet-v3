//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;

    /// <summary>
    /// Used to checkpoint leases.
    /// </summary>
    internal abstract class DocumentServiceLeaseCheckpointer
    {
        /// <summary>
        /// Checkpoint the lease.
        /// </summary>
        /// <param name="lease">Lease to renew</param>
        /// <param name="continuationToken">Continuation token</param>
        /// <returns>Updated renewed lease</returns>
        /// <exception cref="LeaseLostException">Thrown if other host acquired the lease or lease was deleted</exception>
        public abstract Task<DocumentServiceLease> CheckpointAsync(DocumentServiceLease lease, string continuationToken);
    }
}
