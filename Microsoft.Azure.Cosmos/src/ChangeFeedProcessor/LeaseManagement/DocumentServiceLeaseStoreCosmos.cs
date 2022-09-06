//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Utils;
    using Newtonsoft.Json;

    /// <summary>
    /// Implementation of <see cref="DocumentServiceLeaseStore"/> for state in Azure Cosmos DB
    /// </summary>
    internal sealed class DocumentServiceLeaseStoreCosmos : DocumentServiceLeaseStore
    {
        private readonly Container container;
        private readonly string containerNamePrefix;
        private readonly RequestOptionsFactory requestOptionsFactory;
        private string lockETag;

        public DocumentServiceLeaseStoreCosmos(
            Container container,
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

            return await this.container.ItemExistsAsync(this.requestOptionsFactory.GetPartitionKey(markerDocId, markerDocId), markerDocId).ConfigureAwait(false);
        }

        public override async Task MarkInitializedAsync()
        {
            string markerDocId = this.GetStoreMarkerName();
            InitializedDocument containerDocument = new InitializedDocument { Id = markerDocId };

            this.requestOptionsFactory.AddPartitionKeyIfNeeded((string pk) => containerDocument.PartitionKey = pk, markerDocId);

            using (Stream itemStream = CosmosContainerExtensions.DefaultJsonSerializer.ToStream(containerDocument))
            {
                using (ResponseMessage responseMessage = await this.container.CreateItemStreamAsync(
                    itemStream,
                    this.requestOptionsFactory.GetPartitionKey(markerDocId, markerDocId)).ConfigureAwait(false))
                {
                    responseMessage.EnsureSuccessStatusCode();
                }
            }
        }

        public override async Task<bool> AcquireInitializationLockAsync(TimeSpan lockTime)
        {
            string lockId = this.GetStoreLockName();
            LockDocument containerDocument = new LockDocument() { Id = lockId, TimeToLive = (int)lockTime.TotalSeconds };
            this.requestOptionsFactory.AddPartitionKeyIfNeeded((string pk) => containerDocument.PartitionKey = pk, lockId);

            ItemResponse<LockDocument> document = await this.container.TryCreateItemAsync<LockDocument>(
                this.requestOptionsFactory.GetPartitionKey(lockId, lockId),
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
                IfMatchEtag = this.lockETag,
            };

            bool deleted = await this.container.TryDeleteItemAsync<LockDocument>(
                this.requestOptionsFactory.GetPartitionKey(lockId, lockId),
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
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("partitionKey", NullValueHandling = NullValueHandling.Ignore)]
            public string PartitionKey { get; set; }

            [JsonProperty("ttl")]
            public int TimeToLive { get; set; }
        }

        private class InitializedDocument
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("partitionKey", NullValueHandling = NullValueHandling.Ignore)]
            public string PartitionKey { get; set; }
        }
    }
}