//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement.Streams
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using ChangeFeedObserverFactory= Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing.Streams.ChangeFeedObserverFactory;
    using FeedProcessorFactory = Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing.Streams.FeedProcessorFactory;

    internal sealed class PartitionSupervisorStreamFactory : PartitionSupervisorFactory
    {
        private readonly ChangeFeedObserverFactory observerFactory;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private readonly FeedProcessorFactory partitionProcessorFactory;

        public PartitionSupervisorStreamFactory(
            ChangeFeedObserverFactory observerFactory,
            DocumentServiceLeaseManager leaseManager,
            FeedProcessorFactory partitionProcessorFactory,
            ChangeFeedLeaseOptions options)
        {
            if (observerFactory == null) throw new ArgumentNullException(nameof(observerFactory));
            if (leaseManager == null) throw new ArgumentNullException(nameof(leaseManager));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (partitionProcessorFactory == null) throw new ArgumentNullException(nameof(partitionProcessorFactory));

            this.observerFactory = observerFactory;
            this.leaseManager = leaseManager;
            this.changeFeedLeaseOptions = options;
            this.partitionProcessorFactory = partitionProcessorFactory;
        }

        public override PartitionSupervisor Create(DocumentServiceLease lease)
        {
            if (lease == null)
                throw new ArgumentNullException(nameof(lease));

            var changeFeedObserver = this.observerFactory.CreateObserver();
            var processor = this.partitionProcessorFactory.Create(lease, changeFeedObserver);
            var renewer = new LeaseRenewerCore(lease, this.leaseManager, this.changeFeedLeaseOptions.LeaseRenewInterval);

            return new PartitionSupervisorStreamCore(lease, changeFeedObserver, processor, renewer);
        }

    }

}