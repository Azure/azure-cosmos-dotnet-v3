//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
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

        Task UpdateAsync(
           IReadOnlyList<AddressCacheToken> addressCacheTokens,
           CancellationToken cancellationToken = default(CancellationToken));

        Task UpdateAsync(
           ServerKey serverKey,
           CancellationToken cancellationToken = default(CancellationToken));
    }
}
