//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Collections;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Documents;
    using PartitionKeyRange = Documents.PartitionKeyRange;
    using PartitionKeyRangeIdentity = Documents.PartitionKeyRangeIdentity;

    /// <summary>
    /// The ItemProducer is the base unit of buffering and iterating through documents.
    /// Note that a document producer will let you iterate through documents within the pages of a partition and maintain any state.
    /// In pseudo code this works out to:
    /// for page in partition:
    ///     for document in page:
    ///         yield document
    ///     update_state()
    /// </summary>
    internal sealed class ItemProducer
    {
        /// <summary>
        /// The buffered pages that is thread safe, since the producer and consumer of the queue can be on different threads.
        /// We buffer TryCatch of DoucmentFeedResponse of T, since we want to buffer exceptions,
        /// so that the exception is thrown on the consumer thread (instead of the background producer thread), thus observing the exception.
        /// </summary>
        private readonly AsyncCollection<QueryResponseCore> bufferedPages;

        /// <summary>
        /// The document producer can only be fetching one page at a time.
        /// Since the fetch function can be called by the execution contexts or the scheduler, we use this semaphore to keep the fetch function thread safe.
        /// </summary>
        private readonly SemaphoreSlim fetchSemaphore;

        /// <summary>
        /// Once a document producer tree finishes fetching document they should call on this function so that the higher level execution context can aggregate the number of documents fetched, the request charge, and the query metrics.
        /// </summary>
        private readonly ProduceAsyncCompleteDelegate produceAsyncCompleteCallback;

        /// <summary>
        /// Keeps track of when a fetch happens and ends to calculate scheduling metrics.
        /// </summary>
        private readonly SchedulingStopwatch fetchSchedulingMetrics;

        /// <summary>
        /// Equality comparer to determine if you have come across a distinct document according to the sort order.
        /// </summary>
        private readonly IEqualityComparer<CosmosElement> equalityComparer;

        private readonly CosmosQueryContext queryContext;

        private readonly SqlQuerySpec querySpecForInit;

        private readonly TestInjections testFlags;

        /// <summary>
        /// Over the duration of the life time of a document producer the page size will change, since we have an adaptive page size.
        /// </summary>
        private readonly long pageSize;

        /// <summary>
        /// The current page that is being enumerated.
        /// </summary>
        private IEnumerator<CosmosElement> CurrentPage;

        /// <summary>
        /// The number of items left in the current page, which is used by parallel queries since they need to drain full pages.
        /// </summary>
        private int itemsLeftInCurrentPage;

        /// <summary>
        /// The number of items currently buffered, which is used by the scheduler incase you want to implement give less full document producers a higher priority.
        /// </summary>
        private long bufferedItemCount;

        /// <summary>
        /// Whether or not the document producer has started fetching.
        /// </summary>
        private bool hasStartedFetching;

        /// <summary>
        /// Need this flag so that the document producer stops buffering more results after a fatal exception.
        /// </summary>
        private bool hitException;

        private bool enumeratorPrimed;

        /// <summary>
        /// Initializes a new instance of the ItemProducer class.
        /// </summary>
        /// <param name="queryContext">request context</param>
        /// <param name="querySpecForInit">query spec for initialization</param>
        /// <param name="partitionKeyRange">The partition key range.</param>
        /// <param name="produceAsyncCompleteCallback">The callback to call once you are done fetching.</param>
        /// <param name="equalityComparer">The comparer to use to determine whether the producer has seen a new document.</param>
        /// <param name="testFlags">Flags used to help faciliate testing.</param>
        /// <param name="initialPageSize">The initial page size.</param>
        /// <param name="initialContinuationToken">The initial continuation token.</param>
        public ItemProducer(
            CosmosQueryContext queryContext,
            SqlQuerySpec querySpecForInit,
            PartitionKeyRange partitionKeyRange,
            ProduceAsyncCompleteDelegate produceAsyncCompleteCallback,
            IEqualityComparer<CosmosElement> equalityComparer,
            TestInjections testFlags,
            long initialPageSize = 50,
            string initialContinuationToken = null)
        {
            this.bufferedPages = new AsyncCollection<QueryResponseCore>();

            // We use a binary semaphore to get the behavior of a mutex,
            // since fetching documents from the backend using a continuation token is a critical section.
            this.fetchSemaphore = new SemaphoreSlim(1, 1);
            this.queryContext = queryContext;
            this.querySpecForInit = querySpecForInit;
            this.PartitionKeyRange = partitionKeyRange ?? throw new ArgumentNullException(nameof(partitionKeyRange));
            this.produceAsyncCompleteCallback = produceAsyncCompleteCallback ?? throw new ArgumentNullException(nameof(produceAsyncCompleteCallback));
            this.equalityComparer = equalityComparer ?? throw new ArgumentNullException(nameof(equalityComparer));
            this.pageSize = initialPageSize;
            this.CurrentContinuationToken = initialContinuationToken;
            this.BackendContinuationToken = initialContinuationToken;
            this.PreviousContinuationToken = initialContinuationToken;
            if (!string.IsNullOrEmpty(initialContinuationToken))
            {
                this.hasStartedFetching = true;
                this.IsActive = true;
            }

            this.fetchSchedulingMetrics = new SchedulingStopwatch();
            this.fetchSchedulingMetrics.Ready();

            this.testFlags = testFlags;

            this.HasMoreResults = true;
        }

        public delegate void ProduceAsyncCompleteDelegate(
            int numberOfDocuments,
            double requestCharge,
            IReadOnlyCollection<QueryPageDiagnostics> diagnostics,
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
        /// The current continuation token that the user has read from the document producer tree.
        /// This is used for determining whether there are more results.
        /// </summary>
        public string CurrentContinuationToken { get; private set; }

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
        public CosmosElement Current => this.CurrentPage?.Current;

        /// <summary>
        /// A static object representing that the move next operation succeeded, and was able to load the next page
        /// </summary>
        internal static readonly (bool successfullyMovedNext, QueryResponseCore? failureResponse) IsSuccessResponse = (true, null);

        /// <summary>
        /// A static object representing that there is no more pages to load. 
        /// </summary>
        internal static readonly (bool successfullyMovedNext, QueryResponseCore? failureResponse) IsDoneResponse = (false, null);

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
                if (!this.HasMoreBackendResults || this.hitException)
                {
                    // Just NOP
                    return;
                }

                int pageSize = (int)Math.Min(this.pageSize, int.MaxValue);

                QueryResponseCore feedResponse = await this.queryContext.ExecuteQueryAsync(
                    querySpecForInit: this.querySpecForInit,
                    continuationToken: this.BackendContinuationToken,
                    partitionKeyRange: new PartitionKeyRangeIdentity(
                            this.queryContext.ContainerResourceId,
                            this.PartitionKeyRange.Id),
                    isContinuationExpected: this.queryContext.IsContinuationExpected,
                    pageSize: pageSize,
                    schedulingStopwatch: this.fetchSchedulingMetrics,
                    cancellationToken: token);

                if ((this.testFlags != null) && this.testFlags.SimulateThrottles)
                {
                    Random random = new Random();
                    if (random.Next() % 2 == 0)
                    {
                        feedResponse = QueryResponseCore.CreateFailure(
                            statusCode: (System.Net.HttpStatusCode)429,
                            subStatusCodes: null,
                            cosmosException: new ThrottledException("Request Rate Too Large"),
                            requestCharge: 0,
                            activityId: QueryResponseCore.EmptyGuidString,
                            diagnostics: QueryResponseCore.EmptyDiagnostics);
                    }
                }

                // Can not simulate an empty page on the first page, since we would return a null continuation token, which will end the query early.
                if ((this.testFlags != null) && this.testFlags.SimulateEmptyPages && (this.BackendContinuationToken != null))
                {
                    Random random = new Random();
                    if (random.Next() % 2 == 0)
                    {
                        feedResponse = QueryResponseCore.CreateSuccess(
                            result: new List<CosmosElement>(),
                            requestCharge: 0,
                            activityId: QueryResponseCore.EmptyGuidString,
                            responseLengthBytes: 0,
                            disallowContinuationTokenMessage: null,
                            continuationToken: this.BackendContinuationToken,
                            diagnostics: QueryResponseCore.EmptyDiagnostics);
                    }
                }

                this.hasStartedFetching = true;
                this.ActivityId = Guid.Parse(feedResponse.ActivityId);
                await this.bufferedPages.AddAsync(feedResponse);
                if (!feedResponse.IsSuccess)
                {
                    // set this flag so that people stop trying to buffer more on this producer.
                    this.hitException = true;
                    return;
                }

                // The backend continuation token is used for the children on splits 
                // and should not be updated on exceptions
                this.BackendContinuationToken = feedResponse.ContinuationToken;

                Interlocked.Add(ref this.bufferedItemCount, feedResponse.CosmosElements.Count);

                this.produceAsyncCompleteCallback(
                    feedResponse.CosmosElements.Count,
                    feedResponse.RequestCharge,
                    feedResponse.Diagnostics,
                    feedResponse.ResponseLengthBytes,
                    token);

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

        public async Task<(bool movedToNextPage, QueryResponseCore? failureReponse)> TryMoveNextPageAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this.itemsLeftInCurrentPage != 0)
            {
                throw new InvalidOperationException("Tried to move onto the next page before finishing the first page.");
            }

            // We need to buffer pages if empty, since thats how we know there are no more pages left.
            await this.BufferMoreIfEmptyAsync(cancellationToken);
            if (this.bufferedPages.Count == 0)
            {
                this.HasMoreResults = false;
                return ItemProducer.IsDoneResponse;
            }

            // Pull a FeedResponse using TryCatch (we could have buffered an exception).
            QueryResponseCore queryResponse = await this.bufferedPages.TakeAsync(cancellationToken);
            if (!queryResponse.IsSuccess)
            {
                this.HasMoreResults = false;
                return (false, queryResponse);
            }

            // Update the state.
            this.PreviousContinuationToken = this.CurrentContinuationToken;
            this.CurrentContinuationToken = queryResponse.ContinuationToken;
            this.CurrentPage = queryResponse.CosmosElements.GetEnumerator();
            this.itemsLeftInCurrentPage = queryResponse.CosmosElements.Count;
            this.enumeratorPrimed = false;
            this.IsAtBeginningOfPage = false;

            return ItemProducer.IsSuccessResponse;
        }

        public bool TryMoveNextDocumentWithinPage()
        {
            if (this.CurrentPage == null)
            {
                return false;
            }

            CosmosElement originalCurrent = this.Current;
            bool movedNext = this.CurrentPage.MoveNext();

            if (!movedNext || ((originalCurrent != null) && !this.equalityComparer.Equals(originalCurrent, this.Current)))
            {
                this.IsActive = false;
            }

            if (!this.enumeratorPrimed)
            {
                this.IsAtBeginningOfPage = true;
                this.enumeratorPrimed = true;
            }
            else
            {
                Interlocked.Decrement(ref this.bufferedItemCount);
                Interlocked.Decrement(ref this.itemsLeftInCurrentPage);

                this.IsAtBeginningOfPage = false;
            }

            if (!movedNext && (this.CurrentContinuationToken == null))
            {
                this.HasMoreResults = false;
            }

            return movedNext;
        }
    }
}