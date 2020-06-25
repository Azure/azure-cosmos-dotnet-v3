// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Coordinates draining pages from multiple <see cref="PartitionRangePageEnumerator"/>, while maintaining a global sort order and handling repartitioning (splits, merge).
    /// </summary>
    internal sealed class CrossPartitionRangePageEnumerator : IAsyncEnumerator<TryCatch<Page>>
    {
        private readonly FeedRangeProvider feedRangeProvider;
        private readonly Func<FeedRange, State, PartitionRangePageEnumerator> createPartitionRangePaginator;
        private readonly PriorityQueue<PartitionRangePageEnumerator> paginators;

        public CrossPartitionRangePageEnumerator(
            FeedRangeProvider feedRangeProvider,
            Func<FeedRange, State, PartitionRangePageEnumerator> createPartitionRangePaginator,
            IEnumerable<PartitionRangePageEnumerator> paginators,
            IComparer<PartitionRangePageEnumerator> comparer)
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

            this.paginators = new PriorityQueue<PartitionRangePageEnumerator>(paginators, comparer);
        }

        public TryCatch<Page> Current { get; private set; }

        public async ValueTask<bool> MoveNextAsync()
        {
            PartitionRangePageEnumerator currentPaginator = this.paginators.Dequeue();
            bool movedNext = await currentPaginator.MoveNextAsync();
            if (!movedNext)
            {
                return false;
            }

            if (currentPaginator.Current.Failed)
            {
                // Check if it's a retryable exception.
                Exception exception = currentPaginator.Current.Exception;
                if (IsSplitException(exception))
                {
                    // Handle split
                    IEnumerable<FeedRange> childRanges = await this.feedRangeProvider.GetChildRangeAsync(currentPaginator.Range);
                    foreach (FeedRange childRange in childRanges)
                    {
                        PartitionRangePageEnumerator childPaginator = this.createPartitionRangePaginator(childRange, currentPaginator.GetState());
                        this.paginators.Enqueue(childPaginator);
                    }

                    // Recursively retry
                    return await this.MoveNextAsync();
                }

                if (IsMergeException(exception))
                {
                    throw new NotImplementedException();
                }
            }

            this.Current = currentPaginator.Current;
            this.paginators.Enqueue(currentPaginator);

            return true;
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
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
