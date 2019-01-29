//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections.Generic;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;

    internal sealed class OrderByDocumentQueryExecutionContext : ParallelDocumentQueryExecutionContextBase<OrderByQueryResult>
    {
        private const string FormatPlaceHolder = "{documentdb-formattableorderbyquery-filter}";
        private const string True = "true";

        private static readonly Func<DocumentProducer<OrderByQueryResult>, int> TaskPriorityFunc =
            producer => producer.BufferedItemCount;

        private readonly PriorityQueue<DocumentProducer<OrderByQueryResult>> documentProducerConsumerQueue;
        private readonly IDictionary<string, string> filters;
        private readonly OrderByConsumeComparer consumeComparer;

        private DocumentProducer<OrderByQueryResult> currentDocumentProducer;
        private int skipCount;
        private string previousRid;
        private string collectionRid;

        private QueryItem[] currentOrderByItems;

        private OrderByDocumentQueryExecutionContext(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            string resourceLink,
            string rewrittenQuery,
            bool isContinuationExpected,
            bool getLazyFeedResponse,
            OrderByConsumeComparer consumeComparer,
            string collectionRid,
            Guid correlatedActivityId) :
            base(
            client,
            resourceTypeEnum,
            resourceType,
            expression,
            feedOptions,
            resourceLink,
            rewrittenQuery,
            correlatedActivityId,
            isContinuationExpected: isContinuationExpected,
            getLazyFeedResponse: getLazyFeedResponse,
            isDynamicPageSizeAllowed: true)
        {
            this.collectionRid = collectionRid;
            this.documentProducerConsumerQueue = new PriorityQueue<DocumentProducer<OrderByQueryResult>>(consumeComparer);
            this.filters = new Dictionary<string, string>();
            this.consumeComparer = consumeComparer;
        }

        public override bool IsDone
        {
            get
            {
                return this.TaskScheduler.IsStopped ||
                    (this.currentDocumentProducer == null && this.documentProducerConsumerQueue.Count <= 0);
            }
        }

        public Guid ActivityId
        {
            get
            {
                if (this.currentDocumentProducer == null)
                {
                    throw new ArgumentNullException("currentDocumentProducer");
                }

                return this.currentDocumentProducer.ActivityId;
            }
        }

        /// <summary>
        /// Returns an serialized array of OrderByContinuationToken, if the query didn't finish producing all results. 
        /// </summary>
        /// <example>
        /// Order by continuation token example.
        /// <![CDATA[
        ///  [{"compositeToken":{"token":"+RID:OpY0AN-mFAACAAAAAAAABA==#RT:1#TRC:1#RTD:qdTAEA==","range":{"min":"05C1D9CD673398","max":"05C1E399CD6732"}},"orderByItems"[{"item":2}],"rid":"OpY0AN-mFAACAAAAAAAABA==","skipCount":0,"filter":"r.key > 1"}]
        /// ]]>
        /// </example>
        protected override string ContinuationToken
        {
            get
            {
                if (!this.IsContinuationExpected)
                {
                    return this.DefaultContinuationToken;
                }

                if (this.IsDone)
                {
                    return null;
                }

                this.UpdateCurrentDocumentProducer();

                if (this.currentOrderByItems != null && this.consumeComparer.CompareOrderByItems(this.currentOrderByItems, this.currentDocumentProducer.Current.OrderByItems) != 0)
                {
                    base.CurrentContinuationTokens.Clear();
                }

                for (int index = base.CurrentContinuationTokens.Count - 1; index >= 0; --index)
                {
                    if (this.consumeComparer.CompareOrderByItems(base.CurrentContinuationTokens.Keys[index].Current.OrderByItems, this.currentDocumentProducer.Current.OrderByItems) != 0)
                    {
                        base.CurrentContinuationTokens.RemoveAt(index);
                    }
                }

                this.currentOrderByItems = this.currentDocumentProducer.Current.OrderByItems;

                base.CurrentContinuationTokens[this.currentDocumentProducer] = null;

                return (base.CurrentContinuationTokens.Count > 0) ?
                    JsonConvert.SerializeObject(
                    base.CurrentContinuationTokens.Keys.Select(producer => this.CreateCrossPartitionOrderByContinuationToken(producer)),
                    DefaultJsonSerializationSettings.Value) : null;
            }
        }

        public static async Task<OrderByDocumentQueryExecutionContext> CreateAsync(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            string resourceLink,
            string collectionRid,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            bool isContinuationExpected,
            bool getLazyFeedResponse,
            string requestContinuation,
            CancellationToken token,
            Guid correlatedActivityId)
        {
            Debug.Assert(
                partitionedQueryExecutionInfo.QueryInfo.HasOrderBy,
                "OrderBy~Context must have order by query info.");

            OrderByDocumentQueryExecutionContext context = new OrderByDocumentQueryExecutionContext(
                client,
                resourceTypeEnum,
                resourceType,
                expression,
                feedOptions,
                resourceLink,
                partitionedQueryExecutionInfo.QueryInfo.RewrittenQuery,
                isContinuationExpected,
                getLazyFeedResponse,
                new OrderByConsumeComparer(partitionedQueryExecutionInfo.QueryInfo.OrderBy),
                collectionRid,
                correlatedActivityId);

            await context.InitializeAsync(
                collectionRid,
                partitionedQueryExecutionInfo.QueryRanges,
                partitionKeyRanges,
                partitionedQueryExecutionInfo.QueryInfo.OrderBy,
                partitionedQueryExecutionInfo.QueryInfo.OrderByExpressions,
                initialPageSize,
                requestContinuation,
                token);

            return context;
        }

        public override async Task<FeedResponse<object>> DrainAsync(int maxElements, CancellationToken token)
        {
            List<object> result = new List<object>();
            List<Uri> replicaUris = new List<Uri>();
            ClientSideRequestStatistics requestStats = new ClientSideRequestStatistics();

            if (maxElements >= int.MaxValue)
            {
                maxElements = this.ActualMaxBufferedItemCount;
            }

            while (!this.IsDone && result.Count < maxElements)
            {
                this.UpdateCurrentDocumentProducer();

                DefaultTrace.TraceVerbose(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | OrderBy~Context.DrainAsync, currentDocumentProducer.Id: {3}, documentProducerConsumeQueue.Count: {4}",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    this.CorrelatedActivityId,
                    this.ActivityId,
                    this.currentDocumentProducer.TargetRange.Id,
                    this.documentProducerConsumerQueue.Count));

                OrderByQueryResult orderByResult = (OrderByQueryResult)this.currentDocumentProducer.Current;
                result.Add(orderByResult.Payload);

                if (this.currentDocumentProducer.RequestStatistics != null)
                {
                    replicaUris.AddRange(this.currentDocumentProducer.RequestStatistics.ContactedReplicas);
                }

                if (this.ShouldIncrementSkipCount(orderByResult.Rid))
                {
                    ++this.skipCount;
                }
                else
                {
                    this.skipCount = 0;
                }

                this.previousRid = orderByResult.Rid;

                if (!await this.TryMoveNextProducerAsync(
                    this.currentDocumentProducer,
                    null,
                    cancellationToken: token))
                {
                    this.currentDocumentProducer = null;
                }
            }

            this.ReduceTotalBufferedItems(result.Count);
            requestStats.ContactedReplicas.AddRange(replicaUris);

            return new FeedResponse<object>(result, result.Count, this.ResponseHeaders, requestStats, this.GetAndResetResponseLengthBytes());
        }

        private bool ShouldIncrementSkipCount(string rid)
        {
            return !this.currentDocumentProducer.IsAtContinuationBoundary && string.Equals(this.previousRid, rid, StringComparison.Ordinal);
        }

        private void UpdateCurrentDocumentProducer()
        {
            if (this.documentProducerConsumerQueue.Count > 0)
            {
                if (this.currentDocumentProducer == null)
                {
                    this.currentDocumentProducer = this.documentProducerConsumerQueue.Dequeue();
                }
                else if (this.documentProducerConsumerQueue.Comparer.Compare(this.currentDocumentProducer, this.documentProducerConsumerQueue.Peek()) > 0)
                {
                    this.documentProducerConsumerQueue.Enqueue(this.currentDocumentProducer);
                    this.currentDocumentProducer = this.documentProducerConsumerQueue.Dequeue();
                }
            }
        }

        private async Task InitializeAsync(
          string collectionRid,
          List<Range<string>> queryRanges,
          List<PartitionKeyRange> partitionKeyRanges,
          SortOrder[] sortOrders,
          string[] orderByExpressions,
          int initialPageSize,
          string requestContinuation,
          CancellationToken cancellationToken)
        {
            this.InitializationSchedulingMetrics.Start();
            try
            {
                OrderByContinuationToken[] suppliedContinuationTokens = this.ValidateAndExtractContinuationTokens(requestContinuation, sortOrders, orderByExpressions);
                Dictionary<string, OrderByContinuationToken> targetRangeToOrderByContinuationMap = null;
                base.DocumentProducers.Capacity = partitionKeyRanges.Count;

                if (suppliedContinuationTokens == null)
                {
                    await base.InitializeAsync(
                        collectionRid,
                        queryRanges,
                        TaskPriorityFunc,
                        partitionKeyRanges,
                        initialPageSize,
                        new SqlQuerySpec(this.QuerySpec.QueryText.Replace(FormatPlaceHolder, True), this.QuerySpec.Parameters),
                        null,
                        cancellationToken);
                }
                else
                {
                    RangeFilterInitializationInfo[] orderByInfos = this.GetPartitionKeyRangesInitializationInfo(
                        suppliedContinuationTokens,
                        partitionKeyRanges,
                        sortOrders,
                        orderByExpressions,
                        out targetRangeToOrderByContinuationMap);

                    Debug.Assert(targetRangeToOrderByContinuationMap != null, "If targetRangeToOrderByContinuationMap can't be null is valid continuation is supplied");

                    this.currentOrderByItems = suppliedContinuationTokens[0].OrderByItems;

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
                            new PartialReadOnlyList<PartitionKeyRange>(partitionKeyRanges, info.StartIndex, info.EndIndex - info.StartIndex + 1);

                        Task initTask = base.InitializeAsync(
                            collectionRid,
                            queryRanges,
                            TaskPriorityFunc,
                            partialRanges,
                            initialPageSize,
                            new SqlQuerySpec(
                                this.QuerySpec.QueryText.Replace(FormatPlaceHolder, info.Filter),
                                this.QuerySpec.Parameters),
                            targetRangeToOrderByContinuationMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.CompositeToken.Token),
                            cancellationToken);

                        foreach (PartitionKeyRange range in partialRanges)
                        {
                            this.filters[range.Id] = info.Filter;
                        }

                        await initTask;
                    }
                }

                // The Foreach loop below is an optimization for the following While loop. The While loop is made single-threaded as the base.DocumentProducers object can change during runtime due to split. Even though the While loop is single threaded, the Foreach loop below makes the producers fetch doucments concurrently. If any of the producers fails to produce due to split (i.e., encounters PartitionKeyRangeGoneException), then the while loop below will take out the failed document producers and replace it approprite ones and then call TryScheduleFetch() on them. 
                foreach (var producer in base.DocumentProducers)
                {
                    producer.TryScheduleFetch();
                }

                // Fetch one item from each of the producers to initialize the priority-queue. "TryMoveNextProducerAsync()" has
                // a side-effect that, if Split is encountered while trying to move, related parent producer will be taken out and child
                // producers will be added to "base.DocumentProducers". 

                for (int index = 0; index < base.DocumentProducers.Count; ++index)
                {
                    DocumentProducer<OrderByQueryResult> producer = base.DocumentProducers[index];

                    if (await this.TryMoveNextProducerAsync(
                        producer,
                        targetRangeToOrderByContinuationMap,
                        cancellationToken: cancellationToken))
                    {
                        producer = base.DocumentProducers[index];

                        OrderByContinuationToken continuationToken =
                            (targetRangeToOrderByContinuationMap != null && targetRangeToOrderByContinuationMap.ContainsKey(producer.TargetRange.Id)) ?
                            targetRangeToOrderByContinuationMap[producer.TargetRange.Id] : null;

                        if (continuationToken != null)
                        {
                            await this.FilterAsync(producer, sortOrders, continuationToken, cancellationToken);
                        }

                        this.documentProducerConsumerQueue.Enqueue(producer);
                    }
                }
            }
            finally
            {
                this.InitializationSchedulingMetrics.Stop();
                DefaultTrace.TraceInformation(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, CorrelatedActivityId: {1} | OrderBy~Context.InitializeAsync",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    this.CorrelatedActivityId));
            }
        }

        private FormattedFilterInfo GetFormattedFilters(
            string[] expressions,
            OrderByContinuationToken[] continuationTokens,
            SortOrder[] sortOrders)
        {
            // Validate the inputs
            for (int index = 0; index < continuationTokens.Length; index++)
            {
                Debug.Assert(continuationTokens[index].OrderByItems.Length == sortOrders.Length, "Expect values and orders are the same size.");
                Debug.Assert(expressions.Length == sortOrders.Length, "Expect expressions and orders are the same size.");
            }

            Tuple<string, string, string> filters = this.GetFormattedFilters(
                expressions,
                continuationTokens[0].OrderByItems.Select(queryItem => queryItem.GetItem()).ToArray(),
                sortOrders);

            return new FormattedFilterInfo(filters.Item1, filters.Item2, filters.Item3);
        }

        private void AppendToBuilders(Tuple<StringBuilder, StringBuilder, StringBuilder> builders, object str)
        {
            this.AppendToBuilders(builders, str, str, str);
        }

        private void AppendToBuilders(Tuple<StringBuilder, StringBuilder, StringBuilder> builders, object left, object target, object right)
        {
            builders.Item1.Append(left);
            builders.Item2.Append(target);
            builders.Item3.Append(right);
        }

        private Tuple<string, string, string> GetFormattedFilters(
            string[] expressions,
            object[] orderByItems,
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
                object orderByItem = orderByItems.First();
                string orderByItemToString = JsonConvert.SerializeObject(orderByItem, DefaultJsonSerializationSettings.Value);
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
                    ArraySegment<object> orderByItemsPrefix = new ArraySegment<object>(orderByItems, 0, prefixLength);

                    bool lastPrefix = prefixLength == numOrderByItems;
                    bool firstPrefix = prefixLength == 1;

                    this.AppendToBuilders(builders, "(");

                    for (int index = 0; index < prefixLength; index++)
                    {
                        string expression = expressionPrefix.ElementAt(index);
                        SortOrder sortOrder = sortOrderPrefix.ElementAt(index);
                        object orderByItem = orderByItemsPrefix.ElementAt(index);
                        bool lastItem = (index == prefixLength - 1);

                        // Append Expression
                        this.AppendToBuilders(builders, expression);
                        this.AppendToBuilders(builders, " ");

                        // Append binary operator
                        if (lastItem)
                        {
                            string inequality = sortOrder == SortOrder.Descending ? "<" : ">";
                            this.AppendToBuilders(builders, inequality);
                            if (lastPrefix)
                            {
                                this.AppendToBuilders(builders, "", "=", "=");
                            }
                        }
                        else
                        {
                            this.AppendToBuilders(builders, "=");
                        }

                        // Append SortOrder
                        string orderByItemToString = JsonConvert.SerializeObject(orderByItem, DefaultJsonSerializationSettings.Value);
                        this.AppendToBuilders(builders, " ");
                        this.AppendToBuilders(builders, orderByItemToString);
                        this.AppendToBuilders(builders, " ");

                        if (!lastItem)
                        {
                            this.AppendToBuilders(builders, "AND ");
                        }
                    }

                    this.AppendToBuilders(builders, ")");
                    if (!lastPrefix)
                    {
                        this.AppendToBuilders(builders, " OR ");
                    }
                }
            }

            return new Tuple<string, string, string>(left.ToString(), target.ToString(), right.ToString());
        }

        private async Task FilterAsync(
            DocumentProducer<OrderByQueryResult> producer,
            SortOrder[] sortOrders,
            OrderByContinuationToken continuationToken,
            CancellationToken cancellationToken)
        {
            // When we resume a query on a partition there is a possibility that we only read a partial page from the backend
            // meaning that will we repeat some documents if we didn't do anything about it. 
            // The solution is to filter all the documents that come before in the sort order, since we have already emitted them to the client.
            // The key is to seek until we get an order by value that matches the order by value we left off on.
            // Once we do that we need to seek to the correct _rid within the term,
            // since there might be many documents with the same order by value we left off on.

            ResourceId continuationRid;
            if (!ResourceId.TryParse(continuationToken.Rid, out continuationRid))
            {
                DefaultTrace.TraceWarning(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Invalid Rid in the continuation token {3} for OrderBy~Context.",
                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                        this.CorrelatedActivityId,
                        this.ActivityId,
                        continuationToken.CompositeToken.Token));
                throw new BadRequestException(RMResources.InvalidContinuationToken);
            }

            Dictionary<string, ResourceId> resourceIds = new Dictionary<string, ResourceId>();
            int itemToSkip = continuationToken.SkipCount;
            bool continuationRidVerified = false;

            while (true)
            {
                OrderByQueryResult orderByResult = (OrderByQueryResult)producer.Current;
                // Throw away documents until it matches the item from the continuation token.
                int cmp = 0;
                for (int i = 0; i < sortOrders.Length; ++i)
                {
                    cmp = ItemComparer.Instance.Compare(
                        continuationToken.OrderByItems[i].GetItem(),
                        orderByResult.OrderByItems[i].GetItem());

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
                    ResourceId rid;
                    if (!resourceIds.TryGetValue(orderByResult.Rid, out rid))
                    {
                        if (!ResourceId.TryParse(orderByResult.Rid, out rid))
                        {
                            DefaultTrace.TraceWarning(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Invalid Rid in the continuation token {3} for OrderBy~Context.",
                                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                    this.CorrelatedActivityId,
                                    this.ActivityId,
                                    continuationToken.CompositeToken.Token));
                            throw new BadRequestException(RMResources.InvalidContinuationToken);
                        }

                        resourceIds.Add(orderByResult.Rid, rid);
                    }

                    if (!continuationRidVerified)
                    {
                        if (continuationRid.Database != rid.Database || continuationRid.DocumentCollection != rid.DocumentCollection)
                        {
                            DefaultTrace.TraceWarning(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Invalid Rid in the continuation token {3} for OrderBy~Context.",
                                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                    this.CorrelatedActivityId,
                                    this.ActivityId,
                                    continuationToken.CompositeToken.Token));
                            throw new BadRequestException(RMResources.InvalidContinuationToken);
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

                if (!await producer.MoveNextAsync(cancellationToken))
                {
                    break;
                }
            }
        }

        private OrderByContinuationToken CreateCrossPartitionOrderByContinuationToken(DocumentProducer<OrderByQueryResult> documentProducer)
        {
            OrderByQueryResult orderByResult = documentProducer.Current;

            string filter = null;
            this.filters.TryGetValue(documentProducer.TargetRange.Id, out filter);

            return new OrderByContinuationToken
            {
                CompositeToken = new CompositeContinuationToken
                {
                    Token = documentProducer.PreviousResponseContinuation,
                    Range = documentProducer.TargetRange.ToRange(),
                },
                OrderByItems = orderByResult.OrderByItems,
                Rid = orderByResult.Rid,
                SkipCount = this.ShouldIncrementSkipCount(orderByResult.Rid) ? this.skipCount + 1 : 0,
                Filter = filter
            };
        }

        private Task<bool> TryMoveNextProducerAsync(
            DocumentProducer<OrderByQueryResult> producer,
            Dictionary<string, OrderByContinuationToken> targetRangeToOrderByContinuationMap,
            CancellationToken cancellationToken)
        {
            return base.TryMoveNextProducerAsync(
               producer,
               (currentProducer) => this.RepairOrderByContext(currentProducer, targetRangeToOrderByContinuationMap),
               cancellationToken);
        }

        private async Task<DocumentProducer<OrderByQueryResult>> RepairOrderByContext(
            DocumentProducer<OrderByQueryResult> parentProducer,
            Dictionary<string, OrderByContinuationToken> targetRangeToOrderByContinuationMap)
        {
            List<PartitionKeyRange> replacementRanges = await base.GetReplacementRanges(parentProducer.TargetRange, this.collectionRid);
            string parentRangeId = parentProducer.TargetRange.Id;
            int indexOfCurrentDocumentProducer = base.DocumentProducers.BinarySearch(
                        parentProducer,
                        Comparer<DocumentProducer<OrderByQueryResult>>.Create(
                            (producer1, producer2) => string.CompareOrdinal(producer1.TargetRange.MinInclusive, producer2.TargetRange.MinInclusive)));

            Debug.Assert(indexOfCurrentDocumentProducer >= 0, "Index of a producer in the Producers list can't be < 0");

            // default filter is "true", since it gets used when we replace the FormatPlaceHolder and if there is no parent filter, then the query becomes
            // SELECT * FROM c where blah and true
            string parentFilter;
            if (!this.filters.TryGetValue(parentRangeId, out parentFilter))
            {
                parentFilter = True;
            }

            replacementRanges.ForEach(pkr => this.filters.Add(pkr.Id, parentFilter));

            await base.RepairContextAsync(
                this.collectionRid,
                indexOfCurrentDocumentProducer,
                TaskPriorityFunc,
                replacementRanges,
                new SqlQuerySpec(
                    this.QuerySpec.QueryText.Replace(FormatPlaceHolder, parentFilter),
                    this.QuerySpec.Parameters));

            this.filters.Remove(parentRangeId);

            if (targetRangeToOrderByContinuationMap != null && targetRangeToOrderByContinuationMap.ContainsKey(parentRangeId))
            {
                for (int index = 0; index < replacementRanges.Count; ++index)
                {
                    targetRangeToOrderByContinuationMap[replacementRanges[index].Id] = targetRangeToOrderByContinuationMap[parentRangeId];
                    targetRangeToOrderByContinuationMap[replacementRanges[index].Id].CompositeToken.Range = replacementRanges[index].ToRange();
                }

                targetRangeToOrderByContinuationMap.Remove(parentRangeId);
            }

            return base.DocumentProducers[indexOfCurrentDocumentProducer];
        }

        private RangeFilterInitializationInfo[] GetPartitionKeyRangesInitializationInfo(
            OrderByContinuationToken[] suppliedContinuationTokens,
            List<PartitionKeyRange> partitionKeyRanges,
            SortOrder[] sortOrders,
            string[] orderByExpressions,
            out Dictionary<string, OrderByContinuationToken> targetRangeToContinuationTokenMap)
        {
            int minIndex = base.FindTargetRangeAndExtractContinuationTokens(
                partitionKeyRanges,
                suppliedContinuationTokens.Select(
                    token => Tuple.Create(token, token.CompositeToken.Range)),
                out targetRangeToContinuationTokenMap);

            FormattedFilterInfo formattedFilterInfo = this.GetFormattedFilters(
                orderByExpressions,
                suppliedContinuationTokens,
                sortOrders);

            return new RangeFilterInitializationInfo[]
            {
                new RangeFilterInitializationInfo(formattedFilterInfo.FilterForRangesLeftOfTargetRanges, 0, minIndex - 1),
                new RangeFilterInitializationInfo(formattedFilterInfo.FiltersForTargetRange, minIndex, minIndex),
                new RangeFilterInitializationInfo(formattedFilterInfo.FilterForRangesRightOfTargetRanges, minIndex + 1, partitionKeyRanges.Count - 1),
            };
        }

        private OrderByContinuationToken[] ValidateAndExtractContinuationTokens(string requestContinuation, SortOrder[] sortOrders, string[] orderByExpressions)
        {
            OrderByContinuationToken[] suppliedCompositeContinuationTokens = null;

            try
            {
                if (!string.IsNullOrEmpty(requestContinuation))
                {
                    suppliedCompositeContinuationTokens = JsonConvert.DeserializeObject<OrderByContinuationToken[]>(requestContinuation, DefaultJsonSerializationSettings.Value);
                    if (suppliedCompositeContinuationTokens == null)
                    {
                        throw new JsonException();
                    }

                    if (orderByExpressions == null || orderByExpressions.Length <= 0 || orderByExpressions.Length != sortOrders.Length)
                    {
                        DefaultTrace.TraceWarning($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}, CorrelatedActivityId: {this.CorrelatedActivityId} | Invalid order-by expression in the continuation token {requestContinuation} for OrderBy~Context.");
                        throw new BadRequestException(RMResources.InvalidContinuationToken);
                    }

                    foreach (OrderByContinuationToken suppliedToken in suppliedCompositeContinuationTokens)
                    {
                        if (suppliedToken.CompositeToken == null ||
                                suppliedToken.CompositeToken.Range == null ||
                                suppliedToken.OrderByItems == null ||
                                suppliedToken.OrderByItems.Length <= 0)
                        {
                            DefaultTrace.TraceWarning($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}, CorrelatedActivityId: {this.CorrelatedActivityId} | One of more fields missing in the continuation token {requestContinuation} for OrderBy~Context.");
                            throw new BadRequestException($"One of more fields missing in the continuation token {requestContinuation} for OrderBy~Context.");
                        }
                        else
                        {
                            if (suppliedToken.OrderByItems.Length != sortOrders.Length)
                            {
                                DefaultTrace.TraceWarning($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}, CorrelatedActivityId: {this.CorrelatedActivityId} | Invalid order-by items in ontinutaion token {requestContinuation} for OrderBy~Context.");
                                throw new BadRequestException($"Invalid order-by items in ontinutaion token {requestContinuation} for OrderBy~Context.");
                            }
                        }
                    }

                    if (suppliedCompositeContinuationTokens.Length == 0)
                    {
                        DefaultTrace.TraceWarning(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Invalid continuation format {3} for OrderBy~Context.",
                                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                this.CorrelatedActivityId,
                                this.ActivityId,
                                requestContinuation));
                        throw new BadRequestException(RMResources.InvalidContinuationToken);
                    }
                }
            }
            catch (JsonException ex)
            {
                DefaultTrace.TraceWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Invalid JSON in continuation token {3} for OrderBy~Context, exception: {4}",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    this.CorrelatedActivityId,
                    this.currentDocumentProducer == null ? "No Activity ID yet" : this.ActivityId.ToString(),
                    requestContinuation,
                    ex.Message));

                throw new BadRequestException(RMResources.InvalidContinuationToken, ex);
            }

            return suppliedCompositeContinuationTokens;
        }

        public struct FormattedFilterInfo
        {
            /*
             * There could be multiple target ranges for split. And they could be diffrent as they can
             * make diffrent amount of progress. 
             */
            public readonly string FiltersForTargetRange;
            public readonly string FilterForRangesLeftOfTargetRanges;
            public readonly string FilterForRangesRightOfTargetRanges;

            public FormattedFilterInfo(string leftFilter, string targetFilter, string rightFilters)
            {
                this.FilterForRangesLeftOfTargetRanges = leftFilter;
                this.FiltersForTargetRange = targetFilter;
                this.FilterForRangesRightOfTargetRanges = rightFilters;
            }
        }
    }
}
