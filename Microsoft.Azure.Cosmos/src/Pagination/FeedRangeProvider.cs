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

    internal sealed class FeedRangeProvider : IFeedRangeProvider
    {
        private readonly CosmosQueryClient cosmosQueryClient;
        private readonly string collectionRid;

        public FeedRangeProvider(CosmosQueryClient cosmosQueryClient, string collectionRid)
        {
            this.cosmosQueryClient = cosmosQueryClient ?? throw new ArgumentNullException(nameof(cosmosQueryClient));
            this.collectionRid = collectionRid ?? throw new ArgumentNullException(nameof(collectionRid));
        }

        public async Task<IEnumerable<FeedRangeInternal>> GetChildRangeAsync(
            FeedRangeInternal feedRange,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (feedRange == null)
            {
                throw new ArgumentNullException(nameof(feedRange));
            }

            if (!(feedRange is FeedRangeEpk feedRangeEpk))
            {
                throw new ArgumentOutOfRangeException(nameof(feedRange));
            }

            IReadOnlyList<Documents.PartitionKeyRange> replacementRanges = await this.cosmosQueryClient.TryGetOverlappingRangesAsync(
                this.collectionRid,
                new Documents.Routing.Range<string>(feedRangeEpk.Range.Min, feedRangeEpk.Range.Max, isMaxInclusive: true, isMinInclusive: false),
                forceRefresh: true);

            List<FeedRangeInternal> childFeedRanges = new List<FeedRangeInternal>(replacementRanges.Count);

            foreach (Documents.PartitionKeyRange replacementRange in replacementRanges)
            {
                childFeedRanges.Add(new FeedRangeEpk(replacementRange.ToRange()));
            }

            return childFeedRanges;
        }

        public Task<IEnumerable<FeedRangeInternal>> GetFeedRangesAsync(
            CancellationToken cancellationToken) => this.GetChildRangeAsync(
                FeedRangeEpk.FullRange,
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
