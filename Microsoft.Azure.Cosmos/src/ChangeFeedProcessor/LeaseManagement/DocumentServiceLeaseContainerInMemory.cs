//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class DocumentServiceLeaseContainerInMemory : DocumentServiceLeaseContainer
    {
        private readonly ConcurrentDictionary<string, DocumentServiceLease> container;

        public DocumentServiceLeaseContainerInMemory(ConcurrentDictionary<string, DocumentServiceLease> container)
        {
            this.container = container;
        }

        public override Task<IReadOnlyList<DocumentServiceLease>> GetAllLeasesAsync()
        {
            return Task.FromResult<IReadOnlyList<DocumentServiceLease>>(this.container.Values.ToList().AsReadOnly());
        }

        public override Task<IEnumerable<DocumentServiceLease>> GetOwnedLeasesAsync()
        {
            return Task.FromResult<IEnumerable<DocumentServiceLease>>(this.container.Values.AsEnumerable());
        }

        public override Task<IReadOnlyList<LeaseExportData>> ExportLeasesAsync(
            string exportedBy,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            List<LeaseExportData> exportedLeases = new List<LeaseExportData>();
            
            foreach (DocumentServiceLease lease in this.container.Values)
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

            return Task.FromResult<IReadOnlyList<LeaseExportData>>(exportedLeases.AsReadOnly());
        }

        public override Task ImportLeasesAsync(
            IReadOnlyList<LeaseExportData> leases,
            string importedBy,
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default)
        {
            if (leases == null)
            {
                throw new ArgumentNullException(nameof(leases));
            }

            cancellationToken.ThrowIfCancellationRequested();

            foreach (LeaseExportData exportData in leases)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DocumentServiceLease lease = LeaseExportHelper.FromExportData(exportData, importedBy);

                if (overwriteExisting)
                {
                    this.container[lease.Id] = lease;
                }
                else
                {
                    // Only add if not already present
                    this.container.TryAdd(lease.Id, lease);
                }
            }

            return Task.CompletedTask;
        }
    }
}
