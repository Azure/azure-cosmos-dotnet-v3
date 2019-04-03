//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal sealed class DocumentServiceLeaseContainerCosmos : DocumentServiceLeaseContainer
    {
        private readonly CosmosContainer container;
        private readonly DocumentServiceLeaseStoreManagerSettings settings;

        public DocumentServiceLeaseContainerCosmos(
            CosmosContainer container,
            DocumentServiceLeaseStoreManagerSettings settings)
        {
            this.container = container;
            this.settings = settings;
        }

        public override async Task<IReadOnlyList<DocumentServiceLease>> GetAllLeasesAsync()
        {
            return await this.ListDocumentsAsync(this.settings.GetPartitionLeasePrefix()).ConfigureAwait(false);
        }

        public override async Task<IEnumerable<DocumentServiceLease>> GetOwnedLeasesAsync()
        {
            var ownedLeases = new List<DocumentServiceLease>();
            foreach (DocumentServiceLease lease in await this.GetAllLeasesAsync().ConfigureAwait(false))
            {
                if (string.Compare(lease.Owner, this.settings.HostName, StringComparison.OrdinalIgnoreCase) == 0)
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

            var query = this.container.Items.CreateItemQuery<DocumentServiceLeaseCore>(
                "SELECT * FROM c WHERE STARTSWITH(c.id, '" + prefix + "')",
                0 /* max concurrency */);
            var leases = new List<DocumentServiceLeaseCore>();
            while (query.HasMoreResults)
            {
                leases.AddRange(await query.FetchNextSetAsync().ConfigureAwait(false));
            }

            return leases;
        }
    }
}
