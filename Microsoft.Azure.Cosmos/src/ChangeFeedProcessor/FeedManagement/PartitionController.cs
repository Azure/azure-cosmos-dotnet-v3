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

    internal abstract class PartitionController
    {
        public abstract Task AddOrUpdateLeaseAsync(DocumentServiceLease lease);

        public abstract Task InitializeAsync();

        public abstract Task ShutdownAsync();
    }
}