//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// <see cref="DocumentServiceLeaseManager"/> implementation that uses In-Memory
    /// </summary>
    internal sealed class DocumentServiceLeaseManagerInMemory : DocumentServiceLeaseManager
    {
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
            {
                throw new ArgumentNullException(nameof(lease));
            }

            return this.leaseUpdater.UpdateLeaseAsync(
                lease,
                lease.Id,
                Cosmos.PartitionKey.Null,
                serverLease =>
                {
                    serverLease.Properties = lease.Properties;
                    return serverLease;
                });
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

        public override Task ReleaseAsync(DocumentServiceLease lease)
        {
            if (lease == null)
                throw new ArgumentNullException(nameof(lease));

            if (!this.container.TryGetValue(lease.CurrentLeaseToken, out DocumentServiceLease refreshedLease))
            {
                DefaultTrace.TraceInformation("Lease with token {0} failed to release lease. The lease is gone already.", lease.CurrentLeaseToken);
                throw new LeaseLostException(lease);
            }

            return this.leaseUpdater.UpdateLeaseAsync(
                refreshedLease,
                refreshedLease.Id,
                Cosmos.PartitionKey.Null,
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

            this.container.TryRemove(lease.CurrentLeaseToken, out DocumentServiceLease _);
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
                DefaultTrace.TraceInformation("Lease with token {0} failed to renew lease. The lease is gone already.", lease.CurrentLeaseToken);
                throw new LeaseLostException(lease);
            }

            return this.leaseUpdater.UpdateLeaseAsync(
                refreshedLease,
                refreshedLease.Id,
                Cosmos.PartitionKey.Null,
                serverLease => serverLease);
        }

        public override Task<DocumentServiceLease> UpdatePropertiesAsync(DocumentServiceLease lease)
        {
            if (lease == null) throw new ArgumentNullException(nameof(lease));

            return this.leaseUpdater.UpdateLeaseAsync(
                lease,
                lease.Id,
                Cosmos.PartitionKey.Null,
                serverLease =>
                {
                    serverLease.Properties = lease.Properties;
                    return serverLease;
                });
        }

        private Task<DocumentServiceLease> TryCreateDocumentServiceLeaseAsync(DocumentServiceLease documentServiceLease)
        {
            bool created = this.container.TryAdd(
                documentServiceLease.CurrentLeaseToken,
                documentServiceLease);
            if (created)
            {
                DefaultTrace.TraceInformation("Created lease with lease token {0}.", documentServiceLease.CurrentLeaseToken);
                return Task.FromResult<DocumentServiceLease>(documentServiceLease);
            }

            DefaultTrace.TraceInformation("Some other host created lease for {0}.", documentServiceLease.CurrentLeaseToken);
            return Task.FromResult<DocumentServiceLease>(null);
        }
    }
}
