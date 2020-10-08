//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class EncryptionFeedIterator<T> : FeedIterator<T>
    {
        private readonly FeedIterator feedIterator;
        private readonly CosmosResponseFactory responseFactory;

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
            ResponseMessage responseMessage;

            if (typeof(T) == typeof(DecryptableItem))
            {
                IReadOnlyCollection<T> resource;
                EncryptionFeedIterator encryptionFeedIterator = this.feedIterator as EncryptionFeedIterator;
                (responseMessage, resource) = await encryptionFeedIterator.ReadNextWithoutDecryptionAsync<T>(cancellationToken);

                return DecryptableFeedResponse<T>.CreateResponse(
                    responseMessage,
                    resource);
            }
            else
            {
                responseMessage = await this.feedIterator.ReadNextAsync(cancellationToken);
            }

            return this.responseFactory.CreateItemFeedResponse<T>(responseMessage);
        }
    }
}
