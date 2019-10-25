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

        public async Task<Page<T>> GetPageAsync(string continuation = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Response response = await this.feedIterator.ReadNextAsync(cancellationToken);

            // TODO: Once Page<T> is abstract, we need to override so the ContinuationToken is Lazy to avoid requesting it when its not needed or for DISTINCT queries
            return new Page<T>(this.responseCreator(response), response.Headers.GetContinuationToken(), response);
        }
    }
}