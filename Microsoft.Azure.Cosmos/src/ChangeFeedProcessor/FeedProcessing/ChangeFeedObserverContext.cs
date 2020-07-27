//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents the context passed to <see cref="ChangeFeedObserver{T}"/> events.
    /// </summary>
    internal abstract class ChangeFeedObserverContext
    {
        /// <summary>
        /// Gets the <see cref="FeedRange"/> associated with the items being processed.
        /// </summary>
        public abstract FeedRange FeedRange { get; }

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