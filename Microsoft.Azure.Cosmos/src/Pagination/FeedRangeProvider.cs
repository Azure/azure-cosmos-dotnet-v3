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

    internal sealed class FeedRangeProvider
    {
        private readonly CosmosQueryClient cosmosQueryClient;
        private readonly string collectionRid;

        public FeedRangeProvider(CosmosQueryClient cosmosQueryClient, string collectionRid)
        {
            this.cosmosQueryClient = cosmosQueryClient ?? throw new ArgumentNullException(nameof(cosmosQueryClient));
            this.collectionRid = collectionRid ?? throw new ArgumentNullException(nameof(collectionRid));
        }

        public async Task<IEnumerable<FeedRange>> GetChildRangeAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (feedRange == null)
            {
                throw new ArgumentNullException(nameof(feedRange));
            }

            if (!(feedRange is FeedRangeEpk feedRangeEPK))
            {
                throw new ArgumentOutOfRangeException(nameof(feedRange));
            }

            IReadOnlyList<Documents.PartitionKeyRange> replacementRanges = await this.cosmosQueryClient.TryGetOverlappingRangesAsync(
                this.collectionRid,
                new Documents.Routing.Range<string>(feedRangeEPK.Range.Min, feedRangeEPK.Range.Max, isMaxInclusive: true, isMinInclusive: false),
                forceRefresh: true);

            List<FeedRange> childFeedRanges = new List<FeedRange>(replacementRanges.Count);

            foreach (Documents.PartitionKeyRange replacementRange in replacementRanges)
            {
                childFeedRanges.Add(new FeedRangeEpk(replacementRange.ToRange()));
            }

            return childFeedRanges;
        }
    }
}
