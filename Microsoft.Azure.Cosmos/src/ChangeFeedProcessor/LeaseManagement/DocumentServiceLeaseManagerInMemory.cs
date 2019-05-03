namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.Logging;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;

    /// <summary>
    /// <see cref="DocumentServiceLeaseManager"/> implementation that uses In-Memory
    /// </summary>
    internal sealed class DocumentServiceLeaseManagerInMemory : DocumentServiceLeaseManager
    {
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly DocumentServiceLeaseUpdater leaseUpdater;
        private readonly ConcurrentDictionary<string, DocumentServiceLease> container;

        public DocumentServiceLeaseManagerInMemory(
            DocumentServiceLeaseUpdater leaseUpdater,
            ConcurrentDictionary<string, DocumentServiceLease> container)
        {
            this.leaseUpdater = leaseUpdater;
            this.container = container;
        }

        public override Task<DocumentServiceLease> AcquireAsync(DocumentServiceLease lease)
        {
            if (lease == null)
                throw new ArgumentNullException(nameof(lease));

            return this.leaseUpdater.UpdateLeaseAsync(
                lease,
                lease.Id,
                null /*partitionKey*/,
                serverLease =>
                {
                    serverLease.Properties = lease.Properties;
                    return serverLease;
                });
        }

        public override Task<DocumentServiceLease> CreateLeaseIfNotExistAsync(string leaseToken, string continuationToken)
        {
            if (leaseToken == null)
                throw new ArgumentNullException(nameof(leaseToken));

            var documentServiceLease = new DocumentServiceLeaseCore
            {
                LeaseId = leaseToken,
                LeaseToken = leaseToken,
                ContinuationToken = continuationToken,
            };

            bool created = this.container.TryAdd(
                leaseToken,
                documentServiceLease);
            if (created)
            {
                Logger.InfoFormat("Created lease with lease token {0}.", leaseToken);
                return Task.FromResult<DocumentServiceLease>(documentServiceLease);
            }

            Logger.InfoFormat("Some other host created lease for {0}.", leaseToken);
            return Task.FromResult<DocumentServiceLease>(null);
        }

        public override Task ReleaseAsync(DocumentServiceLease lease)
        {
            if (lease == null)
                throw new ArgumentNullException(nameof(lease));

            if (!this.container.TryGetValue(lease.CurrentLeaseToken, out DocumentServiceLease refreshedLease))
            {
                Logger.InfoFormat("Lease with token {0} failed to release lease. The lease is gone already.", lease.CurrentLeaseToken);
                throw new LeaseLostException(lease);
            }

            return this.leaseUpdater.UpdateLeaseAsync(
                refreshedLease,
                refreshedLease.Id,
                null /*partitionKey*/,
                serverLease =>
                {
                    serverLease.Owner = null;
                    return serverLease;
                });
        }

        public override Task DeleteAsync(DocumentServiceLease lease)
        {
            if (lease?.Id == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            this.container.TryRemove(lease.CurrentLeaseToken, out DocumentServiceLease removedLease);
            return Task.CompletedTask;
        }

        public override Task<DocumentServiceLease> RenewAsync(DocumentServiceLease lease)
        {
            if (lease == null)
                throw new ArgumentNullException(nameof(lease));

            // Get fresh lease. The assumption here is that checkpointing is done with higher frequency than lease renewal so almost
            // certainly the lease was updated in between.
            if (!this.container.TryGetValue(lease.CurrentLeaseToken, out DocumentServiceLease refreshedLease))
            {
                Logger.InfoFormat("Lease with token {0} failed to renew lease. The lease is gone already.", lease.CurrentLeaseToken);
                throw new LeaseLostException(lease);
            }

            return this.leaseUpdater.UpdateLeaseAsync(
                refreshedLease,
                refreshedLease.Id,
                null /*partitionKey*/,
                serverLease =>
                {
                    return serverLease;
                });
        }

        public override Task<DocumentServiceLease> UpdatePropertiesAsync(DocumentServiceLease lease)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));

            return this.leaseUpdater.UpdateLeaseAsync(
                lease,
                lease.Id,
                null /*partitionKey*/,
                serverLease =>
                {
                    serverLease.Properties = lease.Properties;
                    return serverLease;
                });
        }
    }
}
