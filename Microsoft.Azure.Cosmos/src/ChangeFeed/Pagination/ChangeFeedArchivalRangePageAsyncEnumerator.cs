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
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Used with Full-Fildelity Change Feed to iterate over archival partition as part of Log Store split handling.
    /// </summary>
    internal sealed class ChangeFeedArchivalRangePageAsyncEnumerator : PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>
    {
        private readonly IChangeFeedDataSource dataSource;
        private readonly FeedRangeArchivalPartition archivalRange;
        private readonly ChangeFeedState state;
        private readonly ChangeFeedPaginationOptions paginationOptions;

        public ChangeFeedArchivalRangePageAsyncEnumerator(
            IChangeFeedDataSource dataSource,
            FeedRangeArchivalPartition archivalRange,
            FeedRangeState<ChangeFeedState> feedRangeState,
            ChangeFeedPaginationOptions paginationOptions,
            CancellationToken cancellationToken)
            : base(feedRangeState, cancellationToken)
        {
            this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            this.archivalRange = archivalRange ?? throw new ArgumentNullException(nameof(archivalRange));
            this.state = feedRangeState.State;
            this.paginationOptions = paginationOptions ?? throw new ArgumentNullException(nameof(paginationOptions));
        }

        public override ValueTask DisposeAsync() => default;

        public bool IsDrained { get; private set; }

        public FeedRangeArchivalPartition ArchivalRange => this.ArchivalRange;

        protected override async Task<TryCatch<ChangeFeedPage>> GetNextPageAsync(
            ITrace trace,
            CancellationToken cancellationToken)
        {
            // Each archival range provides the following:
            // - routing range id
            // - archival range id (via graph) -- this will be used for actual data
            // - original EPK range

            // If this throws, FFCFSplitStrategy will take care of the rest.
            TryCatch<ChangeFeedPage> result = await this.dataSource.MonadicChangeFeedAsync(
                new FeedRangeState<ChangeFeedState>(this.archivalRange, this.state),
                this.paginationOptions,
                trace,
                cancellationToken);

            // Check if archival range is drained.
            if (result.Succeeded && result.Result is ChangeFeedNotModifiedPage)
            {
                this.IsDrained = true;

                CosmosException goneException = new CosmosException(
                    message: $"Archival Range: {this.archivalRange.DataRangeId} (routed to {this.archivalRange.RoutingPartitionKeyRangeId}) is drained.",
                    statusCode: System.Net.HttpStatusCode.Gone,
                    subStatusCode: (int)SubStatusCodes.PartitionKeyRangeGone,
                    activityId: Guid.NewGuid().ToString(),
                    requestCharge: default);

                return TryCatch<ChangeFeedPage>.FromException(goneException);
            }

            // Note: check for failures and throw to upper layer.
            return result;
        }
    }
}
