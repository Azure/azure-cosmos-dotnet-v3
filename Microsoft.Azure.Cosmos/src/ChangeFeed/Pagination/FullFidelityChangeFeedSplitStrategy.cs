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
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using CosmosPagination = Microsoft.Azure.Cosmos.Pagination;

    internal class FullFidelityChangeFeedSplitStrategy : CosmosPagination.DefaultSplitStrategy<ChangeFeedPage, ChangeFeedState>
    {
        private readonly IChangeFeedDataSource dataSource;
        private readonly ChangeFeedPaginationOptions paginationOptions;
        private readonly CancellationToken cancellationToken;

        public FullFidelityChangeFeedSplitStrategy(
            CosmosPagination.IFeedRangeProvider feedRangeProvider,
            IChangeFeedDataSource dataSource,
            ChangeFeedPaginationOptions paginationOptions,
            CancellationToken cancellationToken)
            : base(
                  feedRangeProvider,
                  ChangeFeedPartitionRangePageAsyncEnumerator.MakeCreateFunction(dataSource, paginationOptions, cancellationToken))
        {
            this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            this.paginationOptions = paginationOptions ?? throw new ArgumentNullException(nameof(paginationOptions));
            this.cancellationToken = cancellationToken;
        }

        public override async Task HandleSplitAsync(
            CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> currentEnumerator,
            CosmosPagination.IQueue<CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>> enumerators,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            // For 'start from now' we don't need to go back in time to get changes from archival partiton(s).
            if (currentEnumerator.FeedRangeState.State == ChangeFeedState.Now())
            {
                await base.HandleSplitAsync(currentEnumerator, enumerators, trace, cancellationToken);
                return;
            }

            List<FeedRangeArchivalPartition> archivalRanges = await this.feedRangeProvider.GetArchivalRangesAsync(
                currentEnumerator.FeedRangeState.FeedRange,
                trace,
                cancellationToken);

            if (archivalRanges == null || archivalRanges.Count == 0)
            {
                await base.HandleSplitAsync(currentEnumerator, enumerators, trace, cancellationToken);
                return;
            }

            if (currentEnumerator is ChangeFeedArchivalRangePageAsyncEnumerator existingArchivalEnumerator)
            {
                // Check that archival range is drained -- ArchivalEnumerator would throw Gone Exception.
                if (existingArchivalEnumerator.IsDrained)
                {
                    SplitGraph splitGraph = existingArchivalEnumerator.ArchivalRange.SplitGraph;

                    foreach (SplitGraphNode childNode in splitGraph.Root.Children)
                    {
                        if (childNode.Children.Count == 0)
                        {
                            // TODO: check other states validated in default strategy.
                            PartitionKeyRange childPkRange = splitGraph.LeafRanges[childNode.PartitionKeyRangeId];
                            FeedRangeInternal childRange = new FeedRangeEpk(
                                new Documents.Routing.Range<string>(
                                    min: childPkRange.MinInclusive,
                                    max: childPkRange.MaxExclusive,
                                    isMinInclusive: true,
                                    isMaxInclusive: false));

                            CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> childPaginator =
                                this.partitionRangeEnumeratorCreator(
                                    new CosmosPagination.FeedRangeState<ChangeFeedState>(childRange, currentEnumerator.FeedRangeState.State));

                            enumerators.Enqueue(childPaginator);
                        }
                        else
                        {
                            // We need to drain the child who got split as wells. Example:
                            // 1 -> 2
                            //   -> 3(1) -> 4(1) -- this means that 3 used to have archival store for 1 and 4 has archival store for 3.
                            //           -> 5(3)
                            // At this point we are finishing 1 and are at child corresponding to 3(1).
                            FeedRangeArchivalPartition archivalChild = new FeedRangeArchivalPartition(
                                splitGraph.Root.PartitionKeyRangeId.ToString(),
                                new SplitGraph(childNode, splitGraph.LeafRanges));

                            ChangeFeedArchivalRangePageAsyncEnumerator archivalEnumerator = new ChangeFeedArchivalRangePageAsyncEnumerator(
                                this.dataSource,
                                archivalChild,
                                currentEnumerator.FeedRangeState,
                                this.paginationOptions,
                                cancellationToken);

                            enumerators.Enqueue(archivalEnumerator);
                        }
                    }
                }
                else
                {
                    // TODO: FFCF: implement.
                    throw new NotImplementedException("TODO: add support for split of partition that owns archival one.");
                }
            }
            else
            {
                ChangeFeedArchivalRangePageAsyncEnumerator archivalEnumerator = new ChangeFeedArchivalRangePageAsyncEnumerator(
                    this.dataSource,
                    archivalRanges[0],
                    currentEnumerator.FeedRangeState,
                    this.paginationOptions,
                    cancellationToken);

                enumerators.Enqueue(archivalEnumerator);
            }
        }
    }
}
