//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Core.Trace;
    using Exceptions;
    using global::Azure;
    using global::Azure.Storage.Blobs;
    using global::Azure.Storage.Blobs.Models;
    using Utils;

    /// <summary>
    /// <see cref="DocumentServiceLeaseUpdater"/> that uses Azure Cosmos DB
    /// </summary>
    internal sealed class DocumentServiceLeaseUpdaterAzureStorage : DocumentServiceLeaseUpdater
    {
        private const int RetryCountOnConflict = 5;
        private readonly BlobContainerClient container;
        private readonly Dictionary<string, ETag> blobIdToEtag;

        public DocumentServiceLeaseUpdaterAzureStorage(BlobContainerClient container)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
            this.blobIdToEtag = new Dictionary<string, ETag>();
        }

        public override async Task<DocumentServiceLease> UpdateLeaseAsync(
            DocumentServiceLease cachedLease,
            string itemId,
            PartitionKey partitionKey,
            Func<DocumentServiceLease, DocumentServiceLease> 
                updateLease)
        {
            DocumentServiceLease lease = cachedLease;
            for (int retryCount = RetryCountOnConflict; retryCount >= 0; retryCount--)
            {
                lease = updateLease(lease);
                if (lease == null)
                {
                    return null;
                }

                lease.Timestamp = DateTime.UtcNow;
                DocumentServiceLease leaseDocument = await this.TryReplaceLeaseAsync(lease, itemId).ConfigureAwait(false);
                if (leaseDocument != null)
                {
                    return leaseDocument;
                }

                DefaultTrace.TraceInformation("Lease with token {0} update conflict. Reading the current version of lease.", lease.CurrentLeaseToken);

                try
                {
                    BlobClient blob = this.container.GetBlobClient(itemId);
                    Stream stream = (await blob.DownloadAsync()).Value.Content;
                    stream.Position = 0;
                    DocumentServiceLease serverLease = CosmosContainerExtensions.DefaultJsonSerializer.FromStream<DocumentServiceLease>(stream);

                    DefaultTrace.TraceInformation(
                    "Lease with token {0} update failed because the lease with concurrency token '{1}' was updated by host '{2}' with concurrency token '{3}'. Will retry, {4} retry(s) left.",
                    lease.CurrentLeaseToken,
                    lease.ConcurrencyToken,
                    serverLease.Owner,
                    serverLease.ConcurrencyToken,
                    retryCount);

                    lease = serverLease;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    DefaultTrace.TraceInformation("Lease with token {0} no longer exists", lease.CurrentLeaseToken);
                    throw new LeaseLostException(lease, true);
                }
            }

            throw new LeaseLostException(lease);
        }

        private async Task<DocumentServiceLease> TryReplaceLeaseAsync(
            DocumentServiceLease lease,
            string itemId)
        {
            try
            {
                BlobClient blob = this.container.GetBlobClient(itemId);
                ETag eTag;
                this.blobIdToEtag.TryGetValue(itemId, out eTag);
                using (Stream stream = CosmosContainerExtensions.DefaultJsonSerializer.ToStream<DocumentServiceLease>(lease))
                {
                    BlobContentInfo result = await blob.UploadAsync(stream, conditions: new BlobRequestConditions {IfMatch = eTag});
                    this.blobIdToEtag[itemId] = result.ETag;
                }

                return lease;
            }
            catch (RequestFailedException ex)
            {
                DefaultTrace.TraceWarning("Lease operation exception, status code: {0}", ex.ErrorCode);
                if (ex.Status == (int)HttpStatusCode.NotFound)
                {
                    throw new LeaseLostException(lease, true);
                }

                if (ex.Status == (int)HttpStatusCode.PreconditionFailed)
                {
                    return null;
                }

                if (ex.Status == (int)HttpStatusCode.Conflict)
                {
                    throw new LeaseLostException(lease, ex, false);
                }

                throw;
            }
        }
    }
}
