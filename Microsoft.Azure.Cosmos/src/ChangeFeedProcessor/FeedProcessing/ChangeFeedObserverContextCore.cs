//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;

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

        internal ChangeFeedObserverContextCore(string leaseToken, ResponseMessage feedResponse, PartitionCheckpointer checkpointer)
        {
            this.LeaseToken = leaseToken;
            this.DocumentFeedResponse = feedResponse;
            this.checkpointer = checkpointer;
        }

        public override string LeaseToken { get; }

        public ResponseMessage DocumentFeedResponse { get; }

        /// <summary>
        /// Checkpoints progress of a stream. This method is valid only if manual checkpoint was configured.
        /// Client may accept multiple change feed batches to process in parallel.
        /// Once first N document processing was finished the client can call checkpoint on the last completed batches in the row.
        /// In case of automatic checkpointing this is method throws.
        /// </summary>
        /// <exception cref="Exceptions.LeaseLostException">Thrown if other host acquired the lease or the lease was deleted</exception>
        public override Task CheckpointAsync()
        {
            return this.checkpointer.CheckpointPartitionAsync(this.DocumentFeedResponse.Headers.ContinuationToken);
        }
    }
}