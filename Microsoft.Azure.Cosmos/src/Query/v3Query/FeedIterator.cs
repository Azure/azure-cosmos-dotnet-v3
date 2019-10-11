//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cosmos Result set iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    public abstract class FeedIterator
    {
        /// <summary>
        /// Tells if there is more results that need to be retrieved from the service
        /// </summary>
        public abstract bool HasMoreResults { get; }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public abstract Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Tries to get the continuation token for the feed iterator.
        /// Useful to avoid exceptions.
        /// Useful to avoid the cost serialization until needed.
        /// </summary>
        /// <param name="continuationToken">The continuation to resume from.</param>
        /// <returns>Whether or not we can get the continuaiton token.</returns>
        internal abstract bool TryGetContinuationToken(out string continuationToken);
    }
}