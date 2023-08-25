//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;

    /// <summary>
    /// Factory class used to create instance(s) of <see cref="FeedProcessor"/>.
    /// </summary>
    internal abstract class FeedProcessorFactory
    {
        /// <summary>
        /// Creates an instance of a <see cref="FeedProcessor"/>.
        /// </summary>
        /// <param name="lease">Lease to be used for feed processing</param>
        /// <param name="observer">Observer to be used</param>
        /// <returns>An instance of a <see cref="FeedProcessor"/>.</returns>
        public abstract FeedProcessor Create(DocumentServiceLease lease, ChangeFeedObserver observer);
    }
}
