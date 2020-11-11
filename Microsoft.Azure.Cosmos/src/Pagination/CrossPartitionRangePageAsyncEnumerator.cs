// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
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
    internal sealed class CrossPartitionRangePageAsyncEnumerator<TPage, TState> : IAsyncEnumerator<TryCatch<CrossFeedRangePage<TPage, TState>>>
        where TPage : Page<TState>
        where TState : State
    {
        private readonly IFeedRangeProvider feedRangeProvider;
        private readonly CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator;
        private readonly AsyncLazy<IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>>> lazyEnumerators;
        private CancellationToken cancellationToken;

        public CrossPartitionRangePageAsyncEnumerator(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<TPage, TState> createPartitionRangeEnumerator,
            IComparer<PartitionRangePageAsyncEnumerator<TPage, TState>> comparer,
            int? maxConcurrency,
            CancellationToken cancellationToken,
            CrossFeedRangeState<TState> state = default)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(feedRangeProvider));
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
            this.cancellationToken = cancellationToken;

            this.lazyEnumerators = new AsyncLazy<IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>>>(async (CancellationToken token) =>
            {
                ReadOnlyMemory<FeedRangeState<TState>> rangeAndStates;
                if (state != default)
                {
                    rangeAndStates = state.Value;
                }
                else
                {
                    // Fan out to all partitions with default state
                    List<FeedRangeEpk> ranges = await feedRangeProvider.GetFeedRangesAsync(token);

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
                    BufferedPartitionRangePageAsyncEnumerator<TPage, TState> bufferedEnumerator = new BufferedPartitionRangePageAsyncEnumerator<TPage, TState>(enumerator, cancellationToken);
                    bufferedEnumerators.Add(bufferedEnumerator);
                }

                if (maxConcurrency.HasValue)
                {
                    await ParallelPrefetch.PrefetchInParallelAsync(bufferedEnumerators, maxConcurrency.Value, token);
                }

                IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> queue;
                if (comparer == null)
                {
                    queue = new QueueWrapper<PartitionRangePageAsyncEnumerator<TPage, TState>>(
                        new Queue<PartitionRangePageAsyncEnumerator<TPage, TState>>(bufferedEnumerators));
                }
                else
                {
                    queue = new PriorityQueueWrapper<PartitionRangePageAsyncEnumerator<TPage, TState>>(
                        new PriorityQueue<PartitionRangePageAsyncEnumerator<TPage, TState>>(
                            bufferedEnumerators,
                            comparer));
                }

                return queue;
            });
        }

        public TryCatch<CrossFeedRangePage<TPage, TState>> Current { get; private set; }

        public FeedRangeInternal CurrentRange { get; private set; }

        public async ValueTask<bool> MoveNextAsync()
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            IQueue<PartitionRangePageAsyncEnumerator<TPage, TState>> enumerators = await this.lazyEnumerators.GetValueAsync(cancellationToken: this.cancellationToken);
            if (enumerators.Count == 0)
            {
                this.Current = default;
                this.CurrentRange = default;
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
                    IEnumerable<FeedRangeInternal> childRanges = await this.feedRangeProvider.GetChildRangeAsync(
                        currentPaginator.Range,
                        cancellationToken: this.cancellationToken);
                    foreach (FeedRangeInternal childRange in childRanges)
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

        private static bool IsMergeException(Exception exception)
        {
            // TODO: code this out
            return false;
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