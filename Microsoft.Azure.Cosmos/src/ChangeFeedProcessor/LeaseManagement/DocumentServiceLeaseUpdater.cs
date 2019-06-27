//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Threading.Tasks;

    internal abstract class DocumentServiceLeaseUpdater
    {
        public abstract Task<DocumentServiceLease> UpdateLeaseAsync(DocumentServiceLease cachedLease, string leaseId, PartitionKey leasePartitionKey, Func<DocumentServiceLease, DocumentServiceLease> updateLease);
    }
}