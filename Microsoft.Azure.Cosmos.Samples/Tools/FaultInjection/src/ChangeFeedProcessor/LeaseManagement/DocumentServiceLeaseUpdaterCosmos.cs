﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Microsoft.Azure.Cosmos.Core.Trace;

    /// <summary>
    /// <see cref="DocumentServiceLeaseUpdater"/> that uses Azure Cosmos DB
    /// </summary>
    internal sealed class DocumentServiceLeaseUpdaterCosmos : DocumentServiceLeaseUpdater
    {
        private const int RetryCountOnConflict = 5;
        private readonly Container container;

        public DocumentServiceLeaseUpdaterCosmos(Container container)
        {
            this.container = container ?? throw new ArgumentNullException(nameof(container));
        }

        public override async Task<DocumentServiceLease> UpdateLeaseAsync(
            DocumentServiceLease cachedLease,
            string itemId,
            Cosmos.PartitionKey partitionKey,
            Func<DocumentServiceLease, DocumentServiceLease> updateLease)
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
                DocumentServiceLease leaseDocument = await this.TryReplaceLeaseAsync(lease, partitionKey, itemId).ConfigureAwait(false);
                if (leaseDocument != null)
                {
                    return leaseDocument;
                }

                DefaultTrace.TraceInformation("Lease with token {0} update conflict. Reading the current version of lease.", lease.CurrentLeaseToken);

                try
                {
                    DocumentServiceLease serverLease = await this.container.TryGetItemAsync<DocumentServiceLease>(partitionKey, itemId);

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
                    throw new LeaseLostException(lease: lease, innerException: ex, isGone: true);
                }
            }

            throw new LeaseLostException(lease);
        }

        private async Task<DocumentServiceLease> TryReplaceLeaseAsync(
            DocumentServiceLease lease,
            PartitionKey partitionKey,
            string itemId)
        {
            try
            {
                ItemRequestOptions itemRequestOptions = this.CreateIfMatchOptions(lease);
                ItemResponse<DocumentServiceLease> response = await this.container.TryReplaceItemAsync<DocumentServiceLease>(
                    itemId,
                    lease,
                    partitionKey,
                    itemRequestOptions).ConfigureAwait(false);

                return response.Resource;
            }
            catch (CosmosException ex)
            {
                DefaultTrace.TraceWarning("Lease operation exception, status code: {0}", ex.StatusCode);
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new LeaseLostException(lease: lease, innerException: ex, isGone: true);
                }

                if (ex.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    return null;
                }

                if (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    throw new LeaseLostException(lease: lease, innerException: ex, isGone: false);
                }

                throw;
            }
        }

        private ItemRequestOptions CreateIfMatchOptions(DocumentServiceLease lease)
        {
            return new ItemRequestOptions { IfMatchEtag = lease.ConcurrencyToken };
        }
    }
}
