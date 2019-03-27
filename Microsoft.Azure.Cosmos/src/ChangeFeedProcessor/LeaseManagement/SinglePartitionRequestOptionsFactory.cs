//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement
{
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Used to create request options for non-partitioned lease collections.
    /// </summary>
    internal sealed class SinglePartitionRequestOptionsFactory : RequestOptionsFactory
    {
        public override FeedOptions CreateFeedOptions() => null;

        public override string GetPartitionKey(string itemId) => null;
    }
}
