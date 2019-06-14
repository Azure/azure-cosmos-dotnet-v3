//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal sealed class DocumentServiceLeaseContainerCosmos : DocumentServiceLeaseContainer
    {
        private readonly Container container;
        private readonly DocumentServiceLeaseStoreManagerOptions options;

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
            var ownedLeases = new List<DocumentServiceLease>();
            foreach (DocumentServiceLease lease in await this.GetAllLeasesAsync().ConfigureAwait(false))
            {
                if (string.Compare(lease.Owner, this.options.HostName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    ownedLeases.Add(lease);
                }
            }

            return ownedLeases;
        }

        private async Task<IReadOnlyList<DocumentServiceLeaseCore>> ListDocumentsAsync(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                throw new ArgumentException("Prefix must be non-empty string", nameof(prefix));

            var query = this.container.GetItemQueryIterator<DocumentServiceLeaseCore>(
                "SELECT * FROM c WHERE STARTSWITH(c.id, '" + prefix + "')");
            var leases = new List<DocumentServiceLeaseCore>();
            while (query.HasMoreResults)
            {
                leases.AddRange(await query.ReadNextAsync().ConfigureAwait(false));
            }

            return leases;
        }
    }
}
