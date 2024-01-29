//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;

    internal sealed class PartitionSupervisorFactoryCore : PartitionSupervisorFactory
    {
        private readonly ChangeFeedObserverFactory observerFactory;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private readonly FeedProcessorFactory partitionProcessorFactory;

        public PartitionSupervisorFactoryCore(
            ChangeFeedObserverFactory observerFactory,
            DocumentServiceLeaseManager leaseManager,
            FeedProcessorFactory partitionProcessorFactory,
            ChangeFeedLeaseOptions options)
        {
            this.observerFactory = observerFactory ?? throw new ArgumentNullException(nameof(observerFactory));
            this.leaseManager = leaseManager ?? throw new ArgumentNullException(nameof(leaseManager));
            this.changeFeedLeaseOptions = options ?? throw new ArgumentNullException(nameof(options));
            this.partitionProcessorFactory = partitionProcessorFactory ?? throw new ArgumentNullException(nameof(partitionProcessorFactory));
        }

        public override PartitionSupervisor Create(DocumentServiceLease lease)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            ChangeFeedObserver changeFeedObserver = this.observerFactory.CreateObserver();
            FeedProcessor processor = this.partitionProcessorFactory.Create(lease, changeFeedObserver);
            LeaseRenewerCore renewer = new LeaseRenewerCore(lease, this.leaseManager, this.changeFeedLeaseOptions.LeaseRenewInterval);

            return new PartitionSupervisorCore(lease, changeFeedObserver, processor, renewer);
        }
    }
}