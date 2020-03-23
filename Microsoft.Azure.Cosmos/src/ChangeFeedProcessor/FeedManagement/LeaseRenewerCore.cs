//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Core.Trace;

    internal sealed class LeaseRenewerCore : LeaseRenewer
    {
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
                DefaultTrace.TraceInformation("Lease with token {0}: renewer task started.", this.lease.CurrentLeaseToken);
                await Task.Delay(TimeSpan.FromTicks(this.leaseRenewInterval.Ticks / 2), cancellationToken).ConfigureAwait(false);

                while (true)
                {
                    await this.RenewAsync().ConfigureAwait(false);
                    await Task.Delay(this.leaseRenewInterval, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                DefaultTrace.TraceInformation("Lease with token {0}: renewer task stopped.", this.lease.CurrentLeaseToken);
            }
            catch (Exception ex)
            {
                Extensions.TraceException(ex);
                DefaultTrace.TraceCritical("Lease with token {0}: renew lease loop failed", this.lease.CurrentLeaseToken);
                throw;
            }
        }

        private async Task RenewAsync()
        {
            try
            {
                DocumentServiceLease renewedLease = await this.leaseManager.RenewAsync(this.lease).ConfigureAwait(false);
                if (renewedLease != null) this.lease = renewedLease;

                DefaultTrace.TraceInformation("Lease with token {0}: renewed lease with result {1}", this.lease.CurrentLeaseToken, renewedLease != null);
            }
            catch (LeaseLostException leaseLostException)
            {
                Extensions.TraceException(leaseLostException);
                DefaultTrace.TraceError("Lease with token {0}: lost lease on renew.", this.lease.CurrentLeaseToken);
                throw;
            }
            catch (Exception ex)
            {
                Extensions.TraceException(ex);
                DefaultTrace.TraceError("Lease with token {0}: failed to renew lease.", this.lease.CurrentLeaseToken);
            }
        }
    }
}