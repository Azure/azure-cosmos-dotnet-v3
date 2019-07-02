//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Used to create request options for non-partitioned lease collections.
    /// </summary>
    internal sealed class SinglePartitionRequestOptionsFactory : RequestOptionsFactory
    {
        public override FeedOptions CreateFeedOptions() => null;

        public override PartitionKey GetPartitionKey(string itemId) => PartitionKey.None;
    }
}
