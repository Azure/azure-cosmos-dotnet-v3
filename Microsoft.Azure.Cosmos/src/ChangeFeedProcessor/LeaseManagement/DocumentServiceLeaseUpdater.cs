//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement
{
    using System;
    using System.Threading.Tasks;

    internal abstract class DocumentServiceLeaseUpdater
    {
        public abstract Task<DocumentServiceLease> UpdateLeaseAsync(DocumentServiceLease cachedLease, string leaseId, object leasePartitionKey, Func<DocumentServiceLease, DocumentServiceLease> updateLease);
    }
}