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
            PartitionRangePageAsyncEnumerator<TPage, TState> currentEnumerator,
            IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> enumerators,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            // TODO: remove this line.
            List<FeedRangeEpk> allRanges = await this.feedRangeProvider.GetFeedRangesAsync(trace, cancellationToken);

            List<FeedRangeEpk> childRanges = await this.feedRangeProvider.GetChildRangeAsync(
                currentEnumerator.FeedRangeState.FeedRange,
                trace,
                cancellationToken);

            if (childRanges.Count <= 1)
            {
                // We optimistically assumed that the cache is not stale.
                // In the event that it is (where we only get back one child / the partition that we think got split)
                // Then we need to refresh the cache
                await this.feedRangeProvider.RefreshProviderAsync(trace, cancellationToken);
                childRanges = await this.feedRangeProvider.GetChildRangeAsync(
                    currentEnumerator.FeedRangeState.FeedRange,
                    trace,
                    cancellationToken);
            }

            if (childRanges.Count < 1)
            {
                string errorMessage = "SDK invariant violated 4795CC37: Must have at least one EPK range in a cross partition enumerator";
                throw Resource.CosmosExceptions.CosmosExceptionFactory.CreateInternalServerErrorException(
                    message: errorMessage,
                    headers: null,
                    stackTrace: null,
                    trace: trace,
                    error: new Microsoft.Azure.Documents.Error { Code = "SDK_invariant_violated_4795CC37", Message = errorMessage });
            }

            if (childRanges.Count == 1)
            {
                // On a merge, the 410/1002 results in a single parent
                // We maintain the current enumerator's range and let the RequestInvokerHandler logic kick in
                enumerators.Enqueue(currentEnumerator);
            }
            else
            {
                // Split
                foreach (FeedRangeInternal childRange in childRanges)
                {
                    PartitionRangePageAsyncEnumerator<TPage, TState> childPaginator = this.partitionRangeEnumeratorCreator(
                        new FeedRangeState<TState>(childRange, currentEnumerator.FeedRangeState.State));
                    enumerators.Enqueue(childPaginator);
                }
            }
        }
    }
}
