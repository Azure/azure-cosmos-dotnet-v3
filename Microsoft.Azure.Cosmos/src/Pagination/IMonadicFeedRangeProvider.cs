// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    internal interface IMonadicFeedRangeProvider
    {
        Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken);

        Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(
            CancellationToken cancellationToken);
    }
}
