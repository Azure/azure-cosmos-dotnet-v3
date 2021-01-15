// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;

    internal interface IChangeFeedDataSource : IMonadicChangeFeedDataSource
    {
        Task<ChangeFeedPage> ChangeFeedAsync(
            ChangeFeedState state,
            FeedRangeInternal feedRange,
            int pageSize,
            ChangeFeedMode changeFeedMode,
            ITrace trace,
            CancellationToken cancellationToken);
    }
}
