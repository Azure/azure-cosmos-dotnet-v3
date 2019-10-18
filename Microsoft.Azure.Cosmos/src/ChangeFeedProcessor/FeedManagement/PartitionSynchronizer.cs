//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Read DocDB partitions and create leases if they do not exist
    /// </summary>
    internal abstract class PartitionSynchronizer
    {
        public abstract Task CreateMissingLeasesAsync();

        public abstract Task<IEnumerable<DocumentServiceLease>> SplitPartitionAsync(DocumentServiceLease lease);
    }
}