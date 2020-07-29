// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Pagination
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Coordinates draining pages from multiple <see cref="PartitionRangePageAsyncEnumerator{ItemEnumeratorPage<TState>, TState}"/>, while maintaining a global sort order and handling repartitioning (splits, merge).
    /// Also does things on a per item basis.
    /// </summary>
    internal sealed class CrossPartitionRangeItemAsyncEnumerator<TState> : IAsyncEnumerator<TryCatch<CosmosElement>>
        where TState : State
    {
        private readonly IFeedRangeProvider feedRangeProvider;
        private readonly CreatePartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState> createPartitionRangeEnumerator;
        private readonly Func<PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState>, Task<TryCatch<ItemEnumeratorPage<TState>>>> intializeAsync;
        private readonly PriorityQueue<PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState>> enumerators;
        private readonly Queue<PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState>> uninitializedEnumerators;

        public CrossPartitionRangeItemAsyncEnumerator(
            IFeedRangeProvider feedRangeProvider,
            CreatePartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState> createPartitionRangeEnumerator,
            IEnumerable<PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState>> uninitializedEnumerators,
            Func<PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState>, Task<TryCatch<ItemEnumeratorPage<TState>>>> initializeAsync,
            IComparer<CosmosElement> itemComparer)
        {
            this.feedRangeProvider = feedRangeProvider ?? throw new ArgumentNullException(nameof(feedRangeProvider));
            this.createPartitionRangeEnumerator = createPartitionRangeEnumerator ?? throw new ArgumentNullException(nameof(createPartitionRangeEnumerator));
            this.intializeAsync = initializeAsync ?? throw new ArgumentNullException(nameof(initializeAsync));
            this.uninitializedEnumerators = new Queue<PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState>>(uninitializedEnumerators);
            this.enumerators = new PriorityQueue<PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState>>(new EnumeratorComparer());
        }

        public TryCatch<CrossPartitionPage<ItemEnumeratorPage<TState>, TState>> Current { get; private set; }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (this.uninitializedEnumerators.Count != 0)
            {
                PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState> uninitializedEnumerator = this.uninitializedEnumerators.Dequeue();
                TryCatch<ItemEnumeratorPage<TState>> initializeMonad = await this.intializeAsync(uninitializedEnumerator);
                if (initializeMonad.Failed)
                {
                    if (!await this.TryHandleExceptionAsync(uninitializedEnumerator))
                    {
                        this.uninitializedEnumerators.Enqueue(uninitializedEnumerator);
                    }

                    this.Current = TryCatch<CrossPartitionPage<ItemEnumeratorPage<TState>, TState>>.FromException(uninitializedEnumerator.Current.Exception);
                    return true;
                }

                // Once the enumerator has been initialized we can add it back to the priority queue 
                this.enumerators.Enqueue(uninitializedEnumerator);

                // We want to report back the metrics from initialization, so that the user has accurate metrics,
                // But we need to make up a fake continuation token, since we aren't in a valid state to continue from.
                this.Current = TryCatch<CrossPartitionPage<ItemEnumeratorPage<TState>, TState>>.FromResult(
                    new CrossPartitionPage<ItemEnumeratorPage<TState>, TState>(
                        initializeMonad.Result,
                        new CrossPartitionState<TState>(new List<(PartitionKeyRange, TState)>())));

                // and recursively retry
                return await this.MoveNextAsync();
            }

            if (this.enumerators.Count == 0)
            {
                return false;
            }

            PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState> currentEnumerator = this.enumerators.Dequeue();
            if (currentEnumerator.Current.Result.Items.Count == 0)
            {

            }

            if (!await currentEnumerator.MoveNextAsync())
            {
                // Current enumerator is empty,
                // so recursively retry on the next enumerator.
                return await this.MoveNextAsync();
            }

            if (currentEnumerator.Current.Failed)
            {

            }

            if (currentEnumerator.State != null)
            {
                currentEnumerator.Enqueue(currentPaginator);
            }

            TryCatch<ItemEnumeratorPage<TState>> backendPage = currentEnumerator.Current;
            if (backendPage.Failed)
            {
                this.Current = TryCatch<CrossPartitionPage<ItemEnumeratorPage<TState>, TState>>.FromException(backendPage.Exception);
                return true;
            }

            CrossPartitionState<TState> crossPartitionState;
            if (enumerators.Count == 0)
            {
                crossPartitionState = null;
            }
            else
            {
                List<(PartitionKeyRange, TState)> feedRangeAndStates = new List<(PartitionKeyRange, TState)>(enumerators.Count);
                foreach (PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState> enumerator in enumerators)
                {
                    feedRangeAndStates.Add((enumerator.Range, enumerator.State));
                }

                crossPartitionState = new CrossPartitionState<TState>(feedRangeAndStates);
            }

            this.Current = TryCatch<CrossPartitionPage<ItemEnumeratorPage<TState>, TState>>.FromResult(
                new CrossPartitionPage<ItemEnumeratorPage<TState>, TState>(backendPage.Result, crossPartitionState));
            return true;
        }

        private async Task<bool> TryHandleExceptionAsync(PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState> enumerator)
        {
            if (!enumerator.Current.Failed)
            {
                throw new InvalidOperationException("Enumerator was not in a faulted state.");
            }

            Exception exception = enumerator.Current.Exception;

            // Check if it's a retryable exception.
            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            if (IsSplitException(exception))
            {
                // Handle split
                IEnumerable<PartitionKeyRange> childRanges = await this.feedRangeProvider.GetChildRangeAsync(
                    enumerator.Range,
                    cancellationToken: default);
                foreach (PartitionKeyRange childRange in childRanges)
                {
                    PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState> childPaginator = this.createPartitionRangeEnumerator(
                        childRange,
                        enumerator.State);
                    this.uninitializedEnumerators.Enqueue(childPaginator);
                }

                return true;
            }

            if (IsMergeException(exception))
            {
                throw new NotImplementedException();
            }

            return false;
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

        private sealed class EnumeratorComparer : IComparer<PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState>>
        {
            public int Compare(PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState> x, PartitionRangePageAsyncEnumerator<ItemEnumeratorPage<TState>, TState> y)
            {
                throw new NotImplementedException();
            }
        }
    }
}
