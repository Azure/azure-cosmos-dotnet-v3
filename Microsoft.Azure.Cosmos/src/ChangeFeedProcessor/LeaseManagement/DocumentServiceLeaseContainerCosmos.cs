//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Documents;

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

        public override async Task<IReadOnlyList<JsonElement>> ExportLeasesAsync(
            CancellationToken cancellationToken = default)
        {
            List<JsonElement> exportedLeases = new List<JsonElement>();

            string prefix = this.options.ContainerNamePrefix;
            string query = "SELECT * FROM c WHERE STARTSWITH(c.id, '" + prefix + "')";

            using FeedIterator iterator = this.container.GetItemQueryStreamIterator(
                query,
                continuationToken: null,
                requestOptions: queryRequestOptions);

            while (iterator.HasMoreResults)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using (ResponseMessage responseMessage = await iterator.ReadNextAsync().ConfigureAwait(false))
                {
                    responseMessage.EnsureSuccessStatusCode();

                    using (JsonDocument feedResponse = await JsonDocument.ParseAsync(responseMessage.Content).ConfigureAwait(false))
                    {
                        if (!feedResponse.RootElement.TryGetProperty("Documents", out JsonElement documents))
                        {
                            continue;
                        }

                        foreach (JsonElement docElement in documents.EnumerateArray())
                        {
                            if (!docElement.TryGetProperty("id", out JsonElement idElement)
                                || idElement.ValueKind != JsonValueKind.String)
                            {
                                continue;
                            }

                            string id = idElement.GetString();

                            if (!this.IsMetadataDocumentId(id))
                            {
                                // Regular lease - deserialize, validate FeedRange
                                string raw = docElement.GetRawText();
                                using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(raw)))
                                {
                                    DocumentServiceLease lease = CosmosContainerExtensions.DefaultJsonSerializer.FromStream<DocumentServiceLease>(ms);

                                    if (lease.FeedRange == null)
                                    {
                                        throw new LeaseOperationNotSupportedException(lease, "ExportLeases");
                                    }
                                }
                            }

                            exportedLeases.Add(docElement.Clone());
                        }
                    }
                }
            }

            return exportedLeases.AsReadOnly();
        }

        public override async Task ImportLeasesAsync(
            IReadOnlyList<JsonElement> leases,
            bool overwriteExisting = false,
            CancellationToken cancellationToken = default)
        {
            if (leases == null)
            {
                throw new ArgumentNullException(nameof(leases));
            }

            foreach (JsonElement leaseElement in leases)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (leaseElement.ValueKind == JsonValueKind.Undefined || leaseElement.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                // Detect metadata document from the raw JSON before deserializing
                bool isMetadataDocument = leaseElement.TryGetProperty("id", out JsonElement idElement)
                    && this.IsMetadataDocumentId(idElement.GetString());

                if (isMetadataDocument)
                {
                    // Metadata document (.info, .lock) - import raw JSON to preserve original structure
                    string id = idElement.GetString();
                    Microsoft.Azure.Cosmos.PartitionKey pk = new Microsoft.Azure.Cosmos.PartitionKey(id);
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(leaseElement.GetRawText());

                    using (MemoryStream stream = new MemoryStream(bytes))
                    {
                        if (overwriteExisting)
                        {
                            using (ResponseMessage response = await this.container.UpsertItemStreamAsync(stream, pk).ConfigureAwait(false))
                            {
                                response.EnsureSuccessStatusCode();
                            }
                        }
                        else
                        {
                            using (ResponseMessage response = await this.container.CreateItemStreamAsync(stream, pk).ConfigureAwait(false))
                            {
                                if (response.StatusCode != HttpStatusCode.Conflict)
                                {
                                    response.EnsureSuccessStatusCode();
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Regular lease - deserialize and validate
                    string payloadJson = leaseElement.GetRawText();
                    using (MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payloadJson)))
                    {
                        DocumentServiceLease lease = CosmosContainerExtensions.DefaultJsonSerializer.FromStream<DocumentServiceLease>(stream);
                        if (lease == null)
                        {
                            continue;
                        }

                        if (lease.FeedRange == null)
                        {
                            throw new LeaseOperationNotSupportedException(lease, "ImportLeases");
                        }

                        if (overwriteExisting)
                        {
                            await this.UpsertLeaseAsync(lease).ConfigureAwait(false);
                        }
                        else
                        {
                            await this.TryCreateLeaseAsync(lease).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determines if a document ID belongs to a metadata document.
        /// Metadata documents are system artifacts like ".info" and ".lock" documents.
        /// </summary>
        private bool IsMetadataDocumentId(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            return id.EndsWith(".info", StringComparison.Ordinal)
                || id.EndsWith(".lock", StringComparison.Ordinal);
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
            Microsoft.Azure.Cosmos.PartitionKey partitionKey = this.GetPartitionKey(lease);
            ItemResponse<DocumentServiceLease> response = await this.container.TryCreateItemAsync(
                partitionKey,
                lease).ConfigureAwait(false);

            return response != null;
        }

        private async Task UpsertLeaseAsync(DocumentServiceLease lease)
        {
            Microsoft.Azure.Cosmos.PartitionKey partitionKey = this.GetPartitionKey(lease);

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

        private Microsoft.Azure.Cosmos.PartitionKey GetPartitionKey(DocumentServiceLease lease)
        {
            // If the lease has a partition key, use it; otherwise use the lease ID
            if (!string.IsNullOrEmpty(lease.PartitionKey))
            {
                return new Microsoft.Azure.Cosmos.PartitionKey(lease.PartitionKey);
            }

            return new Microsoft.Azure.Cosmos.PartitionKey(lease.Id);
        }
    }
}
