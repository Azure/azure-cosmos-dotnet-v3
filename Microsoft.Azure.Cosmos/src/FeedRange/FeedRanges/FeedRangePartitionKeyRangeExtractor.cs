// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class FeedRangePartitionKeyRangeExtractor : IFeedRangeAsyncVisitor<IReadOnlyList<Documents.Routing.Range<string>>>
    {
        private readonly ContainerInternal container;

        public FeedRangePartitionKeyRangeExtractor(ContainerInternal container)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public async Task<IReadOnlyList<Documents.Routing.Range<string>>> VisitAsync(FeedRangePartitionKey feedRange, CancellationToken cancellationToken = default)
        {
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.container.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            PartitionKeyDefinition partitionKeyDefinition = await this.container.GetPartitionKeyDefinitionAsync(cancellationToken);
            return await feedRange.GetEffectiveRangesAsync(
                partitionKeyRangeCache,
                await this.container.GetCachedRIDAsync(
                    forceRefresh: false, 
                    NoOpTrace.Singleton, 
                    cancellationToken: cancellationToken),
                partitionKeyDefinition,
                NoOpTrace.Singleton);
        }

        public async Task<IReadOnlyList<Documents.Routing.Range<string>>> VisitAsync(FeedRangePartitionKeyRange feedRange, CancellationToken cancellationToken = default)
        {
            // Migration from PKRangeId scenario
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.container.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            return await feedRange.GetEffectiveRangesAsync(
                routingMapProvider: partitionKeyRangeCache,
                containerRid: await this.container.GetCachedRIDAsync(
                     forceRefresh: false, 
                     NoOpTrace.Singleton, 
                     cancellationToken: cancellationToken),
                partitionKeyDefinition: null,
                trace: NoOpTrace.Singleton);
        }

        public async Task<IReadOnlyList<Documents.Routing.Range<string>>> VisitAsync(FeedRangeEpk feedRange, CancellationToken cancellationToken = default)
        {
            Routing.PartitionKeyRangeCache partitionKeyRangeCache = await this.container.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
            IReadOnlyList<PartitionKeyRange> pkRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                collectionRid: await this.container.GetCachedRIDAsync(
                    forceRefresh: false,
                    NoOpTrace.Singleton, 
                    cancellationToken: cancellationToken),
                range: feedRange.Range,
                trace: NoOpTrace.Singleton,
                forceRefresh: false);
            return pkRanges.Select(pkRange => pkRange.ToRange()).ToList();
        }
    }
}
