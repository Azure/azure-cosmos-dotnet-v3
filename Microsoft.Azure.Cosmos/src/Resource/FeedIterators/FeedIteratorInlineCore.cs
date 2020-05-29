//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal sealed class FeedIteratorInlineCore : FeedIteratorInternal
    {
        private readonly FeedIteratorBase feedIteratorInternal;
        private readonly CosmosClientContext clientContext;

        internal FeedIteratorInlineCore(
            CosmosClientContext clientContext,
            FeedIteratorBase feedIterator)
        {
            this.feedIteratorInternal = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));
            this.clientContext = clientContext ?? throw new ArgumentNullException(nameof(clientContext));
        }

        public override bool HasMoreResults => this.feedIteratorInternal.HasMoreResults;

        public override CosmosElement GetCosmosElementContinuationToken()
        {
            return this.feedIteratorInternal.GetCosmosElementContinuationToken();
        }

        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return this.clientContext.OperationHelperAsync(
                nameof(FeedIteratorInlineCore),
                this.feedIteratorInternal.RequestOptions,
                (diagnostics) =>
                {
                    return this.feedIteratorInternal.ReadNextAsync(diagnostics, cancellationToken);
                });
        }
    }
}