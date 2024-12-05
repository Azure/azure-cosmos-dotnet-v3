//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents.Rntbd;

    internal interface IAddressResolver
    {
        Task<PartitionAddressInformation> ResolveAsync(
            DocumentServiceRequest request,
            bool forceRefreshPartitionAddresses,
            CancellationToken cancellationToken);

        // Expected to be called once
        // Caller can replace the existing instance as idempotency 
        void Register(IConnectionStateListener connectionStateListener);
    }
}
