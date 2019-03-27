//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;

    internal abstract class PartitionController
    {
        public abstract Task AddOrUpdateLeaseAsync(DocumentServiceLease lease);

        public abstract Task InitializeAsync();

        public abstract Task ShutdownAsync();
    }
}