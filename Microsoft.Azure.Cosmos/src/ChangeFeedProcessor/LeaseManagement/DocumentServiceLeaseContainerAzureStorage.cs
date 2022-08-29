//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using global::Azure.Storage.Blobs;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;

    internal sealed class DocumentServiceLeaseContainerAzureStorage : DocumentServiceLeaseContainer
    {
        private readonly BlobContainerClient container;
        private readonly string hostName;

        public DocumentServiceLeaseContainerAzureStorage(
            BlobContainerClient container,
            string hostName)
        {
            this.container = container;
            this.hostName = hostName;
        }

        public override async Task<IReadOnlyList<DocumentServiceLease>> GetAllLeasesAsync()
        {
            return await this.ListDocumentsAsync().ConfigureAwait(false);
        }

        public override async Task<IEnumerable<DocumentServiceLease>> GetOwnedLeasesAsync()
        {
            List<DocumentServiceLease> ownedLeases = new List<DocumentServiceLease>();
            foreach (DocumentServiceLease lease in await this.GetAllLeasesAsync().ConfigureAwait(false))
            {
                if (string.Compare(lease.Owner, this.hostName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    ownedLeases.Add(lease);
                }
            }

            return ownedLeases;
        }

        private async Task<IReadOnlyList<DocumentServiceLease>> ListDocumentsAsync()
        {
            List<DocumentServiceLease> leases = new ();
            await foreach (var blobItem in this.container.GetBlobsAsync())
            {
                if (blobItem.Name == DocumentServiceLeaseStoreAzureStorage.InitializationBlobName)
                {
                    continue;
                }
                using (MemoryStream blobContentStream = new MemoryStream())
                {
                    await this.container.GetBlobClient(blobItem.Name).DownloadToAsync(blobContentStream);
                    blobContentStream.Position = 0;
                    DocumentServiceLease lease = CosmosContainerExtensions.DefaultJsonSerializer.FromStream<DocumentServiceLease>(blobContentStream);
                    leases.Add(lease);
                }
            }

            return leases;
        }
    }
}
