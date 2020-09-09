//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// <see cref="DocumentServiceLeaseManager"/> implementation that uses Azure Cosmos DB service
    /// </summary>
    internal sealed class DocumentServiceLeaseManagerCosmos : DocumentServiceLeaseManager
    {
        private readonly Container leaseContainer;
        private readonly DocumentServiceLeaseUpdater leaseUpdater;
        private readonly DocumentServiceLeaseStoreManagerOptions options;
        private readonly RequestOptionsFactory requestOptionsFactory;

        public DocumentServiceLeaseManagerCosmos(
            Container leaseContainer,
            DocumentServiceLeaseUpdater leaseUpdater,
            DocumentServiceLeaseStoreManagerOptions options,
            RequestOptionsFactory requestOptionsFactory)
        {
            this.leaseContainer = leaseContainer;
            this.leaseUpdater = leaseUpdater;
            this.options = options;
            this.requestOptionsFactory = requestOptionsFactory;
        }

        public override Task<DocumentServiceLease> AcquireAsync(DocumentServiceLease lease)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            string oldOwner = lease.Owner;

            return this.leaseUpdater.UpdateLeaseAsync(
                lease,
                lease.Id,
                this.requestOptionsFactory.GetPartitionKey(lease.Id),
                serverLease =>
                {
                    if (serverLease.Owner != oldOwner)
                    {
                        DefaultTrace.TraceInformation("{0} lease token was taken over by owner '{1}'", lease.CurrentLeaseToken, serverLease.Owner);
                        throw new LeaseLostException(lease);
                    }
                    serverLease.Owner = this.options.HostName;
                    serverLease.Properties = lease.Properties;
                    return serverLease;
                });
        }

        public override async Task<DocumentServiceLease> CreateLeaseIfNotExistAsync(string leaseToken, string continuationToken)
        {
            if (leaseToken == null)
                throw new ArgumentNullException(nameof(leaseToken));

            string leaseDocId = this.GetDocumentId(leaseToken);
            DocumentServiceLeaseCore documentServiceLease = new DocumentServiceLeaseCore
            {
                LeaseId = leaseDocId,
                LeaseToken = leaseToken,
                ContinuationToken = continuationToken,
            };

            bool created = await this.leaseContainer.TryCreateItemAsync<DocumentServiceLeaseCore>(
                this.requestOptionsFactory.GetPartitionKey(documentServiceLease.Id),
                documentServiceLease).ConfigureAwait(false) != null;
            if (created)
            {
                DefaultTrace.TraceInformation("Created lease with lease token {0}.", leaseToken);
                return documentServiceLease;
            }

            DefaultTrace.TraceInformation("Some other host created lease for {0}.", leaseToken);
            return null;
        }

        public override async Task ReleaseAsync(DocumentServiceLease lease)
        {
            if (lease == null)
                throw new ArgumentNullException(nameof(lease));

            DocumentServiceLeaseCore refreshedLease = await this.TryGetLeaseAsync(lease).ConfigureAwait(false);
            if (refreshedLease == null)
            {
                DefaultTrace.TraceInformation("Lease with token {0} failed to release lease. The lease is gone already.", lease.CurrentLeaseToken);
                throw new LeaseLostException(lease);
            }

            await this.leaseUpdater.UpdateLeaseAsync(
                refreshedLease,
                refreshedLease.Id,
                this.requestOptionsFactory.GetPartitionKey(lease.Id),
                serverLease =>
                {
                    if (serverLease.Owner != lease.Owner)
                    {
                        DefaultTrace.TraceInformation("Lease with token {0} no need to release lease. The lease was already taken by another host '{1}'.", lease.CurrentLeaseToken, serverLease.Owner);
                        throw new LeaseLostException(lease);
                    }
                    serverLease.Owner = null;
                    return serverLease;
                }).ConfigureAwait(false);
        }

        public override Task DeleteAsync(DocumentServiceLease lease)
        {
            if (lease?.Id == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            return this.leaseContainer.TryDeleteItemAsync<DocumentServiceLeaseCore>(
                    this.requestOptionsFactory.GetPartitionKey(lease.Id),
                    lease.Id);
        }

        public override async Task<DocumentServiceLease> RenewAsync(DocumentServiceLease lease)
        {
            if (lease == null)
                throw new ArgumentNullException(nameof(lease));

            // Get fresh lease. The assumption here is that checkpointing is done with higher frequency than lease renewal so almost
            // certainly the lease was updated in between.
            DocumentServiceLeaseCore refreshedLease = await this.TryGetLeaseAsync(lease).ConfigureAwait(false);
            if (refreshedLease == null)
            {
                DefaultTrace.TraceInformation("Lease with token {0} failed to renew lease. The lease is gone already.", lease.CurrentLeaseToken);
                throw new LeaseLostException(lease);
            }

            return await this.leaseUpdater.UpdateLeaseAsync(
                refreshedLease,
                refreshedLease.Id,
                this.requestOptionsFactory.GetPartitionKey(lease.Id),
                serverLease =>
                {
                    if (serverLease.Owner != lease.Owner)
                    {
                        DefaultTrace.TraceInformation("Lease with token {0} was taken over by owner '{1}'", lease.CurrentLeaseToken, serverLease.Owner);
                        throw new LeaseLostException(lease);
                    }
                    return serverLease;
                }).ConfigureAwait(false);
        }

        public override Task<DocumentServiceLease> UpdatePropertiesAsync(DocumentServiceLease lease)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));

            if (lease.Owner != this.options.HostName)
            {
                DefaultTrace.TraceInformation("Lease with token '{0}' was taken over by owner '{1}' before lease properties update", lease.CurrentLeaseToken, lease.Owner);
                throw new LeaseLostException(lease);
            }

            return this.leaseUpdater.UpdateLeaseAsync(
                lease,
                lease.Id,
                this.requestOptionsFactory.GetPartitionKey(lease.Id),
                serverLease =>
                {
                    if (serverLease.Owner != lease.Owner)
                    {
                        DefaultTrace.TraceInformation("Lease with token '{0}' was taken over by owner '{1}'", lease.CurrentLeaseToken, serverLease.Owner);
                        throw new LeaseLostException(lease);
                    }
                    serverLease.Properties = lease.Properties;
                    return serverLease;
                });
        }

        private Task<DocumentServiceLeaseCore> TryGetLeaseAsync(DocumentServiceLease lease)
        {
            return this.leaseContainer.TryGetItemAsync<DocumentServiceLeaseCore>(this.requestOptionsFactory.GetPartitionKey(lease.Id), lease.Id);
        }

        private string GetDocumentId(string partitionId)
        {
            return this.options.GetPartitionLeasePrefix() + partitionId;
        }
    }
}
