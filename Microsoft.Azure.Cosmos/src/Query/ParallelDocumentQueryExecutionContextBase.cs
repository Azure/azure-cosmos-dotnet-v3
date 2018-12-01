//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Cosmos.Routing;

    internal abstract class ParallelDocumentQueryExecutionContextBase<T> : DocumentQueryExecutionContextBase, IDocumentQueryExecutionComponent
    {
        protected const int MaxixmumDynamicMaxBufferedItemCountValue = 100000;
        protected const double DynamicPageSizeAdjustmentFactor = 1.6;

        protected static readonly IComparer<DocumentProducer<T>> DefaultComparer =
            Comparer<DocumentProducer<T>>.Create((producer1, producer2) => string.CompareOrdinal(producer1.TargetRange.MinInclusive, producer2.TargetRange.MinInclusive));

        // DocumentProducers
        protected readonly List<DocumentProducer<T>> DocumentProducers;

        // ComparableTaskScheduler
        protected readonly ComparableTaskScheduler TaskScheduler;

        // Metrics
        protected readonly SchedulingStopwatch InitializationSchedulingMetrics;
        protected readonly string DefaultContinuationToken;

        // Flag
        protected readonly bool ShouldPrefetch;
        protected readonly bool IsContinuationExpected;
        protected readonly SortedList<DocumentProducer<T>, string> CurrentContinuationTokens;

        // RequestChargeTracker
        private readonly RequestChargeTracker chargeTracker;

        private IReadOnlyDictionary<string, QueryMetrics> groupedQueryMetrics;
        private ConcurrentBag<Tuple<string, QueryMetrics>> partitionedQueryMetrics;

        // Response Headers
        private readonly INameValueCollection responseHeaders;

        // Caps
        private readonly long actualMaxPageSize;
        private readonly long actualMaxBufferedItemCount;

        // Counters to track states
        private long totalRequestRoundTrips;
        private long totalBufferedItems;
        private long totalResponseLengthBytes;
        private double currentAverageNumberOfRequestsPerTask;

        protected ParallelDocumentQueryExecutionContextBase(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            string resourceLink,
            string rewrittenQuery,
            Guid correlatedActivityId,
            bool isContinuationExpected,
            bool getLazyFeedResponse,
            bool isDynamicPageSizeAllowed) :
            base(
            client,
            resourceTypeEnum,
            resourceType,
            expression,
            feedOptions,
            resourceLink,
            getLazyFeedResponse,
            correlatedActivityId)
        {
            this.DocumentProducers = new List<DocumentProducer<T>>();

            this.chargeTracker = new RequestChargeTracker();
            this.groupedQueryMetrics = new Dictionary<string, QueryMetrics>();
            this.partitionedQueryMetrics = new ConcurrentBag<Tuple<string, QueryMetrics>>();
            this.responseHeaders = new StringKeyValueCollection();

            this.actualMaxBufferedItemCount = Math.Max(this.MaxBufferedItemCount, ParallelQueryConfig.GetConfig().DefaultMaximumBufferSize);
            this.currentAverageNumberOfRequestsPerTask = 1d;

            if (!string.IsNullOrEmpty(rewrittenQuery))
            {
                this.querySpec = new SqlQuerySpec(rewrittenQuery, this.QuerySpec.Parameters);
            }

            this.TaskScheduler = new ComparableTaskScheduler(this.GetCurrentMaximumAllowedConcurrentTasks(0));
            this.ShouldPrefetch = feedOptions.MaxDegreeOfParallelism != 0;
            this.IsContinuationExpected = isContinuationExpected;
            this.DefaultContinuationToken = Guid.NewGuid().ToString();
            this.InitializationSchedulingMetrics = new SchedulingStopwatch();
            this.InitializationSchedulingMetrics.Ready();
            this.CurrentContinuationTokens = new SortedList<DocumentProducer<T>, string>(
                Comparer<DocumentProducer<T>>.Create((producer1, producer2) => string.CompareOrdinal(producer1.TargetRange.MinInclusive, producer2.TargetRange.MinInclusive)));
            this.actualMaxPageSize = this.MaxItemCount.GetValueOrDefault(ParallelQueryConfig.GetConfig().ClientInternalMaxItemCount);

            if (this.actualMaxBufferedItemCount < 0)
            {
                throw new OverflowException("actualMaxBufferedItemCount should never be less than 0");
            }

            if (this.actualMaxBufferedItemCount > int.MaxValue)
            {
                throw new OverflowException("actualMaxBufferedItemCount should never be greater than int.MaxValue");
            }

            if (this.actualMaxPageSize < 0)
            {
                throw new OverflowException("actualMaxPageSize should never be less than 0");
            }

            if (this.actualMaxPageSize > int.MaxValue)
            {
                throw new OverflowException("actualMaxPageSize should never be greater than int.MaxValue");
            }
        }

        public override abstract bool IsDone
        {
            get;
        }

        protected INameValueCollection ResponseHeaders
        {
            get
            {
                if (this.IsDone)
                {
                    this.responseHeaders.Remove(HttpConstants.HttpHeaders.Continuation);
                }
                else
                {
                    this.responseHeaders[HttpConstants.HttpHeaders.Continuation] = this.ContinuationToken;
                }

                this.responseHeaders[HttpConstants.HttpHeaders.ActivityId] = this.CorrelatedActivityId.ToString();
                this.responseHeaders[HttpConstants.HttpHeaders.RequestCharge] = this.chargeTracker.GetAndResetCharge().ToString(CultureInfo.InvariantCulture);
                this.groupedQueryMetrics = Interlocked.Exchange(ref this.partitionedQueryMetrics, new ConcurrentBag<Tuple<string, QueryMetrics>>())
                    .GroupBy(tuple => tuple.Item1, tuple => tuple.Item2)
                    .ToDictionary(group => group.Key, group => QueryMetrics.CreateFromIEnumerable(group));
                this.responseHeaders[HttpConstants.HttpHeaders.QueryMetrics] = QueryMetrics
                    .CreateFromIEnumerable(this.groupedQueryMetrics.Values)
                    .ToDelimitedString();

                return this.responseHeaders;
            }
        }

        protected long FreeItemSpace
        {
            get
            {
                return this.actualMaxBufferedItemCount - Interlocked.Read(ref this.totalBufferedItems);
            }
        }

        protected int ActualMaxBufferedItemCount
        {
            get
            {
                return (int)this.actualMaxBufferedItemCount;
            }
        }

        protected abstract override string ContinuationToken
        {
            get;
        }

        public override void Dispose()
        {
            this.TaskScheduler.Dispose();
        }

        public void Stop()
        {
            this.TaskScheduler.Stop();
        }

        public IReadOnlyDictionary<string, QueryMetrics> GetQueryMetrics()
        {
            return this.groupedQueryMetrics;
        }

        public abstract Task<FeedResponse<object>> DrainAsync(int maxElements, CancellationToken token);

        protected override Task<FeedResponse<dynamic>> ExecuteInternalAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        protected void ReduceTotalBufferedItems(int delta)
        {
            Interlocked.Add(ref this.totalBufferedItems, -delta);
        }

        protected async Task InitializeAsync(
            string collectionRid,
            List<Range<string>> queryRanges,
            Func<DocumentProducer<T>, int> taskPriorityFunc,
            IReadOnlyList<PartitionKeyRange> partitionKeyRanges,
            int initialPageSize,
            SqlQuerySpec querySpecForInit,
            Dictionary<string, string> targetRangeToContinuationMap,
            CancellationToken token)
        {
            CollectionCache collectionCache = await this.Client.GetCollectionCacheAsync();

            INameValueCollection requestHeaders = await this.CreateCommonHeadersAsync(this.GetFeedOptions(null));

            DefaultTrace.TraceInformation(string.Format(
                CultureInfo.InvariantCulture,
                "{0}, CorrelatedActivityId: {1} | Parallel~ContextBase.InitializeAsync, MaxBufferedItemCount: {2}, Target PartitionKeyRange Count: {3}, MaximumConcurrencyLevel: {4}, DocumentProducer Initial Page Size {5}",
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                this.CorrelatedActivityId,
                this.actualMaxBufferedItemCount,
                partitionKeyRanges.Count,
                this.TaskScheduler.MaximumConcurrencyLevel,
                initialPageSize));

            foreach (PartitionKeyRange range in partitionKeyRanges)
            {
                string initialContinuationToken = (targetRangeToContinuationMap != null && targetRangeToContinuationMap.ContainsKey(range.Id)) ? targetRangeToContinuationMap[range.Id] : null;

                this.DocumentProducers.Add(new DocumentProducer<T>(
                    this.TaskScheduler,
                    (continuationToken, pageSize) =>
                    {
                        INameValueCollection headers = requestHeaders.Clone();
                        headers[HttpConstants.HttpHeaders.Continuation] = continuationToken;
                        headers[HttpConstants.HttpHeaders.PageSize] = pageSize.ToString(CultureInfo.InvariantCulture);
                        return this.CreateDocumentServiceRequest(
                            headers,
                            querySpecForInit,
                            range,
                            collectionRid);
                    },
                    range,
                    taskPriorityFunc,
                    this.ExecuteRequestAsync<T>,
                    () => new NonRetriableInvalidPartitionExceptionRetryPolicy(collectionCache, this.Client.RetryPolicy.GetRequestPolicy()),
                    this.OnDocumentProducerCompleteFetching,
                    this.CorrelatedActivityId,
                    initialPageSize,
                    initialContinuationToken));

                if (!string.IsNullOrEmpty(initialContinuationToken))
                {
                    this.CurrentContinuationTokens[this.DocumentProducers[this.DocumentProducers.Count - 1]] = initialContinuationToken;
                }
            }
        }

        protected async Task RepairContextAsync(
            string collectionRid,
            int currentDocumentProducerIndex,
            Func<DocumentProducer<T>, int> taskPriorityFunc,
            IReadOnlyList<PartitionKeyRange> replacementRanges,
            SqlQuerySpec querySpecForRepair,
            Action callback = null)
        {
            CollectionCache collectionCache = await this.Client.GetCollectionCacheAsync();

            INameValueCollection requestHeaders = await this.CreateCommonHeadersAsync(this.GetFeedOptions(null));
            this.DocumentProducers.Capacity = this.DocumentProducers.Count + replacementRanges.Count - 1;
            DocumentProducer<T> replacedDocumentProducer = this.DocumentProducers[currentDocumentProducerIndex];

            DefaultTrace.TraceInformation(string.Format(
                CultureInfo.InvariantCulture,
                "{0}, CorrelatedActivityId: {5} | Parallel~ContextBase.RepairContextAsync, MaxBufferedItemCount: {1}, Replacement PartitionKeyRange Count: {2}, MaximumConcurrencyLevel: {3}, DocumentProducer Initial Page Size {4}",
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                this.actualMaxBufferedItemCount,
                replacementRanges.Count,
                this.TaskScheduler.MaximumConcurrencyLevel,
                replacedDocumentProducer.PageSize,
                this.CorrelatedActivityId));

            int index = currentDocumentProducerIndex + 1;

            foreach (PartitionKeyRange range in replacementRanges)
            {
                this.DocumentProducers.Insert(
                    index++,
                    new DocumentProducer<T>(
                        this.TaskScheduler,
                        (continuationToken, pageSize) =>
                        {
                            INameValueCollection headers = requestHeaders.Clone();
                            headers[HttpConstants.HttpHeaders.Continuation] = continuationToken;
                            headers[HttpConstants.HttpHeaders.PageSize] = pageSize.ToString(CultureInfo.InvariantCulture);
                            return this.CreateDocumentServiceRequest(
                                headers,
                                querySpecForRepair,
                                range,
                                collectionRid);
                        },
                    range,
                    taskPriorityFunc,
                    this.ExecuteRequestAsync<T>,
                    () => new NonRetriableInvalidPartitionExceptionRetryPolicy(collectionCache, this.Client.RetryPolicy.GetRequestPolicy()),
                    this.OnDocumentProducerCompleteFetching,
                    this.CorrelatedActivityId,
                    replacedDocumentProducer.PageSize,
                    replacedDocumentProducer.CurrentBackendContinuationToken));
            }

            this.DocumentProducers.RemoveAt(currentDocumentProducerIndex);

            if (callback != null)
            {
                callback();
            }

            if (this.ShouldPrefetch)
            {
                for (int i = 0; i < replacementRanges.Count; i++)
                {
                    this.DocumentProducers[i + currentDocumentProducerIndex].TryScheduleFetch();
                }
            }

            if (this.CurrentContinuationTokens.Remove(replacedDocumentProducer))
            {
                for (int i = 0; i < replacementRanges.Count; ++i)
                {
                    this.CurrentContinuationTokens[this.DocumentProducers[currentDocumentProducerIndex + i]] = replacedDocumentProducer.CurrentBackendContinuationToken;
                }
            }
        }

        /// <summary>
        /// If a query encounters split up resuming using continuation, we need to regenerate the continuation tokens. 
        /// Specifically, since after split we will have new set of ranges, we need to remove continuation token for the 
        /// parent partition and introduce continuation token for the child partitions. 
        /// 
        /// This function does that. Also in that process, we also check validity of the input continuation tokens. For example, 
        /// even after split the boundary ranges of the child partitions should match with the parent partitions. If the Min and Max
        /// range of a target partition in the continuation token was Min1 and Max1. Then the Min and Max range info for the two 
        /// corresponding child partitions C1Min, C1Max, C2Min, and C2Max should follow the constrain below:
        ///  PMax = C2Max > C2Min > C1Max > C1Min = PMin. 
        /// 
        /// Note that, 
        /// this is assuming the fact that the target partition was split once. But, in reality, the target partition might be split 
        /// multiple times  
        /// </summary>
        /// <Remarks>
        /// The code assumes that merge doesn't happen
        /// </Remarks>

        protected int FindTargetRangeAndExtractContinuationTokens<TContinuationToken>(
            List<PartitionKeyRange> partitionKeyRanges,
            IEnumerable<Tuple<TContinuationToken, Range<string>>> suppliedContinuationTokens,
            out Dictionary<string, TContinuationToken> targetRangeToContinuationTokenMap)
        {
            targetRangeToContinuationTokenMap = new Dictionary<string, TContinuationToken>();

            bool foundInitialRange = false;
            int index = 0;
            int minIndex = -1;

            foreach (Tuple<TContinuationToken, Range<string>> tuple in suppliedContinuationTokens)
            {
                if (!foundInitialRange)
                {
                    PartitionKeyRange targetRange = new PartitionKeyRange
                    {
                        MinInclusive = tuple.Item2.Min,
                        MaxExclusive = tuple.Item2.Max
                    };

                    minIndex = partitionKeyRanges.BinarySearch(
                        targetRange,
                        Comparer<PartitionKeyRange>.Create((range1, range2) => string.CompareOrdinal(range1.MinInclusive, range2.MinInclusive)));

                    if (minIndex < 0)
                    {
                        DefaultTrace.TraceWarning(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "{0}, CorrelatedActivityId: {2} | Invalid format for continuation token {1} for OrderBy~Context.",
                                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                tuple.Item1.ToString(),
                                this.CorrelatedActivityId));
                        throw new BadRequestException(RMResources.InvalidContinuationToken);
                    }

                    index = minIndex;
                    foundInitialRange = true;
                }

                if (partitionKeyRanges[index].ToRange().Equals(tuple.Item2))
                {
                    targetRangeToContinuationTokenMap.Add(partitionKeyRanges[index++].Id, tuple.Item1);
                }
                else
                {
                    bool canConsume = true;
                    if (string.CompareOrdinal(partitionKeyRanges[index].MinInclusive, tuple.Item2.Min) == 0
                        && string.CompareOrdinal(tuple.Item2.Max, partitionKeyRanges[index].MaxExclusive) > 0)
                    {
                        while (index < partitionKeyRanges.Count
                                && string.CompareOrdinal(partitionKeyRanges[index].MaxExclusive, tuple.Item2.Max) <= 0)
                        {
                            targetRangeToContinuationTokenMap.Add(partitionKeyRanges[index++].Id, tuple.Item1);
                        }

                        if (index > 0 && string.CompareOrdinal(partitionKeyRanges[index - 1].MaxExclusive, tuple.Item2.Max) != 0)
                        {
                            canConsume = false;
                        }
                    }
                    else
                    {
                        canConsume = false;
                    }

                    if (!canConsume)
                    {
                        DefaultTrace.TraceWarning(
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "{0}, CorrelatedActivityId: {1} | Invalid format for continuation token {2} for OrderBy~Context.",
                                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                                this.CorrelatedActivityId,
                                tuple.Item1.ToString()));
                        throw new BadRequestException(RMResources.InvalidContinuationToken);
                    }
                }

                if (index >= partitionKeyRanges.Count)
                {
                    break;
                }
            }

            return minIndex;
        }

        protected async Task<bool> TryMoveNextProducerAsync(
            DocumentProducer<T> producer,
            Func<DocumentProducer<T>, Task<DocumentProducer<T>>> producerRepairCallback,
            CancellationToken cancellationToken)
        {
            bool movedNext = false;
            DocumentProducer<T> currentProducer = producer;

            while (true)
            {
                bool needRefreshedPartitionKeyRangeCache = false;
                try
                {
                    movedNext = await currentProducer.MoveNextAsync(cancellationToken);
                }
                catch (DocumentClientException ex)
                {
                    if (!(needRefreshedPartitionKeyRangeCache = base.NeedPartitionKeyRangeCacheRefresh(ex)))
                    {
                        throw;
                    }
                }

                if (needRefreshedPartitionKeyRangeCache)
                {
                    currentProducer = await producerRepairCallback(currentProducer);
                }
                else
                {
                    break;
                }
            }

            if (!movedNext)
            {
                this.CurrentContinuationTokens.Remove(currentProducer);
            }

            return movedNext;
        }

        private void OnDocumentProducerCompleteFetching(
            DocumentProducer<T> producer,
            int size,
            double resourceUnitUsage,
            QueryMetrics queryMetrics,
            long responseLengthBytes,
            CancellationToken token)
        {
            // Update charge and states
            this.chargeTracker.AddCharge(resourceUnitUsage);
            Interlocked.Add(ref this.totalBufferedItems, size);
            Interlocked.Increment(ref this.totalRequestRoundTrips);
            this.IncrementResponseLengthBytes(responseLengthBytes);
            this.partitionedQueryMetrics.Add(Tuple.Create(producer.TargetRange.Id, queryMetrics));

            //Check to see if we can buffer more item
            long countToAdd = size - this.FreeItemSpace;
            if (countToAdd > 0 &&
                this.actualMaxBufferedItemCount < MaxixmumDynamicMaxBufferedItemCountValue - countToAdd)
            {
                DefaultTrace.TraceVerbose(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, CorrelatedActivityId: {4} | Id: {1}, increasing MaxBufferedItemCount {2} by {3}.",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    producer.TargetRange.Id,
                    this.actualMaxBufferedItemCount,
                    countToAdd,
                    this.CorrelatedActivityId));

                countToAdd += this.actualMaxBufferedItemCount;
            }

            // Adjust max DoP if necessary
            this.AdjustTaskSchedulerMaximumConcurrencyLevel();

            // Fetch again if necessary
            if (!producer.FetchedAll)
            {
                if (producer.PageSize < this.actualMaxPageSize)
                {
                    producer.PageSize = Math.Min((long)(producer.PageSize * DynamicPageSizeAdjustmentFactor), this.actualMaxPageSize);

                    Debug.Assert(producer.PageSize >= 0 && producer.PageSize <= int.MaxValue, string.Format("producer.PageSize is invalid at {0}", producer.PageSize));
                }

                if (this.ShouldPrefetch &&
                    this.FreeItemSpace - producer.NormalizedPageSize > 0)
                {
                    producer.TryScheduleFetch();
                }
            }

            DefaultTrace.TraceVerbose(string.Format(
                CultureInfo.InvariantCulture,
                "{0}, CorrelatedActivityId: {5} | Id: {1}, size: {2}, resourceUnitUsage: {3}, taskScheduler.CurrentRunningTaskCount: {4}",
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                producer.TargetRange.Id,
                size,
                resourceUnitUsage,
                this.TaskScheduler.CurrentRunningTaskCount,
                this.CorrelatedActivityId));
        }

        private void AdjustTaskSchedulerMaximumConcurrencyLevel()
        {
            lock (this.TaskScheduler)
            {
                int delta = this.GetCurrentMaximumAllowedConcurrentTasks(this.TaskScheduler.CurrentRunningTaskCount) - this.TaskScheduler.MaximumConcurrencyLevel;
                if (delta > 0)
                {
                    this.TaskScheduler.IncreaseMaximumConcurrencyLevel(delta);
                }
            }
        }

        /// <summary>
        /// This function decides the maximum number of concurrent operations.
        ///  1. (feedOptions.MaximumDegreeOfParallelism == -1 or less) => Automatic
        ///  2. (feedOptions.MaximumDegreeOfParallelism == 0 ) => No parallel execution, serial (current implementation). 
        ///  The code should not come here. DefaultExecutor instead of Parallel executor will be executed. 
        ///  3. (feedOptions.MaximumDegreeOfParallelism > 0 ) => Parallel with a max of specified number of tasks
        /// </summary>
        /// <param name="currentRunningTaskCount">
        ///     Current number of running tasks 
        /// </param>
        /// <returns>
        ///     Returns the number of tasks to run
        /// </returns>
        private int GetCurrentMaximumAllowedConcurrentTasks(int currentRunningTaskCount)
        {
            if (this.MaxDegreeOfParallelism >= 1)
            {
                return this.MaxDegreeOfParallelism;
            }

            if (this.MaxDegreeOfParallelism == 0)
            {
                return 1;
            }

            if (currentRunningTaskCount <= 0)
            {
                return ParallelQueryConfig.GetConfig().AutoModeTasksIncrementFactor;
            }

            double currentAverage = (double)Interlocked.Read(ref this.totalRequestRoundTrips) / currentRunningTaskCount;

            if (currentAverage > this.currentAverageNumberOfRequestsPerTask)
            {
                currentRunningTaskCount *= ParallelQueryConfig.GetConfig().AutoModeTasksIncrementFactor;
            }

            this.currentAverageNumberOfRequestsPerTask = currentAverage;
            return Math.Max(currentRunningTaskCount, Math.Max(2, Environment.ProcessorCount * ParallelQueryConfig.GetConfig().NumberOfNetworkCallsPerProcessor));
        }

        protected virtual long GetAndResetResponseLengthBytes()
        {
            return Interlocked.Exchange(ref this.totalResponseLengthBytes, 0);
        }

        protected virtual  long IncrementResponseLengthBytes(long incrementValue)
        {
            return Interlocked.Add(ref this.totalResponseLengthBytes, incrementValue);
        }

    }
}