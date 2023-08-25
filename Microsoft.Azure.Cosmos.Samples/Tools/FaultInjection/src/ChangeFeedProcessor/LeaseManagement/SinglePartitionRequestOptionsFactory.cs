//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Used to create request options for non-partitioned lease collections.
    /// </summary>
    internal sealed class SinglePartitionRequestOptionsFactory : RequestOptionsFactory
    {
        public override void AddPartitionKeyIfNeeded(Action<string> partitionKeySetter, string partitionKey)
        {
        }

        public override PartitionKey GetPartitionKey(string itemId, string partitionKey) => PartitionKey.None;
    }
}
