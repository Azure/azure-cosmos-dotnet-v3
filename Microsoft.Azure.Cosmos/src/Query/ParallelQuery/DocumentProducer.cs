//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.ParallelQuery
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections.Generic;
    using Microsoft.Azure.Cosmos.Internal;

    internal sealed class DocumentProducer<T>
    {
        private const double ItemBufferTheshold = 0.1;
        // The absolute max page size is 4mb
        private const int GlobalMaxPageSize = 4 * 1024 * 1024;

        private readonly ComparableTaskScheduler taskScheduler;
        private readonly AsyncCollection<FetchResult> itemBuffer;
        private readonly Func<string, int, DocumentServiceRequest> createRequestFunc;
        private readonly PartitionKeyRange targetRange;
        private readonly Func<DocumentProducer<T>, int> taskPriorityFunc;
        private readonly Func<DocumentServiceRequest, CancellationToken, Task<FeedResponse<T>>> executeRequestFunc;
        private readonly Guid correlatedActivityId;
        private readonly Func<IDocumentClientRetryPolicy> createRetryPolicyFunc;
        private readonly ProduceAsyncCompleteDelegate produceAsyncCompleteCallback;

        private readonly SchedulingStopwatch moveNextSchedulingMetrics;
        private readonly SchedulingStopwatch fetchSchedulingMetrics;
        private readonly FetchExecutionRangeAccumulator fetchExecutionRangeAccumulator;

        private readonly SemaphoreSlim fetchStateSemaphore;
        
        private Guid activityId;
        private long isFetching;
        private bool isDone;
        private long bufferedItemCount;
        private T current;
        private long numDocumentsFetched;

        private long retries;

        public DocumentProducer(
            ComparableTaskScheduler taskScheduler,
            Func<string, int, DocumentServiceRequest> createRequestFunc,
            PartitionKeyRange targetRange,
            Func<DocumentProducer<T>, int> taskPriorityFunc,
            Func<DocumentServiceRequest, CancellationToken, Task<FeedResponse<T>>> executeRequestFunc,
            Func<IDocumentClientRetryPolicy> createRetryPolicyFunc,
            ProduceAsyncCompleteDelegate produceAsyncCompleteCallback,
            Guid correlatedActivityId,
            long initialPageSize = 50,
            string initialContinuationToken = null)
        {
            if (taskScheduler == null)
            {
                throw new ArgumentNullException("taskScheduler");
            }

            if (createRequestFunc == null)
            {
                throw new ArgumentNullException("documentServiceRequest");
            }

            if (targetRange == null)
            {
                throw new ArgumentNullException("targetRange");
            }

            if (taskPriorityFunc == null)
            {
                throw new ArgumentNullException("taskPriorityFunc");
            }

            if (executeRequestFunc == null)
            {
                throw new ArgumentNullException("executeRequestFunc");
            }

            if (createRetryPolicyFunc == null)
            {
                throw new ArgumentNullException("createRetryPolicyFunc");
            }

            if (produceAsyncCompleteCallback == null)
            {
                throw new ArgumentNullException("produceAsyncCallback");
            }

            this.taskScheduler = taskScheduler;
            this.itemBuffer = new AsyncCollection<FetchResult>();
            this.createRequestFunc = createRequestFunc;
            this.targetRange = targetRange;
            this.taskPriorityFunc = taskPriorityFunc;
            this.createRetryPolicyFunc = createRetryPolicyFunc;
            this.executeRequestFunc = executeRequestFunc;
            this.produceAsyncCompleteCallback = produceAsyncCompleteCallback;
            this.PageSize = initialPageSize;
            if ((int)this.PageSize < 0)
            {
                throw new ArithmeticException("page size is negative..");
            }
            this.correlatedActivityId = correlatedActivityId;
            this.CurrentBackendContinuationToken = initialContinuationToken;

            this.moveNextSchedulingMetrics = new SchedulingStopwatch();
            this.moveNextSchedulingMetrics.Ready();
            this.fetchSchedulingMetrics = new SchedulingStopwatch();
            this.fetchSchedulingMetrics.Ready();
            this.fetchExecutionRangeAccumulator = new FetchExecutionRangeAccumulator(this.targetRange.Id);

            this.fetchStateSemaphore = new SemaphoreSlim(1, 1);
        }

        public delegate void ProduceAsyncCompleteDelegate(
            DocumentProducer<T> producer,
            int size,
            double resourceUnitUsage,
            QueryMetrics queryMetrics,
            long responseLengthBytes,
            CancellationToken token);

        public int BufferedItemCount
        {
            get
            {
                return (int)Interlocked.Read(ref this.bufferedItemCount);
            }
        }

        /// <summary>
        ///     Gets the current element in the iteration.
        /// </summary>
        public T Current
        {
            get
            {
                if (this.isDone)
                {
                    throw new InvalidOperationException("Producer is closed");
                }

                return this.current;
            }
            private set
            {
                this.current = value;
            }
        }

        public PartitionKeyRange TargetRange
        {
            get
            {
                return this.targetRange;
            }
        }

        public bool FetchedAll
        {
            get
            {
                return this.HasStartedFetching && string.IsNullOrEmpty(this.CurrentBackendContinuationToken);
            }
        }

        public long PageSize
        {
            get;
            set;
        }

        public long NormalizedPageSize
        {
            get
            {
                return this.PageSize == -1 ? 1000 : Math.Min(this.PageSize, GlobalMaxPageSize);
            }
        }

        public string PreviousResponseContinuation
        {
            get;
            private set;
        }

        public string ResponseContinuation
        {
            get;
            private set;
        }

        public string CurrentBackendContinuationToken
        {
            get;
            private set;
        }

        public int ItemsTillNextContinuationBoundary
        {
            get;
            private set;
        }

        public bool IsAtContinuationBoundary
        {
            get;
            private set;
        }

        public Guid ActivityId
        {
            get { return this.activityId; }
            private set { this.activityId = value; }
        }

        public ClientSideRequestStatistics RequestStatistics
        {
            get;
            private set;
        }

        private bool ShouldFetch
        {
            get
            {
                return (this.ItemsTillNextContinuationBoundary - 1) < this.NormalizedPageSize * ItemBufferTheshold && this.itemBuffer.Count <= 0;
            }
        }

        private bool HasStartedFetching { get; set; }

        private IEnumerator<T> CurrentEnumerator { get; set; }

        public long TotalResponseLengthBytes { get; private set; }

        public bool TryScheduleFetch(TimeSpan delay = default(TimeSpan))
        {
            if (this.FetchedAll)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref this.isFetching, 1, 0) != 0)
            {
                return false;
            }

            this.ScheduleFetch(this.createRetryPolicyFunc(), delay);
            return true;
        }

        /// <summary>
        ///     Advances to the next element in the sequence, returning the result asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token that can be used to cancel the operation.</param>
        /// <returns>
        ///     Task containing the result of the operation: true if the DocumentProducer was successfully advanced
        ///     to the next element; false if the DocumentProducer has passed the end of the sequence.
        /// </returns>
        public async Task<bool> MoveNextAsync(CancellationToken cancellationToken)
        {
            this.moveNextSchedulingMetrics.Start();
            try
            {
                if (this.isDone)
                {
                    return false;
                }

                if (await this.GetShouldFetchAsync(cancellationToken))
                {
                    this.TryScheduleFetch();
                }

                if (this.MoveNextInternal())
                {
                    this.IsAtContinuationBoundary = false;
                    --this.ItemsTillNextContinuationBoundary;
                    return true;
                }

                FetchResult fetchResult = await this.itemBuffer.TakeAsync(cancellationToken);
                switch (fetchResult.Type)
                {
                    case FetchResultType.Done:
                        this.isDone = true;
                        return false;
                    case FetchResultType.Exception:
                        fetchResult.ExceptionDispatchInfo.Throw();
                        return false;
                    case FetchResultType.Result:
                        this.UpdateStates(fetchResult.FeedResponse);
                        return true;
                    default:
                        throw new InvalidProgramException(fetchResult.Type.ToString());
                }
            }
            finally
            {
                this.moveNextSchedulingMetrics.Stop();
            }
        }

        private async Task<bool> GetShouldFetchAsync(CancellationToken cancellationToken)
        {
            if (this.FetchedAll)
            {
                return false;
            }

            if (this.ShouldFetch)
            {
                await this.fetchStateSemaphore.WaitAsync(cancellationToken);
                try
                {
                    return this.ShouldFetch && Interlocked.Read(ref this.isFetching) == 0;
                }
                finally
                {
                    this.fetchStateSemaphore.Release();
                }
            }

            return false;
        }

        private void ScheduleFetch(IDocumentClientRetryPolicy retryPolicyInstance, TimeSpan delay = default(TimeSpan))
        {
            // For the same DocumentProducer, the priorities of scheduled tasks monotonically decrease. 
            // This makes sure the tasks are scheduled fairly across all DocumentProducer's.
            bool scheduled = this.taskScheduler.TryQueueTask(
                new DocumentProducerComparableTask(
                    this,
                    retryPolicyInstance),
                delay);

            if (!scheduled)
            {
                Interlocked.Exchange(ref this.isFetching, 0);
                throw new InvalidOperationException("Failed to schedule");
            }
        }

        private bool MoveNextInternal()
        {
            if (this.CurrentEnumerator == null || !this.CurrentEnumerator.MoveNext())
            {
                return false;
            }

            this.Current = this.CurrentEnumerator.Current;

            Interlocked.Decrement(ref this.bufferedItemCount);

            return true;
        }

        private void UpdateStates(FeedResponse<T> feedResponse)
        {
            this.PreviousResponseContinuation = this.ResponseContinuation;
            this.ResponseContinuation = feedResponse.ResponseContinuation;
            this.ItemsTillNextContinuationBoundary = feedResponse.Count;
            this.IsAtContinuationBoundary = true;
            this.CurrentEnumerator = feedResponse.GetEnumerator();
            this.RequestStatistics = feedResponse.RequestStatistics;
            this.TotalResponseLengthBytes += feedResponse.ResponseLengthBytes;

            this.MoveNextInternal();
        }

        private void UpdateRequestContinuationToken(string continuationToken)
        {
            this.CurrentBackendContinuationToken = continuationToken;
            this.HasStartedFetching = true;
        }

        private async Task<DocumentProducer<T>> FetchAsync(IDocumentClientRetryPolicy retryPolicyInstance, CancellationToken cancellationToken)
        {
            // TODO: This workflow could be simplified.
            FetchResult exceptionFetchResult = null;
            try
            {
                this.fetchSchedulingMetrics.Start();
                this.fetchExecutionRangeAccumulator.BeginFetchRange();
                FeedResponse<T> feedResponse = null;
                double requestCharge = 0;
                long responseLengthBytes = 0;
                QueryMetrics queryMetrics = QueryMetrics.Zero;
                do
                {
                    int pageSize = (int)Math.Min(this.PageSize, (long)int.MaxValue);

                    Debug.Assert(pageSize >= 0, string.Format("pageSize was negative ... this.PageSize: {0}", this.PageSize));
                    using (DocumentServiceRequest request = this.createRequestFunc(this.CurrentBackendContinuationToken, pageSize))
                    {
                        retryPolicyInstance = retryPolicyInstance ?? this.createRetryPolicyFunc();
                        retryPolicyInstance.OnBeforeSendRequest(request);

                        // Custom backoff and retry
                        ExceptionDispatchInfo exception = null;
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            feedResponse = await this.executeRequestFunc(request, cancellationToken);
                            this.fetchExecutionRangeAccumulator.EndFetchRange(feedResponse.Count, Interlocked.Read(ref this.retries));
                            this.ActivityId = Guid.Parse(feedResponse.ActivityId);
                        }
                        catch (Exception ex)
                        {
                            exception = ExceptionDispatchInfo.Capture(ex);
                        }

                        if (exception != null)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            ShouldRetryResult shouldRetryResult = await retryPolicyInstance.ShouldRetryAsync(exception.SourceException, cancellationToken);

                            shouldRetryResult.ThrowIfDoneTrying(exception);

                            this.ScheduleFetch(retryPolicyInstance, shouldRetryResult.BackoffTime);
                            Interlocked.Increment(ref this.retries);
                            return this;
                        }

                        requestCharge += feedResponse.RequestCharge;
                        responseLengthBytes += feedResponse.ResponseLengthBytes;
                        if (feedResponse.Headers[HttpConstants.HttpHeaders.QueryMetrics] != null)
                        {
                            queryMetrics = QueryMetrics.CreateFromDelimitedStringAndClientSideMetrics(
                                feedResponse.Headers[HttpConstants.HttpHeaders.QueryMetrics],
                                new ClientSideMetrics(this.retries, requestCharge,
                                this.fetchExecutionRangeAccumulator.GetExecutionRanges(),
                                new List<Tuple<string, SchedulingTimeSpan>>()),
                                this.activityId);
                            // Reset the counters.
                            Interlocked.Exchange(ref this.retries, 0);
                        }

                        this.UpdateRequestContinuationToken(feedResponse.ResponseContinuation);

                        retryPolicyInstance = null;
                        this.numDocumentsFetched += feedResponse.Count;
                    }
                }

                while (!this.FetchedAll && feedResponse.Count <= 0);
                await this.CompleteFetchAsync(feedResponse, cancellationToken);
                this.produceAsyncCompleteCallback(this, feedResponse.Count, requestCharge, queryMetrics, responseLengthBytes, cancellationToken);
            }
            catch (Exception ex)
            {
                DefaultTrace.TraceWarning(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}, CorrelatedActivityId: {1}, ActivityId {2} | DocumentProducer Id: {3}, Exception in FetchAsync: {4}",
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                    this.correlatedActivityId,
                    this.ActivityId,
                    this.targetRange.Id,
                    ex.Message));

                exceptionFetchResult = new FetchResult(ExceptionDispatchInfo.Capture(ex));
            }
            finally
            {
                this.fetchSchedulingMetrics.Stop();
                if (this.FetchedAll)
                {   
                    // One more callback to send the scheduling metrics
                    this.produceAsyncCompleteCallback(
                        producer: this,
                        size: 0,
                        resourceUnitUsage: 0,
                        queryMetrics: QueryMetrics.CreateFromDelimitedStringAndClientSideMetrics(
                            QueryMetrics.Zero.ToDelimitedString(),
                            new ClientSideMetrics(
                                retries: 0,
                                requestCharge: 0,
                                fetchExecutionRanges: new List<FetchExecutionRange>(),
                                partitionSchedulingTimeSpans: new List<Tuple<string, SchedulingTimeSpan>> { 
                                    new Tuple<string, SchedulingTimeSpan>(this.targetRange.Id, this.fetchSchedulingMetrics.Elapsed)}),
                            Guid.Empty),
                        responseLengthBytes: 0,
                        token: cancellationToken);
                }
            }

            if (exceptionFetchResult != null)
            {
                this.UpdateRequestContinuationToken(this.CurrentBackendContinuationToken);
                await this.itemBuffer.AddAsync(exceptionFetchResult, cancellationToken);
            }

            return this;
        }

        private async Task CompleteFetchAsync(FeedResponse<T> feedResponse, CancellationToken cancellationToken)
        {
            await this.fetchStateSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (feedResponse.Count > 0)
                {
                    await this.itemBuffer.AddAsync(new FetchResult(feedResponse), cancellationToken);
                    Interlocked.Add(ref this.bufferedItemCount, feedResponse.Count);
                }

                if (this.FetchedAll)
                {
                    await this.itemBuffer.AddAsync(FetchResult.DoneResult, cancellationToken);
                }

                Interlocked.Exchange(ref this.isFetching, 0);
            }
            finally
            {
                this.fetchStateSemaphore.Release();
            }
        }

        private sealed class DocumentProducerComparableTask : ComparableTask
        {
            private readonly DocumentProducer<T> producer;
            private readonly IDocumentClientRetryPolicy retryPolicyInstance;

            public DocumentProducerComparableTask(
                DocumentProducer<T> producer,
                IDocumentClientRetryPolicy retryPolicyInstance) :
                base(producer.taskPriorityFunc(producer))
            {
                this.producer = producer;
                this.retryPolicyInstance = retryPolicyInstance;
            }

            public override Task StartAsync(CancellationToken cancellationToken)
            {
                return this.producer.FetchAsync(this.retryPolicyInstance, cancellationToken);
            }

            public override bool Equals(IComparableTask other)
            {
                return this.Equals(other as DocumentProducerComparableTask);
            }

            public override int GetHashCode()
            {
                return this.producer.TargetRange.GetHashCode();
            }

            private bool Equals(DocumentProducerComparableTask other)
            {
                return this.producer.TargetRange.Equals(other.producer.TargetRange);
            }
        }

        private sealed class FetchResult
        {
            public static readonly FetchResult DoneResult = new FetchResult()
            {
                Type = FetchResultType.Done,
            };

            public FetchResult(FeedResponse<T> feedResponse)
            {
                this.FeedResponse = feedResponse;
                this.Type = FetchResultType.Result;
            }

            public FetchResult(ExceptionDispatchInfo exceptionDispatchInfo)
            {
                this.ExceptionDispatchInfo = exceptionDispatchInfo;
                this.Type = FetchResultType.Exception;
            }

            private FetchResult()
            {
            }

            public FetchResultType Type
            {
                get;
                private set;
            }

            public FeedResponse<T> FeedResponse
            {
                get;
                private set;
            }

            public ExceptionDispatchInfo ExceptionDispatchInfo
            {
                get;
                private set;
            }
        }

        private enum FetchResultType
        {
            Done,
            Exception,
            Result,
        }
    }
}