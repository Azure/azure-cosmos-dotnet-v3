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

    internal class FeedProcessorFactoryCore : FeedProcessorFactory
    {
        private readonly ContainerInternal container;
        private readonly ChangeFeedProcessorOptions changeFeedProcessorOptions;
        private readonly DocumentServiceLeaseCheckpointer leaseCheckpointer;

        public FeedProcessorFactoryCore(
            ContainerInternal container,
            ChangeFeedProcessorOptions changeFeedProcessorOptions,
            DocumentServiceLeaseCheckpointer leaseCheckpointer)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.changeFeedProcessorOptions = changeFeedProcessorOptions ?? throw new ArgumentNullException(nameof(changeFeedProcessorOptions));
            this.leaseCheckpointer = leaseCheckpointer ?? throw new ArgumentNullException(nameof(leaseCheckpointer));
        }

        public override FeedProcessor Create(DocumentServiceLease lease, ChangeFeedObserver observer)
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
                StartTime = this.changeFeedProcessorOptions.StartTime
            };

            PartitionCheckpointerCore checkpointer = new PartitionCheckpointerCore(this.leaseCheckpointer, lease);
            ChangeFeedPartitionKeyResultSetIteratorCore iterator = ChangeFeedPartitionKeyResultSetIteratorCore.Create(
                lease: lease,
                continuationToken: options.StartContinuation,
                maxItemCount: options.MaxItemCount,
                container: this.container,
                startTime: options.StartTime,
                startFromBeginning: options.StartFromBeginning);

            return new FeedProcessorCore(observer, iterator, options, checkpointer);
        }
    }
}
