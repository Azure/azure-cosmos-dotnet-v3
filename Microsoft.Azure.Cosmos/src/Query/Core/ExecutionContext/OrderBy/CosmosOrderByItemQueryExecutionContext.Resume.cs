//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using PartitionKeyRange = Documents.PartitionKeyRange;
    using ResourceId = Documents.ResourceId;

    internal sealed partial class CosmosOrderByItemQueryExecutionContext : CosmosCrossPartitionQueryExecutionContext
    {
        private static class Expressions
        {
            public const string LessThan = "<";
            public const string LessThanOrEqualTo = "<=";
            public const string EqualTo = "=";
            public const string GreaterThan = ">";
            public const string GreaterThanOrEqualTo = ">=";
        }

        public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            CosmosElement requestContinuationToken,
            CancellationToken cancellationToken)
        {
            Debug.Assert(
                initParams.PartitionedQueryExecutionInfo.QueryInfo.HasOrderBy,
                "OrderBy~Context must have order by query info.");

            if (queryContext == null)
            {
                throw new ArgumentNullException(nameof(queryContext));
            }

            cancellationToken.ThrowIfCancellationRequested();

            // TODO (brchon): For now we are not honoring non deterministic ORDER BY queries, since there is a bug in the continuation logic.
            // We can turn it back on once the bug is fixed.
            // This shouldn't hurt any query results.
            OrderByItemProducerTreeComparer orderByItemProducerTreeComparer = new OrderByItemProducerTreeComparer(initParams.PartitionedQueryExecutionInfo.QueryInfo.OrderBy.ToArray());
            CosmosOrderByItemQueryExecutionContext context = new CosmosOrderByItemQueryExecutionContext(
                initPararms: queryContext,
                maxConcurrency: initParams.MaxConcurrency,
                maxItemCount: initParams.MaxItemCount,
                maxBufferedItemCount: initParams.MaxBufferedItemCount,
                consumeComparer: orderByItemProducerTreeComparer,
                testSettings: initParams.TestSettings);

            IReadOnlyList<string> orderByExpressions = initParams.PartitionedQueryExecutionInfo.QueryInfo.OrderByExpressions;
            IReadOnlyList<SortOrder> sortOrders = initParams.PartitionedQueryExecutionInfo.QueryInfo.OrderBy;
            if (orderByExpressions.Count != sortOrders.Count)
            {
                throw new ArgumentException("order by expressions count does not match sort order");
            }

            IReadOnlyList<OrderByColumn> columns = orderByExpressions
                .Zip(sortOrders, (expression, order) => new OrderByColumn(expression, order))
                .ToList();

            return (await context.TryInitializeAsync(
                sqlQuerySpec: initParams.SqlQuerySpec,
                requestContinuation: requestContinuationToken,
                collectionRid: initParams.CollectionRid,
                partitionKeyRanges: initParams.PartitionKeyRanges,
                initialPageSize: initParams.InitialPageSize,
                orderByColumns: columns,
                cancellationToken: cancellationToken))
                .Try<IDocumentQueryExecutionComponent>(() => context);
        }

        private async Task<TryCatch> TryInitializeAsync(
            SqlQuerySpec sqlQuerySpec,
            CosmosElement requestContinuation,
            string collectionRid,
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            IReadOnlyList<OrderByColumn> orderByColumns,
            CancellationToken cancellationToken)
        {
            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
            }

            if (collectionRid == null)
            {
                throw new ArgumentNullException(nameof(collectionRid));
            }

            if (partitionKeyRanges == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyRanges));
            }

            if (orderByColumns == null)
            {
                throw new ArgumentNullException(nameof(orderByColumns));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (requestContinuation == null)
            {
                // Start off all the partition key ranges with null continuation
                SqlQuerySpec rewrittenQueryForOrderBy = new SqlQuerySpec(
                    sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: True),
                    sqlQuerySpec.Parameters);
                Dictionary<PartitionKeyRange, string> partitionKeyRangeToContinuationToken = new Dictionary<PartitionKeyRange, string>();
                foreach (PartitionKeyRange partitionKeyRange in partitionKeyRanges)
                {
                    partitionKeyRangeToContinuationToken.Add(key: partitionKeyRange, value: null);
                }

                return await base.TryInitializeAsync(
                    collectionRid,
                    initialPageSize,
                    rewrittenQueryForOrderBy,
                    partitionKeyRangeToContinuationToken,
                    deferFirstPage: false,
                    filter: null,
                    tryFilterAsync: null,
                    cancellationToken);
            }

            TryCatch<PartitionMapping<OrderByContinuationToken>> tryGetOrderByContinuationTokenMapping = TryGetOrderByContinuationTokenMapping(
                partitionKeyRanges,
                requestContinuation,
                orderByColumns.Count);
            if (!tryGetOrderByContinuationTokenMapping.Succeeded)
            {
                return TryCatch.FromException(tryGetOrderByContinuationTokenMapping.Exception);
            }

            IReadOnlyList<CosmosElement> orderByItems = tryGetOrderByContinuationTokenMapping
                .Result
                .TargetPartition
                .Values
                .First()
                .OrderByItems
                .Select(x => x.Item)
                .ToList();
            if (orderByItems.Count != orderByColumns.Count)
            {
                return TryCatch.FromException(
                    new MalformedContinuationTokenException($"Order By Items from continuation token did not match the query text. Order by item count: {orderByItems.Count()} did not match column count {orderByColumns.Count()}. Continuation token: {requestContinuation}"));
            }

            ReadOnlyMemory<(OrderByColumn, CosmosElement)> columnAndItems = orderByColumns.Zip(orderByItems, (column, item) => (column, item)).ToArray();

            // For ascending order-by, left of target partition has filter expression > value,
            // right of target partition has filter expression >= value, 
            // and target partition takes the previous filter from continuation (or true if no continuation)
            (string leftFilter, string targetFilter, string rightFilter) = CosmosOrderByItemQueryExecutionContext.GetFormattedFilters(columnAndItems);
            List<(IReadOnlyDictionary<PartitionKeyRange, OrderByContinuationToken>, string)> tokenMappingAndFilters = new List<(IReadOnlyDictionary<PartitionKeyRange, OrderByContinuationToken>, string)>()
            {
                { (tryGetOrderByContinuationTokenMapping.Result.PartitionsLeftOfTarget, leftFilter) },
                { (tryGetOrderByContinuationTokenMapping.Result.TargetPartition, targetFilter) },
                { (tryGetOrderByContinuationTokenMapping.Result.PartitionsRightOfTarget, rightFilter) },
            };

            IReadOnlyList<SortOrder> sortOrders = orderByColumns.Select(column => column.SortOrder).ToList();
            foreach ((IReadOnlyDictionary<PartitionKeyRange, OrderByContinuationToken> tokenMapping, string filter) in tokenMappingAndFilters)
            {
                SqlQuerySpec rewrittenQueryForOrderBy = new SqlQuerySpec(
                    sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: filter),
                    sqlQuerySpec.Parameters);

                TryCatch tryInitialize = await base.TryInitializeAsync(
                    collectionRid,
                    initialPageSize,
                    rewrittenQueryForOrderBy,
                    tokenMapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.CompositeContinuationToken.Token),
                    deferFirstPage: false,
                    filter,
                    tryFilterAsync: async (itemProducerTree) =>
                    {
                        if (!tokenMapping.TryGetValue(
                            itemProducerTree.PartitionKeyRange,
                            out OrderByContinuationToken continuationToken))
                        {
                            throw new InvalidOperationException($"Failed to retrieve {nameof(OrderByContinuationToken)}.");
                        }

                        if (continuationToken == null)
                        {
                            return TryCatch.FromResult();
                        }

                        return await this.TryFilterAsync(
                            itemProducerTree,
                            sortOrders,
                            continuationToken,
                            cancellationToken);
                    },
                    cancellationToken);
                if (!tryInitialize.Succeeded)
                {
                    return tryInitialize;
                }
            }

            return TryCatch.FromResult();
        }

        private static TryCatch<PartitionMapping<OrderByContinuationToken>> TryGetOrderByContinuationTokenMapping(
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

            TryCatch<IReadOnlyList<OrderByContinuationToken>> tryExtractContinuationTokens = TryExtractContinuationTokens(continuationToken, numOrderByItems);
            if (!tryExtractContinuationTokens.Succeeded)
            {
                return TryCatch<PartitionMapping<OrderByContinuationToken>>.FromException(tryExtractContinuationTokens.Exception);
            }

            return TryGetInitializationInfo(
                partitionKeyRanges,
                tryExtractContinuationTokens.Result);
        }

        private static TryCatch<IReadOnlyList<OrderByContinuationToken>> TryExtractContinuationTokens(
            CosmosElement requestContinuation,
            int numOrderByItems)
        {
            if (requestContinuation == null)
            {
                throw new ArgumentNullException("continuation can not be null or empty.");
            }

            if (numOrderByItems < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numOrderByItems));
            }

            if (!(requestContinuation is CosmosArray cosmosArray))
            {
                return TryCatch<IReadOnlyList<OrderByContinuationToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Order by continuation token must be an array: {requestContinuation}."));
            }

            List<OrderByContinuationToken> orderByContinuationTokens = new List<OrderByContinuationToken>();
            foreach (CosmosElement arrayItem in cosmosArray)
            {
                TryCatch<OrderByContinuationToken> tryCreateOrderByContinuationToken = OrderByContinuationToken.TryCreateFromCosmosElement(arrayItem);
                if (!tryCreateOrderByContinuationToken.Succeeded)
                {
                    return TryCatch<IReadOnlyList<OrderByContinuationToken>>.FromException(tryCreateOrderByContinuationToken.Exception);
                }

                orderByContinuationTokens.Add(tryCreateOrderByContinuationToken.Result);
            }

            if (orderByContinuationTokens.Count == 0)
            {
                return TryCatch<IReadOnlyList<OrderByContinuationToken>>.FromException(
                    new MalformedContinuationTokenException(
                        $"Order by continuation token cannot be empty: {requestContinuation}."));
            }

            foreach (OrderByContinuationToken suppliedOrderByContinuationToken in orderByContinuationTokens)
            {
                if (suppliedOrderByContinuationToken.OrderByItems.Count != numOrderByItems)
                {
                    return TryCatch<IReadOnlyList<OrderByContinuationToken>>.FromException(
                        new MalformedContinuationTokenException(
                            $"Invalid order-by items in continuation token {requestContinuation} for OrderBy~Context."));
                }
            }

            return TryCatch<IReadOnlyList<OrderByContinuationToken>>.FromResult(orderByContinuationTokens);
        }

        /// <summary>
        /// When resuming an order by query we need to filter the document producers.
        /// </summary>
        /// <param name="producer">The producer to filter down.</param>
        /// <param name="sortOrders">The sort orders.</param>
        /// <param name="continuationToken">The continuation token.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        private async Task<TryCatch> TryFilterAsync(
            ItemProducerTree producer,
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

            foreach (ItemProducerTree tree in producer)
            {
                if (!ResourceId.TryParse(continuationToken.Rid, out ResourceId continuationRid))
                {
                    return TryCatch.FromException(
                        new MalformedContinuationTokenException(
                            $"Invalid Rid in the continuation token {continuationToken.CompositeContinuationToken.Token} for OrderBy~Context."));
                }

                Dictionary<string, ResourceId> resourceIds = new Dictionary<string, ResourceId>();
                int itemToSkip = continuationToken.SkipCount;
                bool continuationRidVerified = false;

                while (true)
                {
                    if (tree.Current == null)
                    {
                        // This document producer doesn't have anymore items.
                        break;
                    }

                    OrderByQueryResult orderByResult = new OrderByQueryResult(tree.Current);
                    // Throw away documents until it matches the item from the continuation token.
                    int cmp = 0;
                    for (int i = 0; i < sortOrders.Count; ++i)
                    {
                        cmp = ItemComparer.Instance.Compare(
                            continuationToken.OrderByItems[i].Item,
                            orderByResult.OrderByItems[i].Item);

                        if (cmp != 0)
                        {
                            cmp = sortOrders[i] == SortOrder.Ascending ? cmp : -cmp;
                            break;
                        }
                    }

                    if (cmp < 0)
                    {
                        // We might have passed the item due to deletions and filters.
                        break;
                    }

                    if (cmp == 0)
                    {
                        if (!resourceIds.TryGetValue(orderByResult.Rid, out ResourceId rid))
                        {
                            if (!ResourceId.TryParse(orderByResult.Rid, out rid))
                            {
                                return TryCatch.FromException(
                                    new MalformedContinuationTokenException(
                                        $"Invalid Rid in the continuation token {continuationToken.CompositeContinuationToken.Token} for OrderBy~Context~TryParse."));
                            }

                            resourceIds.Add(orderByResult.Rid, rid);
                        }

                        if (!continuationRidVerified)
                        {
                            if (continuationRid.Database != rid.Database || continuationRid.DocumentCollection != rid.DocumentCollection)
                            {
                                return TryCatch.FromException(
                                    new MalformedContinuationTokenException(
                                        $"Invalid Rid in the continuation token {continuationToken.CompositeContinuationToken.Token} for OrderBy~Context."));
                            }

                            continuationRidVerified = true;
                        }

                        // Once the item matches the order by items from the continuation tokens
                        // We still need to remove all the documents that have a lower rid in the rid sort order.
                        // If there is a tie in the sort order the documents should be in _rid order in the same direction as the first order by field.
                        // So if it's ORDER BY c.age ASC, c.name DESC the _rids are ASC 
                        // If ti's ORDER BY c.age DESC, c.name DESC the _rids are DESC
                        cmp = continuationRid.Document.CompareTo(rid.Document);
                        if (sortOrders[0] == SortOrder.Descending)
                        {
                            cmp = -cmp;
                        }

                        // We might have passed the item due to deletions and filters.
                        // We also have a skip count for JOINs
                        if (cmp < 0 || (cmp == 0 && itemToSkip-- <= 0))
                        {
                            break;
                        }
                    }

                    if (!tree.TryMoveNextDocumentWithinPage())
                    {
                        while (true)
                        {
                            (bool successfullyMovedNext, QueryResponseCore? failureResponse) = await tree.TryMoveNextPageAsync(cancellationToken);
                            if (!successfullyMovedNext)
                            {
                                if (failureResponse.HasValue)
                                {
                                    return TryCatch.FromException(
                                        failureResponse.Value.CosmosException);
                                }

                                break;
                            }

                            if (tree.IsAtBeginningOfPage)
                            {
                                break;
                            }

                            if (tree.TryMoveNextDocumentWithinPage())
                            {
                                break;
                            }
                        }
                    }
                }
            }

            return TryCatch.FromResult();
        }

        private static void AppendToBuilders((StringBuilder leftFilter, StringBuilder targetFilter, StringBuilder rightFilter) builders, object str)
        {
            CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, str, str, str);
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

            (StringBuilder, StringBuilder, StringBuilder) builders = (left, right, target);

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
                    bool firstPrefix = prefixLength == 1;

                    CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, "(");

                    for (int index = 0; index < prefixLength; index++)
                    {
                        string expression = columnAndItemPrefix[index].orderByColumn.Expression;
                        SortOrder sortOrder = columnAndItemPrefix[index].orderByColumn.SortOrder;
                        CosmosElement orderByItem = columnAndItemPrefix[index].orderByItem;
                        bool lastItem = index == prefixLength - 1;

                        // Append Expression
                        CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, expression);
                        CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, " ");

                        // Append binary operator
                        if (lastItem)
                        {
                            string inequality = sortOrder == SortOrder.Descending ? Expressions.LessThan : Expressions.GreaterThan;
                            CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, inequality);
                            if (lastPrefix)
                            {
                                CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, string.Empty, Expressions.EqualTo, Expressions.EqualTo);
                            }
                        }
                        else
                        {
                            CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, Expressions.EqualTo);
                        }

                        // Append SortOrder
                        StringBuilder sb = new StringBuilder();
                        CosmosElementToQueryLiteral cosmosElementToQueryLiteral = new CosmosElementToQueryLiteral(sb);
                        orderByItem.Accept(cosmosElementToQueryLiteral);
                        string orderByItemToString = sb.ToString();
                        CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, " ");
                        CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, orderByItemToString);
                        CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, " ");

                        if (!lastItem)
                        {
                            CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, "AND ");
                        }
                    }

                    CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, ")");
                    if (!lastPrefix)
                    {
                        CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, " OR ");
                    }
                }
            }

            return (left.ToString(), target.ToString(), right.ToString());
        }

        private readonly struct OrderByInitInfo
        {
            public OrderByInitInfo(
                RangeFilterInitializationInfo[] filters,
                IReadOnlyDictionary<string, OrderByContinuationToken> continuationTokens)
            {
                this.Filters = filters;
                this.ContinuationTokens = continuationTokens;
            }

            public RangeFilterInitializationInfo[] Filters { get; }

            public IReadOnlyDictionary<string, OrderByContinuationToken> ContinuationTokens { get; }
        }

        private readonly struct OrderByColumn
        {
            public OrderByColumn(string expression, SortOrder sortOrder)
            {
                this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
                this.SortOrder = sortOrder;
            }

            public string Expression { get; }
            public SortOrder SortOrder { get; }
        }
    }
}
