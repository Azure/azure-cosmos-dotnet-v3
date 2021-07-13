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
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using CosmosPagination = Microsoft.Azure.Cosmos.Pagination;

    internal class FullFidelityChangeFeedSplitStrategy : CosmosPagination.DefaultSplitStrategy<ChangeFeedPage, ChangeFeedState>
    {
        private readonly CosmosClientContext clientContext;

        public FullFidelityChangeFeedSplitStrategy(
            CosmosPagination.IFeedRangeProvider feedRangeProvider,
            CosmosPagination.CreatePartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> partitionRangeEnumeratorCreator,
            CosmosClientContext clientContext)
            : base(feedRangeProvider, partitionRangeEnumeratorCreator)
        {
            if (feedRangeProvider == null) throw new ArgumentNullException(nameof(feedRangeProvider));
            if (partitionRangeEnumeratorCreator == null) throw new ArgumentNullException(nameof(partitionRangeEnumeratorCreator));
            if (clientContext == null) throw new ArgumentNullException(nameof(clientContext));

            this.clientContext = clientContext;
        }

        public override async Task HandleSplitAsync(
            CosmosPagination.FeedRangeState<ChangeFeedState> rangeState,
            CosmosPagination.IQueue<CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>> enumerators,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ContainerProperties containerProperties = await this.clientContext.GetCachedContainerPropertiesAsync(
                this.containerUri,
                trace,
                cancellationToken);

            //this.clientContext.Client.

            IRoutingMapProvider routingMapProvider = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            List<Range<string>> ranges = await this.feedRangeProvider.GetEffectiveRangesAsync(
                routingMapProvider, collectionResourceId, partitionKeyDefinition, trace);

            return await this.GetTargetPartitionKeyRangesAsync(
                resourceLink,
                collectionResourceId,
                ranges,
                forceRefresh,
                childTrace);



            List<PartitionKeyRange> overlappingRanges = await this.cosmosQueryClient.GetTargetPartitionKeyRangeByFeedRangeAsync(
                this.container.LinkUri,
                await this.container.GetCachedRIDAsync(forceRefresh: false, trace, cancellationToken: cancellationToken),
                containerProperties.PartitionKey,
                feedRange,
                forceRefresh: false,
                trace);
            return TryCatch<List<FeedRangeEpk>>.FromResult(
                overlappingRanges.Select(range => new FeedRangeEpk(
                    new Documents.Routing.Range<string>(
                        min: range.MinInclusive,
                        max: range.MaxExclusive,
                        isMinInclusive: true,
                        isMaxInclusive: false))).ToList());

            // Check how many parent partitions. If 1 partition -- go to archival rerefence.

            // TODO: remove this line.
            List<FeedRangeEpk> allRanges = await this.feedRangeProvider.GetFeedRangesAsync(trace, cancellationToken);
            
            List<FeedRangeEpk> childRanges = await this.GetAndValidateChildRangesAsync(rangeState.FeedRange, trace, cancellationToken);

            foreach (FeedRangeInternal childRange in childRanges)
            {
                //childRange.GetPartitionKeyRangesAsync();

                CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> childPaginator =
                    this.partitionRangeEnumeratorCreator(new CosmosPagination.FeedRangeState<ChangeFeedState>(childRange, rangeState.State));
                enumerators.Enqueue(childPaginator);
            }
        }
    }
}
