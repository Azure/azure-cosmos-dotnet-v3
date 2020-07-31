// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Documents;

    internal sealed class OrderByItemEnumerator : IAsyncEnumerator<TryCatch<QueryPage>>
    {
        private static readonly QueryState UninitializedQueryState = new QueryState(CosmosString.Create("ORDER BY NOT INITIALIZED YET!"));
        private static readonly IReadOnlyList<CosmosElement> EmptyPage = new List<CosmosElement>();
        private readonly IDocumentContainer documentContainer;
        private readonly Func<PartitionKeyRange, QueryState, string, OrderByQueryPartitionRangePageAsyncEnumerator> createEnumerator;
        private readonly Func<OrderByQueryPartitionRangePageAsyncEnumerator, Task<TryCatch<QueryPage>>> intializeAsync;
        private readonly PriorityQueue<OrderByQueryPartitionRangePageAsyncEnumerator> enumerators;
        private readonly Queue<OrderByQueryPartitionRangePageAsyncEnumerator> uninitializedEnumerators;

        public OrderByItemEnumerator(
            IDocumentContainer documentContainer,
            Func<PartitionKeyRange, QueryState, string, OrderByQueryPartitionRangePageAsyncEnumerator> createEnumerator,
            IEnumerable<OrderByQueryPartitionRangePageAsyncEnumerator> uninitializedEnumerators,
            Func<OrderByQueryPartitionRangePageAsyncEnumerator, Task<TryCatch<QueryPage>>> initializeAsync,
            IComparer<OrderByQueryPartitionRangePageAsyncEnumerator> itemComparer)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.createEnumerator = createEnumerator ?? throw new ArgumentNullException(nameof(createEnumerator));
            this.intializeAsync = initializeAsync ?? throw new ArgumentNullException(nameof(initializeAsync));
            this.uninitializedEnumerators = new Queue<OrderByQueryPartitionRangePageAsyncEnumerator>(uninitializedEnumerators);
            this.enumerators = new PriorityQueue<OrderByQueryPartitionRangePageAsyncEnumerator>(itemComparer);
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public async ValueTask<bool> MoveNextAsync()
        {
            if (this.uninitializedEnumerators.Count != 0)
            {
                OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator = this.uninitializedEnumerators.Dequeue();
                TryCatch<QueryPage> initializeMonad = await this.intializeAsync(uninitializedEnumerator);
                if (initializeMonad.Failed)
                {
                    if (!await this.TryHandleExceptionAsync(uninitializedEnumerator))
                    {
                        this.uninitializedEnumerators.Enqueue(uninitializedEnumerator);
                    }

                    this.Current = TryCatch<QueryPage>.FromException(uninitializedEnumerator.Current.Exception);
                    return true;
                }

                // Once the enumerator has been initialized we can add it back to the priority queue 
                this.enumerators.Enqueue(uninitializedEnumerator);

                // We want to report back the metrics from initialization, so that the user has accurate metrics,
                // But we need to make up a fake continuation token, since we aren't in a valid state to continue from.
                QueryPage initialiazationPage = initializeMonad.Result;
                this.Current = TryCatch<QueryPage>.FromResult(
                    new QueryPage(
                        documents: EmptyPage,
                        requestCharge: initialiazationPage.RequestCharge,
                        activityId: initialiazationPage.ActivityId,
                        responseLengthInBytes: initialiazationPage.ResponseLengthInBytes,
                        cosmosQueryExecutionInfo: initialiazationPage.CosmosQueryExecutionInfo,
                        disallowContinuationTokenMessage: initialiazationPage.DisallowContinuationTokenMessage,
                        state: UninitializedQueryState));
                return true;
            }

            if (this.enumerators.Count == 0)
            {
                return false;
            }

            OrderByQueryPartitionRangePageAsyncEnumerator currentEnumerator = this.enumerators.Dequeue();
            if (!currentEnumerator.Current.Result.Enumerator.MoveNext())
            {
                // The order by page ran out of results
                if (await currentEnumerator.MoveNextAsync())
                {
                    this.enumerators.Enqueue(currentEnumerator);

                    TryCatch<OrderByQueryPage> monadicOrderByQueryPage = currentEnumerator.Current;
                    if (monadicOrderByQueryPage.Failed)
                    {
                        this.Current = TryCatch<QueryPage>.FromException(monadicOrderByQueryPage.Exception);
                        return true;
                    }

                    // Return an empty page with the query stats
                    QueryPage page = monadicOrderByQueryPage.Result.Page;
                    this.Current = TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            documents: EmptyPage,
                            requestCharge: page.RequestCharge,
                            activityId: page.ActivityId,
                            responseLengthInBytes: page.ResponseLengthInBytes,
                            cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                            disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                            state: page.State));
                    return true;
                }

                // recursively retry
                return await this.MoveNextAsync();
            }

            // Create a query page with just the one document we consumed
            // No stats to report, since we already reported it when we moved to this page.
            CosmosElement orderByItem = currentEnumerator.Current.Result.Enumerator.Current;
            this.Current = TryCatch<QueryPage>.FromResult(
                new QueryPage(
                    documents: new List<CosmosElement>() { orderByItem },
                    requestCharge: 0,
                    activityId: default,
                    responseLengthInBytes: 0,
                    cosmosQueryExecutionInfo: default,
                    disallowContinuationTokenMessage: default,
                    state: currentEnumerator.Current.Result.State));
            return true;
        }

        private async Task<bool> TryHandleExceptionAsync(OrderByQueryPartitionRangePageAsyncEnumerator enumerator)
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
                IEnumerable<PartitionKeyRange> childRanges = await this.documentContainer.GetChildRangeAsync(
                    enumerator.Range,
                    cancellationToken: default);
                foreach (PartitionKeyRange childRange in childRanges)
                {
                    OrderByQueryPartitionRangePageAsyncEnumerator childPaginator = this.createEnumerator(
                        childRange,
                        enumerator.State,
                        enumerator.Filter);
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
    }
}
