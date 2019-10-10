//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cosmos Result set iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    public abstract class PageIterator<T>
    {
        /// <summary>
        /// Get the next set of results from the cosmos service
        /// </summary>
        /// <param name="continuation">(Optional) .</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A query response from cosmos service</returns>
        public abstract Task<Page<T>> GetPageAsync(string continuation = null, CancellationToken cancellationToken = default(CancellationToken));
    }
}