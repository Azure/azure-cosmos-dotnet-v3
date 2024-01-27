//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal class LinqQueryFeedIterator<T> : FeedIterator<T>
    {
        private readonly FeedIterator<T> source;
        private readonly ClientOperation clientOperation;

        public LinqQueryFeedIterator(FeedIterator<T> source, ClientOperation clientOperation)
        {
            this.source = source;
            this.clientOperation = clientOperation;
        }

        public override bool HasMoreResults => this.source.HasMoreResults;

        public override Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
