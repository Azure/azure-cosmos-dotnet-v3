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
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Default split strategy just enqueues PartitionRangePageAsyncEnumerator for child ranges.
    /// </summary>
    internal class DefaultSplitStrategy<TPage, TState> : ISplitStrategy<TPage, TState>
        where TPage : Page<TState>
        where TState : State
    {
        protected readonly IFeedRangeProvider feedRangeProvider;
        protected readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> partitionRangeEnumeratorCreator;

        public DefaultSplitStrategy(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> partitionRangeEnumeratorCreator)
        {
            this.feedRangeProvider = feedRangeProvider;
            this.partitionRangeEnumeratorCreator = partitionRangeEnumeratorCreator;
        }

        public virtual async Task HandleSplitAsync(
            FeedRangeState<TState> rangeState,
            IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> enumerators,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            // TODO: remove this line.
            List<FeedRangeEpk> allRanges = await this.feedRangeProvider.GetFeedRangesAsync(trace, cancellationToken);

            List<FeedRangeEpk> childRanges = await this.GetAndValidateChildRangesAsync(rangeState.FeedRange, trace, cancellationToken);

            foreach (FeedRangeInternal childRange in childRanges)
            {
                PartitionRangePageAsyncEnumerator<TPage, TState> childPaginator = this.partitionRangeEnumeratorCreator(
                    new FeedRangeState<TState>(childRange, rangeState.State));
                enumerators.Enqueue(childPaginator);
            }
        }

        protected async Task<List<FeedRangeEpk>> GetAndValidateChildRangesAsync(
            FeedRangeInternal range,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            List<FeedRangeEpk> childRanges = await this.feedRangeProvider.GetChildRangeAsync(range, trace, cancellationToken);
            if (childRanges.Count == 0)
            {
                throw new InvalidOperationException("Got back no children");
            }

            if (childRanges.Count == 1)
            {
                // We optimistically assumed that the cache is not stale.
                // In the event that it is (where we only get back one child / the partition that we think got split)
                // Then we need to refresh the cache
                await this.feedRangeProvider.RefreshProviderAsync(trace, cancellationToken);
                childRanges = await this.feedRangeProvider.GetChildRangeAsync(range, trace, cancellationToken);
            }

            if (childRanges.Count() <= 1)
            {
                throw new InvalidOperationException("Expected more than 1 child");
            }

            return childRanges;
        }
    }
}
