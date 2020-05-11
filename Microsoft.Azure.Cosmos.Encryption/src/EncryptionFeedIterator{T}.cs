//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
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
            this.feedIterator = feedIterator;
            this.responseFactory = responseFactory;
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
