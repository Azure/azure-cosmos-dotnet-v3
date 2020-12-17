// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.ChangeFeed.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CosmosPagination = Microsoft.Azure.Cosmos.Pagination;

    internal class FullFidelityChangeFeedSplitStrategy : CosmosPagination.ISplitStrategy<ChangeFeedPage, ChangeFeedState>
    {
        private readonly CosmosPagination.IFeedRangeProvider feedRangeProvider;
        private readonly CosmosPagination.CreatePartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> partitionRangeEnumeratorCreator;

        public FullFidelityChangeFeedSplitStrategy(
            CosmosPagination.IFeedRangeProvider feedRangeProvider,
            CosmosPagination.CreatePartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> partitionRangeEnumeratorCreator)
        {
            this.feedRangeProvider = feedRangeProvider;
            this.partitionRangeEnumeratorCreator = partitionRangeEnumeratorCreator;
        }

        public async Task HandleSplitAsync(
            FeedRangeInternal range,
            ChangeFeedState state,
            CosmosPagination.IQueue<CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>> enumerators,
            CancellationToken cancellationToken)
        {
            List<FeedRangeEpk> childRanges = await this.feedRangeProvider.GetChildRangeAsync(range, cancellationToken: cancellationToken);
            if (childRanges.Count == 0)
            {
                throw new InvalidOperationException("Got back no children");
            }

            if (childRanges.Count == 1)
            {
                // We optimistically assumed that the cache is not stale.
                // In the event that it is (where we only get back one child / the partition that we think got split)
                // Then we need to refresh the cache
                await this.feedRangeProvider.RefreshProviderAsync(cancellationToken);
                childRanges = await this.feedRangeProvider.GetChildRangeAsync(range, cancellationToken: cancellationToken);
            }

            if (childRanges.Count() <= 1)
            {
                throw new InvalidOperationException("Expected more than 1 child");
            }

            foreach (FeedRangeInternal childRange in childRanges)
            {
                //childRange.GetPartitionKeyRangesAsync();

                CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> childPaginator =
                    this.partitionRangeEnumeratorCreator(childRange, state);
                enumerators.Enqueue(childPaginator);
            }
        }
    }
}
