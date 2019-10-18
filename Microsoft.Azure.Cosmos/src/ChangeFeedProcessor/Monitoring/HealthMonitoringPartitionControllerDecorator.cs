//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if AZURECORE
namespace Azure.Cosmos.ChangeFeed
#else
namespace Microsoft.Azure.Cosmos.ChangeFeed
#endif
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;

    internal class HealthMonitoringPartitionControllerDecorator : PartitionController
    {
        private readonly PartitionController inner;
        private readonly HealthMonitor monitor;

        public HealthMonitoringPartitionControllerDecorator(PartitionController inner, HealthMonitor monitor)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        }

        public override async Task AddOrUpdateLeaseAsync(DocumentServiceLease lease)
        {
            try
            {
                await this.inner.AddOrUpdateLeaseAsync(lease);
                await this.monitor.InspectAsync(new HealthMonitoringRecord(HealthSeverity.Informational, MonitoredOperation.AcquireLease, lease, null));
            }
            catch (DocumentClientException)
            {
                throw;
            }
            catch (Exception exception)
            {
                await this.monitor.InspectAsync(new HealthMonitoringRecord(HealthSeverity.Error, MonitoredOperation.AcquireLease, lease, exception));

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