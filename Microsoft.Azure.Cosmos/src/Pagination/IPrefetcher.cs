// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IPrefetcher
    {
        ValueTask PrefetchAsync(CancellationToken cancellationToken);
    }
}
