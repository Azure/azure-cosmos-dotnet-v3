// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Coordinates draining pages from multiple <see cref="PartitionRangePageAsyncEnumerator{TPage, TState}"/>, while maintaining a global sort order and handling repartitioning (splits, merge).
    /// </summary>
    internal sealed class CrossPartitionRangePageAsyncEnumerator<TPage, TState> : IAsyncEnumerator<TryCatch<CrossPartitionPage<TPage, TState>>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly IFeedRangeProvider feedRangeProvider;
        private readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator;
        private readonly AsyncLazy<PriorityQueue<PartitionRangePageAsyncEnumerator<TPage, TState>>> lazyEnumerators;

        public CrossPartitionRangePageAsyncEnumerator(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator,
            IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer,
            int? maxConcurrency,
            CrossPartitionState<TState> state = default)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(feedRangeProvider));
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));

            if (comparer == null)
            {
                throw new ArgumentNullException(nameof(comparer));
            }

            this.lazyEnumerators = new AsyncLazy<PriorityQueue<PartitionRangePageAsyncEnumerator<TPage, TState>>>(async (CancellationToken token) =>
            {
                IReadOnlyList<(PartitionKeyRange, TState)> rangeAndStates;
                if (state != default)
                {
                    rangeAndStates = state.Value;
                }
                else
                {
                    // Fan out to all partitions with default state
                    IEnumerable<PartitionKeyRange> ranges = await feedRangeProvider.GetFeedRangesAsync(token);

                    List<(PartitionKeyRange, TState)> rangesAndStatesBuilder = new List<(PartitionKeyRange, TState)>();
                    foreach (PartitionKeyRange range in ranges)
                    {
                        rangesAndStatesBuilder.Add((range, default));
                    }

                    rangeAndStates = rangesAndStatesBuilder;
                }

                List<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>> bufferedEnumerators = rangeAndStates
                    .Select(rangeAndState =>
                    {
                        PartitionRangePageAsyncEnumerator<TPage, TState> enumerator = createPartitionRangeEnumerator(rangeAndState.Item1, rangeAndState.Item2);
                        BufferedPartitionRangePageAsyncEnumerator<TPage, TState> bufferedEnumerator = new BufferedPartitionRangePageAsyncEnumerator<TPage, TState>(enumerator);
                        return bufferedEnumerator;
                    })
                    .ToList();

                if (maxConcurrency.HasValue)
                {
                    await ParallelPrefetch.PrefetchInParallelAsync(bufferedEnumerators, maxConcurrency.Value);
                }

                PriorityQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> enumerators = new PriorityQueue<PartitionRangePageAsyncEnumerator<TPage, TState>>(
                    bufferedEnumerators,
                    comparer);
                return enumerators;
            });
        }

        public TryCatch<CrossPartitionPage<TPage, TState>> Current { get; private set; }

        public async ValueTask<bool> MoveNextAsync()
        {
            PriorityQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> enumerators = await this.lazyEnumerators.GetValueAsync(cancellationToken: default);
            if (enumerators.Count == 0)
            {
                return false;
            }

            PartitionRangePageAsyncEnumerator<TPage, TState> currentPaginator = enumerators.Dequeue();
            if (!await currentPaginator.MoveNextAsync())
            {
                // Current enumerator is empty,
                // so recursively retry on the next enumerator.
                return await this.MoveNextAsync();
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
                    IEnumerable<PartitionKeyRange> childRanges = await this.feedRangeProvider.GetChildRangeAsync(
                        currentPaginator.Range,
                        cancellationToken: default);
                    foreach (PartitionKeyRange childRange in childRanges)
                    {
                        PartitionRangePageAsyncEnumerator<TPage, TState> childPaginator = this.createPartitionRangeEnumerator(
                            childRange,
                            currentPaginator.State);
                        enumerators.Enqueue(childPaginator);
                    }

                    // Recursively retry
                    return await this.MoveNextAsync();
                }

                if (IsMergeException(exception))
                {
                    throw new NotImplementedException();
                }

                // Just enqueue the paginator and the user can decide if they want to retry.
                enumerators.Enqueue(currentPaginator);

                this.Current = TryCatch<CrossPartitionPage<TPage, TState>>.FromException(currentPaginator.Current.Exception);
                return true;
            }

            if (currentPaginator.State != default)
            {
                // Don't enqueue the paginator otherwise it's an infinite loop.
                enumerators.Enqueue(currentPaginator);
            }

            CrossPartitionState<TState> crossPartitionState;
            if (enumerators.Count == 0)
            {
                crossPartitionState = null;
            }
            else
            {
                List<(PartitionKeyRange, TState)> feedRangeAndStates = new List<(PartitionKeyRange, TState)>(enumerators.Count);
                foreach (PartitionRangePageAsyncEnumerator<TPage, TState> enumerator in enumerators)
                {
                    feedRangeAndStates.Add((enumerator.Range, enumerator.State));
                }

                crossPartitionState = new CrossPartitionState<TState>(feedRangeAndStates);
            }

            this.Current = TryCatch<CrossPartitionPage<TPage, TState>>.FromResult(
                new CrossPartitionPage<TPage, TState>(currentPaginator.Current.Result, crossPartitionState));
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
                && (cosmosException.StatusCode == HttpStatusCode.Gone)
                && (cosmosException.SubStatusCode == (int)Documents.SubStatusCodes.PartitionKeyRangeGone);
        }

        private static bool IsMergeException(Exception exception)
        {
            // TODO: code this out
            return false;
        }
    }
}
