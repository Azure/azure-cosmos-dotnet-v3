//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class EncryptionFeedIterator<T> : FeedIterator<T>
    {
        private readonly EncryptionFeedIterator feedIterator;
        private readonly CosmosResponseFactory responseFactory;

        public EncryptionFeedIterator(
            EncryptionFeedIterator feedIterator,
            CosmosResponseFactory responseFactory)
        {
            this.feedIterator = feedIterator ?? throw new ArgumentNullException(nameof(feedIterator));
            this.responseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage;

            if (typeof(T) == typeof(DecryptableItem))
            {
                IReadOnlyCollection<T> resource;
                (responseMessage, resource) = await this.feedIterator.ReadNextWithoutDecryptionAsync<T>(cancellationToken);

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
