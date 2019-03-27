//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.FeedProcessing;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;

    internal sealed class PartitionSupervisorFactoryCore<T> : PartitionSupervisorFactory
    {
        private readonly ChangeFeedObserverFactory<T> observerFactory;
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly ChangeFeedLeaseOptions changeFeedLeaseOptions;
        private readonly PartitionProcessorFactory<T> partitionProcessorFactory;

        public PartitionSupervisorFactoryCore(
            ChangeFeedObserverFactory<T> observerFactory,
            DocumentServiceLeaseManager leaseManager,
            PartitionProcessorFactory<T> partitionProcessorFactory,
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

            ChangeFeedObserver<T> changeFeedObserver = this.observerFactory.CreateObserver();
            var processor = this.partitionProcessorFactory.Create(lease, changeFeedObserver);
            var renewer = new LeaseRenewerCore(lease, this.leaseManager, this.changeFeedLeaseOptions.LeaseRenewInterval);

            return new PartitionSupervisorCore<T>(lease, changeFeedObserver, processor, renewer);
        }
    }
}