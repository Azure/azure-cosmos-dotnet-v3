//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;

    internal abstract class PartitionSupervisorFactory
    {
        public abstract PartitionSupervisor Create(DocumentServiceLease lease);
    }
}