//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cosmos result set stream iterator. This is used to get the query responses with a Stream content
    /// </summary>
    internal class CosmosDefaultResultSetStreamIterator : CosmosResultSetIterator
    {
        internal delegate Task<CosmosResponseMessage> NextResultSetDelegate(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken);

        internal readonly NextResultSetDelegate nextResultSetDelegate;

        internal CosmosDefaultResultSetStreamIterator(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            NextResultSetDelegate nextDelegate,
            object state = null)
        {
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
        public override Task<CosmosResponseMessage> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.nextResultSetDelegate(this.MaxItemCount, this.continuationToken, this.queryOptions, this.state, cancellationToken)
                .ContinueWith(task =>
                {
                    CosmosResponseMessage response = task.Result;
                    this.continuationToken = response.Headers.Continuation;
                    this.HasMoreResults = GetHasMoreResults(this.continuationToken, response.StatusCode);
                    return response;
                }, cancellationToken);
        }

        internal static string GetContinuationToken(CosmosResponseMessage httpResponseMessage)
        {
            return httpResponseMessage.Headers.Continuation;
        }

        internal static bool GetHasMoreResults(string continuationToken, HttpStatusCode statusCode)
        {
            // this logic might not be sufficient composite continuation token https://msdata.visualstudio.com/CosmosDB/SDK/_workitems/edit/269099
            // in the case where this is a result set iterator for a change feed, not modified indicates that
            // the enumeration is done for now.
            return continuationToken != null && statusCode != HttpStatusCode.NotModified;
        }
    }
}