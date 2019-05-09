//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class ChangeFeedResultSetStreamIterator : CosmosResultSetIteratorCore
    {
        internal ChangeFeedResultSetStreamIterator(
            int? maxItemCount,
            string continuationToken,
            QueryRequestOptions options,
            NextResultSetDelegate nextDelegate,
            object state = null) : base(
                maxItemCount,
                continuationToken, 
                options, 
                nextDelegate,
                state)
        {
        }

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
                    this.continuationToken = response.Headers.ETag;
                    this.HasMoreResults = ChangeFeedResultSetStreamIterator.GetHasMoreResults(this.continuationToken, response.StatusCode);
                    return response;
                }, cancellationToken);
        }
    }
}