//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement;

    /// <summary>
    /// The context passed to <see cref="ChangeFeedObserver{T}"/> events.
    /// </summary>
    internal sealed class ChangeFeedObserverContextCore<T> : ChangeFeedObserverContext
    {
        private readonly PartitionCheckpointer checkpointer;

        internal ChangeFeedObserverContextCore(string leaseToken)
        {
            this.LeaseToken = leaseToken;
        }

        internal ChangeFeedObserverContextCore(string leaseToken, IFeedResponse<T> feedResponse, PartitionCheckpointer checkpointer)
        {
            this.LeaseToken = leaseToken;
            this.FeedResponse = feedResponse;
            this.checkpointer = checkpointer;
        }

        public override string LeaseToken { get; }

        public IFeedResponse<T> FeedResponse { get; }

        /// <summary>
        /// Checkpoints progress of a stream. This method is valid only if manual checkpoint was configured.
        /// Client may accept multiple change feed batches to process in parallel.
        /// Once first N document processing was finished the client can call checkpoint on the last completed batches in the row.
        /// In case of automatic checkpointing this is method throws.
        /// </summary>
        /// <exception cref="Exceptions.LeaseLostException">Thrown if other host acquired the lease or the lease was deleted</exception>
        public override Task CheckpointAsync()
        {
            return this.checkpointer.CheckpointPartitionAsync(this.FeedResponse.ResponseContinuation);
        }
    }
}