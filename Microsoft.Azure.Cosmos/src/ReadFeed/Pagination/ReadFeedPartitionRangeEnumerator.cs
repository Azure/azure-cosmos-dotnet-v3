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
    using Microsoft.Azure.Documents;

    internal sealed class ReadFeedPartitionRangeEnumerator : PartitionRangePageAsyncEnumerator<ReadFeedPage, ReadFeedState>
    {
        private readonly IReadFeedDataSource readFeedDataSource;
        private readonly QueryDefinition queryDefinition;
        private readonly QueryRequestOptions queryRequestOptions;
        private readonly string resourceLink;
        private readonly ResourceType resourceType;
        private readonly int pageSize;

        public ReadFeedPartitionRangeEnumerator(
            IReadFeedDataSource readFeedDataSource,
            FeedRangeInternal feedRange,
            QueryDefinition queryDefinition,
            QueryRequestOptions queryRequestOptions,
            string resourceLink,
            ResourceType resourceType,
            int pageSize,
            CancellationToken cancellationToken,
            ReadFeedState state = null)
            : base(
                  feedRange,
                  cancellationToken,
                  state)
        {
            this.readFeedDataSource = readFeedDataSource ?? throw new ArgumentNullException(nameof(readFeedDataSource));
            this.queryDefinition = queryDefinition;
            this.queryRequestOptions = queryRequestOptions;
            this.resourceLink = resourceLink;
            this.resourceType = resourceType;
            this.pageSize = pageSize;
        }

        public override ValueTask DisposeAsync() => default;

        protected override Task<TryCatch<ReadFeedPage>> GetNextPageAsync(CancellationToken cancellationToken = default) => this.readFeedDataSource.MonadicReadFeedAsync(
            feedRange: this.Range,
            readFeedState: this.State,
            queryDefinition: this.queryDefinition,
            queryRequestOptions: this.queryRequestOptions,
            resourceLink: this.resourceLink,
            resourceType: this.resourceType,
            pageSize: this.pageSize,
            cancellationToken: cancellationToken);
    }
}
