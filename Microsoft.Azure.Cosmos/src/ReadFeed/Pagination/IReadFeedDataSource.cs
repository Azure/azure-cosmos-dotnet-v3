// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IReadFeedDataSource : IMonadicReadFeedDataSource
    {
        Task<ReadFeedPage> ReadFeedAsync(
            ReadFeedState readFeedState,
            FeedRangeInternal feedRange,
            QueryRequestOptions queryRequestOptions,
            int pageSize,
            CancellationToken cancellationToken);
    }
}
