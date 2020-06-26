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
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;

    /// <summary>
    /// Coordinates draining pages from multiple <see cref="PartitionRangePageEnumerator"/>, while maintaining a global sort order and handling repartitioning (splits, merge).
    /// </summary>
    internal sealed class CrossPartitionRangePageEnumerator : IAsyncEnumerator<TryCatch<Page>>
    {
        private readonly IFeedRangeProvider feedRangeProvider;
        private readonly CreatePartitionRangePageEnumerator createPartitionRangeEnumerator;
        private readonly AsyncLazy<PriorityQueue<PartitionRangePageEnumerator>> lazyEnumerators;
        private readonly State originalState;

        public CrossPartitionRangePageEnumerator(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageEnumerator createPartitionRangeEnumerator,
            IComparer<PartitionRangePageEnumerator> comparer,
            State state = default)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(feedRangeProvider));
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            this.originalState = state;

            this.lazyEnumerators = new AsyncLazy<PriorityQueue<PartitionRangePageEnumerator>>(async (CancellationToken token) =>
            {
                List<(FeedRange, State)> rangeAndStates;
                if (state == default)
                {
                    // Fan out to all partitions with default state
                    IEnumerable<FeedRange> ranges = await feedRangeProvider.GetFeedRangesAsync(token);

                    rangeAndStates = new List<(FeedRange, State)>();
                    foreach (FeedRange range in ranges)
                    {
                        rangeAndStates.Add((range, default));
                    }
                }
                else
                {
                    if (!(state is CrossPartitionState crossPartitionState))
                    {
                        throw new ArgumentOutOfRangeException(nameof(state));
                    }

                    rangeAndStates = crossPartitionState.Value;
                }

                PriorityQueue<PartitionRangePageEnumerator> enumerators = new PriorityQueue<PartitionRangePageEnumerator>(comparer);
                foreach ((FeedRange range, State rangeState) in rangeAndStates)
                {
                    PartitionRangePageEnumerator enumerator = createPartitionRangeEnumerator(range, rangeState);
                    enumerators.Enqueue(enumerator);
                }

                return enumerators;
            });
        }

        public TryCatch<Page> Current { get; private set; }

        public State GetState()
        {
            if (!this.lazyEnumerators.ValueInitialized)
            {
                return this.originalState;
            }

            PriorityQueue<PartitionRangePageEnumerator> enumerators = this.lazyEnumerators.Result;
            List<(FeedRange, State)> feedRangeAndStates = new List<(FeedRange, State)>(enumerators.Count);
            foreach (PartitionRangePageEnumerator enumerator in enumerators)
            {
                feedRangeAndStates.Add((enumerator.Range, enumerator.State));
            }

            return new CrossPartitionState(feedRangeAndStates);
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            PriorityQueue<PartitionRangePageEnumerator> enumerators = await this.lazyEnumerators.GetValueAsync(cancellationToken: default);
            PartitionRangePageEnumerator currentPaginator = enumerators.Dequeue();
            bool movedNext = await currentPaginator.MoveNextAsync();
            if (!movedNext)
            {
                return false;
            }

            if (currentPaginator.Current.Failed)
            {
                // Check if it's a retryable exception.
                Exception exception = currentPaginator.Current.Exception;
                while (exception.InnerException != null)
                {
                    exception = exception.InnerException;
                }

                if (IsSplitException(exception))
                {
                    // Handle split
                    IEnumerable<FeedRange> childRanges = await this.feedRangeProvider.GetChildRangeAsync(currentPaginator.Range);
                    foreach (FeedRange childRange in childRanges)
                    {
                        PartitionRangePageEnumerator childPaginator = this.createPartitionRangeEnumerator(childRange, currentPaginator.State);
                        enumerators.Enqueue(childPaginator);
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
            enumerators.Enqueue(currentPaginator);

            return true;
        }

        public ValueTask DisposeAsync()
        {
            // Do Nothing.
            return default;
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

        private sealed class CrossPartitionState : State
        {
            public CrossPartitionState(List<(FeedRange, State)> value)
            {
                this.Value = value;
            }

            public List<(FeedRange, State)> Value { get; }
        }
    }
}
