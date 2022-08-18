//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Newtonsoft.Json.Linq;

    internal sealed class DataEncryptionKeyFeedIterator : FeedIterator
    {
        private readonly FeedIterator feedIterator;

        public DataEncryptionKeyFeedIterator(
            FeedIterator feedIterator)
        {
            this.feedIterator = feedIterator;
        }

        public override bool HasMoreResults => this.feedIterator.HasMoreResults;

        public override async Task<ResponseMessage> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            return await this.feedIterator.ReadNextAsync(cancellationToken);
        }
    }
}
