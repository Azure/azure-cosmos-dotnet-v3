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
    using CosmosPagination = Microsoft.Azure.Cosmos.Pagination;

    internal class FullFidelityChangeFeedSplitStrategy : CosmosPagination.DefaultSplitStrategy<ChangeFeedPage, ChangeFeedState>
    {
        public FullFidelityChangeFeedSplitStrategy(
            CosmosPagination.IFeedRangeProvider feedRangeProvider,
            CosmosPagination.CreatePartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> partitionRangeEnumeratorCreator)
            : base(feedRangeProvider, partitionRangeEnumeratorCreator)
        {
        }

        public override async Task HandleSplitAsync(
            FeedRangeInternal range,
            ChangeFeedState state,
            CosmosPagination.IQueue<CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>> enumerators,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            // Check how many parent partitions. If 1 partition -- go to archival rerefence.

            List<FeedRangeEpk> childRanges = await this.GetAndValidateChildRangesAsync(range, trace, cancellationToken);

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
