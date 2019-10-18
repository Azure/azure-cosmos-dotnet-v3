//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System.Threading.Tasks;

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
