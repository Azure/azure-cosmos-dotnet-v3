//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Used to create request options for partitioned lease collections, when partition key is defined as /id.
    /// </summary>
    internal sealed class PartitionedByIdCollectionRequestOptionsFactory : RequestOptionsFactory
    {
        public override PartitionKey GetPartitionKey(string itemId) => new PartitionKey(itemId);

        public override FeedOptions CreateFeedOptions() => new FeedOptions { EnableCrossPartitionQuery = true };
    }
}
