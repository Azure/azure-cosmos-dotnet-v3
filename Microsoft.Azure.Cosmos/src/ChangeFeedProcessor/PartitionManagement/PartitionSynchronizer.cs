//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;

    /// <summary>
    /// Read DocDB partitions and create leases if they do not exist
    /// </summary>
    internal abstract class PartitionSynchronizer
    {
        public abstract Task CreateMissingLeasesAsync();

        public abstract Task<IEnumerable<DocumentServiceLease>> SplitPartitionAsync(DocumentServiceLease lease);
    }
}