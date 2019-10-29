//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PageIteratorCore<T>
    {
        private readonly FeedIterator feedIterator;
        private readonly Func<Response, IReadOnlyList<T>> responseCreator;

        internal PageIteratorCore(
            FeedIterator feedIterator,
            Func<Response, IReadOnlyList<T>> responseCreator)
        {
            this.responseCreator = responseCreator;
            this.feedIterator = feedIterator;
        }

        public async Task<(Page<T>, bool)> GetPageAsync(
            string continuation = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Response response = await this.feedIterator.ReadNextAsync(cancellationToken);
            
            return (new CosmosPage<T>(this.responseCreator(response), response), this.feedIterator.HasMoreResults);
        }
    }
}