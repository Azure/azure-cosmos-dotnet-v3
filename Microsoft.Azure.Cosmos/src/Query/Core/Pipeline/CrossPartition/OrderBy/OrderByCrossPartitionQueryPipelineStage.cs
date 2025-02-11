// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Tracing;
    using ResourceId = Documents.ResourceId;

    internal static class OrderByCrossPartitionQueryPipelineStage
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

        private sealed class InitializationParameters
        {
            public IDocumentContainer DocumentContainer { get; }

            public ContainerQueryProperties ContainerQueryProperties { get; }

            public SqlQuerySpec SqlQuerySpec { get; }
            
            public IReadOnlyList<FeedRangeEpk> TargetRanges { get; }
            
            public Cosmos.PartitionKey? PartitionKey { get; }
            
            public IReadOnlyList<OrderByColumn> OrderByColumns { get; }
            
            public QueryExecutionOptions QueryPaginationOptions { get; }

            public bool EmitRawOrderByPayload { get; }

            public int MaxConcurrency { get; }

            public InitializationParameters(
                IDocumentContainer documentContainer,
                ContainerQueryProperties containerQueryProperties,
                SqlQuerySpec sqlQuerySpec,
                IReadOnlyList<FeedRangeEpk> targetRanges,
                PartitionKey? partitionKey,
                IReadOnlyList<OrderByColumn> orderByColumns,
                QueryExecutionOptions queryPaginationOptions,
                bool emitRawOrderByPayload,
                int maxConcurrency)
            {
                this.DocumentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
                this.ContainerQueryProperties = containerQueryProperties;
                this.SqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
                this.TargetRanges = targetRanges ?? throw new ArgumentNullException(nameof(targetRanges));
                this.PartitionKey = partitionKey;
                this.OrderByColumns = orderByColumns ?? throw new ArgumentNullException(nameof(orderByColumns));
                this.QueryPaginationOptions = queryPaginationOptions ?? throw new ArgumentNullException(nameof(queryPaginationOptions));
                this.EmitRawOrderByPayload = emitRawOrderByPayload;
                this.MaxConcurrency = maxConcurrency;
            }
        }

        private enum ExecutionState
        {
            Uninitialized,
            Initialized,
            Done
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            ContainerQueryProperties containerQueryProperties,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            Cosmos.PartitionKey? partitionKey,
            IReadOnlyList<OrderByColumn> orderByColumns,
            QueryExecutionOptions queryPaginationOptions,
            int maxConcurrency,
            bool nonStreamingOrderBy,
            bool emitRawOrderByPayload,
            CosmosElement continuationToken)
        {
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

            if (continuationToken != null || !nonStreamingOrderBy)
            {
                return StreamingOrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer,
                    containerQueryProperties,
                    sqlQuerySpec,
                    targetRanges,
                    partitionKey,
                    orderByColumns,
                    queryPaginationOptions,
                    emitRawOrderByPayload,
                    maxConcurrency,
                    continuationToken);
            }

            SqlQuerySpec rewrittenQueryForOrderBy = new SqlQuerySpec(
                sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: TrueFilter),
                sqlQuerySpec.Parameters);

            return TryCatch<IQueryPipelineStage>.FromResult(NonStreamingOrderByPipelineStage.Create(
                documentContainer,
                containerQueryProperties,
                rewrittenQueryForOrderBy,
                targetRanges,
                partitionKey,
                orderByColumns,
                queryPaginationOptions,
                emitRawOrderByPayload,
                maxConcurrency));
        }

        private static async ValueTask MoveNextAsync_InitializeAsync_HandleSplitAsync(
            IDocumentContainer documentContainer,
            ContainerQueryProperties containerQueryProperties,
            Queue<(OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken token)> uninitializedEnumeratorsAndTokens,
            OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator,
            OrderByContinuationToken token,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<FeedRangeEpk> childRanges = await documentContainer.GetChildRangeAsync(
                uninitializedEnumerator.FeedRangeState.FeedRange,
                trace,
                cancellationToken);

            if (childRanges.Count <= 1)
            {
                // We optimistically assumed that the cache is not stale.
                // In the event that it is (where we only get back one child / the partition that we think got split)
                // Then we need to refresh the cache
                await documentContainer.RefreshProviderAsync(trace, cancellationToken);
                childRanges = await documentContainer.GetChildRangeAsync(
                    uninitializedEnumerator.FeedRangeState.FeedRange,
                    trace,
                    cancellationToken);
            }

            if (childRanges.Count < 1)
            {
                string errorMessage = "SDK invariant violated 82086B2D: Must have at least one EPK range in a cross partition enumerator";
                throw Resource.CosmosExceptions.CosmosExceptionFactory.CreateInternalServerErrorException(
                                message: errorMessage,
                                headers: null,
                                stackTrace: null,
                                trace: trace,
                                error: new Microsoft.Azure.Documents.Error { Code = "SDK_invariant_violated_82086B2D", Message = errorMessage });
            }

            if (childRanges.Count == 1)
            {
                // On a merge, the 410/1002 results in a single parent
                // We maintain the current enumerator's range and let the RequestInvokerHandler logic kick in
                OrderByQueryPartitionRangePageAsyncEnumerator childPaginator = OrderByQueryPartitionRangePageAsyncEnumerator.Create(
                    documentContainer,
                    containerQueryProperties,
                    uninitializedEnumerator.SqlQuerySpec,
                    new FeedRangeState<QueryState>(uninitializedEnumerator.FeedRangeState.FeedRange, uninitializedEnumerator.StartOfPageState),
                    partitionKey: null,
                    uninitializedEnumerator.QueryPaginationOptions,
                    uninitializedEnumerator.Filter,
                    PrefetchPolicy.PrefetchSinglePage);
                uninitializedEnumeratorsAndTokens.Enqueue((childPaginator, token));
            }
            else
            {
                // Split
                foreach (FeedRangeInternal childRange in childRanges)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    OrderByQueryPartitionRangePageAsyncEnumerator childPaginator = OrderByQueryPartitionRangePageAsyncEnumerator.Create(
                        documentContainer,
                        containerQueryProperties,
                        uninitializedEnumerator.SqlQuerySpec,
                        new FeedRangeState<QueryState>(childRange, uninitializedEnumerator.StartOfPageState),
                        partitionKey: null,
                        uninitializedEnumerator.QueryPaginationOptions,
                        uninitializedEnumerator.Filter,
                        PrefetchPolicy.PrefetchSinglePage);
                    uninitializedEnumeratorsAndTokens.Enqueue((childPaginator, token));
                }
            }
        }

        private static bool IsSplitException(Exception exception)
        {
            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            return exception.IsPartitionSplitException();
        }

        private static OrderByContinuationToken CreateOrderByContinuationToken(
            ParallelContinuationToken parallelToken,
            OrderByQueryResult orderByQueryResult,
            int skipCount,
            string filter)
        {
            OrderByContinuationToken token;
            // If order by items have c* types then it cannot be converted to resume values
            if (ContainsSupportedResumeTypes(orderByQueryResult.OrderByItems))
            {
                List<SqlQueryResumeValue> resumeValues = new List<SqlQueryResumeValue>(orderByQueryResult.OrderByItems.Count);
                foreach (OrderByItem orderByItem in orderByQueryResult.OrderByItems)
                {
                    resumeValues.Add(SqlQueryResumeValue.FromOrderByValue(orderByItem.Item));
                }

                token = new OrderByContinuationToken(
                    parallelToken,
                    orderByItems: null,
                    resumeValues,
                    orderByQueryResult.Rid,
                    skipCount: skipCount,
                    filter: filter);
            }
            else
            {
                token = new OrderByContinuationToken(
                    parallelToken,
                    orderByQueryResult.OrderByItems,
                    resumeValues: null,
                    orderByQueryResult.Rid,
                    skipCount: skipCount,
                    filter: filter);
            }

            return token;
        }

        // Helper method to check that resume values are of type that is supported by SqlQueryResumeValue
        private static bool ContainsSupportedResumeTypes(IReadOnlyList<OrderByItem> orderByItems)
        {
            foreach (OrderByItem orderByItem in orderByItems)
            {
                if (!orderByItem.Item.Accept(SupportedResumeTypeVisitor.Singleton))
                {
                    return false;
                }
            }

            return true;
        }

        private static CosmosElement RetrievePayload(OrderByQueryResult orderByQueryResult, bool emitRawOrderByPayload)
        {
            return emitRawOrderByPayload ? orderByQueryResult.RawPayload : orderByQueryResult.Payload;
        }

        private static IReadOnlyList<CosmosElement> RetrievePayloads(IReadOnlyList<OrderByQueryResult> orderByQueryResults, bool emitRawOrderByPayload)
        {
            List<CosmosElement> payloads = new List<CosmosElement>(orderByQueryResults.Count);
            foreach (OrderByQueryResult orderByQueryResult in orderByQueryResults)
            {
                payloads.Add(RetrievePayload(orderByQueryResult, emitRawOrderByPayload));
            }

            return payloads;
        }

        /// <summary>
        /// This class is responsible for draining cross partition queries that have order by conditions.
        /// The way order by queries work is that they are doing a k-way merge of sorted lists from each partition with an added condition.
        /// The added condition is that if 2 or more top documents from different partitions are equivalent then we drain from the left most partition first.
        /// This way we can generate a single continuation token for all n partitions.
        /// This class is able to stop and resume execution by generating continuation tokens and reconstructing an execution context from said token.
        /// </summary>
        private sealed class StreamingOrderByCrossPartitionQueryPipelineStage : IQueryPipelineStage
        {
            private readonly IDocumentContainer documentContainer;
            private readonly ContainerQueryProperties containerQueryProperties;
            private readonly IReadOnlyList<SortOrder> sortOrders;
            private readonly PriorityQueue<OrderByQueryPartitionRangePageAsyncEnumerator> enumerators;
            private readonly Queue<(OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken token)> uninitializedEnumeratorsAndTokens;
            private readonly QueryExecutionOptions queryPaginationOptions;
            private readonly bool emitRawOrderByPayload;
            private readonly int maxConcurrency;

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

            private StreamingOrderByCrossPartitionQueryPipelineStage(
                IDocumentContainer documentContainer,
                ContainerQueryProperties containerQueryProperties,
                IReadOnlyList<SortOrder> sortOrders,
                QueryExecutionOptions queryPaginationOptions,
                bool emitRawOrderByPayload,
                int maxConcurrency,
                IEnumerable<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)> uninitializedEnumeratorsAndTokens,
                QueryState state)
            {
                this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
                this.containerQueryProperties = containerQueryProperties;
                this.sortOrders = sortOrders ?? throw new ArgumentNullException(nameof(sortOrders));
                this.enumerators = new PriorityQueue<OrderByQueryPartitionRangePageAsyncEnumerator>(new OrderByEnumeratorComparer(this.sortOrders));
                this.queryPaginationOptions = queryPaginationOptions ?? QueryExecutionOptions.Default;
                this.emitRawOrderByPayload = emitRawOrderByPayload;
                this.maxConcurrency = maxConcurrency < 0 ? throw new ArgumentOutOfRangeException($"{nameof(maxConcurrency)} must be a non negative number.") : maxConcurrency;
                this.uninitializedEnumeratorsAndTokens = new Queue<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)>(uninitializedEnumeratorsAndTokens ?? throw new ArgumentNullException(nameof(uninitializedEnumeratorsAndTokens)));
                this.state = state ?? InitializingQueryState;
            }

            private StreamingOrderByCrossPartitionQueryPipelineStage(
                IDocumentContainer documentContainer,
                ContainerQueryProperties containerQueryProperties,
                IReadOnlyList<SortOrder> sortOrders,
                PriorityQueue<OrderByQueryPartitionRangePageAsyncEnumerator> enumerators,
                Queue<(OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken token)> uninitializedEnumeratorsAndTokens,
                QueryExecutionOptions queryPaginationOptions,
                bool emitRawOrderByPayload,
                int maxConcurrency)
            {
                this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
                this.containerQueryProperties = containerQueryProperties;
                this.sortOrders = sortOrders ?? throw new ArgumentNullException(nameof(sortOrders));
                this.enumerators = enumerators ?? throw new ArgumentNullException(nameof(enumerators));
                this.uninitializedEnumeratorsAndTokens = uninitializedEnumeratorsAndTokens ?? throw new ArgumentNullException(nameof(uninitializedEnumeratorsAndTokens));
                this.queryPaginationOptions = queryPaginationOptions ?? throw new ArgumentNullException(nameof(queryPaginationOptions));
                this.emitRawOrderByPayload = emitRawOrderByPayload;
                this.maxConcurrency = maxConcurrency;
                this.state = InitializingQueryState;
            }

            public TryCatch<QueryPage> Current { get; private set; }

            public ValueTask DisposeAsync()
            {
                return default;
            }

            private async ValueTask<bool> MoveNextAsync_Initialize_FromBeginningAsync(
                OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (uninitializedEnumerator == null)
                {
                    throw new ArgumentNullException(nameof(uninitializedEnumerator));
                }

                // We need to prime the page
                if (!await uninitializedEnumerator.MoveNextAsync(trace, cancellationToken))
                {
                    // No more documents, so just return an empty page
                    this.Current = TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            documents: EmptyPage,
                            requestCharge: 0,
                            activityId: string.Empty,
                            cosmosQueryExecutionInfo: default,
                            distributionPlanSpec: default,
                            disallowContinuationTokenMessage: default,
                            additionalHeaders: default,
                            state: this.state,
                            streaming: true));
                    return true;
                }

                if (uninitializedEnumerator.Current.Failed)
                {
                    if (IsSplitException(uninitializedEnumerator.Current.Exception))
                    {
                        return await this.MoveNextAsync_InitializeAsync_HandleSplitAsync(uninitializedEnumerator, token: null, trace, cancellationToken);
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
                                    cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                                    distributionPlanSpec: default,
                                    disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                                    additionalHeaders: page.AdditionalHeaders,
                                    state: null,
                                    streaming: page.Streaming));
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
                            cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                            distributionPlanSpec: default,
                            disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                            additionalHeaders: page.AdditionalHeaders,
                            state: this.state,
                            page.Streaming));
                }

                return true;
            }

            private async ValueTask<bool> MoveNextAsync_Initialize_FilterAsync(
                OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator,
                OrderByContinuationToken token,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
                        return await this.MoveNextAsync_InitializeAsync_HandleSplitAsync(uninitializedEnumerator, token, trace, cancellationToken);
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
                                cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                                distributionPlanSpec: default,
                                disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                                additionalHeaders: page.AdditionalHeaders,
                                state: null,
                                streaming: page.Streaming));
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
                            return await this.MoveNextAsync_InitializeAsync_HandleSplitAsync(uninitializedEnumerator, token, trace, cancellationToken);
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
                            token.ResumeValues,
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
                        cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                        distributionPlanSpec: default,
                        disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                        additionalHeaders: page.AdditionalHeaders,
                        state: InitializingQueryState,
                        streaming: page.Streaming));

                return true;
            }

            private async ValueTask<bool> MoveNextAsync_InitializeAsync_HandleSplitAsync(
                OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator,
                OrderByContinuationToken token,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                await OrderByCrossPartitionQueryPipelineStage.MoveNextAsync_InitializeAsync_HandleSplitAsync(
                    this.documentContainer,
                    this.containerQueryProperties,
                    this.uninitializedEnumeratorsAndTokens,
                    uninitializedEnumerator,
                    token,
                    trace,
                    cancellationToken);

                // Recursively retry
                return await this.MoveNextAsync(trace, cancellationToken);
            }

            private async ValueTask<bool> MoveNextAsync_InitializeAsync(ITrace trace, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await ParallelPrefetch.PrefetchInParallelAsync(
                    this.uninitializedEnumeratorsAndTokens.Select(value => value.enumerator),
                    this.maxConcurrency,
                    trace,
                    cancellationToken);
                (OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator, OrderByContinuationToken token) = this.uninitializedEnumeratorsAndTokens.Dequeue();
                bool movedNext = token is null
                    ? await this.MoveNextAsync_Initialize_FromBeginningAsync(uninitializedEnumerator, trace, cancellationToken)
                    : await this.MoveNextAsync_Initialize_FilterAsync(uninitializedEnumerator, token, trace, cancellationToken);
                return movedNext;
            }

            private ValueTask<bool> MoveNextAsync_DrainPageAsync(ITrace trace, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

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
                            OrderByContinuationToken orderByContinuationToken = CreateOrderByContinuationToken(
                                new ParallelContinuationToken(
                                        token: ((CosmosString)currentEnumerator.FeedRangeState.State.Value).Value,
                                        range: ((FeedRangeEpk)currentEnumerator.FeedRangeState.FeedRange).Range),
                                orderByQueryResult,
                                skipCount: 0,
                                filter: currentEnumerator.Filter);

                            CosmosElement cosmosElementOrderByContinuationToken = OrderByContinuationToken.ToCosmosElement(orderByContinuationToken);
                            CosmosArray continuationTokenList = CosmosArray.Create(new List<CosmosElement>() { cosmosElementOrderByContinuationToken });

                            this.state = new QueryState(continuationTokenList);

                            // Return a page of results
                            // No stats to report, since we already reported it when we moved to this page.
                            this.Current = TryCatch<QueryPage>.FromResult(
                                new QueryPage(
                                    documents: RetrievePayloads(results, this.emitRawOrderByPayload),
                                    requestCharge: 0,
                                    activityId: default,
                                    cosmosQueryExecutionInfo: default,
                                    distributionPlanSpec: default,
                                    disallowContinuationTokenMessage: default,
                                    additionalHeaders: currentEnumerator.Current.Result.Page.AdditionalHeaders,
                                    state: this.state,
                                    streaming: true));
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
                    OrderByContinuationToken orderByContinuationToken = CreateOrderByContinuationToken(
                        new ParallelContinuationToken(
                                token: currentEnumerator.StartOfPageState != null ? ((CosmosString)currentEnumerator.StartOfPageState.Value).Value : null,
                                range: ((FeedRangeEpk)currentEnumerator.FeedRangeState.FeedRange).Range),
                        orderByQueryResult,
                        skipCount,
                        currentEnumerator.Filter);

                    CosmosElement cosmosElementOrderByContinuationToken = OrderByContinuationToken.ToCosmosElement(orderByContinuationToken);
                    CosmosArray continuationTokenList = CosmosArray.Create(new List<CosmosElement>() { cosmosElementOrderByContinuationToken });

                    state = continuationTokenList;
                }

                this.state = state != null ? new QueryState(state) : null;

                // Return a page of results
                // No stats to report, since we already reported it when we moved to this page.
                this.Current = TryCatch<QueryPage>.FromResult(
                    new QueryPage(
                        documents: RetrievePayloads(results, this.emitRawOrderByPayload),
                        requestCharge: 0,
                        activityId: default,
                        cosmosQueryExecutionInfo: default,
                        distributionPlanSpec: default,
                        disallowContinuationTokenMessage: default,
                        additionalHeaders: currentEnumerator?.Current.Result.Page.AdditionalHeaders,
                        state: this.state,
                        streaming: true));

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
            public ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (trace == null)
                {
                    throw new ArgumentNullException(nameof(trace));
                }

                if (this.uninitializedEnumeratorsAndTokens.Count != 0)
                {
                    return this.MoveNextAsync_InitializeAsync(trace, cancellationToken);
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
                                cosmosQueryExecutionInfo: default,
                                distributionPlanSpec: default,
                                disallowContinuationTokenMessage: default,
                                additionalHeaders: default,
                                state: default,
                                streaming: true));
                        this.returnedFinalPage = true;
                        return new ValueTask<bool>(true);
                    }

                    // Finished draining.
                    return new ValueTask<bool>(false);
                }

                return this.MoveNextAsync_DrainPageAsync(trace, cancellationToken);
            }

            public static IQueryPipelineStage Create(
                IDocumentContainer documentContainer,
                ContainerQueryProperties containerQueryProperties,
                IReadOnlyList<SortOrder> sortOrders,
                PriorityQueue<OrderByQueryPartitionRangePageAsyncEnumerator> enumerators,
                Queue<(OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken token)> uninitializedEnumeratorsAndTokens,
                QueryExecutionOptions queryPaginationOptions,
                bool emitRawOrderByPayload,
                int maxConcurrency)
            {
                return new StreamingOrderByCrossPartitionQueryPipelineStage(
                    documentContainer,
                    containerQueryProperties,
                    sortOrders,
                    enumerators,
                    uninitializedEnumeratorsAndTokens,
                    queryPaginationOptions,
                    emitRawOrderByPayload,
                    maxConcurrency);
            }

            public static TryCatch<IQueryPipelineStage> MonadicCreate(
                IDocumentContainer documentContainer,
                ContainerQueryProperties containerQueryProperties,
                SqlQuerySpec sqlQuerySpec,
                IReadOnlyList<FeedRangeEpk> targetRanges,
                Cosmos.PartitionKey? partitionKey,
                IReadOnlyList<OrderByColumn> orderByColumns,
                QueryExecutionOptions queryPaginationOptions,
                bool emitRawOrderByPayload,
                int maxConcurrency,
                CosmosElement continuationToken)
            {
                // TODO (brchon): For now we are not honoring non deterministic ORDER BY queries, since there is a bug in the continuation logic.
                // We can turn it back on once the bug is fixed.
                // This shouldn't hurt any query results.

                List<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)> enumeratorsAndTokens;
                if (continuationToken == null)
                {
                    // Start off all the partition key ranges with null continuation
                    SqlQuerySpec rewrittenQueryForOrderBy = new SqlQuerySpec(
                        sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: TrueFilter),
                        sqlQuerySpec.Parameters);

                    enumeratorsAndTokens = targetRanges
                        .Select(range => (OrderByQueryPartitionRangePageAsyncEnumerator.Create(
                            documentContainer,
                            containerQueryProperties,
                            rewrittenQueryForOrderBy,
                            new FeedRangeState<QueryState>(range, state: default),
                            partitionKey,
                            queryPaginationOptions,
                            TrueFilter,
                            PrefetchPolicy.PrefetchSinglePage),
                            (OrderByContinuationToken)null))
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

                    OrderByContinuationToken targetContinuationToken = partitionMapping.TargetMapping.Values.First();

                    int orderByResumeValueCount = 0;
                    IReadOnlyList<SqlQueryResumeValue> resumeValues;
                    IReadOnlyList<CosmosElement> orderByItems;
                    if (targetContinuationToken.ResumeValues != null)
                    {
                        // Use SqlQueryResumeValue for continuation if it is present.
                        resumeValues = targetContinuationToken.ResumeValues;
                        orderByItems = null;
                        orderByResumeValueCount = resumeValues.Count;
                    }
                    else
                    {
                        // If continuation token has only OrderByItems, check if it can be converted to SqlQueryResumeValue. This will
                        // help avoid re-writing the query. Conversion will work as long as the order by item type is a supported type. 
                        orderByResumeValueCount = targetContinuationToken.OrderByItems.Count;

                        if (ContainsSupportedResumeTypes(targetContinuationToken.OrderByItems))
                        {
                            // Convert the order by items to SqlQueryResumeValue
                            List<SqlQueryResumeValue> generatedResumeValues = new List<SqlQueryResumeValue>(targetContinuationToken.OrderByItems.Count);
                            //foreach (CosmosElement orderByItem in orderByItems)
                            foreach (OrderByItem orderByItem in targetContinuationToken.OrderByItems)
                            {
                                generatedResumeValues.Add(SqlQueryResumeValue.FromOrderByValue(orderByItem.Item));
                            }

                            resumeValues = generatedResumeValues;
                            orderByItems = null;
                        }
                        else
                        {
                            orderByItems = targetContinuationToken.OrderByItems.Select(x => x.Item).ToList();
                            resumeValues = null;
                        }
                    }

                    if (orderByResumeValueCount != orderByColumns.Count)
                    {
                        return TryCatch<IQueryPipelineStage>.FromException(
                            new MalformedContinuationTokenException(
                                $"Order By Items from continuation token did not match the query text. " +
                                $"Order by item count: {orderByResumeValueCount} did not match column count {orderByColumns.Count()}. " +
                                $"Continuation token: {targetContinuationToken}"));
                    }

                    enumeratorsAndTokens = new List<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)>();
                    if (resumeValues != null)
                    {
                        // Continuation contains resume values, so update SqlQuerySpec to include SqlQueryResumeFilter which
                        // will specify the resume point to the backend. This avoid having to re-write the query. 

                        // Process partitions left of Target. The resume values in these partition have
                        // already been processed so exclude flag is set to true.
                        SqlQuerySpec leftQuerySpec = new SqlQuerySpec(
                            sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: TrueFilter),
                            sqlQuerySpec.Parameters,
                            new SqlQueryResumeFilter(resumeValues, null, true));

                        foreach (KeyValuePair<FeedRangeEpk, OrderByContinuationToken> kvp in partitionMapping.MappingLeftOfTarget)
                        {
                            FeedRangeEpk range = kvp.Key;
                            OrderByContinuationToken token = kvp.Value;
                            OrderByQueryPartitionRangePageAsyncEnumerator remoteEnumerator = OrderByQueryPartitionRangePageAsyncEnumerator.Create(
                                documentContainer,
                                containerQueryProperties,
                                leftQuerySpec,
                                new FeedRangeState<QueryState>(range, token?.ParallelContinuationToken?.Token != null ? new QueryState(CosmosString.Create(token.ParallelContinuationToken.Token)) : null),
                                partitionKey,
                                queryPaginationOptions,
                                filter: null,
                                PrefetchPolicy.PrefetchSinglePage);

                            enumeratorsAndTokens.Add((remoteEnumerator, token));
                        }

                        // Process Target Partitions which is the last partition from which data has been returned. 
                        // For this partition the Rid value needs to be set if present. Exclude flag is not set as the document
                        // matching the Rid will be skipped in SDK based on SkipCount value. 
                        // Backend requests can contains both SqlQueryResumeFilter and ContinuationToken and the backend will pick
                        // the resume point that is bigger i.e. most restrictive
                        foreach (KeyValuePair<FeedRangeEpk, OrderByContinuationToken> kvp in partitionMapping.TargetMapping)
                        {
                            FeedRangeEpk range = kvp.Key;
                            OrderByContinuationToken token = kvp.Value;

                            SqlQuerySpec targetQuerySpec = new SqlQuerySpec(
                                sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: TrueFilter),
                                sqlQuerySpec.Parameters,
                                new SqlQueryResumeFilter(resumeValues, token?.Rid, false));

                            OrderByQueryPartitionRangePageAsyncEnumerator remoteEnumerator = OrderByQueryPartitionRangePageAsyncEnumerator.Create(
                                documentContainer,
                                containerQueryProperties,
                                targetQuerySpec,
                                new FeedRangeState<QueryState>(range, token?.ParallelContinuationToken?.Token != null ? new QueryState(CosmosString.Create(token.ParallelContinuationToken.Token)) : null),
                                partitionKey,
                                queryPaginationOptions,
                                filter: null,
                                PrefetchPolicy.PrefetchSinglePage);

                            enumeratorsAndTokens.Add((remoteEnumerator, token));
                        }

                        // Process partitions right of target. The Resume value in these partitions have not been processed so the exclude value is set to false.
                        SqlQuerySpec rightQuerySpec = new SqlQuerySpec(
                            sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: TrueFilter),
                            sqlQuerySpec.Parameters,
                            new SqlQueryResumeFilter(resumeValues, null, false));

                        foreach (KeyValuePair<FeedRangeEpk, OrderByContinuationToken> kvp in partitionMapping.MappingRightOfTarget)
                        {
                            FeedRangeEpk range = kvp.Key;
                            OrderByContinuationToken token = kvp.Value;
                            OrderByQueryPartitionRangePageAsyncEnumerator remoteEnumerator = OrderByQueryPartitionRangePageAsyncEnumerator.Create(
                                documentContainer,
                                containerQueryProperties,
                                rightQuerySpec,
                                new FeedRangeState<QueryState>(range, token?.ParallelContinuationToken?.Token != null ? new QueryState(CosmosString.Create(token.ParallelContinuationToken.Token)) : null),
                                partitionKey,
                                queryPaginationOptions,
                                filter: null,
                                PrefetchPolicy.PrefetchSinglePage);

                            enumeratorsAndTokens.Add((remoteEnumerator, token));
                        }
                    }
                    else
                    {
                        // If continuation token doesn't have resume values or if order by items cannot be converted to resume values then
                        // rewrite the query filter to get the correct resume point
                        ReadOnlyMemory<(OrderByColumn, CosmosElement)> columnAndItems = orderByColumns.Zip(orderByItems, (column, item) => (column, item)).ToArray();

                        // For ascending order-by, left of target partition has filter expression > value,
                        // right of target partition has filter expression >= value, 
                        // and target partition takes the previous filter from continuation (or true if no continuation)
                        (string leftFilter, string targetFilter, string rightFilter) = GetFormattedFilters(columnAndItems);
                        List<(IReadOnlyDictionary<FeedRangeEpk, OrderByContinuationToken>, string)> tokenMappingAndFilters = new List<(IReadOnlyDictionary<FeedRangeEpk, OrderByContinuationToken>, string)>()
                    {
                        { (partitionMapping.MappingLeftOfTarget, leftFilter) },
                        { (partitionMapping.TargetMapping, targetFilter) },
                        { (partitionMapping.MappingRightOfTarget, rightFilter) },
                    };

                        foreach ((IReadOnlyDictionary<FeedRangeEpk, OrderByContinuationToken> tokenMapping, string filter) in tokenMappingAndFilters)
                        {
                            SqlQuerySpec rewrittenQueryForOrderBy = new SqlQuerySpec(
                                sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: filter),
                                sqlQuerySpec.Parameters);

                            foreach (KeyValuePair<FeedRangeEpk, OrderByContinuationToken> kvp in tokenMapping)
                            {
                                FeedRangeEpk range = kvp.Key;
                                OrderByContinuationToken token = kvp.Value;
                                OrderByQueryPartitionRangePageAsyncEnumerator remoteEnumerator = OrderByQueryPartitionRangePageAsyncEnumerator.Create(
                                    documentContainer,
                                    containerQueryProperties,
                                    rewrittenQueryForOrderBy,
                                    new FeedRangeState<QueryState>(range, token?.ParallelContinuationToken?.Token != null ? new QueryState(CosmosString.Create(token.ParallelContinuationToken.Token)) : null),
                                    partitionKey,
                                    queryPaginationOptions,
                                    filter,
                                    PrefetchPolicy.PrefetchSinglePage);

                                enumeratorsAndTokens.Add((remoteEnumerator, token));
                            }
                        }
                    }
                }

                StreamingOrderByCrossPartitionQueryPipelineStage stage = new StreamingOrderByCrossPartitionQueryPipelineStage(
                    documentContainer,
                    containerQueryProperties,
                    orderByColumns.Select(column => column.SortOrder).ToList(),
                    queryPaginationOptions,
                    emitRawOrderByPayload,
                    maxConcurrency,
                    enumeratorsAndTokens,
                    continuationToken == null ? null : new QueryState(continuationToken));
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
                    int orderByCount = GetOrderByItemCount(suppliedOrderByContinuationToken);
                    if (orderByCount != numOrderByColumns)
                    {
                        return TryCatch<List<OrderByContinuationToken>>.FromException(
                            new MalformedContinuationTokenException(
                                $"Invalid order-by items in continuation token {continuationToken} for OrderBy~Context."));
                    }
                }

                return TryCatch<List<OrderByContinuationToken>>.FromResult(orderByContinuationTokens);
            }

            private static int GetOrderByItemCount(OrderByContinuationToken orderByContinuationToken)
            {
                return orderByContinuationToken.ResumeValues != null ?
                    orderByContinuationToken.ResumeValues.Count : orderByContinuationToken.OrderByItems.Count;
            }

            private static void AppendToBuilders((StringBuilder leftFilter, StringBuilder targetFilter, StringBuilder rightFilter) builders, object str)
            {
                AppendToBuilders(builders, str, str, str);
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
                    if (orderByItem is not CosmosUndefined)
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
                    ReadOnlyMemory<string> isDefinedFunctions = orderByItem.Accept(CosmosElementToIsSystemFunctionsVisitor.Singleton, sortOrder == SortOrder.Ascending);
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

                        AppendToBuilders(builders, "(");

                        for (int index = 0; index < prefixLength; index++)
                        {
                            string expression = columnAndItemPrefix[index].orderByColumn.Expression;
                            SortOrder sortOrder = columnAndItemPrefix[index].orderByColumn.SortOrder;
                            CosmosElement orderByItem = columnAndItemPrefix[index].orderByItem;
                            bool lastItem = index == prefixLength - 1;

                            AppendToBuilders(builders, "(");

                            bool wasInequality;
                            // We need to add the filter for within the same type.
                            if (orderByItem is CosmosUndefined)
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
                                            AppendToBuilders(builders, filters.LessThan, filters.LessThanOrEqualTo, filters.LessThanOrEqualTo);
                                        }
                                        else
                                        {
                                            // >, >=, >=
                                            AppendToBuilders(builders, filters.GreaterThan, filters.GreaterThanOrEqualTo, filters.GreaterThanOrEqualTo);
                                        }
                                    }
                                    else
                                    {
                                        if (sortOrder == SortOrder.Descending)
                                        {
                                            // <, <, <
                                            AppendToBuilders(builders, filters.LessThan, filters.LessThan, filters.LessThan);
                                        }
                                        else
                                        {
                                            // >, >, >
                                            StreamingOrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, filters.GreaterThan, filters.GreaterThan, filters.GreaterThan);
                                        }
                                    }

                                    wasInequality = true;
                                }
                                else
                                {
                                    // =, =, =
                                    AppendToBuilders(builders, filters.EqualTo);
                                    wasInequality = false;
                                }
                            }
                            else
                            {
                                // Append Expression
                                AppendToBuilders(builders, expression);
                                AppendToBuilders(builders, " ");

                                // Append Binary Operator
                                if (lastItem)
                                {
                                    string inequality = sortOrder == SortOrder.Descending ? Expressions.LessThan : Expressions.GreaterThan;
                                    AppendToBuilders(builders, inequality);
                                    if (lastPrefix)
                                    {
                                        AppendToBuilders(builders, string.Empty, Expressions.EqualTo, Expressions.EqualTo);
                                    }

                                    wasInequality = true;
                                }
                                else
                                {
                                    AppendToBuilders(builders, Expressions.EqualTo);
                                    wasInequality = false;
                                }

                                // Append OrderBy Item
                                StringBuilder sb = new StringBuilder();
                                CosmosElementToQueryLiteral cosmosElementToQueryLiteral = new CosmosElementToQueryLiteral(sb);
                                orderByItem.Accept(cosmosElementToQueryLiteral);
                                string orderByItemToString = sb.ToString();
                                AppendToBuilders(builders, " ");
                                AppendToBuilders(builders, orderByItemToString);
                                AppendToBuilders(builders, " ");
                            }

                            if (wasInequality)
                            {
                                // Now we need to include all the types that match the sort order.
                                ReadOnlyMemory<string> isDefinedFunctions = orderByItem.Accept(CosmosElementToIsSystemFunctionsVisitor.Singleton, sortOrder == SortOrder.Ascending);
                                foreach (string isDefinedFunction in isDefinedFunctions.Span)
                                {
                                    AppendToBuilders(builders, " OR ");
                                    AppendToBuilders(builders, $"{isDefinedFunction}({expression}) ");
                                }
                            }

                            AppendToBuilders(builders, ")");

                            if (!lastItem)
                            {
                                AppendToBuilders(builders, " AND ");
                            }
                        }

                        AppendToBuilders(builders, ")");
                        if (!lastPrefix)
                        {
                            AppendToBuilders(builders, " OR ");
                        }
                    }
                }

                return (left.ToString(), target.ToString(), right.ToString());
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

                if (!await enumerator.MoveNextAsync(trace, cancellationToken))
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
                        sortOrderCompare = continuationToken.ResumeValues != null
                            ? continuationToken.ResumeValues[i].CompareTo(orderByResult.OrderByItems[i].Item)
                            : ItemComparer.Instance.Compare(
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

                    Lazy<CosmosQueryExecutionInfo> cosmosQueryExecutionInfo = orderByQueryPage.Page.CosmosQueryExecutionInfo;
                    if ((cosmosQueryExecutionInfo == null) || cosmosQueryExecutionInfo.Value.ReverseRidEnabled)
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
                        if (cosmosQueryExecutionInfo.Value.ReverseIndexScan)
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

            private sealed class CosmosElementToIsSystemFunctionsVisitor : ICosmosElementVisitor<bool, ReadOnlyMemory<string>>
            {
                public static readonly CosmosElementToIsSystemFunctionsVisitor Singleton = new CosmosElementToIsSystemFunctionsVisitor();

                private static class IsSystemFunctions
                {
                    public const string Defined = "IS_DEFINED";
                    public const string Undefined = "NOT IS_DEFINED";
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

                private static readonly ReadOnlyMemory<string> ExtendedTypesSystemFunctionSortOrder = new string[]
                {
                IsSystemFunctions.Undefined,
                IsSystemFunctions.Defined
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

                private static class ExtendedTypesSortOrder
                {
                    public const int Undefined = 0;
                    public const int Defined = 1;
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
                    return GetExtendedTypesIsDefinedFunctions(ExtendedTypesSortOrder.Defined, isAscending);
                }

                public ReadOnlyMemory<string> Visit(CosmosBoolean cosmosBoolean, bool isAscending)
                {
                    return GetIsDefinedFunctions(SortOrder.Boolean, isAscending);
                }

                public ReadOnlyMemory<string> Visit(CosmosGuid cosmosGuid, bool isAscending)
                {
                    return GetExtendedTypesIsDefinedFunctions(ExtendedTypesSortOrder.Defined, isAscending);
                }

                public ReadOnlyMemory<string> Visit(CosmosNull cosmosNull, bool isAscending)
                {
                    return GetIsDefinedFunctions(SortOrder.Null, isAscending);
                }

                public ReadOnlyMemory<string> Visit(CosmosUndefined cosmosUndefined, bool isAscending)
                {
                    return isAscending ? SystemFunctionSortOrder.Slice(start: 1) : ReadOnlyMemory<string>.Empty;
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

                private static ReadOnlyMemory<string> GetIsDefinedFunctions(int index, bool isAscending)
                {
                    return isAscending ? SystemFunctionSortOrder.Slice(index + 1) : SystemFunctionSortOrder.Slice(start: 0, index);
                }

                private static ReadOnlyMemory<string> GetExtendedTypesIsDefinedFunctions(int index, bool isAscending)
                {
                    return isAscending ?
                        ExtendedTypesSystemFunctionSortOrder.Slice(index + 1) :
                        ExtendedTypesSystemFunctionSortOrder.Slice(start: 0, index);
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

        private sealed class SupportedResumeTypeVisitor : ICosmosElementVisitor<bool>
        {
            public static readonly SupportedResumeTypeVisitor Singleton = new SupportedResumeTypeVisitor();

            private SupportedResumeTypeVisitor()
            {
            }

            public bool Visit(CosmosArray cosmosArray)
            {
                return true;
            }

            public bool Visit(CosmosBinary cosmosBinary)
            {
                return false;
            }

            public bool Visit(CosmosBoolean cosmosBoolean)
            {
                return true;
            }

            public bool Visit(CosmosGuid cosmosGuid)
            {
                return false;
            }

            public bool Visit(CosmosNull cosmosNull)
            {
                return true;
            }

            public bool Visit(CosmosNumber cosmosNumber)
            {
                return cosmosNumber.Accept(SqlQueryResumeValue.SupportedResumeNumberTypeVisitor.Singleton);
            }

            public bool Visit(CosmosObject cosmosObject)
            {
                return true;
            }

            public bool Visit(CosmosString cosmosString)
            {
                return true;
            }

            public bool Visit(CosmosUndefined cosmosUndefined)
            {
                return true;
            }
        }

        private sealed class NonStreamingOrderByPipelineStage : IQueryPipelineStage
        {
            private const int FlatHeapSizeLimit = 4096;

            private const int MaximumPageSize = 2048;

            private static readonly QueryState NonStreamingOrderByInProgress = new QueryState(CosmosString.Create("NonStreamingOrderByInProgress"));

            private readonly int pageSize;

            private readonly InitializationParameters parameters;

            private ExecutionState executionState;

            private BufferedOrderByResults bufferedResults;

            public TryCatch<QueryPage> Current { get; private set; }

            private NonStreamingOrderByPipelineStage(InitializationParameters parameters, int pageSize)
            {
                this.parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
                this.pageSize = pageSize;
                this.executionState = ExecutionState.Uninitialized;
            }

            public ValueTask DisposeAsync()
            {
                this.bufferedResults?.Enumerator?.Dispose();
                return default;
            }

            public async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
            {
                if (this.executionState == ExecutionState.Done)
                {
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                bool firstPage = false;
                if (this.executionState == ExecutionState.Uninitialized)
                {
                    firstPage = true;
                    this.bufferedResults = await this.MoveNextAsync_InitializeAsync(trace, cancellationToken);
                    this.executionState = ExecutionState.Initialized;
                }

                List<CosmosElement> documents = new List<CosmosElement>(this.pageSize);
                for (int count = 0; count < this.pageSize && this.bufferedResults.Enumerator.MoveNext(); ++count)
                {
                    documents.Add(RetrievePayload(this.bufferedResults.Enumerator.Current, this.parameters.EmitRawOrderByPayload));
                }

                if (firstPage || documents.Count > 0)
                {
                    double requestCharge = firstPage ? this.bufferedResults.TotalRequestCharge : 0;
                    QueryPage queryPage = new QueryPage(
                        documents: documents,
                        requestCharge: requestCharge,
                        activityId: this.bufferedResults.QueryPageParameters.ActivityId,
                        cosmosQueryExecutionInfo: this.bufferedResults.QueryPageParameters.CosmosQueryExecutionInfo,
                        distributionPlanSpec: this.bufferedResults.QueryPageParameters.DistributionPlanSpec,
                        disallowContinuationTokenMessage: DisallowContinuationTokenMessages.NonStreamingOrderBy,
                        additionalHeaders: this.bufferedResults.QueryPageParameters.AdditionalHeaders,
                        state: documents.Count > 0 ? NonStreamingOrderByInProgress : null,
                        streaming: false);

                    this.Current = TryCatch<QueryPage>.FromResult(queryPage);
                    return true;
                }
                else
                {
                    this.executionState = ExecutionState.Done;
                    return false;
                }
            }

            private async Task<BufferedOrderByResults> MoveNextAsync_InitializeAsync(ITrace trace, CancellationToken cancellationToken)
            {
                ITracingAsyncEnumerator<TryCatch<OrderByQueryPage>> enumerator = await OrderByCrossPartitionRangePageEnumerator.CreateAsync(
                    this.parameters.DocumentContainer,
                    this.parameters.ContainerQueryProperties,
                    this.parameters.SqlQuerySpec,
                    this.parameters.TargetRanges,
                    this.parameters.PartitionKey,
                    this.parameters.QueryPaginationOptions,
                    this.parameters.MaxConcurrency,
                    trace,
                    cancellationToken);

                IReadOnlyList<SortOrder> sortOrders = this.parameters.OrderByColumns.Select(column => column.SortOrder).ToList();

                OrderByQueryResultComparer comparer = new OrderByQueryResultComparer(sortOrders);
                BufferedOrderByResults bufferedResults = await OrderByCrossPartitionEnumerator.CreateAsync(
                    enumerator,
                    comparer,
                    FlatHeapSizeLimit,
                    trace,
                    cancellationToken);

                return bufferedResults;
            }

            public static IQueryPipelineStage Create(
                IDocumentContainer documentContainer,
                ContainerQueryProperties containerQueryProperties,
                SqlQuerySpec sqlQuerySpec,
                IReadOnlyList<FeedRangeEpk> targetRanges,
                Cosmos.PartitionKey? partitionKey,
                IReadOnlyList<OrderByColumn> orderByColumns,
                QueryExecutionOptions queryPaginationOptions,
                bool emitRawOrderByPayload,
                int maxConcurrency)
            {
                int pageSize = queryPaginationOptions.PageSizeLimit.GetValueOrDefault(MaximumPageSize) > 0 ?
                    Math.Min(MaximumPageSize, queryPaginationOptions.PageSizeLimit.Value) :
                    MaximumPageSize;

                InitializationParameters parameters = new InitializationParameters(
                    documentContainer,
                    containerQueryProperties,
                    sqlQuerySpec,
                    targetRanges,
                    partitionKey,
                    orderByColumns,
                    queryPaginationOptions,
                    emitRawOrderByPayload,
                    maxConcurrency);

                return new NonStreamingOrderByPipelineStage(
                    parameters,
                    pageSize);
            }
        }

        private sealed class OrderByCrossPartitionRangePageEnumerator : ITracingAsyncEnumerator<TryCatch<OrderByQueryPage>>
        {
            private readonly IDocumentContainer documentContainer;

            private readonly ContainerQueryProperties containerQueryProperties;

            private readonly Queue<(OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken token)> enumeratorsAndTokens;

            public TryCatch<OrderByQueryPage> Current { get; private set; }

            private OrderByCrossPartitionRangePageEnumerator(
                IDocumentContainer documentContainer,
                Queue<(OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken token)> enumeratorsAndTokens,
                ContainerQueryProperties containerQueryProperties)
            {
                this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
                this.enumeratorsAndTokens = enumeratorsAndTokens ?? throw new ArgumentNullException(nameof(enumeratorsAndTokens));
                this.containerQueryProperties = containerQueryProperties;
            }

            public static async Task<ITracingAsyncEnumerator<TryCatch<OrderByQueryPage>>> CreateAsync(
                IDocumentContainer documentContainer,
                ContainerQueryProperties containerQueryProperties,
                SqlQuerySpec sqlQuerySpec,
                IReadOnlyList<FeedRangeEpk> targetRanges,
                Cosmos.PartitionKey? partitionKey,
                QueryExecutionOptions queryPaginationOptions,
                int maxConcurrency,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                Queue<(OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken token)> enumeratorsAndTokens =
                    new Queue<(OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken token)>(targetRanges.Count);
                foreach (FeedRangeEpk range in targetRanges)
                {
                    OrderByQueryPartitionRangePageAsyncEnumerator enumerator = OrderByQueryPartitionRangePageAsyncEnumerator.Create(
                        documentContainer,
                        containerQueryProperties,
                        sqlQuerySpec,
                        new FeedRangeState<QueryState>(range, state: null),
                        partitionKey,
                        queryPaginationOptions,
                        filter: null,
                        PrefetchPolicy.PrefetchAll);

                    enumeratorsAndTokens.Enqueue(new (enumerator, null));
                }

                await ParallelPrefetch.PrefetchInParallelAsync(
                    enumeratorsAndTokens.Select(x => x.enumerator),
                    maxConcurrency,
                    trace,
                    cancellationToken);

                return new OrderByCrossPartitionRangePageEnumerator(documentContainer, enumeratorsAndTokens, containerQueryProperties);
            }

            public async ValueTask DisposeAsync()
            {
                foreach ((OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken _) in this.enumeratorsAndTokens)
                {
                    try
                    {
                        await enumerator.DisposeAsync();
                    }
                    catch
                    {
                    }
                }
            }

            public async ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
            {
                while (this.enumeratorsAndTokens.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    (OrderByQueryPartitionRangePageAsyncEnumerator enumerator, OrderByContinuationToken token) = this.enumeratorsAndTokens.Dequeue();
                    if (await enumerator.MoveNextAsync(trace, cancellationToken))
                    {
                        if (enumerator.Current.Succeeded)
                        {
                            OrderByContinuationToken continuationToken;
                            if (enumerator.Current.Result.Page.Documents.Count > 0)
                            {
                                // Use the token for the next page, since we fully drained the page.
                                continuationToken = enumerator.FeedRangeState.State?.Value != null ?
                                    CreateOrderByContinuationToken(
                                        new ParallelContinuationToken(
                                                token: ((CosmosString)enumerator.FeedRangeState.State.Value).Value,
                                                range: ((FeedRangeEpk)enumerator.FeedRangeState.FeedRange).Range),
                                        new OrderByQueryResult(enumerator.Current.Result.Page.Documents[enumerator.Current.Result.Page.Documents.Count - 1]),
                                        skipCount: 0,
                                        filter: enumerator.Filter) :
                                    null;
                            }
                            else
                            {
                                // Empty page, so we cannot create a new resume value: just use the old one.
                                continuationToken = token;
                            }

                            this.Current = enumerator.Current;
                            this.enumeratorsAndTokens.Enqueue((enumerator, continuationToken));
                            return true;
                        }
                        else
                        {
                            if (IsSplitException(enumerator.Current.Exception))
                            {
                                await MoveNextAsync_InitializeAsync_HandleSplitAsync(
                                    this.documentContainer,
                                    this.containerQueryProperties,
                                    this.enumeratorsAndTokens,
                                    enumerator,
                                    token,
                                    trace,
                                    cancellationToken);
                            }
                            else
                            {
                                throw enumerator.Current.Exception;
                            }
                        }
                    }
                }

                return false;
            }
        }
    }
}
