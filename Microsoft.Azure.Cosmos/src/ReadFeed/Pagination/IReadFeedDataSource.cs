// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal interface IReadFeedDataSource : IMonadicReadFeedDataSource
    {
        Task<ReadFeedPage> ReadFeedAsync(
            FeedRangeState<ReadFeedState> feedRangeState,
            ReadFeedExecutionOptions readFeedPaginationOptions,
            ITrace trace,
            CancellationToken cancellationToken);
    }
}
