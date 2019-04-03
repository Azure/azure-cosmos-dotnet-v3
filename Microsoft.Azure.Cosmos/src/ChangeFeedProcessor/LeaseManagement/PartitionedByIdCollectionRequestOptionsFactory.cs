//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
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
