//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;

    internal sealed class DocumentServiceLeaseContainerCosmos : DocumentServiceLeaseContainer
    {
        private readonly Container container;
        private readonly DocumentServiceLeaseStoreManagerOptions options;
        private static readonly QueryRequestOptions queryRequestOptions = new QueryRequestOptions() { MaxConcurrency = 0 };

        public DocumentServiceLeaseContainerCosmos(
            Container container,
            DocumentServiceLeaseStoreManagerOptions options)
        {
            this.container = container;
            this.options = options;
        }

        public override async Task<IReadOnlyList<DocumentServiceLease>> GetAllLeasesAsync()
        {
            return await this.ListDocumentsAsync(this.options.GetPartitionLeasePrefix()).ConfigureAwait(false);
        }

        public override async Task<IEnumerable<DocumentServiceLease>> GetOwnedLeasesAsync()
        {
            List<DocumentServiceLease> ownedLeases = new List<DocumentServiceLease>();
            foreach (DocumentServiceLease lease in await this.GetAllLeasesAsync().ConfigureAwait(false))
            {
                if (string.Compare(lease.Owner, this.options.HostName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    ownedLeases.Add(lease);
                }
            }

            return ownedLeases;
        }

        public override async Task<IReadOnlyList<LeaseExportData>> ExportLeasesAsync(
            string exportedBy,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<DocumentServiceLease> allLeases = await this.GetAllLeasesAsync().ConfigureAwait(false);

            List<LeaseExportData> exportedLeases = new List<LeaseExportData>();

            foreach (DocumentServiceLease lease in allLeases)
            {
                cancellationToken.ThrowIfCancellationRequested();

                LeaseExportData exportData = LeaseExportHelper.ToExportData(lease, exportedBy);

                // Include any existing ownership history from previous imports
                List<LeaseOwnershipHistory> existingHistory = LeaseExportHelper.GetOwnershipHistory(lease);
                if (existingHistory.Count > 0)
                {
                    // Prepend existing history (it will be before the current "exported" entry)
                    existingHistory.AddRange(exportData.OwnershipHistory);
                    exportData.OwnershipHistory = existingHistory;
                }

                exportedLeases.Add(exportData);
            }

            return exportedLeases.AsReadOnly();
        }

        public override async Task ImportLeasesAsync(
            IReadOnlyList<LeaseExportData> leases,
            string importedBy,
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default)
        {
            if (leases == null)
            {
                throw new ArgumentNullException(nameof(leases));
            }

            foreach (LeaseExportData exportData in leases)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DocumentServiceLease lease = LeaseExportHelper.FromExportData(exportData, importedBy);

                if (overwriteExisting)
                {
                    // Use upsert to create or replace
                    await this.UpsertLeaseAsync(lease).ConfigureAwait(false);
                }
                else
                {
                    // Try to create, ignore if already exists
                    await this.TryCreateLeaseAsync(lease).ConfigureAwait(false);
                }
            }
        }

        private async Task<IReadOnlyList<DocumentServiceLease>> ListDocumentsAsync(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                throw new ArgumentException("Prefix must be non-empty string", nameof(prefix));

            using FeedIterator iterator = this.container.GetItemQueryStreamIterator(
                "SELECT * FROM c WHERE STARTSWITH(c.id, '" + prefix + "')",
                continuationToken: null,
                requestOptions: queryRequestOptions);

            List<DocumentServiceLease> leases = new List<DocumentServiceLease>();
            while (iterator.HasMoreResults)
            {
                using (ResponseMessage responseMessage = await iterator.ReadNextAsync().ConfigureAwait(false))
                {
                    responseMessage.EnsureSuccessStatusCode();
                    leases.AddRange(CosmosFeedResponseSerializer.FromFeedResponseStream<DocumentServiceLease>(
                        CosmosContainerExtensions.DefaultJsonSerializer,
                        responseMessage.Content));
                }   
            }

            return leases;
        }

        private async Task<bool> TryCreateLeaseAsync(DocumentServiceLease lease)
        {
            PartitionKey partitionKey = this.GetPartitionKey(lease);
            ItemResponse<DocumentServiceLease> response = await this.container.TryCreateItemAsync(
                partitionKey,
                lease).ConfigureAwait(false);

            return response != null;
        }

        private async Task UpsertLeaseAsync(DocumentServiceLease lease)
        {
            PartitionKey partitionKey = this.GetPartitionKey(lease);

            using (System.IO.Stream itemStream = CosmosContainerExtensions.DefaultJsonSerializer.ToStream(lease))
            {
                using (ResponseMessage response = await this.container.UpsertItemStreamAsync(
                    itemStream, 
                    partitionKey).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        private PartitionKey GetPartitionKey(DocumentServiceLease lease)
        {
            // If the lease has a partition key, use it; otherwise use the lease ID
            if (!string.IsNullOrEmpty(lease.PartitionKey))
            {
                return new PartitionKey(lease.PartitionKey);
            }

            return new PartitionKey(lease.Id);
        }
    }
}
