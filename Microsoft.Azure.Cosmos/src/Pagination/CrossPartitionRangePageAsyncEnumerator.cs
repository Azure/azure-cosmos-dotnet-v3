// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Coordinates draining pages from multiple <see cref="PartitionRangePageAsyncEnumerator{TPage, TState}"/>, while maintaining a global sort order and handling repartitioning (splits, merge).
    /// </summary>
    internal sealed class CrossPartitionRangePageAsyncEnumerator<TPage, TState> : ITracingAsyncEnumerator<TryCatch<CrossFeedRangePage<TPage, TState>>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly IFeedRangeProvider feedRangeProvider;
        private readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator;
        private readonly AsyncLazy<IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>>> lazyEnumerators;
        private FeedRangeState<TState>? nextState;

        public CrossPartitionRangePageAsyncEnumerator(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator,
            IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer,
            int? maxConcurrency,
            PrefetchPolicy prefetchPolicy,
            CrossFeedRangeState<TState> state = default)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(feedRangeProvider));
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));

            this.lazyEnumerators = new AsyncLazy<IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>>>((ITrace trace, CancellationToken token) =>
                InitializeEnumeratorsAsync(
                    feedRangeProvider,
                    createPartitionRangeEnumerator,
                    comparer,
                    maxConcurrency,
                    prefetchPolicy,
                    state,
                    trace,
                    token));
        }

        public TryCatch<CrossFeedRangePage<TPage, TState>> Current { get; private set; }

        public FeedRangeInternal CurrentRange { get; private set; }

        public async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            using (ITrace childTrace = trace.StartChild(name: nameof(MoveNextAsync), component: TraceComponent.Pagination, level: TraceLevel.Info))
            {
                IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> enumerators = await this.lazyEnumerators.GetValueAsync(
                    childTrace,
                    cancellationToken);
                if (enumerators.Count == 0)
                {
                    this.Current = default;
                    this.CurrentRange = default;
                    this.nextState = default;
                    return false;
                }

                PartitionRangePageAsyncEnumerator<TPage, TState> currentPaginator = enumerators.Dequeue();
                bool moveNextResult = false;
                try
                {
                    moveNextResult = await currentPaginator.MoveNextAsync(childTrace, cancellationToken);
                }
                catch
                {
                    // Re-queue the enumerator to avoid emptying the queue
                    enumerators.Enqueue(currentPaginator);
                    throw;
                }

                if (!moveNextResult)
                {
                    // Current enumerator is empty,
                    // so recursively retry on the next enumerator.
                    return await this.MoveNextAsync(childTrace, cancellationToken);
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
                        List<FeedRangeEpk> childRanges = await this.feedRangeProvider.GetChildRangeAsync(
                            currentPaginator.FeedRangeState.FeedRange,
                            childTrace,
                            cancellationToken);

                        if (childRanges.Count <= 1)
                        {
                            // We optimistically assumed that the cache is not stale.
                            // In the event that it is (where we only get back one child / the partition that we think got split)
                            // Then we need to refresh the cache
                            await this.feedRangeProvider.RefreshProviderAsync(childTrace, cancellationToken);
                            childRanges = await this.feedRangeProvider.GetChildRangeAsync(
                                currentPaginator.FeedRangeState.FeedRange,
                                childTrace,
                                cancellationToken);
                        }

                        if (childRanges.Count < 1)
                        {
                            string errorMessage = "SDK invariant violated 4795CC37: Must have at least one EPK range in a cross partition enumerator";
                            throw Resource.CosmosExceptions.CosmosExceptionFactory.CreateInternalServerErrorException(
                                message: errorMessage,
                                headers: null,
                                stackTrace: null,
                                trace: childTrace,
                                error: new Microsoft.Azure.Documents.Error { Code = "SDK_invariant_violated_4795CC37", Message = errorMessage });
                        }

                        if (childRanges.Count == 1)
                        {
                            // On a merge, the 410/1002 results in a single parent
                            // We maintain the current enumerator's range and let the RequestInvokerHandler logic kick in
                            enumerators.Enqueue(currentPaginator);
                        }
                        else
                        {
                            // Split
                            foreach (FeedRangeInternal childRange in childRanges)
                            {
                                PartitionRangePageAsyncEnumerator<TPage, TState> childPaginator = this.createPartitionRangeEnumerator(
                                    new FeedRangeState<TState>(childRange, currentPaginator.FeedRangeState.State));
                                enumerators.Enqueue(childPaginator);
                            }
                        }

                        // Recursively retry
                        return await this.MoveNextAsync(childTrace, cancellationToken);
                    }

                    // Just enqueue the paginator and the user can decide if they want to retry.
                    enumerators.Enqueue(currentPaginator);

                    this.Current = TryCatch<CrossFeedRangePage<TPage, TState>>.FromException(currentPaginator.Current.Exception);
                    this.CurrentRange = currentPaginator.FeedRangeState.FeedRange;
                    this.nextState = CrossPartitionRangePageAsyncEnumerator<TPage, TState>.GetNextRange(enumerators);
                    return true;
                }

                if (currentPaginator.FeedRangeState.State != default)
                {
                    // Don't enqueue the paginator otherwise it's an infinite loop.
                    enumerators.Enqueue(currentPaginator);
                }

                CrossFeedRangeState<TState> crossPartitionState;
                if (enumerators.Count == 0)
                {
                    crossPartitionState = null;
                }
                else
                {
                    FeedRangeState<TState>[] feedRangeAndStates = new FeedRangeState<TState>[enumerators.Count];
                    int i = 0;
                    foreach (PartitionRangePageAsyncEnumerator<TPage, TState> enumerator in enumerators)
                    {
                        feedRangeAndStates[i++] = enumerator.FeedRangeState;
                    }

                    crossPartitionState = new CrossFeedRangeState<TState>(feedRangeAndStates);
                }

                this.Current = TryCatch<CrossFeedRangePage<TPage, TState>>.FromResult(
                    new CrossFeedRangePage<TPage, TState>(currentPaginator.Current.Result, crossPartitionState));
                this.CurrentRange = currentPaginator.FeedRangeState.FeedRange;
                this.nextState = CrossPartitionRangePageAsyncEnumerator<TPage, TState>.GetNextRange(enumerators);
                return true;
            }
        }

        public ValueTask DisposeAsync()
        {
            // Do Nothing.
            return default;
        }

        public bool TryPeekNext(out FeedRangeState<TState> nextState)
        {
            if (this.nextState.HasValue)
            {
                nextState = this.nextState.Value;
                return true;
            }

            nextState = default;
            return false;
        }

        private static bool IsSplitException(Exception exeception)
        {
            return exeception is CosmosException cosmosException
                && (cosmosException.StatusCode == HttpStatusCode.Gone)
                && (cosmosException.SubStatusCode == (int)Documents.SubStatusCodes.PartitionKeyRangeGone);
        }

        private static FeedRangeState<TState>? GetNextRange(IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> enumerators)
        {
            if (enumerators == null 
                || enumerators.Count == 0)
            {
                return default;
            }

            return enumerators.Peek()?.FeedRangeState;
        }

        private static async Task<IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>>> InitializeEnumeratorsAsync(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator,
            IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer,
            int? maxConcurrency,
            PrefetchPolicy prefetchPolicy,
            CrossFeedRangeState<TState> state,
            ITrace trace,
            CancellationToken token)
        {
            ReadOnlyMemory<FeedRangeState<TState>> rangeAndStates;
            if (state != default)
            {
                rangeAndStates = state.Value;
            }
            else
            {
                // Fan out to all partitions with default state
                List<FeedRangeEpk> ranges = await feedRangeProvider.GetFeedRangesAsync(trace, token);

                List<FeedRangeState<TState>> rangesAndStatesBuilder = new List<FeedRangeState<TState>>(ranges.Count);
                foreach (FeedRangeInternal range in ranges)
                {
                    rangesAndStatesBuilder.Add(new FeedRangeState<TState>(range, default));
                }

                rangeAndStates = rangesAndStatesBuilder.ToArray();
            }

            IReadOnlyList<BufferedPartitionRangePageAsyncEnumeratorBase<TPage, TState>> bufferedEnumerators = CreateBufferedEnumerators(
                prefetchPolicy,
                createPartitionRangeEnumerator,
                rangeAndStates,
                token);

            if (maxConcurrency.HasValue && maxConcurrency.Value > 1)
            {
                await ParallelPrefetch.PrefetchInParallelAsync(bufferedEnumerators, maxConcurrency.Value, trace, token);
            }

            IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> queue = comparer == null
                ? new QueueWrapper<PartitionRangePageAsyncEnumerator<TPage, TState>>(
                    new Queue<PartitionRangePageAsyncEnumerator<TPage, TState>>(bufferedEnumerators))
                : new PriorityQueueWrapper<PartitionRangePageAsyncEnumerator<TPage, TState>>(
                    new PriorityQueue<PartitionRangePageAsyncEnumerator<TPage, TState>>(
                        bufferedEnumerators,
                        comparer));
            return queue;
        }

        private static IReadOnlyList<BufferedPartitionRangePageAsyncEnumeratorBase<TPage, TState>> CreateBufferedEnumerators(
            PrefetchPolicy policy,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator, 
            ReadOnlyMemory<FeedRangeState<TState>> rangeAndStates,
            CancellationToken cancellationToken)
        {
            List<BufferedPartitionRangePageAsyncEnumeratorBase<TPage, TState>> bufferedEnumerators = new (rangeAndStates.Length);
            for (int i = 0; i < rangeAndStates.Length; i++)
            {
                FeedRangeState<TState> feedRangeState = rangeAndStates.Span[i];
                PartitionRangePageAsyncEnumerator<TPage, TState> enumerator = createPartitionRangeEnumerator(feedRangeState);
                BufferedPartitionRangePageAsyncEnumeratorBase<TPage, TState> bufferedEnumerator = policy switch
                {
                    PrefetchPolicy.PrefetchSinglePage => new BufferedPartitionRangePageAsyncEnumerator<TPage, TState>(enumerator),
                    PrefetchPolicy.PrefetchAll => new FullyBufferedPartitionRangeAsyncEnumerator<TPage, TState>(enumerator),
                    _ => throw new ArgumentOutOfRangeException(nameof(policy)),
                };
                bufferedEnumerators.Add(bufferedEnumerator);
            }

            return bufferedEnumerators;
        }

        private interface IQueue<T> : IEnumerable<T>
        {
            T Peek();

            void Enqueue(T item);

            T Dequeue();

            public int Count { get; }
        }

        private sealed class PriorityQueueWrapper<T> : IQueue<T>
        {
            private readonly PriorityQueue<T> implementation;

            public PriorityQueueWrapper(PriorityQueue<T> implementation)
            {
                this.implementation = implementation ?? throw new ArgumentNullException(nameof(implementation));
            }

            public int Count => this.implementation.Count;

            public T Dequeue() => this.implementation.Dequeue();

            public void Enqueue(T item) => this.implementation.Enqueue(item);

            public T Peek() => this.implementation.Peek();

            public IEnumerator<T> GetEnumerator() => this.implementation.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => this.implementation.GetEnumerator();
        }

        private sealed class QueueWrapper<T> : IQueue<T>
        {
            private readonly Queue<T> implementation;

            public QueueWrapper(Queue<T> implementation)
            {
                this.implementation = implementation ?? throw new ArgumentNullException(nameof(implementation));
            }

            public int Count => this.implementation.Count;

            public T Dequeue() => this.implementation.Dequeue();

            public void Enqueue(T item) => this.implementation.Enqueue(item);

            public T Peek() => this.implementation.Peek();

            public IEnumerator<T> GetEnumerator() => this.implementation.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => this.implementation.GetEnumerator();
        }
    }
}