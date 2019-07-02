//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;

    internal abstract class PartitionController
    {
        public abstract Task AddOrUpdateLeaseAsync(DocumentServiceLease lease);

        public abstract Task InitializeAsync();

        public abstract Task ShutdownAsync();
    }
}