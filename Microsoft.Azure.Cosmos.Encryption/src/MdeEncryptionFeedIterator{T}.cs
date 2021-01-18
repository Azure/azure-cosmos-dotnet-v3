//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class MdeEncryptionFeedIterator<T> : FeedIterator<T>
    {
        private readonly FeedIterator feedIterator;
        private readonly CosmosResponseFactory responseFactory;

        public MdeEncryptionFeedIterator(
            MdeEncryptionFeedIterator feedIterator,
            CosmosResponseFactory responseFactory)
        {
            this.feedIterator = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));

            if (feedIterator is not MdeEncryptionFeedIterator)
            {
                throw new ArgumentOutOfRangeException($"{nameof(feedIterator)} must be of type {nameof(MdeEncryptionFeedIterator)}. ");
            }

            this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            using (ResponseMessage responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken))
            {
                return this.responseFactory.CreateItemFeedResponse<T>(responseMessage);
            }
        }
    }
}
