//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    internal abstract class PartitionSupervisorFactory
    {
        public abstract PartitionSupervisor Create(DocumentServiceLease lease);
    }
}