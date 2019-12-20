//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Query;

    internal class PageIteratorCore<T>
    {
        private readonly FeedIterator feedIterator;
        private readonly Func<Response, CancellationToken, Task<IReadOnlyList<T>>> responseCreator;

        internal PageIteratorCore(
            FeedIterator feedIterator,
            Func<Response, CancellationToken,  Task<IReadOnlyList<T>>> responseCreator)
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
            
            return (new CosmosPage<T>(await this.responseCreator(response, cancellationToken), response, this.TryGetContinuationToken), this.feedIterator.HasMoreResults);
        }

        internal bool TryGetContinuationToken(out string state)
        {
            QueryIterator queryIterator = this.feedIterator as QueryIterator;
            if (queryIterator != null)
            {
                return queryIterator.TryGetContinuationToken(out state);
            }

            FeedIteratorCore feedIteratorCore = this.feedIterator as FeedIteratorCore;
            if (feedIteratorCore != null)
            {
                return feedIteratorCore.TryGetContinuationToken(out state);
            }

            throw new ArgumentException($"Unsupported  iterator type of {this.feedIterator.GetType().Name}");
        }
    }
}