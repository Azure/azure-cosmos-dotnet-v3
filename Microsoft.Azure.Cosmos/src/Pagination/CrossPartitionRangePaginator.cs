// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Coordinates draining pages from multiple <see cref="PartitionRangePaginator"/>, while maintaining a global sort order and handling repartitioning (splits, merge).
    /// </summary>
    internal abstract class CrossPartitionRangePaginator
    {
        private readonly FeedRangeProvider feedRangeProvider;
        private readonly Func<FeedRange, State, PartitionRangePaginator> createPartitionRangePaginator;
        private readonly PriorityQueue<PartitionRangePaginator> paginators;

        public CrossPartitionRangePaginator(
            FeedRangeProvider feedRangeProvider,
            Func<FeedRange, State, PartitionRangePaginator> createPartitionRangePaginator,
            IEnumerable<PartitionRangePaginator> paginators,
            IComparer<PartitionRangePaginator> comparer)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(feedRangeProvider));
            this.createPartitionRangePaginator = createPartitionRangePaginator ?? throw new ArgumentNullException(nameof(createPartitionRangePaginator));

            if (paginators == null)
            {
                throw new ArgumentNullException(nameof(paginators));
            }

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(paginators));
            }

            this.paginators = new PriorityQueue<PartitionRangePaginator>(paginators, comparer);
        }

        public Page CurrentPage { get; set; }

        public async Task<TryCatch> TryMoveNextPageAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PartitionRangePaginator currentPaginator = this.paginators.Dequeue();
            TryCatch tryMoveNextPageAsync = await currentPaginator.TryMoveNextPageAsync(cancellationToken);
            if (tryMoveNextPageAsync.Failed)
            {
                Exception exception = tryMoveNextPageAsync.Exception;
                if (IsSplitException(exception))
                {
                    // Handle split
                    IEnumerable<FeedRange> childRanges = await this.feedRangeProvider.GetChildRangeAsync(currentPaginator.FeedRange, cancellationToken);
                    foreach (FeedRange childRange in childRanges)
                    {
                        PartitionRangePaginator childPaginator = this.createPartitionRangePaginator(childRange, currentPaginator.GetState());
                        this.paginators.Enqueue(childPaginator);
                    }

                    // Recursively retry
                    return await this.TryMoveNextPageAsync(cancellationToken);
                }

                if (IsMergeException(exception))
                {
                    throw new NotImplementedException();
                }

                return tryMoveNextPageAsync;
            }

            this.CurrentPage = currentPaginator.CurrentPage;
            this.paginators.Enqueue(currentPaginator);

            return TryCatch.FromResult();
        }

        private static bool IsSplitException(Exception exeception)
        {
            return exeception is CosmosException cosmosException
                && cosmosException.StatusCode == HttpStatusCode.Gone
                && cosmosException.SubStatusCode == (int)Documents.SubStatusCodes.PartitionKeyRangeGone;
        }

        private static bool IsMergeException(Exception exception)
        {
            // TODO: code this out
            return false;
        }
    }
}
