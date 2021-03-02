// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;

    internal interface IMonadicFeedRangeProvider
    {
        Task<TryCatch<List<FeedRangeEpkRange>>> MonadicGetChildRangeAsync(
            FeedRangeInternal feedRange,
            ITrace trace,
            CancellationToken cancellationToken);

        Task<TryCatch<List<FeedRangeEpkRange>>> MonadicGetFeedRangesAsync(
            ITrace trace,
            CancellationToken cancellationToken);

        Task<TryCatch> MonadicRefreshProviderAsync(
            ITrace trace, 
            CancellationToken cancellationToken);
    }
}
