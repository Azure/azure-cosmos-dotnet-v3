//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Documents;

    internal class HealthMonitoringPartitionControllerDecorator : PartitionController
    {
        private readonly PartitionController inner;
        private readonly ChangeFeedProcessorHealthMonitor monitor;

        public HealthMonitoringPartitionControllerDecorator(PartitionController inner, ChangeFeedProcessorHealthMonitor monitor)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        }

        public override async Task AddOrUpdateLeaseAsync(DocumentServiceLease lease)
        {
            try
            {
                await this.inner.AddOrUpdateLeaseAsync(lease);
                await this.monitor.NotifyInformationAsync(ChangeFeedProcessorEvent.AcquireLease, lease.CurrentLeaseToken);
            }
            catch (DocumentClientException)
            {
                throw;
            }
            catch (Exception exception)
            {
                await this.monitor.NotifyErrorAsync(ChangeFeedProcessorEvent.AcquireLease, lease.CurrentLeaseToken, exception);
                throw;
            }
        }

        public override Task InitializeAsync()
        {
            return this.inner.InitializeAsync();
        }

        public override Task ShutdownAsync()
        {
            return this.inner.ShutdownAsync();
        }
    }
}