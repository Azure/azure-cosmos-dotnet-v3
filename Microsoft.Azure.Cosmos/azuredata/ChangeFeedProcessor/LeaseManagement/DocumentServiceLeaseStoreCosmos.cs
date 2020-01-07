//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.ChangeFeed
{
    using System;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    /// <summary>
    /// Implementation of <see cref="DocumentServiceLeaseStore"/> for state in Azure Cosmos DB
    /// </summary>
    internal sealed class DocumentServiceLeaseStoreCosmos : DocumentServiceLeaseStore
    {
        private readonly CosmosContainer container;
        private readonly string containerNamePrefix;
        private readonly RequestOptionsFactory requestOptionsFactory;
        private ETag? lockETag;

        public DocumentServiceLeaseStoreCosmos(
            CosmosContainer container,
            string containerNamePrefix,
            RequestOptionsFactory requestOptionsFactory)
        {
            this.container = container;
            this.containerNamePrefix = containerNamePrefix;
            this.requestOptionsFactory = requestOptionsFactory;
        }

        public override async Task<bool> IsInitializedAsync()
        {
            string markerDocId = this.GetStoreMarkerName();

            return await this.container.ItemExistsAsync(this.requestOptionsFactory.GetPartitionKey(markerDocId), markerDocId).ConfigureAwait(false);
        }

        public override async Task MarkInitializedAsync()
        {
            string markerDocId = this.GetStoreMarkerName();
            var containerDocument = new { id = markerDocId };

            await this.container.CreateItemAsync<dynamic>(
                item: containerDocument,
                partitionKey: this.requestOptionsFactory.GetPartitionKey(markerDocId)).ConfigureAwait(false);
        }

        public override async Task<bool> AcquireInitializationLockAsync(TimeSpan lockTime)
        {
            string lockId = this.GetStoreLockName();
            LockDocument containerDocument = new LockDocument() { Id = lockId, TimeToLive = (int)lockTime.TotalSeconds };
            ItemResponse<LockDocument> document = await this.container.TryCreateItemAsync<LockDocument>(
                this.requestOptionsFactory.GetPartitionKey(lockId),
                containerDocument).ConfigureAwait(false);

            if (document != null)
            {
                this.lockETag = document.ETag;
                return true;
            }

            return false;
        }

        public override async Task<bool> ReleaseInitializationLockAsync()
        {
            string lockId = this.GetStoreLockName();
            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                IfMatch = this.lockETag,
            };

            bool deleted = await this.container.TryDeleteItemAsync<LockDocument>(
                this.requestOptionsFactory.GetPartitionKey(lockId),
                lockId,
                requestOptions).ConfigureAwait(false);

            if (deleted)
            {
                this.lockETag = null;
                return true;
            }

            return false;
        }

        private string GetStoreMarkerName()
        {
            return this.containerNamePrefix + ".info";
        }

        private string GetStoreLockName()
        {
            return this.containerNamePrefix + ".lock";
        }

        private class LockDocument
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("ttl")]
            public int TimeToLive { get; set; }
        }
    }
}