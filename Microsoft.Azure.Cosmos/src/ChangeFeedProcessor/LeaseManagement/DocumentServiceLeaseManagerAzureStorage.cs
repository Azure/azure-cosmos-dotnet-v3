//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Core.Trace;
    using Documents;
    using Exceptions;
    using global::Azure.Storage.Blobs;
    using Query.Core;
    using Query.Core.Monads;
    using Routing;
    using Tracing;
    using Utils;
    using PartitionKey = Cosmos.PartitionKey;

    /// <summary>
    /// <see cref="DocumentServiceLeaseManager"/> implementation that uses Azure Cosmos DB service
    /// </summary>
    internal sealed class DocumentServiceLeaseManagerAzureStorage : DocumentServiceLeaseManager
    {
        private readonly ContainerInternal monitoredContainer;
        private readonly BlobContainerClient leaseContainer;
        private readonly DocumentServiceLeaseUpdater leaseUpdater;
        private readonly string hostName;
        private readonly AsyncLazy<TryCatch<string>> lazyContainerRid;
        private PartitionKeyRangeCache partitionKeyRangeCache;

        public DocumentServiceLeaseManagerAzureStorage(
            ContainerInternal monitoredContainer,
            BlobContainerClient leaseContainer,
            DocumentServiceLeaseUpdater leaseUpdater,
            string hostName)
        {
            this.monitoredContainer = monitoredContainer;
            this.leaseContainer = leaseContainer;
            this.leaseUpdater = leaseUpdater;
            this.hostName = hostName;
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
                PartitionKey.Null,
                serverLease =>
                {
                    if (serverLease.Owner != oldOwner)
                    {
                        DefaultTrace.TraceInformation("{0} lease token was taken over by owner '{1}'", lease.CurrentLeaseToken, serverLease.Owner);
                        throw new LeaseLostException(lease);
                    }
                    serverLease.Owner = this.hostName;
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
            DocumentServiceLeaseCore documentServiceLease = new DocumentServiceLeaseCore
            {
                LeaseId = leaseToken,
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
            DocumentServiceLeaseCoreEpk documentServiceLease = new DocumentServiceLeaseCoreEpk
            {
                LeaseId = leaseToken,
                LeaseToken = leaseToken,
                ContinuationToken = continuationToken,
                FeedRange = feedRange
            };
            
            return this.TryCreateDocumentServiceLeaseAsync(documentServiceLease);
        }

        public override async Task ReleaseAsync(DocumentServiceLease lease)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            DocumentServiceLease refreshedLease;
            try
            {
                refreshedLease = await this.TryGetLeaseAsync(lease).ConfigureAwait(false);
            }
            catch (CosmosException cosmosException) 
            when (cosmosException.StatusCode == HttpStatusCode.NotFound && cosmosException.SubStatusCode == (int)SubStatusCodes.Unknown)
            {
                // Lease is being released after a split, the split itself delete the lease, this is expected
                return;
            }

            if (refreshedLease == null)
            {
                DefaultTrace.TraceInformation("Lease with token {0} failed to release lease. The lease is gone already.", lease.CurrentLeaseToken);
                throw new LeaseLostException(lease);
            }

            await this.leaseUpdater.UpdateLeaseAsync(
                refreshedLease,
                refreshedLease.Id,
                new PartitionKey(),
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

            await this.leaseContainer.DeleteBlobAsync(lease.Id).ConfigureAwait(false);
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
                PartitionKey.Null,
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

            if (lease.Owner != this.hostName)
            {
                DefaultTrace.TraceInformation("Lease with token '{0}' was taken over by owner '{1}' before lease properties update", lease.CurrentLeaseToken, lease.Owner);
                throw new LeaseLostException(lease);
            }

            return await this.leaseUpdater.UpdateLeaseAsync(
                lease,
                lease.Id,
                PartitionKey.Null,
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
            BlobClient blob = this.leaseContainer.GetBlobClient(documentServiceLease.Id);
            try
            {
                using (Stream stream = CosmosContainerExtensions.DefaultJsonSerializer.ToStream(documentServiceLease))
                {
                    await blob.UploadAsync(stream);
                    DefaultTrace.TraceInformation("Created lease with lease token {0}.", documentServiceLease.CurrentLeaseToken);
                    return documentServiceLease;
                }
            }
            catch (Exception)
            {
                DefaultTrace.TraceInformation("Some other host created lease for {0}.", documentServiceLease.CurrentLeaseToken);
                return null;
            }
        }

        private async Task<DocumentServiceLease> TryGetLeaseAsync(DocumentServiceLease lease)
        {
            BlobClient blob = this.leaseContainer.GetBlobClient(lease.Id);
            Stream stream = (await blob.DownloadAsync()).Value.Content;
            stream.Position = 0;
            return CosmosContainerExtensions.DefaultJsonSerializer.FromStream<DocumentServiceLease>(stream);
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
