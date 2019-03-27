// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//  ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing
{
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;

    /// <summary>
    /// Factory class used to create instance(s) of <see cref="PartitionProcessor"/>.
    /// </summary>
    public abstract class PartitionProcessorFactory<T>
    {
        /// <summary>
        /// Creates an instance of a <see cref="PartitionProcessor"/>.
        /// </summary>
        /// <param name="lease">Lease to be used for partition processing</param>
        /// <param name="observer">Observer to be used</param>
        /// <returns>An instance of a <see cref="PartitionProcessor"/>.</returns>
        public abstract PartitionProcessor Create(DocumentServiceLease lease, ChangeFeedObserver<T> observer);
    }
}
