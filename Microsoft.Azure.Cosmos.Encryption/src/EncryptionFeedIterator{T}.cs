//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Query.Core;

    internal sealed class EncryptionFeedIterator<T> : FeedIterator<T>
    {
        private readonly CosmosResponseFactory responseFactory;
        private readonly FeedIterator feedIterator;

        public EncryptionFeedIterator(
            EncryptionFeedIterator feedIterator,
            CosmosResponseFactory responseFactory)
        {
            if (!(feedIterator is EncryptionFeedIterator))
            {
                throw new ArgumentOutOfRangeException($"{nameof(feedIterator)} must be of type {nameof(EncryptionFeedIterator)}.");
            }

            this.feedIterator = feedIterator;
            this.responseFactory = responseFactory;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            using ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);
            return this.responseFactory.CreateItemFeedResponse<T>(responseMessage);
        }
    }
}
