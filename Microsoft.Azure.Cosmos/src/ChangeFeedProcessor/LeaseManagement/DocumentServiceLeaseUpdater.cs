//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System;
    using System.Threading.Tasks;

    internal abstract class DocumentServiceLeaseUpdater
    {
        public abstract Task<DocumentServiceLease> UpdateLeaseAsync(DocumentServiceLease cachedLease, string leaseId, PartitionKey leasePartitionKey, Func<DocumentServiceLease, DocumentServiceLease> updateLease);
    }
}