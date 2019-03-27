//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the context passed to <see cref="ChangeFeedObserver{T}"/> events.
    /// </summary>
    public abstract class ChangeFeedObserverContext
    {
        /// <summary>
        /// Gets the Lease Token associated with the current Observer
        /// </summary>
        public abstract string LeaseToken { get; }

        //public abstract IFeedResponse<Document> FeedResponse { get; }

        /// <summary>
        /// Checkpoints progress of a stream. This method is valid only if manual checkpoint was configured.
        /// Client may accept multiple change feed batches to process in parallel.
        /// Once first N document processing was finished the client can call checkpoint on the last completed batches in the row.
        /// In case of automatic checkpointing this is method throws.
        /// </summary>
        /// <exception cref="Exceptions.LeaseLostException">Thrown if other host acquired the lease or the lease was deleted</exception>
        public abstract Task CheckpointAsync();
    }
}