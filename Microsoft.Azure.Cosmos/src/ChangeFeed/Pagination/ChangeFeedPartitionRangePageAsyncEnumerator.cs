// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class ChangeFeedPartitionRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>
    {
        private readonly IChangeFeedDataSource changeFeedDataSource;
        private readonly ChangeFeedPaginationOptions changeFeedPaginationOptions;

        public ChangeFeedPartitionRangePageAsyncEnumerator(
            IChangeFeedDataSource changeFeedDataSource,
            FeedRangeState<ChangeFeedState> feedRangeState,
            ChangeFeedPaginationOptions changeFeedPaginationOptions,
            CancellationToken cancellationToken)
            : base(feedRangeState, cancellationToken)
        {
            this.changeFeedDataSource = changeFeedDataSource ?? throw new ArgumentNullException(nameof(changeFeedDataSource));
            this.changeFeedPaginationOptions = changeFeedPaginationOptions ?? throw new ArgumentNullException(nameof(changeFeedPaginationOptions));
        }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<ChangeFeedPage>> GetNextPageAsync(
            ITrace trace, 
            CancellationToken cancellationToken) => this.changeFeedDataSource.MonadicChangeFeedAsync(
            this.FeedRangeState,
            this.changeFeedPaginationOptions,
            trace,
            cancellationToken);
    }
}
