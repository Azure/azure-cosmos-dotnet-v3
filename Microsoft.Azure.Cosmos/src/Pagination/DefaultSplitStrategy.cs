// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultSplitStrategy<TPage, TState> : ISplitStrategy<TPage, TState>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly IFeedRangeProvider feedRangeProvider;
        private readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> partitionRangeEnumeratorCreator;

        public DefaultSplitStrategy(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> partitionRangeEnumeratorCreator)
        {
            this.feedRangeProvider = feedRangeProvider;
            this.partitionRangeEnumeratorCreator = partitionRangeEnumeratorCreator;
        }

        public async Task HandleSplitAsync(
            FeedRangeInternal range,
            TState state,
            IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> enumerators,
            CancellationToken cancellationToken)
        {
            List<FeedRangeEpk> allRanges = await this.feedRangeProvider.GetFeedRangesAsync(cancellationToken);

            List<FeedRangeEpk> childRanges = await this.feedRangeProvider.GetChildRangeAsync(
                range,
                cancellationToken: cancellationToken);
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
                childRanges = await this.feedRangeProvider.GetChildRangeAsync(
                    range,
                    cancellationToken: cancellationToken);
            }

            if (childRanges.Count() <= 1)
            {
                throw new InvalidOperationException("Expected more than 1 child");
            }

            foreach (FeedRangeInternal childRange in childRanges)
            {
                PartitionRangePageAsyncEnumerator<TPage, TState> childPaginator = this.partitionRangeEnumeratorCreator(
                    childRange,
                    state);
                enumerators.Enqueue(childPaginator);
            }
        }
    }
}
