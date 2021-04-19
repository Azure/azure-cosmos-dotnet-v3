// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using ResourceId = Documents.ResourceId;

    /// <summary>
    /// CosmosOrderByItemQueryExecutionContext is a concrete implementation for CrossPartitionQueryExecutionContext.
    /// This class is responsible for draining cross partition queries that have order by conditions.
    /// The way order by queries work is that they are doing a k-way merge of sorted lists from each partition with an added condition.
    /// The added condition is that if 2 or more top documents from different partitions are equivalent then we drain from the left most partition first.
    /// This way we can generate a single continuation token for all n partitions.
    /// This class is able to stop and resume execution by generating continuation tokens and reconstructing an execution context from said token.
    /// </summary>
    internal sealed class OrderByCrossPartitionQueryPipelineStage : IQueryPipelineStage
    {
        /// <summary>
        /// Order by queries are rewritten to allow us to inject a filter.
        /// This placeholder is so that we can just string replace it with the filter we want without having to understand the structure of the query.
        /// </summary>
        private const string FormatPlaceHolder = "{documentdb-formattableorderbyquery-filter}";

        /// <summary>
        /// If query does not need a filter then we replace the FormatPlaceHolder with "true", since
        /// "SELECT * FROM c WHERE blah and true" is the same as "SELECT * FROM c where blah"
        /// </summary>
        private const string TrueFilter = "true";

        private static readonly QueryState InitializingQueryState = new QueryState(CosmosString.Create("ORDER BY NOT INITIALIZED YET!"));
        private static readonly IReadOnlyList<CosmosElement> EmptyPage = new List<CosmosElement>();

        private readonly IDocumentContainer documentContainer;
        private readonly IReadOnlyList<SortOrder> sortOrders;
        private readonly PriorityQueue<OrderByQueryPartitionRangePageAsyncEnumerator> enumerators;
        private readonly Queue<(OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken token)> uninitializedEnumeratorsAndTokens;
        private readonly QueryPaginationOptions queryPaginationOptions;
        private readonly int maxConcurrency;

        private CancellationToken cancellationToken;
        private QueryState state;
        private bool returnedFinalPage;

        private static class Expressions
        {
            public const string LessThan = "<";
            public const string LessThanOrEqualTo = "<=";
            public const string EqualTo = "=";
            public const string GreaterThan = ">";
            public const string GreaterThanOrEqualTo = ">=";
            public const string True = "true";
            public const string False = "false";
        }

        private OrderByCrossPartitionQueryPipelineStage(
            IDocumentContainer documentContainer,
            IReadOnlyList<SortOrder> sortOrders,
            QueryPaginationOptions queryPaginationOptions,
            int maxConcurrency,
            IEnumerable<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)> uninitializedEnumeratorsAndTokens,
            QueryState state,
            CancellationToken cancellationToken)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.sortOrders = sortOrders ?? throw new ArgumentNullException(nameof(sortOrders));
            this.enumerators = new PriorityQueue<OrderByQueryPartitionRangePageAsyncEnumerator>(new OrderByEnumeratorComparer(this.sortOrders));
            this.queryPaginationOptions = queryPaginationOptions ?? QueryPaginationOptions.Default;
            this.maxConcurrency = maxConcurrency < 0 ? throw new ArgumentOutOfRangeException($"{nameof(maxConcurrency)} must be a non negative number.") : maxConcurrency;
            this.uninitializedEnumeratorsAndTokens = new Queue<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)>(uninitializedEnumeratorsAndTokens ?? throw new ArgumentNullException(nameof(uninitializedEnumeratorsAndTokens)));
            this.state = state ?? InitializingQueryState;
            this.cancellationToken = cancellationToken;
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public ValueTask DisposeAsync() => default;

        private async ValueTask<bool> MoveNextAsync_Initialize_FromBeginningAsync(
            OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator,
            ITrace trace)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (uninitializedEnumerator == null)
            {
                throw new ArgumentNullException(nameof(uninitializedEnumerator));
            }

            // We need to prime the page
            if (!await uninitializedEnumerator.MoveNextAsync(trace))
            {
                // No more documents, so just return an empty page
                this.Current = TryCatch<QueryPage>.FromResult(
                    new QueryPage(
                        documents: EmptyPage,
                        requestCharge: 0,
                        activityId: string.Empty,
                        responseLengthInBytes: 0,
                        cosmosQueryExecutionInfo: default,
                        disallowContinuationTokenMessage: default,
                        additionalHeaders: default,
                        state: this.state));
                return true;
            }

            if (uninitializedEnumerator.Current.Failed)
            {
                if (IsSplitException(uninitializedEnumerator.Current.Exception))
                {
                    return await this.MoveNextAsync_InitializeAsync_HandleSplitAsync(uninitializedEnumerator, token: null, trace);
                }

                this.uninitializedEnumeratorsAndTokens.Enqueue((uninitializedEnumerator, token: null));
                this.Current = TryCatch<QueryPage>.FromException(uninitializedEnumerator.Current.Exception);
            }
            else
            {
                QueryPage page = uninitializedEnumerator.Current.Result.Page;

                if (!uninitializedEnumerator.Current.Result.Enumerator.MoveNext())
                {
                    // Page was empty
                    if (uninitializedEnumerator.FeedRangeState.State != null)
                    {
                        this.uninitializedEnumeratorsAndTokens.Enqueue((uninitializedEnumerator, token: null));
                    }

                    if ((this.uninitializedEnumeratorsAndTokens.Count == 0) && (this.enumerators.Count == 0))
                    {
                        // Query did not match any results. We need to emit a fake empty page with null continuation
                        this.Current = TryCatch<QueryPage>.FromResult(
                            new QueryPage(
                                documents: EmptyPage,
                                requestCharge: page.RequestCharge,
                                activityId: string.IsNullOrEmpty(page.ActivityId) ? Guid.NewGuid().ToString() : page.ActivityId,
                                responseLengthInBytes: page.ResponseLengthInBytes,
                                cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                                disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                                additionalHeaders: page.AdditionalHeaders,
                                state: null));
                        this.returnedFinalPage = true;
                        return true;
                    }
                }
                else
                {
                    this.enumerators.Enqueue(uninitializedEnumerator);
                }

                // Just return an empty page with the stats
                this.Current = TryCatch<QueryPage>.FromResult(
                    new QueryPage(
                        documents: EmptyPage,
                        requestCharge: page.RequestCharge,
                        activityId: page.ActivityId,
                        responseLengthInBytes: page.ResponseLengthInBytes,
                        cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                        disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                        additionalHeaders: page.AdditionalHeaders,
                        state: this.state));
            }

            return true;
        }

        private async ValueTask<bool> MoveNextAsync_Initialize_FilterAsync(
            OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator,
            OrderByContinuationToken token,
            ITrace trace)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (uninitializedEnumerator == null)
            {
                throw new ArgumentNullException(nameof(uninitializedEnumerator));
            }

            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            TryCatch<(bool, int, TryCatch<OrderByQueryPage>)> filterMonad = await FilterNextAsync(
                uninitializedEnumerator,
                this.sortOrders,
                token,
                trace,
                cancellationToken: default);

            if (filterMonad.Failed)
            {
                if (IsSplitException(filterMonad.Exception))
                {
                    return await this.MoveNextAsync_InitializeAsync_HandleSplitAsync(uninitializedEnumerator, token, trace);
                }

                this.Current = TryCatch<QueryPage>.FromException(filterMonad.Exception);
                return true;
            }

            (bool doneFiltering, int itemsLeftToSkip, TryCatch<OrderByQueryPage> monadicQueryByPage) = filterMonad.Result;
            QueryPage page = uninitializedEnumerator.Current.Result.Page;
            if (doneFiltering)
            {
                if (uninitializedEnumerator.Current.Result.Enumerator.Current != null)
                {
                    this.enumerators.Enqueue(uninitializedEnumerator);
                }
                else if ((this.uninitializedEnumeratorsAndTokens.Count == 0) && (this.enumerators.Count == 0))
                {
                    // Query did not match any results.
                    // We need to emit a fake empty page with null continuation
                    this.Current = TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            documents: EmptyPage,
                            requestCharge: page.RequestCharge,
                            activityId: string.IsNullOrEmpty(page.ActivityId) ? Guid.NewGuid().ToString() : page.ActivityId,
                            responseLengthInBytes: page.ResponseLengthInBytes,
                            cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                            disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                            additionalHeaders: page.AdditionalHeaders,
                            state: null));
                    this.returnedFinalPage = true;
                    return true;
                }
            }
            else
            {
                if (monadicQueryByPage.Failed)
                {
                    if (IsSplitException(filterMonad.Exception))
                    {
                        return await this.MoveNextAsync_InitializeAsync_HandleSplitAsync(uninitializedEnumerator, token, trace);
                    }
                }

                if (uninitializedEnumerator.FeedRangeState.State != default)
                {
                    // We need to update the token 
                    OrderByContinuationToken modifiedToken = new OrderByContinuationToken(
                        new ParallelContinuationToken(
                            ((CosmosString)uninitializedEnumerator.FeedRangeState.State.Value).Value,
                            ((FeedRangeEpk)uninitializedEnumerator.FeedRangeState.FeedRange).Range),
                        token.OrderByItems,
                        token.Rid,
                        itemsLeftToSkip,
                        token.Filter);
                    this.uninitializedEnumeratorsAndTokens.Enqueue((uninitializedEnumerator, modifiedToken));
                    CosmosElement cosmosElementOrderByContinuationToken = OrderByContinuationToken.ToCosmosElement(modifiedToken);
                    CosmosArray continuationTokenList = CosmosArray.Create(new List<CosmosElement>() { cosmosElementOrderByContinuationToken });
                    this.state = new QueryState(continuationTokenList);
                }
            }

            // Just return an empty page with the stats
            this.Current = TryCatch<QueryPage>.FromResult(
                new QueryPage(
                    documents: EmptyPage,
                    requestCharge: page.RequestCharge,
                    activityId: page.ActivityId,
                    responseLengthInBytes: page.ResponseLengthInBytes,
                    cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                    disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                    additionalHeaders: page.AdditionalHeaders,
                    state: InitializingQueryState));

            return true;
        }

        private async ValueTask<bool> MoveNextAsync_InitializeAsync_HandleSplitAsync(
            OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator,
            OrderByContinuationToken token,
            ITrace trace)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<FeedRangeEpk> childRanges = await this.documentContainer.GetChildRangeAsync(
                uninitializedEnumerator.FeedRangeState.FeedRange,
                trace,
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
                await this.documentContainer.RefreshProviderAsync(trace, this.cancellationToken);
                childRanges = await this.documentContainer.GetChildRangeAsync(
                    uninitializedEnumerator.FeedRangeState.FeedRange,
                    trace,
                    this.cancellationToken);
            }

            if (childRanges.Count() <= 1)
            {
                throw new InvalidOperationException("Expected more than 1 child");
            }

            foreach (FeedRangeInternal childRange in childRanges)
            {
                this.cancellationToken.ThrowIfCancellationRequested();

                OrderByQueryPartitionRangePageAsyncEnumerator childPaginator = new OrderByQueryPartitionRangePageAsyncEnumerator(
                    this.documentContainer,
                    uninitializedEnumerator.SqlQuerySpec,
                    new FeedRangeState<QueryState>(childRange, uninitializedEnumerator.StartOfPageState),
                    partitionKey: null,
                    uninitializedEnumerator.QueryPaginationOptions,
                    uninitializedEnumerator.Filter,
                    this.cancellationToken);
                this.uninitializedEnumeratorsAndTokens.Enqueue((childPaginator, token));
            }

            // Recursively retry
            return await this.MoveNextAsync(trace);
        }

        private async ValueTask<bool> MoveNextAsync_InitializeAsync(ITrace trace)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            await ParallelPrefetch.PrefetchInParallelAsync(
                this.uninitializedEnumeratorsAndTokens.Select(value => value.enumerator),
                this.maxConcurrency,
                trace,
                this.cancellationToken);
            (OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator, OrderByContinuationToken token) = this.uninitializedEnumeratorsAndTokens.Dequeue();
            bool movedNext = token is null
                ? await this.MoveNextAsync_Initialize_FromBeginningAsync(uninitializedEnumerator, trace)
                : await this.MoveNextAsync_Initialize_FilterAsync(uninitializedEnumerator, token, trace);
            return movedNext;
        }

        private ValueTask<bool> MoveNextAsync_DrainPageAsync(ITrace trace)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            OrderByQueryPartitionRangePageAsyncEnumerator currentEnumerator = default;
            OrderByQueryResult orderByQueryResult = default;

            // Try to form a page with as many items in the sorted order without having to do async work.
            List<OrderByQueryResult> results = new List<OrderByQueryResult>();
            while (results.Count < this.queryPaginationOptions.PageSizeLimit.GetValueOrDefault(int.MaxValue))
            {
                currentEnumerator = this.enumerators.Dequeue();
                orderByQueryResult = new OrderByQueryResult(currentEnumerator.Current.Result.Enumerator.Current);
                results.Add(orderByQueryResult);

                if (!currentEnumerator.Current.Result.Enumerator.MoveNext())
                {
                    // The order by page ran out of results
                    if (currentEnumerator.FeedRangeState.State != null)
                    {
                        // If the continuation isn't null
                        // then mark the enumerator as unitialized and it will get requeueed on the next iteration with a fresh page.
                        this.uninitializedEnumeratorsAndTokens.Enqueue((currentEnumerator, (OrderByContinuationToken)null));

                        // Use the token for the next page, since we fully drained the enumerator.
                        OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                            new ParallelContinuationToken(
                                token: ((CosmosString)currentEnumerator.FeedRangeState.State.Value).Value,
                                range: ((FeedRangeEpk)currentEnumerator.FeedRangeState.FeedRange).Range),
                            orderByQueryResult.OrderByItems,
                            orderByQueryResult.Rid,
                            skipCount: 0,
                            filter: currentEnumerator.Filter);

                        CosmosElement cosmosElementOrderByContinuationToken = OrderByContinuationToken.ToCosmosElement(orderByContinuationToken);
                        CosmosArray continuationTokenList = CosmosArray.Create(new List<CosmosElement>() { cosmosElementOrderByContinuationToken });

                        this.state = new QueryState(continuationTokenList);

                        // Return a page of results
                        // No stats to report, since we already reported it when we moved to this page.
                        this.Current = TryCatch<QueryPage>.FromResult(
                            new QueryPage(
                                documents: results.Select(result => result.Payload).ToList(),
                                requestCharge: 0,
                                activityId: default,
                                responseLengthInBytes: 0,
                                cosmosQueryExecutionInfo: default,
                                disallowContinuationTokenMessage: default,
                                additionalHeaders: currentEnumerator.Current.Result.Page.AdditionalHeaders,
                                state: this.state));
                        return new ValueTask<bool>(true);
                    }

                    // Todo: we can optimize this by having a special "Done" continuation token 
                    // so we don't grab a full page and filter it through
                    // but this would break older clients, so wait for a compute only fork.

                    break;
                }

                this.enumerators.Enqueue(currentEnumerator);
            }

            // It is possible that we emit multiple documents with the same rid due to JOIN queries.
            // This means it is not enough to serialize the rid that we left on to resume the query.
            // We need to also serialize the number of documents with that rid, so we can skip it when resuming
            int skipCount = results.Where(result => string.Equals(result.Rid, orderByQueryResult.Rid)).Count();

            // Create the continuation token.
            CosmosElement state;
            if ((this.enumerators.Count == 0) && (this.uninitializedEnumeratorsAndTokens.Count == 0))
            {
                state = null;
            }
            else
            {
                OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                    new ParallelContinuationToken(
                        token: currentEnumerator.StartOfPageState != null ? ((CosmosString)currentEnumerator.StartOfPageState.Value).Value : null,
                        range: ((FeedRangeEpk)currentEnumerator.FeedRangeState.FeedRange).Range),
                    orderByQueryResult.OrderByItems,
                    orderByQueryResult.Rid,
                    skipCount: skipCount,
                    filter: currentEnumerator.Filter);

                CosmosElement cosmosElementOrderByContinuationToken = OrderByContinuationToken.ToCosmosElement(orderByContinuationToken);
                CosmosArray continuationTokenList = CosmosArray.Create(new List<CosmosElement>() { cosmosElementOrderByContinuationToken });

                state = continuationTokenList;
            }

            this.state = state != null ? new QueryState(state) : null;

            // Return a page of results
            // No stats to report, since we already reported it when we moved to this page.
            this.Current = TryCatch<QueryPage>.FromResult(
                new QueryPage(
                    documents: results.Select(result => result.Payload).ToList(),
                    requestCharge: 0,
                    activityId: default,
                    responseLengthInBytes: 0,
                    cosmosQueryExecutionInfo: default,
                    disallowContinuationTokenMessage: default,
                    additionalHeaders: currentEnumerator?.Current.Result.Page.AdditionalHeaders,
                    state: this.state));

            if (state == null)
            {
                this.returnedFinalPage = true;
            }

            return new ValueTask<bool>(true);
        }

        //// In order to maintain the continuation token for the user we must drain with a few constraints
        //// 1) We always drain from the partition, which has the highest priority item first
        //// 2) If multiple partitions have the same priority item then we drain from the left most first
        ////   otherwise we would need to keep track of how many of each item we drained from each partition
        ////   (just like parallel queries).
        //// Visually that look the following case where we have three partitions that are numbered and store letters.
        //// For teaching purposes I have made each item a tuple of the following form:
        ////      <item stored in partition, partition number>
        //// So that duplicates across partitions are distinct, but duplicates within partitions are indistinguishable.
        ////      |-------|   |-------|   |-------|
        ////      | <a,1> |   | <a,2> |   | <a,3> |
        ////      | <a,1> |   | <b,2> |   | <c,3> |
        ////      | <a,1> |   | <b,2> |   | <c,3> |
        ////      | <d,1> |   | <c,2> |   | <c,3> |
        ////      | <d,1> |   | <e,2> |   | <f,3> |
        ////      | <e,1> |   | <h,2> |   | <j,3> |
        ////      | <f,1> |   | <i,2> |   | <k,3> |
        ////      |-------|   |-------|   |-------|
        //// Now the correct drain order in this case is:
        ////  <a,1>,<a,1>,<a,1>,<a,2>,<a,3>,<b,2>,<b,2>,<c,2>,<c,3>,<c,3>,<c,3>,
        ////  <d,1>,<d,1>,<e,1>,<e,2>,<f,1>,<f,3>,<h,2>,<i,2>,<j,3>,<k,3>
        //// In more mathematical terms
        ////  1) <x, y> always comes before <z, y> where x < z
        ////  2) <i, j> always come before <i, k> where j < k
        public ValueTask<bool> MoveNextAsync()
        {
            return this.MoveNextAsync(NoOpTrace.Singleton);
        }

        public ValueTask<bool> MoveNextAsync(ITrace trace)
        {
            this.cancellationToken.ThrowIfCancellationRequested();

            if (trace == null)
            {
                throw new ArgumentNullException(nameof(trace));
            }

            if (this.uninitializedEnumeratorsAndTokens.Count != 0)
            {
                return this.MoveNextAsync_InitializeAsync(trace);
            }

            if (this.enumerators.Count == 0)
            {
                if (!this.returnedFinalPage)
                {
                    // return a empty page with null continuation token
                    this.Current = TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            documents: EmptyPage,
                            requestCharge: 0,
                            activityId: Guid.NewGuid().ToString(),
                            responseLengthInBytes: 0,
                            cosmosQueryExecutionInfo: default,
                            disallowContinuationTokenMessage: default,
                            additionalHeaders: default,
                            state: null));
                    this.returnedFinalPage = true;
                    return new ValueTask<bool>(true);
                }

                // Finished draining.
                return new ValueTask<bool>(false);
            }

            return this.MoveNextAsync_DrainPageAsync(trace);
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            Cosmos.PartitionKey? partitionKey,
            IReadOnlyList<OrderByColumn> orderByColumns,
            QueryPaginationOptions queryPaginationOptions,
            int maxConcurrency,
            CosmosElement continuationToken,
            CancellationToken cancellationToken)
        {
            // TODO (brchon): For now we are not honoring non deterministic ORDER BY queries, since there is a bug in the continuation logic.
            // We can turn it back on once the bug is fixed.
            // This shouldn't hurt any query results.

            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
            }

            if (targetRanges == null)
            {
                throw new ArgumentNullException(nameof(targetRanges));
            }

            if (targetRanges.Count == 0)
            {
                throw new ArgumentException($"{nameof(targetRanges)} must not be empty.");
            }

            if (orderByColumns == null)
            {
                throw new ArgumentNullException(nameof(orderByColumns));
            }

            if (orderByColumns.Count == 0)
            {
                throw new ArgumentException($"{nameof(orderByColumns)} must not be empty.");
            }

            List<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)> enumeratorsAndTokens;
            if (continuationToken == null)
            {
                // Start off all the partition key ranges with null continuation
                SqlQuerySpec rewrittenQueryForOrderBy = new SqlQuerySpec(
                    sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: TrueFilter),
                    sqlQuerySpec.Parameters);

                enumeratorsAndTokens = targetRanges
                    .Select(range => (new OrderByQueryPartitionRangePageAsyncEnumerator(
                        documentContainer,
                        rewrittenQueryForOrderBy,
                        new FeedRangeState<QueryState>(range, state: default),
                        partitionKey,
                        queryPaginationOptions,
                        TrueFilter,
                        cancellationToken), (OrderByContinuationToken)null))
                    .ToList();
            }
            else
            {
                TryCatch<PartitionMapper.PartitionMapping<OrderByContinuationToken>> monadicGetOrderByContinuationTokenMapping = MonadicGetOrderByContinuationTokenMapping(
                    targetRanges,
                    continuationToken,
                    orderByColumns.Count);
                if (monadicGetOrderByContinuationTokenMapping.Failed)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(monadicGetOrderByContinuationTokenMapping.Exception);
                }

                PartitionMapper.PartitionMapping<OrderByContinuationToken> partitionMapping = monadicGetOrderByContinuationTokenMapping.Result;
                IReadOnlyList<CosmosElement> orderByItems = partitionMapping
                    .TargetMapping
                    .Values
                    .First()
                    .OrderByItems
                    .Select(x => x.Item)
                    .ToList();

                if (orderByItems.Count != orderByColumns.Count)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(
                        new MalformedContinuationTokenException(
                            $"Order By Items from continuation token did not match the query text. " +
                            $"Order by item count: {orderByItems.Count()} did not match column count {orderByColumns.Count()}. " +
                            $"Continuation token: {continuationToken}"));
                }

                ReadOnlyMemory<(OrderByColumn, CosmosElement)> columnAndItems = orderByColumns.Zip(orderByItems, (column, item) => (column, item)).ToArray();

                // For ascending order-by, left of target partition has filter expression > value,
                // right of target partition has filter expression >= value, 
                // and target partition takes the previous filter from continuation (or true if no continuation)
                (string leftFilter, string targetFilter, string rightFilter) = OrderByCrossPartitionQueryPipelineStage.GetFormattedFilters(columnAndItems);
                List<(IReadOnlyDictionary<FeedRangeEpk, OrderByContinuationToken>, string)> tokenMappingAndFilters = new List<(IReadOnlyDictionary<FeedRangeEpk, OrderByContinuationToken>, string)>()
                {
                    { (partitionMapping.MappingLeftOfTarget, leftFilter) },
                    { (partitionMapping.TargetMapping, targetFilter) },
                    { (partitionMapping.MappingRightOfTarget, rightFilter) },
                };

                enumeratorsAndTokens = new List<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)>();
                foreach ((IReadOnlyDictionary<FeedRangeEpk, OrderByContinuationToken> tokenMapping, string filter) in tokenMappingAndFilters)
                {
                    SqlQuerySpec rewrittenQueryForOrderBy = new SqlQuerySpec(
                        sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: filter),
                        sqlQuerySpec.Parameters);

                    foreach (KeyValuePair<FeedRangeEpk, OrderByContinuationToken> kvp in tokenMapping)
                    {
                        FeedRangeEpk range = kvp.Key;
                        OrderByContinuationToken token = kvp.Value;
                        OrderByQueryPartitionRangePageAsyncEnumerator remoteEnumerator = new OrderByQueryPartitionRangePageAsyncEnumerator(
                            documentContainer,
                            rewrittenQueryForOrderBy,
                            new FeedRangeState<QueryState>(range, token?.ParallelContinuationToken?.Token != null ? new QueryState(CosmosString.Create(token.ParallelContinuationToken.Token)) : null),
                            partitionKey,
                            queryPaginationOptions,
                            filter,
                            cancellationToken);

                        enumeratorsAndTokens.Add((remoteEnumerator, token));
                    }
                }
            }

            OrderByCrossPartitionQueryPipelineStage stage = new OrderByCrossPartitionQueryPipelineStage(
                documentContainer,
                orderByColumns.Select(column => column.SortOrder).ToList(),
                queryPaginationOptions,
                maxConcurrency,
                enumeratorsAndTokens,
                continuationToken == null ? null : new QueryState(continuationToken),
                cancellationToken);
            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }

        private static TryCatch<PartitionMapper.PartitionMapping<OrderByContinuationToken>> MonadicGetOrderByContinuationTokenMapping(
            IReadOnlyList<FeedRangeEpk> partitionKeyRanges,
            CosmosElement continuationToken,
            int numOrderByItems)
        {
            if (partitionKeyRanges == null)
            {
                throw new ArgumentOutOfRangeException(nameof(partitionKeyRanges));
            }

            if (numOrderByItems < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numOrderByItems));
            }

            if (continuationToken == null)
            {
                throw new ArgumentNullException(nameof(continuationToken));
            }

            TryCatch<List<OrderByContinuationToken>> monadicExtractContinuationTokens = MonadicExtractOrderByTokens(continuationToken, numOrderByItems);
            if (monadicExtractContinuationTokens.Failed)
            {
                return TryCatch<PartitionMapper.PartitionMapping<OrderByContinuationToken>>.FromException(monadicExtractContinuationTokens.Exception);
            }

            return PartitionMapper.MonadicGetPartitionMapping(
                partitionKeyRanges,
                monadicExtractContinuationTokens.Result);
        }

        private static TryCatch<List<OrderByContinuationToken>> MonadicExtractOrderByTokens(
            CosmosElement continuationToken,
            int numOrderByColumns)
        {
            if (continuationToken == null)
            {
                return TryCatch<List<OrderByContinuationToken>>.FromResult(default);
            }

            if (!(continuationToken is CosmosArray cosmosArray))
            {
                return TryCatch<List<OrderByContinuationToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Order by continuation token must be an array: {continuationToken}."));
            }

            if (cosmosArray.Count == 0)
            {
                return TryCatch<List<OrderByContinuationToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Order by continuation token cannot be empty: {continuationToken}."));
            }

            List<OrderByContinuationToken> orderByContinuationTokens = new List<OrderByContinuationToken>();
            foreach (CosmosElement arrayItem in cosmosArray)
            {
                TryCatch<OrderByContinuationToken> tryCreateOrderByContinuationToken = OrderByContinuationToken.TryCreateFromCosmosElement(arrayItem);
                if (!tryCreateOrderByContinuationToken.Succeeded)
                {
                    return TryCatch<List<OrderByContinuationToken>>.FromException(tryCreateOrderByContinuationToken.Exception);
                }

                orderByContinuationTokens.Add(tryCreateOrderByContinuationToken.Result);
            }

            foreach (OrderByContinuationToken suppliedOrderByContinuationToken in orderByContinuationTokens)
            {
                if (suppliedOrderByContinuationToken.OrderByItems.Count != numOrderByColumns)
                {
                    return TryCatch<List<OrderByContinuationToken>>.FromException(
                        new MalformedContinuationTokenException(
                            $"Invalid order-by items in continuation token {continuationToken} for OrderBy~Context."));
                }
            }

            return TryCatch<List<OrderByContinuationToken>>.FromResult(orderByContinuationTokens);
        }

        private static void AppendToBuilders((StringBuilder leftFilter, StringBuilder targetFilter, StringBuilder rightFilter) builders, object str)
        {
            OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, str, str, str);
        }

        private static void AppendToBuilders((StringBuilder leftFilter, StringBuilder targetFilter, StringBuilder rightFilter) builders, object left, object target, object right)
        {
            builders.leftFilter.Append(left);
            builders.targetFilter.Append(target);
            builders.rightFilter.Append(right);
        }

        private static (string leftFilter, string targetFilter, string rightFilter) GetFormattedFilters(
            ReadOnlyMemory<(OrderByColumn orderByColumn, CosmosElement orderByItem)> columnAndItems)
        {
            // When we run cross partition queries, 
            // we only serialize the continuation token for the partition that we left off on.
            // The only problem is that when we resume the order by query, 
            // we don't have continuation tokens for all other partition.
            // The saving grace is that the data has a composite sort order(query sort order, partition key range id)
            // so we can generate range filters which in turn the backend will turn into rid based continuation tokens,
            // which is enough to get the streams of data flowing from all partitions.
            // The details of how this is done is described below:
            int numOrderByItems = columnAndItems.Length;
            bool isSingleOrderBy = numOrderByItems == 1;
            StringBuilder left = new StringBuilder();
            StringBuilder target = new StringBuilder();
            StringBuilder right = new StringBuilder();

            (StringBuilder, StringBuilder, StringBuilder) builders = (left, target, right);

            if (isSingleOrderBy)
            {
                //For a single order by query we resume the continuations in this manner
                //    Suppose the query is SELECT* FROM c ORDER BY c.string ASC
                //        And we left off on partition N with the value "B"
                //        Then
                //            All the partitions to the left will have finished reading "B"
                //            Partition N is still reading "B"
                //            All the partitions to the right have let to read a "B
                //        Therefore the filters should be
                //            > "B" , >= "B", and >= "B" respectively
                //    Repeat the same logic for DESC and you will get
                //            < "B", <= "B", and <= "B" respectively
                //    The general rule becomes
                //        For ASC
                //            > for partitions to the left
                //            >= for the partition we left off on
                //            >= for the partitions to the right
                //        For DESC
                //            < for partitions to the left
                //            <= for the partition we left off on
                //            <= for the partitions to the right
                (OrderByColumn orderByColumn, CosmosElement orderByItem) = columnAndItems.Span[0];
                (string expression, SortOrder sortOrder) = (orderByColumn.Expression, orderByColumn.SortOrder);

                AppendToBuilders(builders, "( ");

                // We need to add the filter for within the same type.
                if (orderByItem != default)
                {
                    StringBuilder sb = new StringBuilder();
                    CosmosElementToQueryLiteral cosmosElementToQueryLiteral = new CosmosElementToQueryLiteral(sb);
                    orderByItem.Accept(cosmosElementToQueryLiteral);

                    string orderByItemToString = sb.ToString();

                    left.Append($"{expression} {(sortOrder == SortOrder.Descending ? Expressions.LessThan : Expressions.GreaterThan)} {orderByItemToString}");
                    target.Append($"{expression} {(sortOrder == SortOrder.Descending ? Expressions.LessThanOrEqualTo : Expressions.GreaterThanOrEqualTo)} {orderByItemToString}");
                    right.Append($"{expression} {(sortOrder == SortOrder.Descending ? Expressions.LessThanOrEqualTo : Expressions.GreaterThanOrEqualTo)} {orderByItemToString}");
                }
                else
                {
                    // User is ordering by undefined, so we need to avoid a null reference exception.

                    // What we really want is to support expression > undefined, 
                    // but the engine evaluates to undefined instead of true or false,
                    // so we work around this by using the IS_DEFINED() system function.

                    ComparisionWithUndefinedFilters filters = new ComparisionWithUndefinedFilters(expression);
                    left.Append($"{(sortOrder == SortOrder.Descending ? filters.LessThan : filters.GreaterThan)}");
                    target.Append($"{(sortOrder == SortOrder.Descending ? filters.LessThanOrEqualTo : filters.GreaterThanOrEqualTo)}");
                    right.Append($"{(sortOrder == SortOrder.Descending ? filters.LessThanOrEqualTo : filters.GreaterThanOrEqualTo)}");
                }

                // Now we need to include all the types that match the sort order.
                ReadOnlyMemory<string> isDefinedFunctions = orderByItem == default
                    ? CosmosElementToIsSystemFunctionsVisitor.VisitUndefined(sortOrder == SortOrder.Ascending)
                    : orderByItem.Accept(CosmosElementToIsSystemFunctionsVisitor.Singleton, sortOrder == SortOrder.Ascending);
                foreach (string isDefinedFunction in isDefinedFunctions.Span)
                {
                    AppendToBuilders(builders, " OR ");
                    AppendToBuilders(builders, $"{isDefinedFunction}({expression})");
                }

                AppendToBuilders(builders, " )");
            }
            else
            {
                //For a multi order by query
                //    Suppose the query is SELECT* FROM c ORDER BY c.string ASC, c.number ASC
                //        And we left off on partition N with the value("A", 1)
                //        Then
                //            All the partitions to the left will have finished reading("A", 1)
                //            Partition N is still reading("A", 1)
                //            All the partitions to the right have let to read a "(A", 1)
                //        The filters are harder to derive since their are multiple columns
                //        But the problem reduces to "How do you know one document comes after another in a multi order by query"
                //        The answer is to just look at it one column at a time.
                //        For this particular scenario:
                //        If a first column is greater ex. ("B", blah), then the document comes later in the sort order
                //            Therefore we want all documents where the first column is greater than "A" which means > "A"
                //        Or if the first column is a tie, then you look at the second column ex. ("A", blah).
                //            Therefore we also want all documents where the first column was a tie but the second column is greater which means = "A" AND > 1
                //        Therefore the filters should be
                //            (> "A") OR (= "A" AND > 1), (> "A") OR (= "A" AND >= 1), (> "A") OR (= "A" AND >= 1)
                //            Notice that if we repeated the same logic we for single order by we would have gotten
                //            > "A" AND > 1, >= "A" AND >= 1, >= "A" AND >= 1
                //            which is wrong since we missed some documents
                //    Repeat the same logic for ASC, DESC
                //            (> "A") OR (= "A" AND < 1), (> "A") OR (= "A" AND <= 1), (> "A") OR (= "A" AND <= 1)
                //        Again for DESC, ASC
                //            (< "A") OR (= "A" AND > 1), (< "A") OR (= "A" AND >= 1), (< "A") OR (= "A" AND >= 1)
                //        And again for DESC DESC
                //            (< "A") OR (= "A" AND < 1), (< "A") OR (= "A" AND <= 1), (< "A") OR (= "A" AND <= 1)
                //    The general we look at all prefixes of the order by columns to look for tie breakers.
                //        Except for the full prefix whose last column follows the rules for single item order by
                //        And then you just OR all the possibilities together
                for (int prefixLength = 1; prefixLength <= numOrderByItems; prefixLength++)
                {
                    ReadOnlySpan<(OrderByColumn orderByColumn, CosmosElement orderByItem)> columnAndItemPrefix = columnAndItems.Span.Slice(start: 0, length: prefixLength);

                    bool lastPrefix = prefixLength == numOrderByItems;

                    OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, "(");

                    for (int index = 0; index < prefixLength; index++)
                    {
                        string expression = columnAndItemPrefix[index].orderByColumn.Expression;
                        SortOrder sortOrder = columnAndItemPrefix[index].orderByColumn.SortOrder;
                        CosmosElement orderByItem = columnAndItemPrefix[index].orderByItem;
                        bool lastItem = index == prefixLength - 1;

                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, "(");

                        bool wasInequality;
                        // We need to add the filter for within the same type.
                        if (orderByItem == default)
                        {
                            ComparisionWithUndefinedFilters filters = new ComparisionWithUndefinedFilters(expression);

                            // Refer to the logic from single order by for how we are handling order by undefined
                            if (lastItem)
                            {
                                if (lastPrefix)
                                {
                                    if (sortOrder == SortOrder.Descending)
                                    {
                                        // <, <=, <=
                                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, filters.LessThan, filters.LessThanOrEqualTo, filters.LessThanOrEqualTo);
                                    }
                                    else
                                    {
                                        // >, >=, >=
                                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, filters.GreaterThan, filters.GreaterThanOrEqualTo, filters.GreaterThanOrEqualTo);
                                    }
                                }
                                else
                                {
                                    if (sortOrder == SortOrder.Descending)
                                    {
                                        // <, <, <
                                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, filters.LessThan, filters.LessThan, filters.LessThan);
                                    }
                                    else
                                    {
                                        // >, >, >
                                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, filters.GreaterThan, filters.GreaterThan, filters.GreaterThan);
                                    }
                                }

                                wasInequality = true;
                            }
                            else
                            {
                                // =, =, =
                                OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, filters.EqualTo);
                                wasInequality = false;
                            }
                        }
                        else
                        {
                            // Append Expression
                            OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, expression);
                            OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, " ");

                            // Append Binary Operator
                            if (lastItem)
                            {
                                string inequality = sortOrder == SortOrder.Descending ? Expressions.LessThan : Expressions.GreaterThan;
                                OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, inequality);
                                if (lastPrefix)
                                {
                                    OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, string.Empty, Expressions.EqualTo, Expressions.EqualTo);
                                }

                                wasInequality = true;
                            }
                            else
                            {
                                OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, Expressions.EqualTo);
                                wasInequality = false;
                            }

                            // Append OrderBy Item
                            StringBuilder sb = new StringBuilder();
                            CosmosElementToQueryLiteral cosmosElementToQueryLiteral = new CosmosElementToQueryLiteral(sb);
                            orderByItem.Accept(cosmosElementToQueryLiteral);
                            string orderByItemToString = sb.ToString();
                            OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, " ");
                            OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, orderByItemToString);
                            OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, " ");
                        }

                        if (wasInequality)
                        {
                            // Now we need to include all the types that match the sort order.
                            ReadOnlyMemory<string> isDefinedFunctions = orderByItem == default
                                ? CosmosElementToIsSystemFunctionsVisitor.VisitUndefined(sortOrder == SortOrder.Ascending)
                                : orderByItem.Accept(CosmosElementToIsSystemFunctionsVisitor.Singleton, sortOrder == SortOrder.Ascending);
                            foreach (string isDefinedFunction in isDefinedFunctions.Span)
                            {
                                AppendToBuilders(builders, " OR ");
                                AppendToBuilders(builders, $"{isDefinedFunction}({expression}) ");
                            }
                        }

                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, ")");

                        if (!lastItem)
                        {
                            OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, " AND ");
                        }
                    }

                    OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, ")");
                    if (!lastPrefix)
                    {
                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, " OR ");
                    }
                }
            }

            // For the target filter we can make an optimization to just return "true",
            // since we already have the backend continuation token to resume with.
            return (left.ToString(), TrueFilter, right.ToString());
        }

        private static async Task<TryCatch<(bool doneFiltering, int itemsLeftToSkip, TryCatch<OrderByQueryPage> monadicQueryByPage)>> FilterNextAsync(
            OrderByQueryPartitionRangePageAsyncEnumerator enumerator,
            IReadOnlyList<SortOrder> sortOrders,
            OrderByContinuationToken continuationToken,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // When we resume a query on a partition there is a possibility that we only read a partial page from the backend
            // meaning that will we repeat some documents if we didn't do anything about it. 
            // The solution is to filter all the documents that come before in the sort order, since we have already emitted them to the client.
            // The key is to seek until we get an order by value that matches the order by value we left off on.
            // Once we do that we need to seek to the correct _rid within the term,
            // since there might be many documents with the same order by value we left off on.
            // Finally we need to skip some duplicate _rids, since JOINs emit multiples documents with the same rid and we read a partial page.
            // You can also think about this as a seek on a composite index where the columns are [sort_order, rid, skip_count]

            int itemsToSkip = continuationToken.SkipCount;
            if (!ResourceId.TryParse(continuationToken.Rid, out ResourceId continuationRid))
            {
                return TryCatch<(bool, int, TryCatch<OrderByQueryPage>)>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid Rid in the continuation token {continuationToken.ParallelContinuationToken.Token} for OrderBy~Context."));
            }

            if (!await enumerator.MoveNextAsync(trace))
            {
                return TryCatch<(bool, int, TryCatch<OrderByQueryPage>)>.FromResult((true, 0, enumerator.Current));
            }

            TryCatch<OrderByQueryPage> monadicOrderByQueryPage = enumerator.Current;
            if (monadicOrderByQueryPage.Failed)
            {
                return TryCatch<(bool, int, TryCatch<OrderByQueryPage>)>.FromException(monadicOrderByQueryPage.Exception);
            }

            OrderByQueryPage orderByQueryPage = monadicOrderByQueryPage.Result;
            IEnumerator<CosmosElement> documents = orderByQueryPage.Enumerator;

            while (documents.MoveNext())
            {
                int sortOrderCompare = 0;
                // Filter out documents until we find something that matches the sort order.
                OrderByQueryResult orderByResult = new OrderByQueryResult(documents.Current);
                for (int i = 0; (i < sortOrders.Count) && (sortOrderCompare == 0); ++i)
                {
                    sortOrderCompare = ItemComparer.Instance.Compare(
                        continuationToken.OrderByItems[i].Item,
                        orderByResult.OrderByItems[i].Item);

                    if (sortOrderCompare != 0)
                    {
                        sortOrderCompare = sortOrders[i] == SortOrder.Ascending ? sortOrderCompare : -sortOrderCompare;
                    }
                }

                if (sortOrderCompare < 0)
                {
                    // We might have passed the item due to deletions and filters.
                    return TryCatch<(bool, int, TryCatch<OrderByQueryPage>)>.FromResult((true, 0, enumerator.Current));
                }

                if (sortOrderCompare > 0)
                {
                    // This document does not match the sort order, so skip it.
                    continue;
                }

                // Once the item matches the order by items from the continuation tokens
                // We still need to remove all the documents that have a lower or same rid in the rid sort order.
                // If there is a tie in the sort order the documents should be in _rid order in the same direction as the index (given by the backend)
                ResourceId rid = ResourceId.Parse(orderByResult.Rid);
                int ridOrderCompare = continuationRid.Document.CompareTo(rid.Document);
                if ((orderByQueryPage.Page.CosmosQueryExecutionInfo == null) || orderByQueryPage.Page.CosmosQueryExecutionInfo.ReverseRidEnabled)
                {
                    // If reverse rid is enabled on the backend then fallback to the old way of doing it.
                    if (sortOrders[0] == SortOrder.Descending)
                    {
                        ridOrderCompare = -ridOrderCompare;
                    }
                }
                else
                {
                    // Go by the whatever order the index wants
                    if (orderByQueryPage.Page.CosmosQueryExecutionInfo.ReverseIndexScan)
                    {
                        ridOrderCompare = -ridOrderCompare;
                    }
                }

                if (ridOrderCompare < 0)
                {
                    // We might have passed the rid due to deletions and filters.
                    return TryCatch<(bool, int, TryCatch<OrderByQueryPage>)>.FromResult((true, 0, enumerator.Current));
                }

                if (ridOrderCompare > 0)
                {
                    // This document does not match the rid order, so skip it.
                    continue;
                }

                // At this point we need to skip due to joins
                if (--itemsToSkip < 0)
                {
                    return TryCatch<(bool, int, TryCatch<OrderByQueryPage>)>.FromResult((true, 0, enumerator.Current));
                }
            }

            // If we made it here it means we failed to find the resume order by item which is possible
            // if the user added documents inbetween continuations, so we need to yield and filter the next page of results also.
            return TryCatch<(bool, int, TryCatch<OrderByQueryPage>)>.FromResult((false, itemsToSkip, enumerator.Current));
        }

        private static bool IsSplitException(Exception exception)
        {
            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            return exception is CosmosException cosmosException
                && (cosmosException.StatusCode == HttpStatusCode.Gone)
                && (cosmosException.SubStatusCode == (int)Documents.SubStatusCodes.PartitionKeyRangeGone);
        }

        public void SetCancellationToken(CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            foreach (OrderByQueryPartitionRangePageAsyncEnumerator enumerator in this.enumerators)
            {
                enumerator.SetCancellationToken(cancellationToken);
            }

            foreach ((OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken) enumeratorAndToken in this.uninitializedEnumeratorsAndTokens)
            {
                enumeratorAndToken.Item1.SetCancellationToken(cancellationToken);
            }
        }

        private sealed class CosmosElementToIsSystemFunctionsVisitor : ICosmosElementVisitor<bool, ReadOnlyMemory<string>>
        {
            public static readonly CosmosElementToIsSystemFunctionsVisitor Singleton = new CosmosElementToIsSystemFunctionsVisitor();

            private static class IsSystemFunctions
            {
                public const string Undefined = "not IS_DEFINED";
                public const string Null = "IS_NULL";
                public const string Boolean = "IS_BOOLEAN";
                public const string Number = "IS_NUMBER";
                public const string String = "IS_STRING";
                public const string Array = "IS_ARRAY";
                public const string Object = "IS_OBJECT";
            }

            private static readonly ReadOnlyMemory<string> SystemFunctionSortOrder = new string[]
            {
                IsSystemFunctions.Undefined,
                IsSystemFunctions.Null,
                IsSystemFunctions.Boolean,
                IsSystemFunctions.Number,
                IsSystemFunctions.String,
                IsSystemFunctions.Array,
                IsSystemFunctions.Object,
            };

            private static class SortOrder
            {
                public const int Undefined = 0;
                public const int Null = 1;
                public const int Boolean = 2;
                public const int Number = 3;
                public const int String = 4;
                public const int Array = 5;
                public const int Object = 6;
            }

            private CosmosElementToIsSystemFunctionsVisitor()
            {
            }

            public ReadOnlyMemory<string> Visit(CosmosArray cosmosArray, bool isAscending)
            {
                return GetIsDefinedFunctions(SortOrder.Array, isAscending);
            }

            public ReadOnlyMemory<string> Visit(CosmosBinary cosmosBinary, bool isAscending)
            {
                throw new NotImplementedException();
            }

            public ReadOnlyMemory<string> Visit(CosmosBoolean cosmosBoolean, bool isAscending)
            {
                return GetIsDefinedFunctions(SortOrder.Boolean, isAscending);
            }

            public ReadOnlyMemory<string> Visit(CosmosGuid cosmosGuid, bool isAscending)
            {
                throw new NotImplementedException();
            }

            public ReadOnlyMemory<string> Visit(CosmosNull cosmosNull, bool isAscending)
            {
                return GetIsDefinedFunctions(SortOrder.Null, isAscending);
            }

            public ReadOnlyMemory<string> Visit(CosmosNumber cosmosNumber, bool isAscending)
            {
                return GetIsDefinedFunctions(SortOrder.Number, isAscending);
            }

            public ReadOnlyMemory<string> Visit(CosmosObject cosmosObject, bool isAscending)
            {
                return GetIsDefinedFunctions(SortOrder.Object, isAscending);
            }

            public ReadOnlyMemory<string> Visit(CosmosString cosmosString, bool isAscending)
            {
                return GetIsDefinedFunctions(SortOrder.String, isAscending);
            }

            public static ReadOnlyMemory<string> VisitUndefined(bool isAscending)
            {
                return isAscending ? SystemFunctionSortOrder.Slice(start: 1) : ReadOnlyMemory<string>.Empty;
            }

            private static ReadOnlyMemory<string> GetIsDefinedFunctions(int index, bool isAscending)
            {
                return isAscending ? SystemFunctionSortOrder.Slice(index + 1) : SystemFunctionSortOrder.Slice(start: 0, index);
            }
        }

        private readonly struct ComparisionWithUndefinedFilters
        {
            public ComparisionWithUndefinedFilters(
                string expression)
            {
                this.LessThan = "false";
                this.LessThanOrEqualTo = $"NOT IS_DEFINED({expression})";
                this.EqualTo = $"NOT IS_DEFINED({expression})";
                this.GreaterThan = $"IS_DEFINED({expression})";
                this.GreaterThanOrEqualTo = "true";
            }

            public string LessThan { get; }
            public string LessThanOrEqualTo { get; }
            public string EqualTo { get; }
            public string GreaterThan { get; }
            public string GreaterThanOrEqualTo { get; }
        }
    }
}
