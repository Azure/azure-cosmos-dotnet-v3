//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedManagement
{
    internal abstract class PartitionSupervisorFactory
    {
        public abstract PartitionSupervisor Create(DocumentServiceLease lease);
    }
}