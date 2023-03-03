//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IAddressResolver
    {
        Task<PartitionAddressInformation> ResolveAsync(
            DocumentServiceRequest request,
            bool forceRefreshPartitionAddresses,
            CancellationToken cancellationToken);
    }
}
