//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cosmos feed stream iterator. This is used to get the query responses with a Stream content
    /// </summary>
    internal class FeedIteratorCore : FeedIterator
    {
        internal delegate Task<ResponseMessage> NextResultSetDelegate(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken);

        internal readonly NextResultSetDelegate nextResultSetDelegate;
        protected bool hasMoreResultsInternal;

        internal FeedIteratorCore(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            NextResultSetDelegate nextDelegate,
            object state = null)
        {
            this.nextResultSetDelegate = nextDelegate;
            this.hasMoreResultsInternal = true;
            this.state = state;
            this.MaxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
            this.queryOptions = options;
        }

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly RequestOptions queryOptions;

        /// <summary>
        /// The state of the result set.
        /// </summary>
        protected readonly object state;

        /// <summary>
        /// The Continuation Token
        /// </summary>
        protected string continuationToken;

        /// <summary>
        /// The max item count to return as part of the query
        /// </summary>
        protected int? MaxItemCount;

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ResponseMessage response = await this.nextResultSetDelegate(this.MaxItemCount, this.continuationToken, this.queryOptions, this.state, cancellationToken);
            this.continuationToken = response.Headers.ContinuationToken;
            this.hasMoreResultsInternal = GetHasMoreResults(this.continuationToken, response.StatusCode);
            return response;
        }

        internal static string GetContinuationToken(ResponseMessage httpResponseMessage)
        {
            return httpResponseMessage.Headers.ContinuationToken;
        }

        internal static bool GetHasMoreResults(string continuationToken, HttpStatusCode statusCode)
        {
            // this logic might not be sufficient composite continuation token https://msdata.visualstudio.com/CosmosDB/SDK/_workitems/edit/269099
            // in the case where this is a result set iterator for a change feed, not modified indicates that
            // the enumeration is done for now.
            return continuationToken != null && statusCode != HttpStatusCode.NotModified;
        }
    }

    /// <summary>
    /// Cosmos feed iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    /// <typeparam name="T">The response object type that can be deserialized</typeparam>
    internal class FeedIteratorCore<T> : FeedIterator<T>
    {
        internal delegate Task<FeedResponse<T>> NextResultSetDelegate(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken);

        internal readonly NextResultSetDelegate nextResultSetDelegate;
        private bool hasMoreResultsInternal;

        internal FeedIteratorCore(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            NextResultSetDelegate nextDelegate,
            object state = null)
        {
            if (nextDelegate == null)
            {
                throw new ArgumentNullException(nameof(nextDelegate));
            }

            this.nextResultSetDelegate = nextDelegate;
            this.hasMoreResultsInternal = true;
            this.state = state;
            this.MaxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
            this.queryOptions = options;
        }

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly RequestOptions queryOptions;

        /// <summary>
        /// The state of the result set.
        /// </summary>
        protected readonly object state;

        /// <summary>
        /// The Continuation Token
        /// </summary>
        protected string continuationToken;

        /// <summary>
        /// The max item count to return as part of the query
        /// </summary>
        protected int? MaxItemCount;

        public override bool HasMoreResults => this.hasMoreResultsInternal;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            FeedResponse<T> response = await this.nextResultSetDelegate(this.MaxItemCount, this.continuationToken, this.queryOptions, this.state, cancellationToken);
            this.hasMoreResultsInternal = response.HasMoreResults;
            this.continuationToken = response.InternalContinuationToken;
            return response;

        }

        internal static ReadFeedResponse<T> CreateCosmosQueryResponse(
                ResponseMessage cosmosResponseMessage,
                CosmosSerializer jsonSerializer)
        {
            using (cosmosResponseMessage)
            {
                // Throw the exception if the query failed.
                cosmosResponseMessage.EnsureSuccessStatusCode();

                string continuationToken = FeedIteratorCore.GetContinuationToken(cosmosResponseMessage);
                bool hasMoreResults = FeedIteratorCore.GetHasMoreResults(continuationToken, cosmosResponseMessage.StatusCode);

                return ReadFeedResponse<T>.CreateResponse<T>(
                    responseMessageHeaders: cosmosResponseMessage.Headers,
                    stream: cosmosResponseMessage.Content,
                    jsonSerializer: jsonSerializer,
                    hasMoreResults: hasMoreResults);
            }
        }
    }
}