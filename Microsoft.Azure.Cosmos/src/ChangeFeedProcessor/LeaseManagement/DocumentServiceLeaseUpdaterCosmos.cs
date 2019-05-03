//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.Logging;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// <see cref="DocumentServiceLeaseUpdater"/> that uses Azure Cosmos DB
    /// </summary>
    internal sealed class DocumentServiceLeaseUpdaterCosmos : DocumentServiceLeaseUpdater
    {
        private const int RetryCountOnConflict = 5;
        private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();
        private readonly CosmosContainer container;

        public DocumentServiceLeaseUpdaterCosmos(CosmosContainer container)
        {
            if (container == null) throw new ArgumentNullException(nameof(container));
            this.container = container;
        }

        public override async Task<DocumentServiceLease> UpdateLeaseAsync(DocumentServiceLease cachedLease, string itemId, object partitionKey, Func<DocumentServiceLease, DocumentServiceLease> updateLease)
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
                DocumentServiceLeaseCore leaseDocument = await this.TryReplaceLeaseAsync((DocumentServiceLeaseCore)lease, partitionKey, itemId).ConfigureAwait(false);
                if (leaseDocument != null)
                {
                    return leaseDocument;
                }

                Logger.InfoFormat("Lease with token {0} update conflict. Reading the current version of lease.", lease.CurrentLeaseToken);

                CosmosItemResponse<DocumentServiceLeaseCore> response = await this.container.Items.ReadItemAsync<DocumentServiceLeaseCore>(
                    partitionKey, itemId).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Logger.InfoFormat("Lease with token {0} no longer exists", lease.CurrentLeaseToken);
                    throw new LeaseLostException(lease, true);
                }

                DocumentServiceLeaseCore serverLease = response.Resource;

                Logger.InfoFormat(
                    "Lease with token {0} update failed because the lease with concurrency token '{1}' was updated by host '{2}' with concurrency token '{3}'. Will retry, {4} retry(s) left.",
                    lease.CurrentLeaseToken,
                    lease.ConcurrencyToken,
                    serverLease.Owner,
                    serverLease.ConcurrencyToken,
                    retryCount);

                lease = serverLease;
            }

            throw new LeaseLostException(lease);
        }

        private async Task<DocumentServiceLeaseCore> TryReplaceLeaseAsync(DocumentServiceLeaseCore lease, object partitionKey, string itemId)
        {
            try
            {
                CosmosItemResponse<DocumentServiceLeaseCore> response = await this.container.Items.ReplaceItemAsync<DocumentServiceLeaseCore>(
                    partitionKey,
                    itemId, 
                    lease, 
                    this.CreateIfMatchOptions(lease)).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new LeaseLostException(lease, true);
                }

                return response.Resource;
            }
            catch (CosmosException ex)
            {
                Logger.WarnFormat("Lease operation exception, status code: ", ex.StatusCode);
                if (ex.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    return null;
                }

                if (ex.StatusCode == HttpStatusCode.Conflict)
                {
                    throw new LeaseLostException(lease, ex, false);
                }

                throw;
            }
        }

        private CosmosItemRequestOptions CreateIfMatchOptions(DocumentServiceLease lease)
        {
            var ifMatchCondition = new AccessCondition { Type = AccessConditionType.IfMatch, Condition = lease.ConcurrencyToken };
            return new CosmosItemRequestOptions { AccessCondition = ifMatchCondition };
        }
    }
}
