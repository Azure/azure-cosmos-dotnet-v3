// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal interface IFeedRangeProvider : IMonadicFeedRangeProvider
    {
        Task<List<FeedRangeEpk>> GetChildRangeAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken);

        Task<List<FeedRangeEpk>> GetFeedRangesAsync(
            CancellationToken cancellationToken);
    }
}
