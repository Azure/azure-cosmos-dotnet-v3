//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// The DocumentProducer is the base unit of buffering and iterating through documents.
    /// Note that a document producer will let you iterate through documents within the pages of a partition and maintain any state.
    /// In pseudo code this works out to:
    /// for page in partition:
    ///     for document in page:
    ///         yield document
    ///     update_state()
    /// </summary>
    internal sealed class DocumentProducer
    {
        /// <summary>
        /// The buffered pages that is thread safe, since the producer and consumer of the queue can be on different threads.
        /// We buffer TryMonad of DoucmentFeedResponse of T, since we want to buffer exceptions,
        /// so that the exception is thrown on the consumer thread (instead of the background producer thread), thus observing the exception.
        /// </summary>
        private readonly AsyncCollection<TryMonad<DocumentFeedResponse<CosmosElement>>> bufferedPages;

        /// <summary>
        /// The document producer can only be fetching one page at a time.
        /// Since the fetch function can be called by the execution contexts or the scheduler, we use this semaphore to keep the fetch function thread safe.
        /// </summary>
        private readonly SemaphoreSlim fetchSemaphore;

        /// <summary>
        /// The callback function used to create a <see cref="DocumentServiceRequest"/> that is the entry point to fetch documents from the backend.
        /// </summary>
        private readonly Func<PartitionKeyRange, string, int, DocumentServiceRequest> createRequestFunc;

        /// <summary>
        /// The callback used to take a <see cref="DocumentServiceRequest"/> and retrieve a page of documents as a <see cref="DocumentFeedResponse{T}"/>
        /// </summary>
        private readonly Func<DocumentServiceRequest, IDocumentClientRetryPolicy, CancellationToken, Task<DocumentFeedResponse<CosmosElement>>> executeRequestFunc;

        /// <summary>
        /// Callback used to create a retry policy that will be used to determine when and how to retry fetches.
        /// </summary>
        private readonly Func<IDocumentClientRetryPolicy> createRetryPolicyFunc;

        /// <summary>
        /// Once a document producer tree finishes fetching document they should call on this function so that the higher level execution context can aggregate the number of documents fetched, the request charge, and the query metrics.
        /// </summary>
        private readonly ProduceAsyncCompleteDelegate produceAsyncCompleteCallback;

        /// <summary>
        /// Keeps track of when a fetch happens and ends to calculate scheduling metrics.
        /// </summary>
        private readonly SchedulingStopwatch fetchSchedulingMetrics;

        /// <summary>
        /// Keeps track of fetch ranges.
        /// </summary>
        private readonly FetchExecutionRangeAccumulator fetchExecutionRangeAccumulator;

        /// <summary>
        /// Equality comparer to determine if you have come across a distinct document according to the sort order.
        /// </summary>
        private readonly IEqualityComparer<CosmosElement> equalityComparer;

        /// <summary>
        /// The current continuation token that the user has read from the document producer tree.
        /// This is used for determining whether there are more results.
        /// </summary>
        private string currentContinuationToken;

        /// <summary>
        /// The number of items left in the current page, which is used by parallel queries since they need to drain full pages.
        /// </summary>
        private int itemsLeftInCurrentPage;

        /// <summary>
        /// The number of items currently buffered, which is used by the scheduler incase you want to implement give less full document producers a higher priority.
        /// </summary>
        private long bufferedItemCount;

        /// <summary>
        /// The current page that is being enumerated.
        /// </summary>
        private IEnumerator<CosmosElement> currentPage;

        /// <summary>
        /// Whether or not the document producer has started fetching.
        /// </summary>
        private bool hasStartedFetching;

        /// <summary>
        /// Need this flag so that the document producer stops buffering more results after a fatal exception.
        /// </summary>
        private bool documentProducerHitException;

        /// <summary>
        /// Initializes a new instance of the DocumentProducer class.
        /// </summary>
        /// <param name="partitionKeyRange">The partition key range.</param>
        /// <param name="createRequestFunc">The callback to create a request.</param>
        /// <param name="executeRequestFunc">The callback to execute the request.</param>
        /// <param name="createRetryPolicyFunc">The callback to create the retry policy.</param>
        /// <param name="produceAsyncCompleteCallback">The callback to call once you are done fetching.</param>
        /// <param name="equalityComparer">The comparer to use to determine whether the producer has seen a new document.</param>
        /// <param name="initialPageSize">The initial page size.</param>
        /// <param name="initialContinuationToken">The initial continuation token.</param>
        public DocumentProducer(
            PartitionKeyRange partitionKeyRange,
            Func<PartitionKeyRange, string, int, DocumentServiceRequest> createRequestFunc,
            Func<DocumentServiceRequest, IDocumentClientRetryPolicy, CancellationToken, Task<DocumentFeedResponse<CosmosElement>>> executeRequestFunc,
            Func<IDocumentClientRetryPolicy> createRetryPolicyFunc,
            ProduceAsyncCompleteDelegate produceAsyncCompleteCallback,
            IEqualityComparer<CosmosElement> equalityComparer,
            long initialPageSize = 50,
            string initialContinuationToken = null)
        {
            this.bufferedPages = new AsyncCollection<TryMonad<DocumentFeedResponse<CosmosElement>>>();
            // We use a binary semaphore to get the behavior of a mutex,
            // since fetching documents from the backend using a continuation token is a critical section.
            this.fetchSemaphore = new SemaphoreSlim(1, 1);
            if (partitionKeyRange == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyRange));
            }

            if (createRequestFunc == null)
            {
                throw new ArgumentNullException(nameof(createRequestFunc));
            }

            if (executeRequestFunc == null)
            {
                throw new ArgumentNullException(nameof(executeRequestFunc));
            }

            if (createRetryPolicyFunc == null)
            {
                throw new ArgumentNullException(nameof(createRetryPolicyFunc));
            }

            if (produceAsyncCompleteCallback == null)
            {
                throw new ArgumentNullException(nameof(produceAsyncCompleteCallback));
            }

            if (equalityComparer == null)
            {
                throw new ArgumentNullException(nameof(equalityComparer));
            }

            this.PartitionKeyRange = partitionKeyRange;
            this.createRequestFunc = createRequestFunc;
            this.executeRequestFunc = executeRequestFunc;
            this.createRetryPolicyFunc = createRetryPolicyFunc;
            this.produceAsyncCompleteCallback = produceAsyncCompleteCallback;
            this.equalityComparer = equalityComparer;
            this.PageSize = initialPageSize;
            this.currentContinuationToken = initialContinuationToken;
            this.BackendContinuationToken = initialContinuationToken;
            this.PreviousContinuationToken = initialContinuationToken;
            if (!string.IsNullOrEmpty(initialContinuationToken))
            {
                this.hasStartedFetching = true;
                this.IsActive = true;
            }

            this.fetchSchedulingMetrics = new SchedulingStopwatch();
            this.fetchSchedulingMetrics.Ready();
            this.fetchExecutionRangeAccumulator = new FetchExecutionRangeAccumulator();

            this.HasMoreResults = true;
        }

        public delegate void ProduceAsyncCompleteDelegate(
            DocumentProducer producer,
            int numberOfDocuments,
            double requestCharge,
            QueryMetrics queryMetrics,
            long responseLengthInBytes,
            CancellationToken token);

        /// <summary>
        /// Gets the <see cref="PartitionKeyRange"/> for the partition that this document producer is fetching from.
        /// </summary>
        public PartitionKeyRange PartitionKeyRange
        {
            get;
        }

        /// <summary>
        /// Gets or sets the filter predicate for the document producer that is used by order by execution context.
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        /// Gets the previous continuation token.
        /// </summary>
        public string PreviousContinuationToken { get; private set; }

        /// <summary>
        /// Gets the backend continuation token.
        /// </summary>
        public string BackendContinuationToken { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the continuation token for this producer needs to be given back as part of the composite continuation token.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this producer is at the beginning of the page.
        /// </summary>
        public bool IsAtBeginningOfPage { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this producer has more results.
        /// </summary>
        public bool HasMoreResults { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this producer has more backend results.
        /// </summary>
        public bool HasMoreBackendResults => this.hasStartedFetching == false
                    || (this.hasStartedFetching == true && !string.IsNullOrEmpty(this.BackendContinuationToken));

        /// <summary>
        /// Gets how many items are left in the current page.
        /// </summary>
        public int ItemsLeftInCurrentPage => this.itemsLeftInCurrentPage;

        /// <summary>
        /// Gets how many documents are buffered in this producer.
        /// </summary>
        public int BufferedItemCount => (int)this.bufferedItemCount;

        /// <summary>
        /// Gets or sets the page size of this producer.
        /// </summary>
        public long PageSize { get; set; }

        /// <summary>
        /// Gets the activity for the last request made by this document producer.
        /// </summary>
        public Guid ActivityId { get; private set; }

        /// <summary>
        /// Gets the current document in this producer.
        /// </summary>
        public CosmosElement Current { get; private set; }

        /// <summary>
        /// Moves to the next document in the producer.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>Whether or not we successfully moved to the next document.</returns>
        public async Task<bool> MoveNextAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            CosmosElement originalCurrent = this.Current;
            bool movedNext = await this.MoveNextAsyncImplementationAsync(token);

            if (!movedNext || (originalCurrent != null && !this.equalityComparer.Equals(originalCurrent, this.Current)))
            {
                this.IsActive = false;
            }

            return movedNext;
        }

        /// <summary>
        /// Buffers more documents if the producer is empty.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        public async Task BufferMoreIfEmptyAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (this.bufferedPages.Count == 0)
            {
                await this.BufferMoreDocumentsAsync(token);
            }
        }

        /// <summary>
        /// Buffers more documents in the producer.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>A task to await on.</returns>
        public async Task BufferMoreDocumentsAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                await this.fetchSemaphore.WaitAsync();
                if (!this.HasMoreBackendResults || this.documentProducerHitException)
                {
                    // Just NOP
                    return;
                }

                this.fetchSchedulingMetrics.Start();
                this.fetchExecutionRangeAccumulator.BeginFetchRange();
                int pageSize = (int)Math.Min(this.PageSize, int.MaxValue);
                using (DocumentServiceRequest request = this.createRequestFunc(this.PartitionKeyRange, this.BackendContinuationToken, pageSize))
                {
                    IDocumentClientRetryPolicy retryPolicy = this.createRetryPolicyFunc();

                    // Custom backoff and retry
                    int retries = 0;
                    while (true)
                    {
                        try
                        {
                            DocumentFeedResponse<CosmosElement> feedResponse = await this.executeRequestFunc(request, retryPolicy, token);
                            this.fetchExecutionRangeAccumulator.EndFetchRange(
                                this.PartitionKeyRange.Id,
                                feedResponse.ActivityId,
                                feedResponse.Count,
                                retries);
                            this.fetchSchedulingMetrics.Stop();
                            this.hasStartedFetching = true;
                            this.BackendContinuationToken = feedResponse.ResponseContinuation;
                            this.ActivityId = Guid.Parse(feedResponse.ActivityId);
                            await this.bufferedPages.AddAsync(TryMonad<DocumentFeedResponse<CosmosElement>>.FromResult(feedResponse));
                            Interlocked.Add(ref this.bufferedItemCount, feedResponse.Count);

                            QueryMetrics queryMetrics = QueryMetrics.Zero;
                            if (feedResponse.ResponseHeaders[HttpConstants.HttpHeaders.QueryMetrics] != null)
                            {
                                queryMetrics = QueryMetrics.CreateFromDelimitedStringAndClientSideMetrics(
                                    feedResponse.ResponseHeaders[HttpConstants.HttpHeaders.QueryMetrics],
                                    new ClientSideMetrics(
                                        retries,
                                        feedResponse.RequestCharge,
                                        this.fetchExecutionRangeAccumulator.GetExecutionRanges(),
                                        new List<Tuple<string, SchedulingTimeSpan>>()));
                            }

                            if (!this.HasMoreBackendResults)
                            {
                                queryMetrics = QueryMetrics.CreateWithSchedulingMetrics(
                                    queryMetrics,
                                    new List<Tuple<string, SchedulingTimeSpan>>
                                    {
                                        new Tuple<string, SchedulingTimeSpan>(
                                            this.PartitionKeyRange.Id,
                                            this.fetchSchedulingMetrics.Elapsed)
                                    });
                            }

                            this.produceAsyncCompleteCallback(
                                this,
                                feedResponse.Count,
                                feedResponse.RequestCharge,
                                queryMetrics,
                                feedResponse.ResponseLengthBytes,
                                token);

                            break;
                        }
                        catch (Exception exception)
                        {
                            // See if we need to retry or just throw
                            ShouldRetryResult shouldRetryResult = await retryPolicy.ShouldRetryAsync(exception, token);
                            if (!shouldRetryResult.ShouldRetry)
                            {
                                Exception exceptionToBuffer;
                                if (shouldRetryResult.ExceptionToThrow != null)
                                {
                                    exceptionToBuffer = shouldRetryResult.ExceptionToThrow;
                                }
                                else
                                {
                                    // Propagate original exception.
                                    exceptionToBuffer = exception;
                                }

                                // Buffer the exception instead of throwing, since we don't want an unobserved exception.
                                await this.bufferedPages.AddAsync(TryMonad<DocumentFeedResponse<CosmosElement>>.FromException(exceptionToBuffer));

                                // set this flag so that people stop trying to buffer more on this producer.
                                this.documentProducerHitException = true;
                                break;
                            }
                            else
                            {
                                await Task.Delay(shouldRetryResult.BackoffTime);
                                retries++;
                            }
                        }
                    }
                }
            }
            finally
            {
                this.fetchSchedulingMetrics.Stop();
                this.fetchSemaphore.Release();
            }
        }

        public void Shutdown()
        {
            this.HasMoreResults = false;
        }

        /// <summary>
        /// Implementation of move next async.
        /// After this function is called the wrapper function determines if a distinct document has been read and updates the 'isActive' flag.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>Whether or not we successfully moved to the next document in the producer.</returns>
        private async Task<bool> MoveNextAsyncImplementationAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!this.HasMoreResults)
            {
                return false;
            }

            // Always try reading from current page first
            if (this.MoveNextDocumentWithinCurrentPage())
            {
                return true;
            }
            else
            {
                // We might be at a continuation boundary so we need to move to the next page
                if (await this.MoveNextPageAsync(token))
                {
                    return true;
                }
                else
                {
                    this.HasMoreResults = false;
                    return false;
                }
            }
        }

        private bool MoveToFirstDocumentInPage()
        {
            if (this.currentPage == null || !this.currentPage.MoveNext())
            {
                return false;
            }

            this.Current = this.currentPage.Current;
            this.IsAtBeginningOfPage = true;

            return true;
        }

        /// <summary>
        /// Tries to moved to the next document within the current page that we are reading from.
        /// </summary>
        /// <returns>Whether the operation was successful.</returns>
        private bool MoveNextDocumentWithinCurrentPage()
        {
            if (this.currentPage == null)
            {
                return false;
            }

            bool movedNext = this.currentPage.MoveNext();
            this.Current = this.currentPage.Current;

            this.IsAtBeginningOfPage = false;

            Interlocked.Decrement(ref this.bufferedItemCount);
            Interlocked.Decrement(ref this.itemsLeftInCurrentPage);

            return movedNext;
        }

        /// <summary>
        /// Tries to the move to the next page in the document producer.
        /// </summary>
        /// <param name="token">The cancellation token.</param>
        /// <returns>Whether the operation was successful.</returns>
        private async Task<bool> MoveNextPageAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (this.itemsLeftInCurrentPage != 0)
            {
                throw new InvalidOperationException("Tried to move onto the next page before finishing the first page.");
            }

            // We need to buffer pages if empty, since that how we know there are no more pages left.
            await this.BufferMoreIfEmptyAsync(token);

            if (this.bufferedPages.Count == 0)
            {
                return false;
            }

            // Pull a FeedResponse using TryMonad (we could have buffered an exception).
            TryMonad<DocumentFeedResponse<CosmosElement>> tryMonad = await this.bufferedPages.TakeAsync(token);
            DocumentFeedResponse<CosmosElement> feedResponse = tryMonad.Match<DocumentFeedResponse<CosmosElement>>(
                onSuccess: ((page) =>
                {
                    return page;
                }),
                onError: (exceptionDispatchInfo) =>
                {
                    exceptionDispatchInfo.Throw();
                    return null;
                });

            // Update the state.
            this.PreviousContinuationToken = this.currentContinuationToken;
            this.currentContinuationToken = feedResponse.ResponseContinuation;
            this.currentPage = feedResponse.GetEnumerator();
            this.IsAtBeginningOfPage = true;
            this.itemsLeftInCurrentPage = feedResponse.Count;

            // Prime the enumerator,
            // so that current is pointing to the first document instead of one before.
            if (this.MoveToFirstDocumentInPage())
            {
                this.IsAtBeginningOfPage = true;
                return true;
            }
            else
            {
                // We got an empty page
                if (this.currentContinuationToken != null)
                {
                    return await this.MoveNextPageAsync(token);
                }

                return false;
            }
        }

        private struct TryMonad<TResult>
        {
            private readonly TResult result;
            private readonly ExceptionDispatchInfo exceptionDispatchInfo;
            private readonly bool succeeded;

            private TryMonad(
                TResult result,
                ExceptionDispatchInfo exceptionDispatchInfo,
                bool succeeded)
            {
                this.result = result;
                this.exceptionDispatchInfo = exceptionDispatchInfo;
                this.succeeded = succeeded;
            }

            public static TryMonad<TResult> FromResult(TResult result)
            {
                return new TryMonad<TResult>(
                    result: result,
                    exceptionDispatchInfo: default(ExceptionDispatchInfo),
                    succeeded: true);
            }

            public static TryMonad<TResult> FromException(Exception exception)
            {
                return new TryMonad<TResult>(
                    result: default(TResult),
                    exceptionDispatchInfo: ExceptionDispatchInfo.Capture(exception),
                    succeeded: false);
            }

            public TOutput Match<TOutput>(
                Func<TResult, TOutput> onSuccess,
                Func<ExceptionDispatchInfo, TOutput> onError)
            {
                if (this.succeeded)
                {
                    return onSuccess(this.result);
                }
                else
                {
                    return onError(this.exceptionDispatchInfo);
                }
            }
        }
    }
}