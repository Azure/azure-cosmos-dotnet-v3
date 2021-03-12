//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// <see cref="DocumentServiceLeaseManager"/> implementation that uses Azure Cosmos DB service
    /// </summary>
    internal sealed class DocumentServiceLeaseManagerCosmos : DocumentServiceLeaseManager
    {
        private readonly ContainerInternal monitoredContainer;
        private readonly ContainerInternal leaseContainer;
        private readonly DocumentServiceLeaseUpdater leaseUpdater;
        private readonly DocumentServiceLeaseStoreManagerOptions options;
        private readonly RequestOptionsFactory requestOptionsFactory;
        private readonly AsyncLazy<TryCatch<string>> lazyContainerRid;
        private PartitionKeyRangeCache partitionKeyRangeCache;

        public DocumentServiceLeaseManagerCosmos(
            ContainerInternal monitoredContainer,
            ContainerInternal leaseContainer,
            DocumentServiceLeaseUpdater leaseUpdater,
            DocumentServiceLeaseStoreManagerOptions options,
            RequestOptionsFactory requestOptionsFactory)
        {
            this.monitoredContainer = monitoredContainer;
            this.leaseContainer = leaseContainer;
            this.leaseUpdater = leaseUpdater;
            this.options = options;
            this.requestOptionsFactory = requestOptionsFactory;
            this.lazyContainerRid = new AsyncLazy<TryCatch<string>>(valueFactory: (trace, innerCancellationToken) => this.TryInitializeContainerRIdAsync(innerCancellationToken));
        }

        public override async Task<DocumentServiceLease> AcquireAsync(DocumentServiceLease lease)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            string oldOwner = lease.Owner;

            // We need to add the range information to any older leases
            // This would not happen with new created leases but we need to be back compat
            if (lease.FeedRange == null)
            {
                if (!this.lazyContainerRid.ValueInitialized)
                {
                    TryCatch<string> tryInitializeContainerRId = await this.lazyContainerRid.GetValueAsync(NoOpTrace.Singleton, default);
                    if (!tryInitializeContainerRId.Succeeded)
                    {
                        throw tryInitializeContainerRId.Exception.InnerException;
                    }

                    this.partitionKeyRangeCache = await this.monitoredContainer.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync(NoOpTrace.Singleton);
                }

                PartitionKeyRange partitionKeyRange = await this.partitionKeyRangeCache.TryGetPartitionKeyRangeByIdAsync(
                    this.lazyContainerRid.Result.Result, 
                    lease.CurrentLeaseToken,
                    NoOpTrace.Singleton);

                if (partitionKeyRange != null)
                {
                    lease.FeedRange = new FeedRangeEpk(partitionKeyRange.ToRange());
                }
            }

            return await this.leaseUpdater.UpdateLeaseAsync(
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
                }).ConfigureAwait(false);
        }

        public override Task<DocumentServiceLease> CreateLeaseIfNotExistAsync(
            PartitionKeyRange partitionKeyRange, 
            string continuationToken)
        {
            if (partitionKeyRange == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyRange));
            }

            string leaseToken = partitionKeyRange.Id;
            string leaseDocId = this.GetDocumentId(leaseToken);
            DocumentServiceLeaseCore documentServiceLease = new DocumentServiceLeaseCore
            {
                LeaseId = leaseDocId,
                LeaseToken = leaseToken,
                ContinuationToken = continuationToken,
                FeedRange = new FeedRangeEpk(partitionKeyRange.ToRange())
            };

            return this.TryCreateDocumentServiceLeaseAsync(documentServiceLease);
        }

        public override Task<DocumentServiceLease> CreateLeaseIfNotExistAsync(
            FeedRangeEpk feedRange,
            string continuationToken)
        {
            if (feedRange == null)
            {
                throw new ArgumentNullException(nameof(feedRange));
            }

            string leaseToken = $"{feedRange.Range.Min}-{feedRange.Range.Max}";
            string leaseDocId = this.GetDocumentId(leaseToken);
            DocumentServiceLeaseCoreEpk documentServiceLease = new DocumentServiceLeaseCoreEpk
            {
                LeaseId = leaseDocId,
                LeaseToken = leaseToken,
                ContinuationToken = continuationToken,
                FeedRange = feedRange
            };

            return this.TryCreateDocumentServiceLeaseAsync(documentServiceLease);
        }

        public override async Task ReleaseAsync(DocumentServiceLease lease)
        {
            if (lease == null)
                throw new ArgumentNullException(nameof(lease));

            DocumentServiceLease refreshedLease = await this.TryGetLeaseAsync(lease).ConfigureAwait(false);
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

        public override async Task DeleteAsync(DocumentServiceLease lease)
        {
            if (lease?.Id == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            await this.leaseContainer.TryDeleteItemAsync<DocumentServiceLeaseCore>(
                    this.requestOptionsFactory.GetPartitionKey(lease.Id),
                    lease.Id).ConfigureAwait(false);
        }

        public override async Task<DocumentServiceLease> RenewAsync(DocumentServiceLease lease)
        {
            if (lease == null)
                throw new ArgumentNullException(nameof(lease));

            // Get fresh lease. The assumption here is that checkpointing is done with higher frequency than lease renewal so almost
            // certainly the lease was updated in between.
            DocumentServiceLease refreshedLease = await this.TryGetLeaseAsync(lease).ConfigureAwait(false);
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

        public override async Task<DocumentServiceLease> UpdatePropertiesAsync(DocumentServiceLease lease)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));

            if (lease.Owner != this.options.HostName)
            {
                DefaultTrace.TraceInformation("Lease with token '{0}' was taken over by owner '{1}' before lease properties update", lease.CurrentLeaseToken, lease.Owner);
                throw new LeaseLostException(lease);
            }

            return await this.leaseUpdater.UpdateLeaseAsync(
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
                }).ConfigureAwait(false);
        }

        private async Task<DocumentServiceLease> TryCreateDocumentServiceLeaseAsync(DocumentServiceLease documentServiceLease)
        {
            bool created = await this.leaseContainer.TryCreateItemAsync<DocumentServiceLease>(
                this.requestOptionsFactory.GetPartitionKey(documentServiceLease.Id),
                documentServiceLease).ConfigureAwait(false) != null;
            if (created)
            {
                DefaultTrace.TraceInformation("Created lease with lease token {0}.", documentServiceLease.CurrentLeaseToken);
                return documentServiceLease;
            }

            DefaultTrace.TraceInformation("Some other host created lease for {0}.", documentServiceLease.CurrentLeaseToken);
            return null;
        }

        private async Task<DocumentServiceLease> TryGetLeaseAsync(DocumentServiceLease lease)
        {
            return await this.leaseContainer.TryGetItemAsync<DocumentServiceLease>(this.requestOptionsFactory.GetPartitionKey(lease.Id), lease.Id).ConfigureAwait(false);
        }

        private string GetDocumentId(string partitionId)
        {
            return this.options.GetPartitionLeasePrefix() + partitionId;
        }

        private async Task<TryCatch<string>> TryInitializeContainerRIdAsync(CancellationToken cancellationToken)
        {
            try
            {
                string containerRId = await this.monitoredContainer.GetCachedRIDAsync(
                    forceRefresh: false,
                    NoOpTrace.Singleton,
                    cancellationToken: cancellationToken);
                return TryCatch<string>.FromResult(containerRId);
            }
            catch (CosmosException cosmosException)
            {
                return TryCatch<string>.FromException(cosmosException);
            }
        }
    }
}
