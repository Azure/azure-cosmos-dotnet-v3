//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;
    using Azure;

    /// <summary>
    /// Cosmos Result set iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    internal abstract class FeedIterator
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
        public abstract Task<Response> ReadNextAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}