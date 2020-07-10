// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Documents;

    internal sealed class FeedRangeProvider : IFeedRangeProvider
    {
        private static readonly PartitionKeyRange FullRange = new PartitionKeyRange()
        {
            MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
            MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
        };

        private readonly CosmosQueryClient cosmosQueryClient;
        private readonly string collectionRid;

        public FeedRangeProvider(CosmosQueryClient cosmosQueryClient, string collectionRid)
        {
            this.cosmosQueryClient = cosmosQueryClient ?? throw new ArgumentNullException(nameof(cosmosQueryClient));
            this.collectionRid = collectionRid ?? throw new ArgumentNullException(nameof(collectionRid));
        }

        public Task<IEnumerable<PartitionKeyRange>> GetChildRangeAsync(
            PartitionKeyRange partitionKeyRange,
            CancellationToken cancellationToken) => this.cosmosQueryClient.TryGetOverlappingRangesAsync(
                this.collectionRid,
                partitionKeyRange.ToRange(),
                forceRefresh: true)
                .ContinueWith((task) => (IEnumerable<PartitionKeyRange>)task.Result);

        public Task<IEnumerable<PartitionKeyRange>> GetFeedRangesAsync(
            CancellationToken cancellationToken) => this.GetChildRangeAsync(
                FeedRangeProvider.FullRange,
                cancellationToken);

        public Task<FeedRangeEpk> ToEffectivePartitionKeyRangeAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<FeedRangePartitionKeyRange> ToPhysicalPartitionKeyRangeAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
