//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;

    internal class FeedProcessorFactoryCore<T> : FeedProcessorFactory<T>
    {
        private readonly ContainerInternal container;
        private readonly ChangeFeedProcessorOptions changeFeedProcessorOptions;
        private readonly DocumentServiceLeaseCheckpointer leaseCheckpointer;
        private readonly CosmosSerializerCore serializerCore;

        public FeedProcessorFactoryCore(
            ContainerInternal container,
            ChangeFeedProcessorOptions changeFeedProcessorOptions,
            DocumentServiceLeaseCheckpointer leaseCheckpointer,
            CosmosSerializerCore serializerCore)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.changeFeedProcessorOptions = changeFeedProcessorOptions ?? throw new ArgumentNullException(nameof(changeFeedProcessorOptions));
            this.leaseCheckpointer = leaseCheckpointer ?? throw new ArgumentNullException(nameof(leaseCheckpointer));
            this.serializerCore = serializerCore ?? throw new ArgumentNullException(nameof(serializerCore));
        }

        public override FeedProcessor Create(DocumentServiceLease lease, ChangeFeedObserver<T> observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (lease == null) throw new ArgumentNullException(nameof(lease));

            ProcessorOptions options = new ProcessorOptions
            {
                StartContinuation = !string.IsNullOrEmpty(lease.ContinuationToken) ?
                    lease.ContinuationToken :
                    this.changeFeedProcessorOptions.StartContinuation,
                LeaseToken = lease.CurrentLeaseToken,
                FeedPollDelay = this.changeFeedProcessorOptions.FeedPollDelay,
                MaxItemCount = this.changeFeedProcessorOptions.MaxItemCount,
                StartFromBeginning = this.changeFeedProcessorOptions.StartFromBeginning,
                StartTime = this.changeFeedProcessorOptions.StartTime,
                SessionToken = this.changeFeedProcessorOptions.SessionToken,
            };

            string partitionKeyRangeId = lease.CurrentLeaseToken;

            PartitionCheckpointerCore checkpointer = new PartitionCheckpointerCore(this.leaseCheckpointer, lease);
            ChangeFeedPartitionKeyResultSetIteratorCore iterator = ResultSetIteratorUtils.BuildResultSetIterator(
                partitionKeyRangeId: partitionKeyRangeId,
                continuationToken: options.StartContinuation,
                maxItemCount: options.MaxItemCount,
                container: this.container,
                startTime: options.StartTime,
                startFromBeginning: options.StartFromBeginning);

            return new FeedProcessorCore<T>(observer, iterator, options, checkpointer, this.serializerCore);
        }
    }
}
