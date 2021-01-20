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
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Tracing;

    /// <summary>
    /// Coordinates draining pages from multiple <see cref="PartitionRangePageAsyncEnumerator{TPage, TState}"/>, while maintaining a global sort order and handling repartitioning (splits, merge).
    /// </summary>
    internal sealed class CrossPartitionRangePageAsyncEnumerator<TPage, TState> : IAsyncEnumerator<TryCatch<CrossFeedRangePage<TPage, TState>>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly IFeedRangeProvider feedRangeProvider;
        private readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator;
        private readonly AsyncLazy<IQueue<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>>> lazyEnumerators;
        private readonly int? maxConcurrency;
        private readonly bool isStreamingOperation;
        private CancellationToken cancellationToken;

        public CrossPartitionRangePageAsyncEnumerator(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator,
            IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer,
            int? maxConcurrency,
            bool isStreamingOperation,
            CancellationToken cancellationToken,
            CrossFeedRangeState<TState> state = default)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(feedRangeProvider));
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
            this.maxConcurrency = maxConcurrency;
            this.isStreamingOperation = isStreamingOperation;
            this.cancellationToken = cancellationToken;

            this.lazyEnumerators = new AsyncLazy<IQueue<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>>>(async (ITrace trace, CancellationToken token) =>
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

                List<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>> bufferedEnumerators = new List<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>>(rangeAndStates.Length);
                for (int i = 0; i < rangeAndStates.Length; i++)
                {
                    FeedRangeState<TState> feedRangeState = rangeAndStates.Span[i];
                    PartitionRangePageAsyncEnumerator<TPage, TState> enumerator = createPartitionRangeEnumerator(feedRangeState.FeedRange, feedRangeState.State);
                    BufferedPartitionRangePageAsyncEnumerator<TPage, TState> bufferedEnumerator = new BufferedPartitionRangePageAsyncEnumerator<TPage, TState>(enumerator, this.cancellationToken);
                    bufferedEnumerators.Add(bufferedEnumerator);
                }

                if (maxConcurrency.HasValue)
                {
                    await ParallelPrefetch.PrefetchInParallelAsync(bufferedEnumerators, maxConcurrency.Value, trace, token);
                }

                IQueue<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>> queue;
                if (comparer == null)
                {
                    queue = new QueueWrapper<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>>(
                        new Queue<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>>(bufferedEnumerators));
                }
                else
                {
                    queue = new PriorityQueueWrapper<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>>(
                        new PriorityQueue<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>>(
                            bufferedEnumerators,
                            comparer));
                }

                return queue;
            });
        }

        public TryCatch<CrossFeedRangePage<TPage, TState>> Current { get; private set; }

        public FeedRangeInternal CurrentRange { get; private set; }

        public ValueTask<bool> MoveNextAsync()
        {
            return this.MoveNextAsync(NoOpTrace.Singleton);
        }

        public async ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            this.cancellationToken.ThrowIfCancellationRequested();

            using (ITrace childTrace = trace.StartChild(name: nameof(MoveNextAsync), component: TraceComponent.Pagination, level: TraceLevel.Info))
            {
                IQueue<BufferedPartitionRangePageAsyncEnumerator<TPage, TState>> enumerators = await this.lazyEnumerators.GetValueAsync(
                    childTrace,
                    cancellationToken: this.cancellationToken);
                if (enumerators.Count == 0)
                {
                    this.Current = default;
                    this.CurrentRange = default;
                    return false;
                }

                if (!this.isStreamingOperation && this.maxConcurrency.HasValue)
                {
                    await ParallelPrefetch.PrefetchInParallelAsync(
                        enumerators,
                        this.maxConcurrency.Value,
                        childTrace,
                        this.cancellationToken);
                }

                BufferedPartitionRangePageAsyncEnumerator<TPage, TState> currentPaginator = enumerators.Dequeue();
                if (!await currentPaginator.MoveNextAsync(childTrace))
                {
                    // Current enumerator is empty,
                    // so recursively retry on the next enumerator.
                    return await this.MoveNextAsync(childTrace);
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
                            currentPaginator.Range,
                            childTrace,
                            this.cancellationToken);
                        if (childRanges.Count == 0)
                        {
                            throw new InvalidOperationException("Got back no children");
                        }

                        if (childRanges.Count == 1)
                        {
                            // We optimistically assumed that the cache is not stale.
                            // In the event that it is (where we only get back one child / the partition that we think got split)
                            // Then we need to refresh the cache
                            await this.feedRangeProvider.RefreshProviderAsync(childTrace, this.cancellationToken);
                            childRanges = await this.feedRangeProvider.GetChildRangeAsync(
                                currentPaginator.Range,
                                childTrace,
                                this.cancellationToken);
                        }

                        if (childRanges.Count() <= 1)
                        {
                            throw new InvalidOperationException("Expected more than 1 child");
                        }

                        foreach (FeedRangeInternal childRange in childRanges)
                        {
                            PartitionRangePageAsyncEnumerator<TPage, TState> childPaginator = this.createPartitionRangeEnumerator(
                                childRange,
                                currentPaginator.State);
                            BufferedPartitionRangePageAsyncEnumerator<TPage, TState> bufferedChildPaginator = new BufferedPartitionRangePageAsyncEnumerator<TPage, TState>(
                                childPaginator,
                                this.cancellationToken);
                            enumerators.Enqueue(bufferedChildPaginator);
                        }

                        // Recursively retry
                        return await this.MoveNextAsync(childTrace);
                    }

                    // Just enqueue the paginator and the user can decide if they want to retry.
                    enumerators.Enqueue(currentPaginator);

                    this.Current = TryCatch<CrossFeedRangePage<TPage, TState>>.FromException(currentPaginator.Current.Exception);
                    this.CurrentRange = currentPaginator.Range;
                    return true;
                }

                if (currentPaginator.State != default)
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
                        feedRangeAndStates[i++] = new FeedRangeState<TState>(enumerator.Range, enumerator.State);
                    }

                    crossPartitionState = new CrossFeedRangeState<TState>(feedRangeAndStates);
                }

                this.Current = TryCatch<CrossFeedRangePage<TPage, TState>>.FromResult(
                    new CrossFeedRangePage<TPage, TState>(currentPaginator.Current.Result, crossPartitionState));
                this.CurrentRange = currentPaginator.Range;
                return true;
            }
        }

        public ValueTask DisposeAsync()
        {
            // Do Nothing.
            return default;
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
        }

        private static bool IsSplitException(Exception exeception)
        {
            return exeception is CosmosException cosmosException
                && (cosmosException.StatusCode == HttpStatusCode.Gone)
                && (cosmosException.SubStatusCode == (int)Documents.SubStatusCodes.PartitionKeyRangeGone);
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