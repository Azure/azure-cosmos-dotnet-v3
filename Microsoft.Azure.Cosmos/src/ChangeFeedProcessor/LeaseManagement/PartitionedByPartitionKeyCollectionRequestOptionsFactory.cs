//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Used to create request options for partitioned lease collections, when partition key is defined as /partitionKey.
    /// </summary>
    internal sealed class PartitionedByPartitionKeyCollectionRequestOptionsFactory : RequestOptionsFactory
    {
        public override PartitionKey GetPartitionKey(string itemId, string partitionKey) => new PartitionKey(partitionKey);

        public override void AddPartitionKeyIfNeeded(Action<string> partitionKeySetter, string partitionKey)
        {
            partitionKeySetter(partitionKey);
        }
    }
}
