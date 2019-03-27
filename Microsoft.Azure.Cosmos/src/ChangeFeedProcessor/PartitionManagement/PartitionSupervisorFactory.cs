//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    internal abstract class PartitionSupervisorFactory
    {
        public abstract PartitionSupervisor Create(DocumentServiceLease lease);
    }
}