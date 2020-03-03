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
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using PartitionKeyRange = Documents.PartitionKeyRange;
    using ResourceId = Documents.ResourceId;

    /// <summary>
    /// CosmosOrderByItemQueryExecutionContext is a concrete implementation for CrossPartitionQueryExecutionContext.
    /// This class is responsible for draining cross partition queries that have order by conditions.
    /// The way order by queries work is that they are doing a k-way merge of sorted lists from each partition with an added condition.
    /// The added condition is that if 2 or more top documents from different partitions are equivalent then we drain from the left most partition first.
    /// This way we can generate a single continuation token for all n partitions.
    /// This class is able to stop and resume execution by generating continuation tokens and reconstructing an execution context from said token.
    /// </summary>
    internal sealed class CosmosOrderByItemQueryExecutionContext : CosmosCrossPartitionQueryExecutionContext
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
        private const string True = "true";

        /// <summary>
        /// Function to determine the priority of fetches.
        /// Basically we are fetching from the partition with the least number of buffered documents first.
        /// </summary>
        private static readonly Func<ItemProducerTree, int> FetchPriorityFunction = itemProducerTree => itemProducerTree.BufferedItemCount;

        /// <summary>
        /// Skip count used for JOIN queries.
        /// You can read up more about this in the documentation for the continuation token.
        /// </summary>
        private int skipCount;

        /// <summary>
        /// We need to keep track of the previousRid, since order by queries don't drain full pages.
        /// </summary>
        private string previousRid;

        private IList<OrderByItem> previousOrderByItems;

        /// <summary>
        /// Initializes a new instance of the CosmosOrderByItemQueryExecutionContext class.
        /// </summary>
        /// <param name="initPararms">The params used to construct the base class.</param>
        /// For cross partition order by queries a query like "SELECT c.id, c.field_0 ORDER BY r.field_7 gets rewritten as:
        /// <![CDATA[
        /// SELECT r._rid, [{"item": r.field_7}] AS orderByItems, {"id": r.id, "field_0": r.field_0} AS payload
        /// FROM r
        /// WHERE({ document db - formattable order by query - filter})
        /// ORDER BY r.field_7]]>
        /// This is needed because we need to add additional filters to the query when we resume from a continuation,
        /// and it lets us easily parse out the _rid orderByItems, and payload without parsing the entire document (and having to remember the order by field).
        /// <param name="maxConcurrency">The max concurrency</param>
        /// <param name="maxBufferedItemCount">The max buffered item count</param>
        /// <param name="maxItemCount">Max item count</param>
        /// <param name="consumeComparer">Comparer used to internally compare documents from different sorted partitions.</param>
        /// <param name="testSettings">Test settings.</param>
        private CosmosOrderByItemQueryExecutionContext(
            CosmosQueryContext initPararms,
            int? maxConcurrency,
            int? maxItemCount,
            int? maxBufferedItemCount,
            OrderByItemProducerTreeComparer consumeComparer,
            TestInjections testSettings)
            : base(
                queryContext: initPararms,
                maxConcurrency: maxConcurrency,
                maxItemCount: maxItemCount,
                maxBufferedItemCount: maxBufferedItemCount,
                moveNextComparer: consumeComparer,
                fetchPrioirtyFunction: CosmosOrderByItemQueryExecutionContext.FetchPriorityFunction,
                equalityComparer: new OrderByEqualityComparer(consumeComparer),
                returnResultsInDeterministicOrder: true,
                testSettings: testSettings)
        {
        }

        /// <summary>
        /// Gets the continuation token for an order by query.
        /// </summary>
        protected override string ContinuationToken
        {
            // In general the continuation token for order by queries contains the following information:
            // 1) What partition did we leave off on
            // 2) What value did we leave off 
            // Along with the constraints that we get from how we drain the documents:
            //      Let <x, y> mean that the last item we drained was item x from partition y.
            //      Then we know that for all partitions
            //          * < y that we have drained all items <= x
            //          * > y that we have drained all items < x
            //          * = y that we have drained all items <= x based on the backend continuation token for y
            // With this information we have captured the progress for all partitions in a single continuation token.
            get
            {
                IEnumerable<ItemProducer> activeItemProducers = this.GetActiveItemProducers();
                string continuationToken;
                if (activeItemProducers.Any())
                {
                    IEnumerable<CosmosElement> orderByContinuationTokens = activeItemProducers.Select((itemProducer) =>
                    {
                        OrderByQueryResult orderByQueryResult = new OrderByQueryResult(itemProducer.Current);
                        string filter = itemProducer.Filter;
                        OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                            new CompositeContinuationToken
                            {
                                Token = itemProducer.PreviousContinuationToken,
                                Range = itemProducer.PartitionKeyRange.ToRange(),
                            },
                            orderByQueryResult.OrderByItems,
                            orderByQueryResult.Rid,
                            this.ShouldIncrementSkipCount(itemProducer) ? this.skipCount + 1 : 0,
                            filter);

                        return OrderByContinuationToken.ToCosmosElement(orderByContinuationToken);
                    });

                    continuationToken = CosmosArray.Create(orderByContinuationTokens).ToString();
                }
                else
                {
                    continuationToken = null;
                }

                // Note we are no longer escaping non ascii continuation tokens.
                // It is the callers job to encode a continuation token before adding it to a header in their service.

                return continuationToken;
            }
        }

        /// <summary>
        /// Gets the FeedToken for an order by query
        /// </summary>
        protected override FeedToken FeedToken
        {
            // In general the continuation token for order by queries contains the following information:
            // 1) What partition did we leave off on
            // 2) What value did we leave off 
            // Along with the constraints that we get from how we drain the documents:
            //      Let <x, y> mean that the last item we drained was item x from partition y.
            //      Then we know that for all partitions
            //          * < y that we have drained all items <= x
            //          * > y that we have drained all items < x
            //          * = y that we have drained all items <= x based on the backend continuation token for y
            // With this information we have captured the progress for all partitions in a single continuation token.
            get
            {
                IEnumerable<ItemProducer> allProducers = this.GetAllItemProducers();
                if (!allProducers.Any())
                {
                    return null;
                }

                List<Documents.Routing.Range<string>> rangesList = allProducers.Select(producer => producer.PartitionKeyRange.ToRange()).ToList();
                rangesList.Sort(Documents.Routing.Range<string>.MinComparer.Instance);
                Documents.Routing.Range<string> completeRange = new Documents.Routing.Range<string>(rangesList[0].Min, rangesList[rangesList.Count - 1].Max, true, false);
                // Single FeedToken with the completeRange and continuation
                FeedTokenEPKRange feedToken = new FeedTokenEPKRange(
                    string.Empty, // Rid or container reference not available
                    completeRange,
                    this.ContinuationToken);
                return feedToken;
            }
        }

        public static async Task<TryCatch<IDocumentQueryExecutionComponent>> TryCreateAsync(
            CosmosQueryContext queryContext,
            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams,
            string requestContinuationToken,
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
            OrderByItemProducerTreeComparer orderByItemProducerTreeComparer = new OrderByItemProducerTreeComparer(initParams.PartitionedQueryExecutionInfo.QueryInfo.OrderBy);
            CosmosOrderByItemQueryExecutionContext context = new CosmosOrderByItemQueryExecutionContext(
                initPararms: queryContext,
                maxConcurrency: initParams.MaxConcurrency,
                maxItemCount: initParams.MaxItemCount,
                maxBufferedItemCount: initParams.MaxBufferedItemCount,
                consumeComparer: orderByItemProducerTreeComparer,
                testSettings: initParams.TestSettings);

            return (await context.TryInitializeAsync(
                sqlQuerySpec: initParams.SqlQuerySpec,
                requestContinuation: requestContinuationToken,
                collectionRid: initParams.CollectionRid,
                partitionKeyRanges: initParams.PartitionKeyRanges,
                initialPageSize: initParams.InitialPageSize,
                sortOrders: initParams.PartitionedQueryExecutionInfo.QueryInfo.OrderBy,
                orderByExpressions: initParams.PartitionedQueryExecutionInfo.QueryInfo.OrderByExpressions,
                cancellationToken: cancellationToken))
                .Try<IDocumentQueryExecutionComponent>(() => context);
        }

        /// <summary>
        /// Drains a page of documents from this context.
        /// </summary>
        /// <param name="maxElements">The maximum number of elements.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A task that when awaited on return a page of documents.</returns>
        public override async Task<QueryResponseCore> DrainAsync(int maxElements, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

            List<CosmosElement> results = new List<CosmosElement>();
            while (results.Count < maxElements)
            {
                // Only drain from the highest priority document producer 
                // We need to pop and push back the document producer tree, since the priority changes according to the sort order.
                ItemProducerTree currentItemProducerTree = this.PopCurrentItemProducerTree();
                try
                {
                    if (!currentItemProducerTree.HasMoreResults)
                    {
                        // This means there are no more items to drain
                        break;
                    }

                    OrderByQueryResult orderByQueryResult = new OrderByQueryResult(currentItemProducerTree.Current);

                    // Only add the payload, since other stuff is garbage from the caller's perspective.
                    results.Add(orderByQueryResult.Payload);

                    // If we are at the beginning of the page and seeing an rid from the previous page we should increment the skip count
                    // due to the fact that JOINs can make a document appear multiple times and across continuations, so we don't want to
                    // surface this more than needed. More information can be found in the continuation token docs.
                    if (this.ShouldIncrementSkipCount(currentItemProducerTree.CurrentItemProducerTree.Root))
                    {
                        ++this.skipCount;
                    }
                    else
                    {
                        this.skipCount = 0;
                    }

                    this.previousRid = orderByQueryResult.Rid;
                    this.previousOrderByItems = orderByQueryResult.OrderByItems;

                    if (!currentItemProducerTree.TryMoveNextDocumentWithinPage())
                    {
                        while (true)
                        {
                            (bool movedToNextPage, QueryResponseCore? failureResponse) = await currentItemProducerTree.TryMoveNextPageAsync(cancellationToken);
                            if (!movedToNextPage)
                            {
                                if (failureResponse.HasValue)
                                {
                                    // TODO: We can buffer this failure so that the user can still get the pages we already got.
                                    return failureResponse.Value;
                                }

                                break;
                            }

                            if (currentItemProducerTree.IsAtBeginningOfPage)
                            {
                                break;
                            }

                            if (currentItemProducerTree.TryMoveNextDocumentWithinPage())
                            {
                                break;
                            }
                        }
                    }
                }
                finally
                {
                    this.PushCurrentItemProducerTree(currentItemProducerTree);
                }
            }

            return QueryResponseCore.CreateSuccess(
                result: results,
                requestCharge: this.requestChargeTracker.GetAndResetCharge(),
                activityId: null,
                responseLengthBytes: this.GetAndResetResponseLengthBytes(),
                disallowContinuationTokenMessage: null,
                continuationToken: this.ContinuationToken,
                diagnostics: this.GetAndResetDiagnostics());
        }

        /// <summary>
        /// Gets whether or not we should increment the skip count based on the rid of the document.
        /// </summary>
        /// <param name="currentItemProducer">The current document producer.</param>
        /// <returns>Whether or not we should increment the skip count.</returns>
        private bool ShouldIncrementSkipCount(ItemProducer currentItemProducer)
        {
            // If we are not at the beginning of the page and we saw the same rid again.
            return !currentItemProducer.IsAtBeginningOfPage &&
                string.Equals(
                    this.previousRid,
                    new OrderByQueryResult(currentItemProducer.Current).Rid,
                    StringComparison.Ordinal);
        }

        private async Task<TryCatch> TryInitializeAsync(
            SqlQuerySpec sqlQuerySpec,
            string requestContinuation,
            string collectionRid,
            List<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            SortOrder[] sortOrders,
            string[] orderByExpressions,
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

            if (sortOrders == null)
            {
                throw new ArgumentNullException(nameof(sortOrders));
            }

            if (orderByExpressions == null)
            {
                throw new ArgumentNullException(nameof(orderByExpressions));
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (requestContinuation == null)
            {
                SqlQuerySpec sqlQuerySpecForInit = new SqlQuerySpec(
                    sqlQuerySpec.QueryText.Replace(oldValue: FormatPlaceHolder, newValue: True),
                    sqlQuerySpec.Parameters);

                TryCatch tryInitialize = await base.TryInitializeAsync(
                    collectionRid,
                    partitionKeyRanges,
                    initialPageSize,
                    sqlQuerySpecForInit,
                    cancellationToken: cancellationToken,
                    targetRangeToContinuationMap: null,
                    deferFirstPage: false,
                    filter: null,
                    tryFilterAsync: null);
                if (!tryInitialize.Succeeded)
                {
                    return tryInitialize;
                }
            }
            else
            {
                TryCatch<OrderByContinuationToken[]> tryExtractContinuationTokens = CosmosOrderByItemQueryExecutionContext.TryExtractContinuationTokens(
                    requestContinuation,
                    sortOrders,
                    orderByExpressions);
                if (!tryExtractContinuationTokens.Succeeded)
                {
                    return TryCatch.FromException(tryExtractContinuationTokens.Exception);
                }

                TryCatch<OrderByInitInfo> tryGetOrderByInitInfo = CosmosOrderByItemQueryExecutionContext.TryGetOrderByPartitionKeyRangesInitializationInfo(
                    tryExtractContinuationTokens.Result,
                    partitionKeyRanges,
                    sortOrders,
                    orderByExpressions);
                if (!tryGetOrderByInitInfo.Succeeded)
                {
                    return TryCatch.FromException(tryGetOrderByInitInfo.Exception);
                }

                OrderByInitInfo initiaizationInfo = tryGetOrderByInitInfo.Result;
                RangeFilterInitializationInfo[] orderByInfos = initiaizationInfo.Filters;
                IReadOnlyDictionary<string, OrderByContinuationToken> targetRangeToOrderByContinuationMap = initiaizationInfo.ContinuationTokens;
                Debug.Assert(
                    targetRangeToOrderByContinuationMap != null,
                    "If targetRangeToOrderByContinuationMap can't be null is valid continuation is supplied");

                // For ascending order-by, left of target partition has filter expression > value,
                // right of target partition has filter expression >= value, 
                // and target partition takes the previous filter from continuation (or true if no continuation)
                foreach (RangeFilterInitializationInfo info in orderByInfos)
                {
                    if (info.StartIndex > info.EndIndex)
                    {
                        continue;
                    }

                    PartialReadOnlyList<PartitionKeyRange> partialRanges =
                        new PartialReadOnlyList<PartitionKeyRange>(
                            partitionKeyRanges,
                            info.StartIndex,
                            info.EndIndex - info.StartIndex + 1);

                    SqlQuerySpec sqlQuerySpecForInit = new SqlQuerySpec(
                        sqlQuerySpec.QueryText.Replace(FormatPlaceHolder, info.Filter),
                        sqlQuerySpec.Parameters);

                    TryCatch tryInitialize = await base.TryInitializeAsync(
                        collectionRid,
                        partialRanges,
                        initialPageSize,
                        sqlQuerySpecForInit,
                        targetRangeToOrderByContinuationMap.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.CompositeContinuationToken.Token),
                        false,
                        info.Filter,
                        async (itemProducerTree) =>
                        {
                            if (targetRangeToOrderByContinuationMap.TryGetValue(
                                itemProducerTree.Root.PartitionKeyRange.Id,
                                out OrderByContinuationToken continuationToken))
                            {
                                TryCatch tryFilter = await this.TryFilterAsync(
                                    itemProducerTree,
                                    sortOrders,
                                    continuationToken,
                                    cancellationToken);

                                if (!tryFilter.Succeeded)
                                {
                                    return tryFilter;
                                }
                            }

                            return TryCatch.FromResult();
                        },
                        cancellationToken);
                    if (!tryInitialize.Succeeded)
                    {
                        return tryInitialize;
                    }
                }
            }

            return TryCatch.FromResult();
        }

        private static TryCatch<OrderByContinuationToken[]> TryExtractContinuationTokens(
            string requestContinuation,
            SortOrder[] sortOrders,
            string[] orderByExpressions)
        {
            Debug.Assert(
                !(orderByExpressions == null
                || orderByExpressions.Length <= 0
                || sortOrders == null
                || sortOrders.Length <= 0
                || orderByExpressions.Length != sortOrders.Length),
                "Partitioned QueryExecutionInfo returned bogus results.");

            if (string.IsNullOrWhiteSpace(requestContinuation))
            {
                throw new ArgumentNullException("continuation can not be null or empty.");
            }

            if (!CosmosArray.TryParse(requestContinuation, out CosmosArray cosmosArray))
            {
                return TryCatch<OrderByContinuationToken[]>.FromException(
                    new MalformedContinuationTokenException($"Order by continuation token must be an array: {requestContinuation}."));
            }

            List<OrderByContinuationToken> orderByContinuationTokens = new List<OrderByContinuationToken>();
            foreach (CosmosElement arrayItem in cosmosArray)
            {
                TryCatch<OrderByContinuationToken> tryCreateOrderByContinuationToken = OrderByContinuationToken.TryCreateFromCosmosElement(arrayItem);
                if (!tryCreateOrderByContinuationToken.Succeeded)
                {
                    return TryCatch<OrderByContinuationToken[]>.FromException(tryCreateOrderByContinuationToken.Exception);
                }

                orderByContinuationTokens.Add(tryCreateOrderByContinuationToken.Result);
            }

            if (orderByContinuationTokens.Count == 0)
            {
                return TryCatch<OrderByContinuationToken[]>.FromException(
                    new MalformedContinuationTokenException($"Order by continuation token cannot be empty: {requestContinuation}."));
            }

            foreach (OrderByContinuationToken suppliedOrderByContinuationToken in orderByContinuationTokens)
            {
                if (suppliedOrderByContinuationToken.OrderByItems.Count != sortOrders.Length)
                {
                    return TryCatch<OrderByContinuationToken[]>.FromException(
                        new MalformedContinuationTokenException($"Invalid order-by items in continuation token {requestContinuation} for OrderBy~Context."));
                }
            }

            return TryCatch<OrderByContinuationToken[]>.FromResult(orderByContinuationTokens.ToArray());
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
            SortOrder[] sortOrders,
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
                        new MalformedContinuationTokenException($"Invalid Rid in the continuation token {continuationToken.CompositeContinuationToken.Token} for OrderBy~Context."));
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
                    for (int i = 0; i < sortOrders.Length; ++i)
                    {
                        cmp = ItemComparer.Instance.Compare(
                            continuationToken.OrderByItems[i].Item,
                            orderByResult.OrderByItems[i].Item);

                        if (cmp != 0)
                        {
                            cmp = sortOrders[i] != SortOrder.Descending ? cmp : -cmp;
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
                                    new MalformedContinuationTokenException($"Invalid Rid in the continuation token {continuationToken.CompositeContinuationToken.Token} for OrderBy~Context~TryParse."));

                            }

                            resourceIds.Add(orderByResult.Rid, rid);
                        }

                        if (!continuationRidVerified)
                        {
                            if (continuationRid.Database != rid.Database || continuationRid.DocumentCollection != rid.DocumentCollection)
                            {
                                return TryCatch.FromException(
                                    new MalformedContinuationTokenException($"Invalid Rid in the continuation token {continuationToken.CompositeContinuationToken.Token} for OrderBy~Context."));
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

        /// <summary>
        /// Gets the filters for every partition.
        /// </summary>
        private static TryCatch<OrderByInitInfo> TryGetOrderByPartitionKeyRangesInitializationInfo(
            OrderByContinuationToken[] suppliedContinuationTokens,
            List<PartitionKeyRange> partitionKeyRanges,
            SortOrder[] sortOrders,
            string[] orderByExpressions)
        {
            TryCatch<InitInfo<OrderByContinuationToken>> tryFindRangeAndContinuationTokensMonad = CosmosCrossPartitionQueryExecutionContext.TryFindTargetRangeAndExtractContinuationTokens(
                partitionKeyRanges,
                suppliedContinuationTokens
                    .Select(token => Tuple.Create(token, token.CompositeContinuationToken.Range)));

            return tryFindRangeAndContinuationTokensMonad.Try<OrderByInitInfo>((indexAndContinuationTokens) =>
            {
                int minIndex = indexAndContinuationTokens.TargetIndex;
                IReadOnlyDictionary<string, OrderByContinuationToken> partitionKeyRangeToContinuationToken = indexAndContinuationTokens.ContinuationTokens;

                FormattedFilterInfo formattedFilterInfo = CosmosOrderByItemQueryExecutionContext.GetFormattedFilters(
                    orderByExpressions,
                    suppliedContinuationTokens,
                    sortOrders);

                RangeFilterInitializationInfo[] filters = new RangeFilterInitializationInfo[]
                {
                    new RangeFilterInitializationInfo(
                        filter: formattedFilterInfo.FilterForRangesLeftOfTargetRanges,
                        startIndex: 0,
                        endIndex: minIndex - 1),
                    new RangeFilterInitializationInfo(
                        filter: formattedFilterInfo.FiltersForTargetRange,
                        startIndex: minIndex,
                        endIndex: minIndex),
                    new RangeFilterInitializationInfo(
                        filter: formattedFilterInfo.FilterForRangesRightOfTargetRanges,
                        startIndex: minIndex + 1,
                        endIndex: partitionKeyRanges.Count - 1),
                };

                return new OrderByInitInfo(
                    filters,
                    partitionKeyRangeToContinuationToken);
            });
        }

        /// <summary>
        /// Gets the formatted filters for every partition.
        /// </summary>
        /// <param name="expressions">The filter expressions.</param>
        /// <param name="continuationTokens">The continuation token.</param>
        /// <param name="sortOrders">The sort orders.</param>
        /// <returns>The formatted filters for every partition.</returns>
        private static FormattedFilterInfo GetFormattedFilters(
            string[] expressions,
            OrderByContinuationToken[] continuationTokens,
            SortOrder[] sortOrders)
        {
            // Validate the inputs
            for (int index = 0; index < continuationTokens.Length; index++)
            {
                Debug.Assert(continuationTokens[index].OrderByItems.Count == sortOrders.Length, "Expect values and orders are the same size.");
                Debug.Assert(expressions.Length == sortOrders.Length, "Expect expressions and orders are the same size.");
            }

            Tuple<string, string, string> filters = CosmosOrderByItemQueryExecutionContext.GetFormattedFilters(
                expressions,
                continuationTokens[0].OrderByItems.Select(orderByItem => orderByItem.Item).ToArray(),
                sortOrders);

            return new FormattedFilterInfo(filters.Item1, filters.Item2, filters.Item3);
        }

        private static void AppendToBuilders(Tuple<StringBuilder, StringBuilder, StringBuilder> builders, object str)
        {
            CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, str, str, str);
        }

        private static void AppendToBuilders(Tuple<StringBuilder, StringBuilder, StringBuilder> builders, object left, object target, object right)
        {
            builders.Item1.Append(left);
            builders.Item2.Append(target);
            builders.Item3.Append(right);
        }

        private static Tuple<string, string, string> GetFormattedFilters(
            string[] expressions,
            CosmosElement[] orderByItems,
            SortOrder[] sortOrders)
        {
            // When we run cross partition queries, 
            // we only serialize the continuation token for the partition that we left off on.
            // The only problem is that when we resume the order by query, 
            // we don't have continuation tokens for all other partition.
            // The saving grace is that the data has a composite sort order(query sort order, partition key range id)
            // so we can generate range filters which in turn the backend will turn into rid based continuation tokens,
            // which is enough to get the streams of data flowing from all partitions.
            // The details of how this is done is described below:
            int numOrderByItems = expressions.Length;
            bool isSingleOrderBy = numOrderByItems == 1;
            StringBuilder left = new StringBuilder();
            StringBuilder target = new StringBuilder();
            StringBuilder right = new StringBuilder();

            Tuple<StringBuilder, StringBuilder, StringBuilder> builders = new Tuple<StringBuilder, StringBuilder, StringBuilder>(left, right, target);

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
                string expression = expressions.First();
                SortOrder sortOrder = sortOrders.First();
                CosmosElement orderByItem = orderByItems.First();
                StringBuilder sb = new StringBuilder();
                CosmosElementToQueryLiteral cosmosElementToQueryLiteral = new CosmosElementToQueryLiteral(sb);
                orderByItem.Accept(cosmosElementToQueryLiteral);
                string orderByItemToString = sb.ToString();
                left.Append($"{expression} {(sortOrder == SortOrder.Descending ? "<" : ">")} {orderByItemToString}");
                target.Append($"{expression} {(sortOrder == SortOrder.Descending ? "<=" : ">=")} {orderByItemToString}");
                right.Append($"{expression} {(sortOrder == SortOrder.Descending ? "<=" : ">=")} {orderByItemToString}");
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
                    ArraySegment<string> expressionPrefix = new ArraySegment<string>(expressions, 0, prefixLength);
                    ArraySegment<SortOrder> sortOrderPrefix = new ArraySegment<SortOrder>(sortOrders, 0, prefixLength);
                    ArraySegment<CosmosElement> orderByItemsPrefix = new ArraySegment<CosmosElement>(orderByItems, 0, prefixLength);

                    bool lastPrefix = prefixLength == numOrderByItems;
                    bool firstPrefix = prefixLength == 1;

                    CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, "(");

                    for (int index = 0; index < prefixLength; index++)
                    {
                        string expression = expressionPrefix.ElementAt(index);
                        SortOrder sortOrder = sortOrderPrefix.ElementAt(index);
                        CosmosElement orderByItem = orderByItemsPrefix.ElementAt(index);
                        bool lastItem = index == prefixLength - 1;

                        // Append Expression
                        CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, expression);
                        CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, " ");

                        // Append binary operator
                        if (lastItem)
                        {
                            string inequality = sortOrder == SortOrder.Descending ? "<" : ">";
                            CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, inequality);
                            if (lastPrefix)
                            {
                                CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, string.Empty, "=", "=");
                            }
                        }
                        else
                        {
                            CosmosOrderByItemQueryExecutionContext.AppendToBuilders(builders, "=");
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

            return new Tuple<string, string, string>(left.ToString(), target.ToString(), right.ToString());
        }

        private readonly struct OrderByInitInfo
        {
            public OrderByInitInfo(RangeFilterInitializationInfo[] filters, IReadOnlyDictionary<string, OrderByContinuationToken> continuationTokens)
            {
                this.Filters = filters;
                this.ContinuationTokens = continuationTokens;
            }

            public RangeFilterInitializationInfo[] Filters { get; }

            public IReadOnlyDictionary<string, OrderByContinuationToken> ContinuationTokens { get; }
        }

        /// <summary>
        /// Struct to hold all the filters for every partition.
        /// </summary>
        private readonly struct FormattedFilterInfo
        {
            /// <summary>
            /// Filters for current partition.
            /// </summary>
            public readonly string FiltersForTargetRange;

            /// <summary>
            /// Filters for partitions left of the current partition.
            /// </summary>
            public readonly string FilterForRangesLeftOfTargetRanges;

            /// <summary>
            /// Filters for partitions right of the current partition.
            /// </summary>
            public readonly string FilterForRangesRightOfTargetRanges;

            /// <summary>
            /// Initializes a new instance of the FormattedFilterInfo struct.
            /// </summary>
            /// <param name="leftFilter">The filters for the partitions left of the current partition.</param>
            /// <param name="targetFilter">The filters for the current partition.</param>
            /// <param name="rightFilters">The filters for the partitions right of the current partition.</param>
            public FormattedFilterInfo(string leftFilter, string targetFilter, string rightFilters)
            {
                this.FilterForRangesLeftOfTargetRanges = leftFilter;
                this.FiltersForTargetRange = targetFilter;
                this.FilterForRangesRightOfTargetRanges = rightFilters;
            }
        }

        /// <summary>
        /// Equality comparer used to determine if a document producer needs it's continuation token returned.
        /// Basically just says that the continuation token can be flushed once you stop seeing duplicates.
        /// </summary>
        private sealed class OrderByEqualityComparer : IEqualityComparer<CosmosElement>
        {
            /// <summary>
            /// The order by comparer.
            /// </summary>
            private readonly OrderByItemProducerTreeComparer orderByConsumeComparer;

            /// <summary>
            /// Initializes a new instance of the OrderByEqualityComparer class.
            /// </summary>
            /// <param name="orderByConsumeComparer">The order by consume comparer.</param>
            public OrderByEqualityComparer(OrderByItemProducerTreeComparer orderByConsumeComparer)
            {
                this.orderByConsumeComparer = orderByConsumeComparer ?? throw new ArgumentNullException($"{nameof(orderByConsumeComparer)} can not be null.");
            }

            /// <summary>
            /// Gets whether two OrderByQueryResult instances are equal.
            /// </summary>
            /// <param name="x">The first.</param>
            /// <param name="y">The second.</param>
            /// <returns>Whether two OrderByQueryResult instances are equal.</returns>
            public bool Equals(CosmosElement x, CosmosElement y)
            {
                OrderByQueryResult orderByQueryResultX = new OrderByQueryResult(x);
                OrderByQueryResult orderByQueryResultY = new OrderByQueryResult(y);
                return this.orderByConsumeComparer.CompareOrderByItems(
                    orderByQueryResultX.OrderByItems,
                    orderByQueryResultY.OrderByItems) == 0;
            }

            /// <summary>
            /// Gets the hash code for object.
            /// </summary>
            /// <param name="obj">The object to hash.</param>
            /// <returns>The hash code for the OrderByQueryResult object.</returns>
            public int GetHashCode(CosmosElement obj)
            {
                return 0;
            }
        }
    }
}
