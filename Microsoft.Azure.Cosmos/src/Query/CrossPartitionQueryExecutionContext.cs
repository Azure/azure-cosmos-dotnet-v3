//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Collections.Generic;
    using Common;
    using ExecutionComponent;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using ParallelQuery;
    using Routing;

    /// <summary>
    /// This class is responsible for maintaining a forest of <see cref="DocumentProducerTree"/>.
    /// The trees in this forest are ordered using a priority queue and the nodes within the forest are internally ordered using a comparator.
    /// The ordering is determine by the concrete derived class.
    /// This class allows derived classes to iterate through the documents in the forest using Current and MoveNext semantics.
    /// This class is also responsible for prefetching documents if necessary using <see cref="ComparableTaskScheduler"/> whose ordering is also determined by the derived classes.
    /// This class also aggregated all metrics from sending queries to individual partitions.
    /// </summary>
    internal abstract class CrossPartitionQueryExecutionContext : DocumentQueryExecutionContextBase, IDocumentQueryExecutionComponent
    {
        /// <summary>
        /// When a document producer tree successfully fetches a page we increase the page size by this factor so that any particular document producer will only ever make O(log(n)) roundtrips, while also only ever grabbing at most twice the number of documents needed.
        /// </summary>
        private const double DynamicPageSizeAdjustmentFactor = 1.6;

        /// <summary>
        /// Priority Queue of DocumentProducerTrees that make a forest that can be iterated on.
        /// </summary>
        private readonly PriorityQueue<DocumentProducerTree> documentProducerForest;

        /// <summary>
        /// Function used to determine which document producer to fetch from first
        /// </summary>
        private readonly Func<DocumentProducerTree, int> fetchPrioirtyFunction;

        /// <summary>
        /// The task scheduler that kicks off all the prefetches behind the scenes.
        /// </summary>
        private readonly ComparableTaskScheduler comparableTaskScheduler;

        /// <summary>
        /// The equality comparer used to determine whether a document producer needs it's continuation token to be part of the composite continuation token.
        /// </summary>
        private readonly IEqualityComparer<CosmosElement> equalityComparer;

        /// <summary>
        /// Request Charge Tracker used to atomically add request charges (doubles).
        /// </summary>
        private readonly RequestChargeTracker requestChargeTracker;

        /// <summary>
        /// The actual max page size after all the optimizations have been made it in the create document query execution context layer.
        /// </summary>
        private readonly long actualMaxPageSize;

        /// <summary>
        /// The actual max buffered item count after all the optimizations have been made it in the create document query execution context layer.
        /// </summary>
        private readonly long actualMaxBufferedItemCount;

        /// <summary>
        /// This stores all the query metrics which have been grouped by partition id.
        /// When a feed response is returned (which includes multiple partitions and potentially multiple continuations)
        /// we take a snapshot of partitionedQueryMetrics and store it in grouped query metrics.
        /// </summary>
        private IReadOnlyDictionary<string, QueryMetrics> groupedQueryMetrics;

        /// <summary>
        /// This stores the running query metrics.
        /// When a feed response is returned he take a snapshot of this bag and store it in groupedQueryMetrics.
        /// The bag is then emptied and available to store the query metric for future continuations.
        /// </summary>
        /// <remarks>
        /// Due to the nature of parallel queries and prefetches the query metrics you get for a single continuation does not always 
        /// map to how much work was done to get that continuation.
        /// For example say for a simple cross partition query we return the first page of the results from the first partition,
        /// but behind the scenes we prefetched from other partitions.
        /// Another example is for an order by query we return one page of results but it only required us to use partial pages from each partition, 
        /// but we eventually used the whole page for the next continuation; which continuation reports the cost?
        /// Basically the only thing we can ensure is if you drain a query fully you should get back the same query metrics by the end.
        /// </remarks>
        private ConcurrentBag<Tuple<string, QueryMetrics>> partitionedQueryMetrics;

        /// <summary>
        /// Total number of buffered items to determine if we can go for another prefetch while still honoring the MaxBufferedItemCount.
        /// </summary>
        private long totalBufferedItems;

        /// <summary>
        /// The total response length.
        /// </summary>
        private long totalResponseLengthBytes;

        /// <summary>
        /// Initializes a new instance of the CrossPartitionQueryExecutionContext class.
        /// </summary>
        /// <param name="initParams">Constructor parameters for the base class.</param>
        /// <param name="rewrittenQuery">
        /// Queries will get rewritten for different reasons.
        /// You can read more about this in the details from the concrete classes.
        /// </param>
        /// <param name="moveNextComparer">Comparer used to figure out that document producer tree to serve documents from next.</param>
        /// <param name="fetchPrioirtyFunction">The priority function to determine which partition to fetch documents from next.</param>
        /// <param name="equalityComparer">Used to determine whether we need to return the continuation token for a partition.</param>
        protected CrossPartitionQueryExecutionContext(
            DocumentQueryExecutionContextBase.InitParams initParams,
            string rewrittenQuery,
            IComparer<DocumentProducerTree> moveNextComparer,
            Func<DocumentProducerTree, int> fetchPrioirtyFunction,
            IEqualityComparer<CosmosElement> equalityComparer)
            : base(initParams)
        {
            if (!string.IsNullOrWhiteSpace(rewrittenQuery))
            {
                this.querySpec = new SqlQuerySpec(rewrittenQuery, this.QuerySpec.Parameters);
            }

            if (moveNextComparer == null)
            {
                throw new ArgumentNullException($"{nameof(moveNextComparer)} can not be null");
            }

            if (fetchPrioirtyFunction == null)
            {
                throw new ArgumentNullException($"{nameof(fetchPrioirtyFunction)} can not be null");
            }

            if (equalityComparer == null)
            {
                throw new ArgumentNullException($"{nameof(equalityComparer)} can not be null");
            }

            this.documentProducerForest = new PriorityQueue<DocumentProducerTree>(moveNextComparer, isSynchronized: true);
            this.fetchPrioirtyFunction = fetchPrioirtyFunction;
            this.comparableTaskScheduler = new ComparableTaskScheduler(initParams.FeedOptions.MaxDegreeOfParallelism);
            this.equalityComparer = equalityComparer;
            this.requestChargeTracker = new RequestChargeTracker();
            this.partitionedQueryMetrics = new ConcurrentBag<Tuple<string, QueryMetrics>>();
            this.actualMaxPageSize = this.MaxItemCount.GetValueOrDefault(ParallelQueryConfig.GetConfig().ClientInternalMaxItemCount);
            if (this.actualMaxPageSize < 0)
            {
                throw new OverflowException("actualMaxPageSize should never be less than 0");
            }

            if (this.actualMaxPageSize > int.MaxValue)
            {
                throw new OverflowException("actualMaxPageSize should never be greater than int.MaxValue");
            }

            if (IsMaxBufferedItemCountSet(this.MaxBufferedItemCount))
            {
                this.actualMaxBufferedItemCount = this.MaxBufferedItemCount;
            }
            else
            {
                this.actualMaxBufferedItemCount = ParallelQueryConfig.GetConfig().DefaultMaximumBufferSize;
            }

            if (this.actualMaxBufferedItemCount < 0)
            {
                throw new OverflowException("actualMaxBufferedItemCount should never be less than 0");
            }

            if (this.actualMaxBufferedItemCount > int.MaxValue)
            {
                throw new OverflowException("actualMaxBufferedItemCount should never be greater than int.MaxValue");
            }
        }

        /// <summary>
        /// Gets a value indicating whether this context is done having documents drained.
        /// </summary>
        public override bool IsDone
        {
            get
            {
                return !this.HasMoreResults;
            }
        }

        protected int ActualMaxBufferedItemCount
        {
            get
            {
                return (int)this.actualMaxBufferedItemCount;
            }
        }

        protected int ActualMaxPageSize
        {
            get
            {
                return (int)this.actualMaxPageSize;
            }
        }

        /// <summary>
        /// Gets the continuation token for the context.
        /// This method is overridden by the derived class, since they all have different continuation tokens.
        /// </summary>
        protected override abstract string ContinuationToken
        {
            get;
        }

        /// <summary>
        /// Gets a value indicating whether we are allowed to prefetch.
        /// </summary>
        private bool CanPrefetch
        {
            get
            {
                return this.MaxDegreeOfParallelism != 0;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the context still has more results.
        /// </summary>
        private bool HasMoreResults
        {
            get
            {
                return this.documentProducerForest.Count != 0 && this.CurrentDocumentProducerTree().HasMoreResults;
            }
        }

        /// <summary>
        /// Gets the number of documents we can still buffer.
        /// </summary>
        private long FreeItemSpace
        {
            get
            {
                return this.actualMaxBufferedItemCount - Interlocked.Read(ref this.totalBufferedItems);
            }
        }

        /// <summary>
        /// Gets the response headers for the context.
        /// </summary>
        /// <returns>The response headers for the context.</returns>
        public CosmosQueryResponseMessageHeaders GetResponseHeaders()
        {
            string continuationToken = this.ContinuationToken;
            if (continuationToken == "[]")
            {
                throw new InvalidOperationException("Somehow a document query execution context returned an empty array of continuations.");
            }

            CosmosQueryResponseMessageHeaders responseHeaders = new CosmosQueryResponseMessageHeaders(continuationToken, null, this.ResourceTypeEnum, this.ResourceLink);
            this.SetQueryMetrics();

            IReadOnlyDictionary<string, QueryMetrics> groupedQueryMetrics = this.GetQueryMetrics();
            if (groupedQueryMetrics != null && groupedQueryMetrics.Count != 0)
            {
                responseHeaders[HttpConstants.HttpHeaders.QueryMetrics] = QueryMetrics
                .CreateFromIEnumerable(groupedQueryMetrics.Values)
                .ToDelimitedString();
            }

            responseHeaders.RequestCharge = this.requestChargeTracker
                .GetAndResetCharge();

            return responseHeaders;
        }

        /// <summary>
        /// Gets the query metrics that are set in SetQueryMetrics
        /// </summary>
        /// <returns>The grouped query metrics.</returns>
        public IReadOnlyDictionary<string, QueryMetrics> GetQueryMetrics()
        {
            return new PartitionedQueryMetrics(this.groupedQueryMetrics);
        }

        /// <summary>
        /// After a split you need to maintain the continuation tokens for all the child document producers until a condition is met.
        /// For example lets say that a document producer is at continuation X and it gets split,
        /// then the children each get continuation X, but since you only drain from one of them at a time you are left with the first child having 
        /// continuation X + delta and the second child having continuation X (draw this out if you are following along).
        /// At this point you have the answer the question: "Which continuation token do you return to the user?".
        /// Let's say you return X, then when you come back to the first child you will be repeating work, thus returning some documents more than once.
        /// Let's say you return X + delta, then you fine when you return to the first child, but when you get to the second child you don't have a continuation token
        /// meaning that you will be repeating all the document for the second partition up until X and again you will be returning some documents more than once.
        /// Thus you have to return the continuation token for both children.
        /// Both this means you are returning more than 1 continuation token for the rest of the query.
        /// Well a naive optimization is to flush the continuation for a child partition once you are done draining from it, which isn't bad for a parallel query,
        /// but if you have an order by query you might not be done with a producer until the end of the query.
        /// The next optimization for a parallel query is to flush the continuation token the moment you start reading from a child partition.
        /// This works for a parallel query, but breaks for an order by query.
        /// The final realization is that for an order by query you are only choosing between multiple child partitions when their is a tie,
        /// so the key is that you can dump the continuation token the moment you come across a new order by item.
        /// For order by queries that is determined by the order by field and for parallel queries that is the moment you come by a new rid (which is any document, since rids are unique within a partition).
        /// So by passing an equality comparer to the document producers they can determine whether they are still "active".
        /// </summary>
        /// <returns>
        /// Returns all document producers whose continuation token you have to return.
        /// Only during a split will this list contain more than 1 item.
        /// </returns>
        public IEnumerable<DocumentProducer> GetActiveDocumentProducers()
        {
            lock (this.documentProducerForest)
            {
                DocumentProducerTree current = this.documentProducerForest.Peek().CurrentDocumentProducerTree;
                if (current.HasMoreResults && !current.IsActive)
                {
                    // If the current document producer tree has more results, but isn't active.
                    // then we still want to emit it, since it won't get picked up in the below for loop.
                    yield return current.Root;
                }

                foreach (DocumentProducerTree documentProducerTree in this.documentProducerForest)
                {
                    foreach (DocumentProducer documentProducer in documentProducerTree.GetActiveDocumentProducers())
                    {
                        yield return documentProducer;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the current document producer tree that should be drained from.
        /// </summary>
        /// <returns>The current document producer tree that should be drained from.</returns>
        public DocumentProducerTree CurrentDocumentProducerTree()
        {
            return this.documentProducerForest.Peek();
        }

        /// <summary>
        /// Pushes a document producer back to the queue.
        /// </summary>
        public void PushCurrentDocumentProducerTree(DocumentProducerTree documentProducerTree)
        {
            this.documentProducerForest.Enqueue(documentProducerTree);
        }

        /// <summary>
        /// Pops the current document producer tree that should be drained from.
        /// </summary>
        /// <returns>The current document producer tree that should be drained from.</returns>
        public DocumentProducerTree PopCurrentDocumentProducerTree()
        {
            return this.documentProducerForest.Dequeue();
        }

        /// <summary>
        /// Disposes of the context and implements IDisposable.
        /// </summary>
        public override void Dispose()
        {
            this.comparableTaskScheduler.Dispose();
        }

        /// <summary>
        /// Stops the execution context.
        /// </summary>
        public void Stop()
        {
            this.comparableTaskScheduler.Stop();
        }

        /// <summary>
        /// Drains documents from this execution context.
        /// This method is abstract and meant for the concrete classes to implement.
        /// </summary>
        /// <param name="maxElements">The maximum number of elements to drain (you might get less).</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task that when awaited on will return a feed response.</returns>
        public abstract Task<QueryResponse> DrainAsync(int maxElements, CancellationToken token);

        /// <summary>
        /// Initializes cross partition query execution context by initializing the necessary document producers.
        /// </summary>
        /// <param name="collectionRid">The collection to drain from.</param>
        /// <param name="partitionKeyRanges">The partitions to target.</param>
        /// <param name="initialPageSize">The page size to start the document producers off with.</param>
        /// <param name="querySpecForInit">The query specification for the rewritten query.</param>
        /// <param name="targetRangeToContinuationMap">Map from partition to it's corresponding continuation token.</param>
        /// <param name="deferFirstPage">Whether or not we should defer the fetch of the first page from each partition.</param>
        /// <param name="filter">The filter to inject in the predicate.</param>
        /// <param name="filterCallback">The callback used to filter each partition.</param>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        protected async Task InitializeAsync(
            string collectionRid,
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            SqlQuerySpec querySpecForInit,
            Dictionary<string, string> targetRangeToContinuationMap,
            bool deferFirstPage,
            string filter,
            Func<DocumentProducerTree, Task> filterCallback,
            CancellationToken token)
        {
            CollectionCache collectionCache = await this.Client.GetCollectionCacheAsync();
            INameValueCollection requestHeaders = await this.CreateCommonHeadersAsync(this.GetFeedOptions(null));

            this.TraceInformation(string.Format(
                CultureInfo.InvariantCulture,
                "parallel~contextbase.initializeasync, queryspec {0}, maxbuffereditemcount: {1}, target partitionkeyrange count: {2}, maximumconcurrencylevel: {3}, documentproducer initial page size {4}",
                JsonConvert.SerializeObject(this.querySpec, DefaultJsonSerializationSettings.Value),
                this.actualMaxBufferedItemCount,
                partitionKeyRanges.Count,
                this.comparableTaskScheduler.MaximumConcurrencyLevel,
                initialPageSize));

            List<DocumentProducerTree> documentProducerTrees = new List<DocumentProducerTree>();
            foreach (PartitionKeyRange partitionKeyRange in partitionKeyRanges)
            {
                string initialContinuationToken = (targetRangeToContinuationMap != null && targetRangeToContinuationMap.ContainsKey(partitionKeyRange.Id)) ? targetRangeToContinuationMap[partitionKeyRange.Id] : null;

                DocumentProducerTree documentProducerTree = new DocumentProducerTree(
                    partitionKeyRange,
                    //// Create Document Service Request callback
                    (pkRange, continuationToken, pageSize) =>
                    {
                        INameValueCollection headers = requestHeaders.Clone();
                        headers[HttpConstants.HttpHeaders.Continuation] = continuationToken;
                        headers[HttpConstants.HttpHeaders.PageSize] = pageSize.ToString(CultureInfo.InvariantCulture);
                        return this.CreateDocumentServiceRequest(
                            headers,
                            querySpecForInit,
                            pkRange,
                            collectionRid);
                    },
                    this.ExecuteRequestLazyAsync,
                    //// Retry policy callback
                    () => new NonRetriableInvalidPartitionExceptionRetryPolicy(collectionCache, this.Client.ResetSessionTokenRetryPolicy.GetRequestPolicy()),
                    this.OnDocumentProducerTreeCompleteFetching,
                    this.documentProducerForest.Comparer as IComparer<DocumentProducerTree>,
                    this.equalityComparer,
                    this.Client,
                    deferFirstPage,
                    collectionRid,
                    initialPageSize,
                    initialContinuationToken);

                documentProducerTree.Filter = filter;

                // Prefetch if necessary, and populate consume queue.
                if (this.CanPrefetch)
                {
                    this.TryScheduleFetch(documentProducerTree);
                }

                documentProducerTrees.Add(documentProducerTree);
            }

            // Using loop fisson so that we can load the document producers in parallel
            foreach (DocumentProducerTree documentProducerTree in documentProducerTrees)
            {
                if (!deferFirstPage)
                {
                    await documentProducerTree.MoveNextIfNotSplitAsync(token);
                }

                if (filterCallback != null)
                {
                    await filterCallback(documentProducerTree);
                }

                if (documentProducerTree.HasMoreResults)
                {
                    this.documentProducerForest.Enqueue(documentProducerTree);
                }
            }
        }

        /// <summary>
        /// <para>
        /// If a query encounters split up resuming using continuation, we need to regenerate the continuation tokens. 
        /// Specifically, since after split we will have new set of ranges, we need to remove continuation token for the 
        /// parent partition and introduce continuation token for the child partitions. 
        /// </para>
        /// <para>
        /// This function does that. Also in that process, we also check validity of the input continuation tokens. For example, 
        /// even after split the boundary ranges of the child partitions should match with the parent partitions. If the Min and Max
        /// range of a target partition in the continuation token was Min1 and Max1. Then the Min and Max range info for the two 
        /// corresponding child partitions C1Min, C1Max, C2Min, and C2Max should follow the constrain below:
        ///  PMax = C2Max > C2Min > C1Max > C1Min = PMin.
        /// </para>
        /// </summary>
        /// <param name="partitionKeyRanges">The partition key ranges to extract continuation tokens for.</param>
        /// <param name="suppliedContinuationTokens">The continuation token that the user supplied.</param>
        /// <param name="targetRangeToContinuationTokenMap">The output dictionary of partition key range to continuation token.</param>
        /// <typeparam name="TContinuationToken">The type of continuation token to generate.</typeparam>
        /// <Remarks>
        /// The code assumes that merge doesn't happen and 
        /// </Remarks>
        /// <returns>The index of the partition whose MinInclusive is equal to the suppliedContinuationTokens</returns>
        protected int FindTargetRangeAndExtractContinuationTokens<TContinuationToken>(
            List<PartitionKeyRange> partitionKeyRanges,
            IEnumerable<Tuple<TContinuationToken, Range<string>>> suppliedContinuationTokens,
            out Dictionary<string, TContinuationToken> targetRangeToContinuationTokenMap)
        {
            if (partitionKeyRanges == null)
            {
                throw new ArgumentNullException($"{nameof(partitionKeyRanges)} can not be null.");
            }

            if (partitionKeyRanges.Count < 1)
            {
                throw new ArgumentException($"{nameof(partitionKeyRanges)} must have atleast one element.");
            }

            foreach (PartitionKeyRange partitionKeyRange in partitionKeyRanges)
            {
                if (partitionKeyRange == null)
                {
                    throw new ArgumentException($"{nameof(partitionKeyRanges)} can not have null elements.");
                }
            }

            if (suppliedContinuationTokens == null)
            {
                throw new ArgumentNullException($"{nameof(suppliedContinuationTokens)} can not be null.");
            }

            if (suppliedContinuationTokens.Count() < 1)
            {
                throw new ArgumentException($"{nameof(suppliedContinuationTokens)} must have atleast one element.");
            }

            if (suppliedContinuationTokens.Count() > partitionKeyRanges.Count)
            {
                throw new ArgumentException($"{nameof(suppliedContinuationTokens)} can not have more elements than {nameof(partitionKeyRanges)}.");
            }

            targetRangeToContinuationTokenMap = new Dictionary<string, TContinuationToken>();

            // Find the minimum index.
            Tuple<TContinuationToken, Range<string>> firstContinuationTokenAndRange = suppliedContinuationTokens
                .OrderBy((tuple) => tuple.Item2.Min)
                .First();
            TContinuationToken firstContinuationToken = firstContinuationTokenAndRange.Item1;
            PartitionKeyRange firstContinuationRange = new PartitionKeyRange
            {
                MinInclusive = firstContinuationTokenAndRange.Item2.Min,
                MaxExclusive = firstContinuationTokenAndRange.Item2.Max
            };

            int minIndex = partitionKeyRanges.BinarySearch(
                firstContinuationRange,
                Comparer<PartitionKeyRange>.Create((range1, range2) => string.CompareOrdinal(range1.MinInclusive, range2.MinInclusive)));
            if (minIndex < 0)
            {
                this.TraceWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "Could not find continuation token: {0}",
                    firstContinuationToken.ToString()));
                throw new BadRequestException(RMResources.InvalidContinuationToken);
            }

            foreach (Tuple<TContinuationToken, Range<string>> suppledContinuationToken in suppliedContinuationTokens)
            {
                // find what ranges make up the supplied continuation token
                TContinuationToken continuationToken = suppledContinuationToken.Item1;
                Range<string> range = suppledContinuationToken.Item2;

                IEnumerable<PartitionKeyRange> replacementRanges = partitionKeyRanges
                    .Where((partitionKeyRange) =>
                        string.CompareOrdinal(range.Min, partitionKeyRange.MinInclusive) <= 0 &&
                        string.CompareOrdinal(range.Max, partitionKeyRange.MaxExclusive) >= 0)
                    .OrderBy((partitionKeyRange) => partitionKeyRange.MinInclusive);

                // Could not find the child ranges
                if (replacementRanges.Count() == 0)
                {
                    this.TraceWarning(string.Format(
                        CultureInfo.InvariantCulture,
                        "Could not find continuation token: {0}",
                        continuationToken.ToString()));
                    throw new BadRequestException(RMResources.InvalidContinuationToken);
                }

                // PMax = C2Max > C2Min > C1Max > C1Min = PMin.
                string parentMax = range.Max;
                string child2Max = replacementRanges.Last().MaxExclusive;
                string child2Min = replacementRanges.Last().MinInclusive;
                string child1Max = replacementRanges.First().MaxExclusive;
                string child1Min = replacementRanges.First().MinInclusive;
                string parentMin = range.Min;

                if (!(parentMax == child2Max &&
                    string.CompareOrdinal(child2Max, child2Min) >= 0 &&
                    (replacementRanges.Count() == 1 ? true : string.CompareOrdinal(child2Min, child1Max) >= 0) &&
                    string.CompareOrdinal(child1Max, child1Min) >= 0 &&
                    child1Min == parentMin))
                {
                    this.TraceWarning(string.Format(
                        CultureInfo.InvariantCulture,
                        "PMax = C2Max > C2Min > C1Max > C1Min = PMin: {0}",
                        continuationToken.ToString()));
                    throw new BadRequestException(RMResources.InvalidContinuationToken);
                }

                foreach (PartitionKeyRange partitionKeyRange in replacementRanges)
                {
                    targetRangeToContinuationTokenMap.Add(partitionKeyRange.Id, continuationToken);
                }
            }

            return minIndex;
        }

        protected virtual long GetAndResetResponseLengthBytes()
        {
            return Interlocked.Exchange(ref this.totalResponseLengthBytes, 0);
        }

        protected virtual long IncrementResponseLengthBytes(long incrementValue)
        {
            return Interlocked.Add(ref this.totalResponseLengthBytes, incrementValue);
        }

        /// <summary>
        /// Placeholder function to implement DocumentQueryExecutionContextBase class.
        /// </summary>
        /// <param name="token">The cancellation token that doesn't get used.</param>
        /// <returns>A dummy task to await on.</returns>
        protected override Task<DocumentFeedResponse<CosmosElement>> ExecuteInternalAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Traces a warning with the proper formatting.
        /// </summary>
        /// <param name="message">The message to trace.</param>
        protected void TraceWarning(string message)
        {
            DefaultTrace.TraceWarning(this.GetTrace(message));
        }

        /// <summary>
        /// Traces a verbose message with the proper formatting.
        /// </summary>
        /// <param name="message">The message to trace.</param>
        protected void TraceVerbose(string message)
        {
            DefaultTrace.TraceVerbose(this.GetTrace(message));
        }

        /// <summary>
        /// Traces information with the proper formatting.
        /// </summary>
        /// <param name="message">The message to trace.</param>
        protected void TraceInformation(string message)
        {
            DefaultTrace.TraceInformation(this.GetTrace(message));
        }

        /// <summary>
        /// Since query metrics are being aggregated asynchronously to the feed reponses as explained in the member documentation,
        /// this function allows us to take a snapshot of the query metrics.
        /// </summary>
        private void SetQueryMetrics()
        {
            this.groupedQueryMetrics = Interlocked.Exchange(ref this.partitionedQueryMetrics, new ConcurrentBag<Tuple<string, QueryMetrics>>())
                .GroupBy(tuple => tuple.Item1, tuple => tuple.Item2)
                .ToDictionary(group => group.Key, group => QueryMetrics.CreateFromIEnumerable(group));
        }

        /// <summary>
        /// Tries to schedule a fetch from the document producer tree.
        /// </summary>
        /// <param name="documentProducerTree">The document producer tree to schedule a fetch for.</param>
        /// <returns>Whether or not the fetch was successfully scheduled.</returns>
        private bool TryScheduleFetch(DocumentProducerTree documentProducerTree)
        {
            return this.comparableTaskScheduler.TryQueueTask(
                new DocumentProducerTreeComparableTask(
                    documentProducerTree,
                    this.fetchPrioirtyFunction),
                default(TimeSpan));
        }

        /// <summary>
        /// Function that is given to all the document producers to call on once they are done fetching.
        /// This is so that the CrossPartitionQueryExecutionContext can aggregate metadata from them.
        /// </summary>
        /// <param name="producer">The document producer that just finished fetching.</param>
        /// <param name="itemsBuffered">The number of items that the producer just fetched.</param>
        /// <param name="resourceUnitUsage">The amount of RUs that the producer just consumed.</param>
        /// <param name="queryMetrics">The query metrics that the producer just got back from the backend.</param>
        /// <param name="responseLengthBytes">The length of the response the producer just got back in bytes.</param>
        /// <param name="token">The cancellation token.</param>
        /// <remarks>
        /// This function is by nature a bit racy.
        /// A query might be fully drained but a background task is still fetching documents so this will get called after the context is done.
        /// </remarks>
        private void OnDocumentProducerTreeCompleteFetching(
            DocumentProducerTree producer,
            int itemsBuffered,
            double resourceUnitUsage,
            QueryMetrics queryMetrics,
            long responseLengthBytes,
            CancellationToken token)
        {
            // Update charge and states
            this.requestChargeTracker.AddCharge(resourceUnitUsage);
            Interlocked.Add(ref this.totalBufferedItems, itemsBuffered);
            this.IncrementResponseLengthBytes(responseLengthBytes);
            this.partitionedQueryMetrics.Add(Tuple.Create(producer.PartitionKeyRange.Id, queryMetrics));

            // Adjust the producer page size so that we reach the optimal page size.
            producer.PageSize = Math.Min((long)(producer.PageSize * DynamicPageSizeAdjustmentFactor), this.actualMaxPageSize);

            // Adjust Max Degree Of Paralleism if neccesary
            // (needs to wait for comparable task scheudler refactor).

            // Fetch again if necessary
            if (producer.HasMoreBackendResults)
            {
                // 4mb is the max reponse size
                long expectedResponseSize = Math.Min(producer.PageSize, 4 * 1024 * 1024);
                if (this.CanPrefetch && this.FreeItemSpace > expectedResponseSize)
                {
                    this.TryScheduleFetch(producer);
                }
            }

            this.TraceVerbose(string.Format(
                CultureInfo.InvariantCulture,
                "Id: {0}, size: {1}, resourceUnitUsage: {2}, taskScheduler.CurrentRunningTaskCount: {3}",
                producer.PartitionKeyRange.Id,
                itemsBuffered,
                resourceUnitUsage,
                this.comparableTaskScheduler.CurrentRunningTaskCount,
                this.CorrelatedActivityId));
        }

        /// <summary>
        /// Gets the formatting for a trace.
        /// </summary>
        /// <param name="message">The message to format</param>
        /// <returns>The formatted message ready for a trace.</returns>
        private string GetTrace(string message)
        {
            const string TracePrefixFormat = "{0}, CorrelatedActivityId: {1}, ActivityId: {2} | {3}";
            return string.Format(
                CultureInfo.InvariantCulture,
                TracePrefixFormat,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                this.CorrelatedActivityId,
                this.documentProducerForest.Count != 0 ? this.CurrentDocumentProducerTree().ActivityId : Guid.Empty,
                message);
        }

        private static bool IsMaxBufferedItemCountSet(int maxBufferedItemCount)
        {
            return maxBufferedItemCount != default(int);
        }

        /// <summary>
        /// All CrossPartitionQueries need this information on top of the parameter for DocumentQueryExecutionContextBase.
        /// I moved it out into it's own type, so that we don't have to keep passing around all the individual parameters in the factory pattern.
        /// This also allows us to check the arguments once instead of in each of the constructors.
        /// </summary>
        public struct CrossPartitionInitParams
        {
            /// <summary>
            /// Initializes a new instance of the InitParams struct.
            /// </summary>
            /// <param name="collectionRid">The collection rid.</param>
            /// <param name="partitionedQueryExecutionInfo">The partitioned query execution info.</param>
            /// <param name="partitionKeyRanges">The partition key ranges.</param>
            /// <param name="initialPageSize">The initial page size.</param>
            /// <param name="requestContinuation">The request continuation.</param>
            public CrossPartitionInitParams(
                string collectionRid,
                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
                List<PartitionKeyRange> partitionKeyRanges,
                int initialPageSize,
                string requestContinuation)
            {
                if (string.IsNullOrWhiteSpace(collectionRid))
                {
                    throw new ArgumentException($"{nameof(collectionRid)} can not be null, empty, or white space.");
                }

                if (partitionedQueryExecutionInfo == null)
                {
                    throw new ArgumentNullException($"{nameof(partitionedQueryExecutionInfo)} can not be null.");
                }

                if (partitionKeyRanges == null)
                {
                    throw new ArgumentNullException($"{nameof(partitionKeyRanges)} can not be null.");
                }

                foreach (PartitionKeyRange partitionKeyRange in partitionKeyRanges)
                {
                    if (partitionKeyRange == null)
                    {
                        throw new ArgumentNullException($"{nameof(partitionKeyRange)} can not be null.");
                    }
                }

                if (initialPageSize <= 0)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(initialPageSize)} must be atleast 1.");
                }

                //// Request continuation is allowed to be null

                this.CollectionRid = collectionRid;
                this.PartitionedQueryExecutionInfo = partitionedQueryExecutionInfo;
                this.PartitionKeyRanges = partitionKeyRanges;
                this.InitialPageSize = initialPageSize;
                this.RequestContinuation = requestContinuation;
            }

            /// <summary>
            /// Gets the collection rid to drain documents from.
            /// </summary>
            public string CollectionRid { get; }

            /// <summary>
            /// Gets the serialized version of the PipelinedDocumentQueryExecutionContext.
            /// </summary>
            public PartitionedQueryExecutionInfo PartitionedQueryExecutionInfo { get; }

            /// <summary>
            /// Gets the partition key ranges to fan out to.
            /// </summary>
            public List<PartitionKeyRange> PartitionKeyRanges { get; }

            /// <summary>
            /// Gets the initial page size for each document producer.
            /// </summary>
            public int InitialPageSize { get; }

            /// <summary>
            /// Gets the continuation token to use for resuming the context (potentially on a different machine and different SDK).
            /// </summary>
            public string RequestContinuation { get; }
        }

        #region DocumentProducerTreeComparableTask
        /// <summary>
        /// Comparable task for the ComparableTaskScheduler.
        /// This is specifically for tasks that fetch from partitions in a document producer tree.
        /// </summary>
        private sealed class DocumentProducerTreeComparableTask : ComparableTask
        {
            /// <summary>
            /// The producer to fetch from.
            /// </summary>
            private readonly DocumentProducerTree producer;

            /// <summary>
            /// Initializes a new instance of the DocumentProducerTreeComparableTask class.
            /// </summary>
            /// <param name="producer">The producer to fetch from.</param>
            /// <param name="taskPriorityFunction">The callback to determine the fetch priority of the document producer.</param>
            public DocumentProducerTreeComparableTask(
                DocumentProducerTree producer,
                Func<DocumentProducerTree, int> taskPriorityFunction)
                : base(taskPriorityFunction(producer))
            {
                this.producer = producer;
            }

            /// <summary>
            /// Entry point for the function to start fetching.
            /// </summary>
            /// <param name="token">The cancellation token.</param>
            /// <returns>A task to await on.</returns>
            public override Task StartAsync(CancellationToken token)
            {
                return this.producer.BufferMoreDocumentsAsync(token);
            }

            /// <summary>
            /// Determines whether this class is equal to another task.
            /// </summary>
            /// <param name="other">The other task</param>
            /// <returns>Whether this class is equal to another task.</returns>
            public override bool Equals(IComparableTask other)
            {
                return this.Equals(other as DocumentProducerTreeComparableTask);
            }

            /// <summary>
            /// Gets the hash code for this task.
            /// </summary>
            /// <returns>The hash code for this task.</returns>
            public override int GetHashCode()
            {
                return this.producer.PartitionKeyRange.GetHashCode();
            }

            /// <summary>
            /// Internal implementation of equality.
            /// </summary>
            /// <param name="other">The other comparable task to check for equality.</param>
            /// <returns>Whether or not the comparable tasks are equal.</returns>
            private bool Equals(DocumentProducerTreeComparableTask other)
            {
                return this.producer.PartitionKeyRange.Equals(other.producer.PartitionKeyRange);
            }
        }
        #endregion
    }
}
