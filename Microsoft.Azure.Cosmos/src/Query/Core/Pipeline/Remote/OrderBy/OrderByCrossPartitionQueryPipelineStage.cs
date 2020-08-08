// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote.Parallel;
    using Microsoft.Azure.Documents;
    using static Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote.PartitionMapper;

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
        private readonly SqlQuerySpec sqlQuerySpec;
        private readonly IReadOnlyList<SortOrder> sortOrders;
        private readonly PriorityQueue<OrderByQueryPartitionRangePageAsyncEnumerator> enumerators;
        private readonly Queue<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)> uninitializedEnumeratorsAndTokens;
        private readonly int pageSize;

        private QueryState state;

        private static class Expressions
        {
            public const string LessThan = "<";
            public const string LessThanOrEqualTo = "<=";
            public const string EqualTo = "=";
            public const string GreaterThan = ">";
            public const string GreaterThanOrEqualTo = ">=";
        }

        private OrderByCrossPartitionQueryPipelineStage(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<SortOrder> sortOrders,
            int pageSize,
            IEnumerable<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)> uninitializedEnumeratorsAndTokens,
            QueryState state)
        {
            this.documentContainer = documentContainer ?? throw new ArgumentNullException(nameof(documentContainer));
            this.sqlQuerySpec = sqlQuerySpec ?? throw new ArgumentNullException(nameof(sqlQuerySpec));
            this.sortOrders = sortOrders ?? throw new ArgumentNullException(nameof(sortOrders));
            this.enumerators = new PriorityQueue<OrderByQueryPartitionRangePageAsyncEnumerator>(new OrderByEnumeratorComparer(this.sortOrders));
            this.pageSize = pageSize < 0 ? throw new ArgumentOutOfRangeException($"{nameof(pageSize)} must be a non negative number.") : pageSize;
            this.uninitializedEnumeratorsAndTokens = new Queue<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)>(uninitializedEnumeratorsAndTokens ?? throw new ArgumentNullException(nameof(uninitializedEnumeratorsAndTokens)));
            this.state = state ?? InitializingQueryState;
        }

        public TryCatch<QueryPage> Current { get; private set; }

        public ValueTask DisposeAsync() => default;

        public async ValueTask<bool> MoveNextAsync()
        {
            if (this.uninitializedEnumeratorsAndTokens.Count != 0)
            {
                (OrderByQueryPartitionRangePageAsyncEnumerator uninitializedEnumerator, OrderByContinuationToken token) = this.uninitializedEnumeratorsAndTokens.Dequeue();
                if (token is null)
                {
                    // We need to prime the page
                    if (!await uninitializedEnumerator.MoveNextAsync())
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
                                state: this.state));
                        return true;
                    }

                    if (uninitializedEnumerator.Current.Failed)
                    {
                        //TODO HANDLE SPLIT
                        this.uninitializedEnumeratorsAndTokens.Enqueue((uninitializedEnumerator, token));
                        this.Current = TryCatch<QueryPage>.FromException(uninitializedEnumerator.Current.Exception);
                    }
                    else
                    {
                        if (!uninitializedEnumerator.Current.Result.Enumerator.MoveNext())
                        {
                            // Page was empty
                            if (uninitializedEnumerator.State != null)
                            {
                                this.uninitializedEnumeratorsAndTokens.Enqueue((uninitializedEnumerator, token));
                            }

                            if ((this.uninitializedEnumeratorsAndTokens.Count == 0) && (this.enumerators.Count == 0))
                            {
                                // Query did not match any results. We need to emit a fake empty page with null continuation
                                this.Current = TryCatch<QueryPage>.FromResult(
                                    new QueryPage(
                                        documents: EmptyPage,
                                        requestCharge: 0,
                                        activityId: Guid.NewGuid().ToString(),
                                        responseLengthInBytes: 0,
                                        cosmosQueryExecutionInfo: default,
                                        disallowContinuationTokenMessage: default,
                                        state: null));
                                return true;
                            }
                        }
                        else
                        {
                            this.enumerators.Enqueue(uninitializedEnumerator);
                        }

                        QueryPage page = uninitializedEnumerator.Current.Result.Page;
                        // Just return an empty page with the stats
                        this.Current = TryCatch<QueryPage>.FromResult(
                            new QueryPage(
                                documents: EmptyPage,
                                requestCharge: page.RequestCharge,
                                activityId: page.ActivityId,
                                responseLengthInBytes: page.ResponseLengthInBytes,
                                cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                                disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                                state: this.state));
                    }

                    return true;
                }
                else
                {
                    // We need to actually filter the enumerator
                    TryCatch<(bool, TryCatch<OrderByQueryPage>)> filterMonad = await FilterNextAsync(
                        uninitializedEnumerator,
                        this.sortOrders,
                        token,
                        cancellationToken: default);

                    if (filterMonad.Failed)
                    {
                        //TODO HANDLE SPLIT
                        this.Current = TryCatch<QueryPage>.FromException(filterMonad.Exception);
                        return true;
                    }

                    (bool doneFiltering, TryCatch<OrderByQueryPage> monadicQueryByPage) = filterMonad.Result;
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
                                    requestCharge: 0,
                                    activityId: Guid.NewGuid().ToString(),
                                    responseLengthInBytes: 0,
                                    cosmosQueryExecutionInfo: default,
                                    disallowContinuationTokenMessage: default,
                                    state: null));
                            return true;
                        }
                    }
                    else
                    {
                        this.uninitializedEnumeratorsAndTokens.Enqueue((uninitializedEnumerator, token));
                    }

                    QueryPage page = uninitializedEnumerator.Current.Result.Page;
                    // Just return an empty page with the stats
                    this.Current = TryCatch<QueryPage>.FromResult(
                        new QueryPage(
                            documents: EmptyPage,
                            requestCharge: page.RequestCharge,
                            activityId: page.ActivityId,
                            responseLengthInBytes: page.ResponseLengthInBytes,
                            cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                            disallowContinuationTokenMessage: page.DisallowContinuationTokenMessage,
                            state: this.state));

                    return true;
                }
            }

            if (this.enumerators.Count == 0)
            {
                // Finished draining.
                return false;
            }

            OrderByQueryPartitionRangePageAsyncEnumerator currentEnumerator = default;
            OrderByQueryResult orderByQueryResult = default;

            // Try to form a page with as many items in the sorted order without having to do async work.
            List<CosmosElement> documents = new List<CosmosElement>();
            while (documents.Count < this.pageSize)
            {
                currentEnumerator = this.enumerators.Dequeue();
                orderByQueryResult = new OrderByQueryResult(currentEnumerator.Current.Result.Enumerator.Current);
                documents.Add(orderByQueryResult.Payload);

                if (!currentEnumerator.Current.Result.Enumerator.MoveNext())
                {
                    // The order by page ran out of results
                    if (currentEnumerator.State != null)
                    {
                        // If the continuation isn't null
                        // then mark the enumerator as unitialized and it will get requeueed on the next iteration with a fresh page.
                        this.uninitializedEnumeratorsAndTokens.Enqueue((currentEnumerator, (OrderByContinuationToken)null));
                    }

                    break;
                }

                this.enumerators.Enqueue(currentEnumerator);
            }

            string continuationTokenString;
            if ((this.enumerators.Count == 0) && (this.uninitializedEnumeratorsAndTokens.Count == 0))
            {
                continuationTokenString = null;
            }
            else
            {
                OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                    new ParallelContinuationToken(
                        token: currentEnumerator.StartOfPageState != null ? ((CosmosString)currentEnumerator.StartOfPageState.Value).Value : null,
                        range: currentEnumerator.Range.ToRange()),
                    orderByQueryResult.OrderByItems,
                    orderByQueryResult.Rid,
                    skipCount: 0,
                    filter: currentEnumerator.Filter);

                CosmosElement cosmosElementOrderByContinuationToken = OrderByContinuationToken.ToCosmosElement(orderByContinuationToken);
                CosmosArray continuationTokenList = CosmosArray.Create(new List<CosmosElement>() { cosmosElementOrderByContinuationToken });

                continuationTokenString = continuationTokenList.ToString();
            }

            // Return a page of results
            // No stats to report, since we already reported it when we moved to this page.
            this.Current = TryCatch<QueryPage>.FromResult(
                new QueryPage(
                    documents: documents,
                    requestCharge: 0,
                    activityId: default,
                    responseLengthInBytes: 0,
                    cosmosQueryExecutionInfo: default,
                    disallowContinuationTokenMessage: default,
                    state: continuationTokenString != null ? new QueryState(CosmosString.Create(continuationTokenString)) : null));
            return true;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<PartitionKeyRange> targetRanges,
            IReadOnlyList<OrderByColumn> orderByColumns,
            int pageSize,
            CosmosElement continuationToken)
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

            if (pageSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSize));
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
                        range,
                        pageSize,
                        TrueFilter,
                        state: default), (OrderByContinuationToken)null))
                    .ToList();
            }
            else
            {
                TryCatch<PartitionMapping<OrderByContinuationToken>> monadicGetOrderByContinuationTokenMapping = MonadicGetOrderByContinuationTokenMapping(
                    targetRanges,
                    continuationToken,
                    orderByColumns.Count);
                if (monadicGetOrderByContinuationTokenMapping.Failed)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(monadicGetOrderByContinuationTokenMapping.Exception);
                }

                PartitionMapping<OrderByContinuationToken> partitionMapping = monadicGetOrderByContinuationTokenMapping.Result;

                IReadOnlyList<CosmosElement> orderByItems = partitionMapping
                    .TargetPartition
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
                List<(IReadOnlyDictionary<PartitionKeyRange, OrderByContinuationToken>, string)> tokenMappingAndFilters = new List<(IReadOnlyDictionary<PartitionKeyRange, OrderByContinuationToken>, string)>()
                {
                    { (partitionMapping.PartitionsLeftOfTarget, leftFilter) },
                    { (partitionMapping.TargetPartition, targetFilter) },
                    { (partitionMapping.PartitionsRightOfTarget, rightFilter) },
                };

                enumeratorsAndTokens = new List<(OrderByQueryPartitionRangePageAsyncEnumerator, OrderByContinuationToken)>();
                foreach ((IReadOnlyDictionary<PartitionKeyRange, OrderByContinuationToken> tokenMapping, string filter) in tokenMappingAndFilters)
                {
                    SqlQuerySpec rewrittenQueryForOrderBy = new SqlQuerySpec(
                        sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: filter),
                        sqlQuerySpec.Parameters);

                    foreach (KeyValuePair<PartitionKeyRange, OrderByContinuationToken> kvp in tokenMapping)
                    {
                        PartitionKeyRange range = kvp.Key;
                        OrderByContinuationToken token = kvp.Value;
                        OrderByQueryPartitionRangePageAsyncEnumerator remoteEnumerator = new OrderByQueryPartitionRangePageAsyncEnumerator(
                            documentContainer,
                            rewrittenQueryForOrderBy,
                            range,
                            pageSize,
                            filter,
                            state: token?.ParallelContinuationToken?.Token != null ? new QueryState(CosmosString.Create(token.ParallelContinuationToken.Token)) : null);

                        enumeratorsAndTokens.Add((remoteEnumerator, token));
                    }
                }
            }

            OrderByCrossPartitionQueryPipelineStage stage = new OrderByCrossPartitionQueryPipelineStage(
                documentContainer,
                sqlQuerySpec,
                orderByColumns.Select(column => column.SortOrder).ToList(),
                pageSize,
                enumeratorsAndTokens,
                continuationToken == null ? null : new QueryState(continuationToken));
            return TryCatch<IQueryPipelineStage>.FromResult(stage);
        }

        private static TryCatch<PartitionMapping<OrderByContinuationToken>> MonadicGetOrderByContinuationTokenMapping(
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges,
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
                return TryCatch<PartitionMapping<OrderByContinuationToken>>.FromException(monadicExtractContinuationTokens.Exception);
            }

            return MonadicGetPartitionMapping(
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

            if (!(continuationToken is CosmosString continuationTokenString))
            {
                return TryCatch<List<OrderByContinuationToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Order by continuation token must be a string: {continuationToken}."));
            }

            string rawJson = continuationTokenString.Value;

            TryCatch<CosmosArray> monadicCosmosArray = CosmosArray.Monadic.Parse(rawJson);
            if (monadicCosmosArray.Failed)
            {
                return TryCatch<List<OrderByContinuationToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Order by continuation token must be an array: {continuationToken}.",
                        monadicCosmosArray.Exception));
            }

            CosmosArray cosmosArray = monadicCosmosArray.Result;
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

                        // Append Expression
                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, expression);
                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, " ");

                        // Append binary operator
                        if (lastItem)
                        {
                            string inequality = sortOrder == SortOrder.Descending ? Expressions.LessThan : Expressions.GreaterThan;
                            OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, inequality);
                            if (lastPrefix)
                            {
                                OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, string.Empty, Expressions.EqualTo, Expressions.EqualTo);
                            }
                        }
                        else
                        {
                            OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, Expressions.EqualTo);
                        }

                        // Append SortOrder
                        StringBuilder sb = new StringBuilder();
                        CosmosElementToQueryLiteral cosmosElementToQueryLiteral = new CosmosElementToQueryLiteral(sb);
                        orderByItem.Accept(cosmosElementToQueryLiteral);
                        string orderByItemToString = sb.ToString();
                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, " ");
                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, orderByItemToString);
                        OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, " ");

                        if (!lastItem)
                        {
                            OrderByCrossPartitionQueryPipelineStage.AppendToBuilders(builders, "AND ");
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

        /// <summary>
        /// When resuming an order by query we need to filter the document producers.
        /// </summary>
        /// <param name="enumerator">The producer to filter down.</param>
        /// <param name="sortOrders">The sort orders.</param>
        /// <param name="continuationToken">The continuation token.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        private static async Task<TryCatch<(bool, TryCatch<OrderByQueryPage>)>> FilterNextAsync(
            OrderByQueryPartitionRangePageAsyncEnumerator enumerator,
            IReadOnlyList<SortOrder> sortOrders,
            OrderByContinuationToken continuationToken,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // When we resume a query on a partition there is a possibility that we only read a partial page from the backend
            // meaning that will we repeat some documents if we didn't do anything about it. 
            // The solution is to filter all the documents that come before in the sort order, since we have already emitted them to the client.
            // The key is to seek until we get an order by value that matches the order by value we left off on.
            // Once we do that we need to seek to the correct _rid within the term,
            // since there might be many documents with the same order by value we left off on.

            if (!ResourceId.TryParse(continuationToken.Rid, out ResourceId continuationRid))
            {
                return TryCatch<(bool, TryCatch<OrderByQueryPage>)>.FromException(
                    new MalformedContinuationTokenException(
                        $"Invalid Rid in the continuation token {continuationToken.ParallelContinuationToken.Token} for OrderBy~Context."));
            }

            // Throw away documents until it matches the item from the continuation token.
            if (!await enumerator.MoveNextAsync())
            {
                return TryCatch<(bool, TryCatch<OrderByQueryPage>)>.FromResult((true, enumerator.Current));
            }

            TryCatch<OrderByQueryPage> monadicOrderByQueryPage = enumerator.Current;
            if (monadicOrderByQueryPage.Failed)
            {
                return TryCatch<(bool, TryCatch<OrderByQueryPage>)>.FromException(monadicOrderByQueryPage.Exception);
            }

            OrderByQueryPage orderByQueryPage = monadicOrderByQueryPage.Result;
            IEnumerator<CosmosElement> documents = orderByQueryPage.Enumerator;
            while (documents.MoveNext())
            {
                OrderByQueryResult orderByResult = new OrderByQueryResult(documents.Current);
                ResourceId rid = ResourceId.Parse(orderByResult.Rid);
                int cmp = 0;
                for (int i = 0; (i < sortOrders.Count) && (cmp == 0); ++i)
                {
                    cmp = ItemComparer.Instance.Compare(
                        continuationToken.OrderByItems[i].Item,
                        orderByResult.OrderByItems[i].Item);

                    if (cmp != 0)
                    {
                        cmp = sortOrders[i] == SortOrder.Ascending ? cmp : -cmp;
                    }
                }

                if (cmp < 0)
                {
                    // We might have passed the item due to deletions and filters.
                    return TryCatch<(bool, TryCatch<OrderByQueryPage>)>.FromResult((true, enumerator.Current));
                }

                if (cmp == 0)
                {
                    // Once the item matches the order by items from the continuation tokens
                    // We still need to remove all the documents that have a lower or same rid in the rid sort order.
                    // If there is a tie in the sort order the documents should be in _rid order in the same direction as the index (given by the backend)
                    cmp = continuationRid.Document.CompareTo(rid.Document);
                    if ((orderByQueryPage.Page.CosmosQueryExecutionInfo == null) || orderByQueryPage.Page.CosmosQueryExecutionInfo.ReverseRidEnabled)
                    {
                        // If reverse rid is enabled on the backend then fallback to the old way of doing it.
                        if (sortOrders[0] == SortOrder.Descending)
                        {
                            cmp = -cmp;
                        }
                    }
                    else
                    {
                        // Go by the whatever order the index wants
                        if (orderByQueryPage.Page.CosmosQueryExecutionInfo.ReverseIndexScan)
                        {
                            cmp = -cmp;
                        }
                    }

                    if (cmp < 0)
                    {
                        return TryCatch<(bool, TryCatch<OrderByQueryPage>)>.FromResult((true, enumerator.Current));
                    }
                }
            }

            return TryCatch<(bool, TryCatch<OrderByQueryPage>)>.FromResult((false, enumerator.Current));
        }

        private static bool IsSplitException(Exception exeception)
        {
            return exeception is CosmosException cosmosException
                && (cosmosException.StatusCode == HttpStatusCode.Gone)
                && (cosmosException.SubStatusCode == (int)Documents.SubStatusCodes.PartitionKeyRangeGone);
        }
    }
}
