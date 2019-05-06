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
    public abstract class CosmosFeedIterator
    {
        /// <summary>
        /// Tells if there is more results that need to be retrieved from the service
        /// </summary>
        public virtual bool HasMoreResults { get; protected set; }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public abstract Task<CosmosResponseMessage> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken));
    }

    /// <summary>
    /// Cosmos Result set iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    public abstract class CosmosFeedIterator<T>
    {
        /// <summary>
        /// Tells if there is more results that need to be retrieved from the service
        /// </summary>
        public virtual bool HasMoreResults { get; protected set; }

        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public abstract Task<CosmosFeedResponse<T>> FetchNextSetAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}