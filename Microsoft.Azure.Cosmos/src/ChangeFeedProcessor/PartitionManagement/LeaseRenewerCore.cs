//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.Logging;

    internal sealed class LeaseRenewerCore : LeaseRenewer
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly DocumentServiceLeaseManager leaseManager;
        private readonly TimeSpan leaseRenewInterval;
        private DocumentServiceLease lease;

        public LeaseRenewerCore(DocumentServiceLease lease, DocumentServiceLeaseManager leaseManager, TimeSpan leaseRenewInterval)
        {
            this.lease = lease;
            this.leaseManager = leaseManager;
            this.leaseRenewInterval = leaseRenewInterval;
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                Logger.InfoFormat("Lease with token {0}: renewer task started.", this.lease.CurrentLeaseToken);
                await Task.Delay(TimeSpan.FromTicks(this.leaseRenewInterval.Ticks / 2), cancellationToken).ConfigureAwait(false);

                while (true)
                {
                    await this.RenewAsync().ConfigureAwait(false);
                    await Task.Delay(this.leaseRenewInterval, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Logger.InfoFormat("Lease with token {0}: renewer task stopped.", this.lease.CurrentLeaseToken);
            }
            catch (Exception ex)
            {
                Logger.FatalException("Lease with token {0}: renew lease loop failed", ex, this.lease.CurrentLeaseToken);
                throw;
            }
        }

        private async Task RenewAsync()
        {
            try
            {
                var renewedLease = await this.leaseManager.RenewAsync(this.lease).ConfigureAwait(false);
                if (renewedLease != null) this.lease = renewedLease;

                Logger.InfoFormat("Lease with token {0}: renewed lease with result {1}", this.lease.CurrentLeaseToken, renewedLease != null);
            }
            catch (LeaseLostException leaseLostException)
            {
                Logger.ErrorException("Lease with token {0}: lost lease on renew.", leaseLostException, this.lease.CurrentLeaseToken);
                throw;
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Lease with token {0}: failed to renew lease.", ex, this.lease.CurrentLeaseToken);
            }
        }
    }
}