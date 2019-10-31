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
    /// Defines request options for lease requests to use with <see cref="DocumentServiceLeaseStoreManagerCosmos"/> and <see cref="DocumentServiceLeaseStoreCosmos"/>.
    /// </summary>
    internal abstract class RequestOptionsFactory
    {
        public abstract PartitionKey GetPartitionKey(string itemId);

        public abstract FeedOptions CreateFeedOptions();
    }
}
