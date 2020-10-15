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

    internal sealed class ReadFeedPartitionRangeEnumerator : PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>
    {
        private readonly IReadFeedDataSource readFeedDataSource;
        private readonly int pageSize;

        public ReadFeedPartitionRangeEnumerator(
            IReadFeedDataSource readFeedDataSource,
            FeedRangeInternal feedRange,
            int pageSize,
            CancellationToken cancellationToken,
            ReadFeedState state = null)
            : base(
                  feedRange,
                  cancellationToken,
                  state)
        {
            this.readFeedDataSource = readFeedDataSource ?? throw new ArgumentNullException(nameof(readFeedDataSource));
            this.pageSize = pageSize;
        }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<ReadFeedPage>> GetNextPageAsync(CancellationToken cancellationToken = default) => this.readFeedDataSource.MonadicReadFeedAsync(
            feedRange: this.Range,
            readFeedState: this.State,
            pageSize: this.pageSize,
            cancellationToken: cancellationToken);
    }
}
