//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DataEncryptionKeyFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;

        public DataEncryptionKeyFeedIterator(
            FeedIterator feedIterator)
        {
            this.feedIterator = feedIterator;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return this.feedIterator.ReadNextAsync(cancellationToken);
        }
    }
}
