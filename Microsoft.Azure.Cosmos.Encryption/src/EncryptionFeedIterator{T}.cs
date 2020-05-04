//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Threading;
    using System.Threading.Tasks;

#if PREVIEW
    public
#else
    internal
#endif
    class EncryptionFeedIterator<T> : FeedIterator<T>
    {
        private readonly FeedIterator FeedIterator;
        private readonly CosmosResponseFactory ResponseFactory;

        public EncryptionFeedIterator(
            FeedIterator feedIterator,
            CosmosResponseFactory responseFactory)
        {
            this.FeedIterator = feedIterator;
            this.ResponseFactory = responseFactory;
        }

        public override bool HasMoreResults => this.FeedIterator.HasMoreResults;

        public override async Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            ResponseMessage responseMessage = await this.FeedIterator.ReadNextAsync(cancellationToken);
            return this.ResponseFactory.CreateItemFeedResponse<T>(responseMessage);
        }
    }
}
