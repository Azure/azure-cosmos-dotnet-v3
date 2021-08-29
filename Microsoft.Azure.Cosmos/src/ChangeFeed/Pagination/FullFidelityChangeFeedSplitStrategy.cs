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
            CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> currentEnumerator,
            CosmosPagination.IQueue<CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState>> enumerators,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            // For 'start from now' we don't need to over back in time and get changes from archival partiton(s).
            if (currentEnumerator.FeedRangeState.State == ChangeFeedState.Now())
            {
                await base.HandleSplitAsync(currentEnumerator, enumerators, trace, cancellationToken);
                return;
            }

            List<FeedRangeArchivalPartition> archivalRanges = await this.feedRangeProvider.GetArchivalRangesAsync(
                currentEnumerator.FeedRangeState.FeedRange,
                trace,
                cancellationToken);

            //CosmosPagination.PartitionRangePageAsyncEnumerator<ChangeFeedPage, ChangeFeedState> archivalPaginator =
            //    this.partitionRangeEnumeratorCreator(new CosmosPagination.FeedRangeState<ChangeFeedState>(
            //        archivalRange,
            //        currentEnumerator.FeedRangeState.State));
            //enumerators.Enqueue(archivalPaginator);

            // TODO: this needs to be done after archival partition is drained.
            // Continue handle split for child partitions. Default strategy knows how to do that.
            await base.HandleSplitAsync(currentEnumerator, enumerators, trace, cancellationToken);

            //ContainerProperties containerProperties = await this.clientContext.GetCachedContainerPropertiesAsync(
            //    this.containerUri,
            //    trace,
            //    cancellationToken);

            //////this.clientContext.Client.

            //IRoutingMapProvider routingMapProvider = await this.clientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);

            //List<Range<string>> ranges = await this.feedRangeProvider.GetEffectiveRangesAsync(
            //    routingMapProvider, collectionResourceId, partitionKeyDefinition, trace);

            //return await this.GetTargetPartitionKeyRangesAsync(
            //    resourceLink,
            //    collectionResourceId,
            //    ranges,
            //    forceRefresh,
            //    childTrace);

            //List<PartitionKeyRange> overlappingRanges = await this.cosmosQueryClient.GetTargetPartitionKeyRangeByFeedRangeAsync(
            //    this.container.LinkUri,
            //    await this.container.GetCachedRIDAsync(forceRefresh: false, trace, cancellationToken: cancellationToken),
            //    containerProperties.PartitionKey,
            //    feedRange,
            //    forceRefresh: false,
            //    trace);
            //return TryCatch<List<FeedRangeEpk>>.FromResult(
            //    overlappingRanges.Select(range => new FeedRangeEpk(
            //        new Documents.Routing.Range<string>(
            //            min: range.MinInclusive,
            //            max: range.MaxExclusive,
            //            isMinInclusive: true,
            //            isMaxInclusive: false))).ToList());

            //// Check how many parent partitions. If 1 partition -- go to archival rerefence.
        }
    }
}
