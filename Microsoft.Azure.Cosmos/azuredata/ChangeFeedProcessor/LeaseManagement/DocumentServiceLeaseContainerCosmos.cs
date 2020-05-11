//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;

    internal sealed class DocumentServiceLeaseContainerCosmos : DocumentServiceLeaseContainer
    {
        private readonly CosmosContainer container;
        private readonly DocumentServiceLeaseStoreManagerOptions options;
        private static readonly QueryRequestOptions queryRequestOptions = new QueryRequestOptions() { MaxConcurrency = 0 };

        public DocumentServiceLeaseContainerCosmos(
            CosmosContainer container,
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

        private async Task<IReadOnlyList<DocumentServiceLeaseCore>> ListDocumentsAsync(string prefix)
        {
            if (string.IsNullOrEmpty(prefix))
                throw new ArgumentException("Prefix must be non-empty string", nameof(prefix));

            List<DocumentServiceLeaseCore> leases = new List<DocumentServiceLeaseCore>();
            await foreach (Response page in this.container.GetItemQueryStreamResultsAsync(
                "SELECT * FROM c WHERE STARTSWITH(c.id, '" + prefix + "')",
                continuationToken: null,
                requestOptions: queryRequestOptions))
            {
                leases.AddRange(CosmosContainerExtensions.DefaultJsonSerializer.FromStream<CosmosFeedResponseUtil<DocumentServiceLeaseCore>>(page.ContentStream).Data);
            }

            return leases;
        }
    }
}
