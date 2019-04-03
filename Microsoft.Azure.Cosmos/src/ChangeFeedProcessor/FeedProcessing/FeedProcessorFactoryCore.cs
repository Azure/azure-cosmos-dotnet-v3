// ----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//  ----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing
{
    using System;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;

    internal class FeedProcessorFactoryCore<T> : FeedProcessorFactory<T>
    {
        private readonly CosmosContainer container;
        private readonly ChangeFeedProcessorOptions changeFeedProcessorOptions;
        private readonly DocumentServiceLeaseCheckpointer leaseCheckpointer;

        public FeedProcessorFactoryCore(
            CosmosContainer container,
            ChangeFeedProcessorOptions changeFeedProcessorOptions,
            DocumentServiceLeaseCheckpointer leaseCheckpointer)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            if (changeFeedProcessorOptions == null) throw new ArgumentNullException(nameof(changeFeedProcessorOptions));
            if (leaseCheckpointer == null) throw new ArgumentNullException(nameof(leaseCheckpointer));

            this.container = container;
            this.changeFeedProcessorOptions = changeFeedProcessorOptions;
            this.leaseCheckpointer = leaseCheckpointer;
        }

        public override FeedProcessor Create(DocumentServiceLease lease, ChangeFeedObserver<T> observer)
        {
            if (observer == null) throw new ArgumentNullException(nameof(observer));
            if (lease == null) throw new ArgumentNullException(nameof(lease));

            var settings = new ProcessorSettings
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

            var checkpointer = new PartitionCheckpointerCore(this.leaseCheckpointer, lease);
            return new FeedProcessorCore<T>(observer, this.container, settings, checkpointer);
        }
    }
}
