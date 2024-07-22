// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal interface IChangeFeedDataSource : IMonadicChangeFeedDataSource
    {
        Task<ChangeFeedPage> ChangeFeedAsync(
            FeedRangeState<ChangeFeedState> feedRangeState,
            ChangeFeedExecutionOptions changeFeedPaginationOptions,
            ITrace trace,
            CancellationToken cancellationToken);
    }
}
