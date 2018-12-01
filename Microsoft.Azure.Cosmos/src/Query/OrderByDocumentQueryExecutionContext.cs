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

        private static readonly IDictionary<SortOrder, string[]> FilterFormats = new Dictionary<SortOrder, string[]>(2)
        {

            { SortOrder.Descending, new[] { "{0} < {1}", string.Empty, "{0} <= {1}", } },
            { SortOrder.Ascending, new[] { "{0} > {1}", string.Empty, "{0} >= {1}", } },
        };

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
            for (int index = 0; index < continuationTokens.Length; index++)
            {
                Debug.Assert(continuationTokens[index].OrderByItems.Length == sortOrders.Length, "Expect values and orders are the same size.");
                Debug.Assert(expressions.Length == sortOrders.Length, "Expect expressions and orders are the same size.");
                Debug.Assert(continuationTokens[index].OrderByItems.Length == 1, "Expect exactly 1 value.");
            }

            string[] formats = (string[])OrderByDocumentQueryExecutionContext.FilterFormats[sortOrders[0]].Clone();
            Debug.Assert(formats.Length == 3, "formats array should have 3 elements.");
            formats[1] = continuationTokens[0].Filter ?? True;
            string[] filters = new string[3];
            for (int i = 0; i < filters.Length; ++i)
            {
                filters[i] = string.Format(
                        CultureInfo.InvariantCulture,
                        formats[i],
                        expressions[0],
                        JsonConvert.SerializeObject(continuationTokens[0].OrderByItems[0].GetItem(), DefaultJsonSerializationSettings.Value));
            }

            return new FormattedFilterInfo(filters[0], filters[1], filters[2]);
        }

        private async Task FilterAsync(
            DocumentProducer<OrderByQueryResult> producer,
            SortOrder[] sortOrders,
            OrderByContinuationToken continuationToken,
            CancellationToken cancellationToken)
        {
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

                    cmp = continuationRid.Document.CompareTo(rid.Document);
                    if (sortOrders[sortOrders.Length - 1] == SortOrder.Descending)
                    {
                        cmp = -cmp;
                    }

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
                        DefaultTrace.TraceWarning(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Invalid order-by expression in the continuation token {3} for OrderBy~Context.",
                                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                this.CorrelatedActivityId,
                                requestContinuation,
                                this.ActivityId));
                        throw new BadRequestException(RMResources.InvalidContinuationToken);
                    }

                    foreach (OrderByContinuationToken suppliedToken in suppliedCompositeContinuationTokens)
                    {
                        if (suppliedToken.CompositeToken == null ||
                                suppliedToken.CompositeToken.Range == null ||
                                suppliedToken.OrderByItems == null ||
                                suppliedToken.OrderByItems.Length <= 0)
                        {
                            DefaultTrace.TraceWarning(
                                string.Format(
                                    CultureInfo.InvariantCulture,
                                    "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | One of more fields missing in the continuation token {3} for OrderBy~Context.",
                                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                    this.CorrelatedActivityId,
                                    this.ActivityId,
                                    requestContinuation));
                            throw new BadRequestException(RMResources.InvalidContinuationToken);
                        }
                        else
                        {
                            if (suppliedToken.OrderByItems.Length != sortOrders.Length)
                            {
                                DefaultTrace.TraceWarning(
                                    string.Format(
                                        CultureInfo.InvariantCulture,
                                        "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Invalid order-by items in ontinutaion token {3} for OrderBy~Context.",
                                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                        this.CorrelatedActivityId,
                                        this.ActivityId,
                                        requestContinuation));
                                throw new BadRequestException(RMResources.InvalidContinuationToken);
                            }

                            // Note: We can support order-by continuation with multiple fields once we support composite index
                            if (suppliedToken.OrderByItems.Length > 1)
                            {
                                DefaultTrace.TraceWarning(
                                    string.Format(
                                        CultureInfo.InvariantCulture,
                                        "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | Order-by continuation {3} with multiple fields not supported for OrderBy~Context.",
                                        DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                        this.CorrelatedActivityId,
                                        this.ActivityId,
                                        requestContinuation));
                                throw new BadRequestException(RMResources.InvalidContinuationToken);
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
