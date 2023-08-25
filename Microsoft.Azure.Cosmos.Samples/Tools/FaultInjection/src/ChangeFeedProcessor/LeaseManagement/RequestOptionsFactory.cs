//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Defines request options for lease requests to use with <see cref="DocumentServiceLeaseStoreManagerCosmos"/> and <see cref="DocumentServiceLeaseStoreCosmos"/>.
    /// </summary>
    internal abstract class RequestOptionsFactory
    {
        public abstract PartitionKey GetPartitionKey(string itemId, string partitionKey);

        public abstract void AddPartitionKeyIfNeeded(Action<string> partitionKeySetter, string partitionKey);
    }
}
