//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    /// <summary>
    /// Factory class used to create instance(s) of <see cref="FeedProcessor"/>.
    /// </summary>
    internal abstract class FeedProcessorFactory<T>
    {
        /// <summary>
        /// Creates an instance of a <see cref="FeedProcessor"/>.
        /// </summary>
        /// <param name="lease">Lease to be used for feed processing</param>
        /// <param name="observer">Observer to be used</param>
        /// <returns>An instance of a <see cref="FeedProcessor"/>.</returns>
        public abstract FeedProcessor Create(DocumentServiceLease lease, ChangeFeedObserver<T> observer);
    }
}
