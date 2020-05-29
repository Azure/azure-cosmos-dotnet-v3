//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Cosmos feed iterator that keeps track of the continuation token when retrieving results form a query.
    /// </summary>
    /// <typeparam name="T">The response object type that can be deserialized</typeparam>
    internal sealed class FeedIteratorCore<T> : FeedIteratorBase<T>
    {
        private readonly FeedIteratorBase feedIterator;
        private readonly Func<ResponseMessage, FeedResponse<T>> responseCreator;

        internal FeedIteratorCore(
            FeedIteratorBase feedIterator,
            Func<ResponseMessage, FeedResponse<T>> responseCreator)
        {
            this.responseCreator = responseCreator;
            this.feedIterator = feedIterator;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        internal override RequestOptions RequestOptions => this.feedIterator.RequestOptions;

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            return this.feedIterator.GetCosmosElementContinuationToken();
        }

        internal override async Task<FeedResponse<T>> ReadNextAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ResponseMessage response = await this.feedIterator.ReadNextAsync(
                diagnosticsContext,
                cancellationToken);

            return this.responseCreator(response);
        }
    }
}
