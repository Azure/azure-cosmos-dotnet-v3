//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Used to create request options for partitioned lease collections, when partition key is defined as /id.
    /// </summary>
    internal sealed class PartitionedByIdCollectionRequestOptionsFactory : RequestOptionsFactory
    {
        public override string GetPartitionKey(string itemId) => itemId;

        public override FeedOptions CreateFeedOptions() => new FeedOptions { EnableCrossPartitionQuery = true };
    }
}
