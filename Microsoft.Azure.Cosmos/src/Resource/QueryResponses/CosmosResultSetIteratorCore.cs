//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cosmos Result set iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    internal class CosmosResultSetIteratorCore : CosmosResultSetIterator
    {
        internal delegate Task<CosmosQueryResponse> NextResultSetDelegate(
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken);

        internal readonly NextResultSetDelegate nextResultSetDelegate;

        internal CosmosResultSetIteratorCore(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            NextResultSetDelegate nextDelegate,
            object state = null)
        {
            if (nextDelegate == null)
            {
                throw new ArgumentNullException(nameof(nextDelegate));
            }

            this.nextResultSetDelegate = nextDelegate;
            this.HasMoreResults = true;
            this.state = state;
            this.MaxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
            this.queryOptions = options;
        }

        /// <summary>
        /// The Continuation Token
        /// </summary>
        protected string continuationToken;

        /// <summary>
        /// The max item count to return as part of the query
        /// </summary>
        protected int? MaxItemCount;

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly CosmosRequestOptions queryOptions;

        /// <summary>
        /// The state of the result set.
        /// </summary>
        protected readonly object state;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override Task<CosmosQueryResponse> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            return this.nextResultSetDelegate(this.continuationToken, this.queryOptions, this.state, cancellationToken)
                .ContinueWith(task =>
                {
                    CosmosQueryResponse response = task.Result;
                    this.continuationToken = response.ContinuationToken;
                    this.HasMoreResults = response.GetHasMoreResults();
                    return response;
                }, cancellationToken);
        }
    }

    /// <summary>
    /// Cosmos Result set iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    /// <typeparam name="T">The response object type that can be deserialized</typeparam>
    internal class CosmosDefaultResultSetIterator<T> : CosmosResultSetIterator<T>
    {
        internal delegate Task<CosmosQueryResponse<T>> NextResultSetDelegate(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken);

        internal readonly NextResultSetDelegate nextResultSetDelegate;

        internal CosmosDefaultResultSetIterator(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            NextResultSetDelegate nextDelegate,
            object state = null)
        {
            if (nextDelegate == null)
            {
                throw new ArgumentNullException(nameof(nextDelegate));
            }

            this.nextResultSetDelegate = nextDelegate;
            this.HasMoreResults = true;
            this.state = state;
            this.MaxItemCount = maxItemCount;
            this.continuationToken = continuationToken;
            this.queryOptions = options;
        }

        /// <summary>
        /// The Continuation Token
        /// </summary>
        protected string continuationToken;

        /// <summary>
        /// The query options for the result set
        /// </summary>
        protected readonly CosmosRequestOptions queryOptions;

        /// <summary>
        /// The state of the result set.
        /// </summary>
        protected readonly object state;

        /// <summary>
        /// The max item count to return as part of the query
        /// </summary>
        protected int? MaxItemCount;

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public override Task<CosmosQueryResponse<T>> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            return this.nextResultSetDelegate(this.MaxItemCount, this.continuationToken, this.queryOptions, this.state, cancellationToken)
                .ContinueWith(task =>
                {
                    CosmosQueryResponse<T> response = task.Result;
                    this.HasMoreResults = response.GetHasMoreResults();
                    this.continuationToken = response.InternalContinuationToken;
                    
                    return response;
                }, cancellationToken);
        }

        internal static CosmosQueryResponse<T> CreateCosmosQueryResponse(
                CosmosResponseMessage cosmosResponseMessage,
                CosmosJsonSerializer jsonSerializer)
        {
            using (cosmosResponseMessage)
            {
                // Throw the exception if the query failed.
                cosmosResponseMessage.EnsureSuccessStatusCode();

                string continuationToken = CosmosFeedResultSetIteratorCore.GetContinuationToken(cosmosResponseMessage);
                bool hasMoreResults = CosmosFeedResultSetIteratorCore.GetHasMoreResults(continuationToken, cosmosResponseMessage.StatusCode);

                return CosmosQueryResponse<T>.CreateResponse<T>(
                    stream: cosmosResponseMessage.Content,
                    jsonSerializer: jsonSerializer,
                    continuationToken: continuationToken,
                    hasMoreResults: hasMoreResults);
            }
        }
    }
}