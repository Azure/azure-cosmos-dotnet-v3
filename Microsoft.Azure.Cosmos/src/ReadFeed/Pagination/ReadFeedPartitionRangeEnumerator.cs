//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Pagination
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class ReadFeedPartitionRangeEnumerator : PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>
    {
        private readonly IReadFeedDataSource readFeedDataSource;
        private readonly ReadFeedExecutionOptions readFeedPaginationOptions;

        public ReadFeedPartitionRangeEnumerator(
            IReadFeedDataSource readFeedDataSource,
            FeedRangeState<ReadFeedState> feedRangeState,
            ReadFeedExecutionOptions readFeedPaginationOptions)
            : base(feedRangeState)
        {
            this.readFeedDataSource = readFeedDataSource ?? throw new ArgumentNullException(nameof(readFeedDataSource));
            this.readFeedPaginationOptions = readFeedPaginationOptions;
        }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<ReadFeedPage>> GetNextPageAsync(ITrace trace, CancellationToken cancellationToken = default) => this.readFeedDataSource.MonadicReadFeedAsync(
            feedRangeState: this.FeedRangeState,
            readFeedPaginationOptions: this.readFeedPaginationOptions,
            trace: trace,
            cancellationToken: cancellationToken);
    }
}
