// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// Encryption Supported CosmosClient
    /// </summary>
    internal sealed class EncryptionCosmosClient : CosmosClient
    {
        private readonly CosmosClient cosmosClient;

        private readonly AsyncCache<string, ClientEncryptionPolicy> clientEncryptionPolicyCache;

        private readonly AsyncCache<string, ClientEncryptionKeyProperties> clientEncryptionKeyPropertiesCache;

        private readonly List<string> encryptedDatabaseIds = new List<string>();

        public EncryptionCosmosClient(CosmosClient cosmosClient, EncryptionKeyStoreProvider encryptionKeyStoreProvider)
        {
            this.cosmosClient = cosmosClient;
            this.ClientEncryptionPolicyRefreshManager = new ClientEncryptionPropertiesRefreshManager(this, TimeSpan.FromMinutes(30));
            this.clientEncryptionPolicyCache = new AsyncCache<string, ClientEncryptionPolicy>();
            this.clientEncryptionKeyPropertiesCache = new AsyncCache<string, ClientEncryptionKeyProperties>();
            this.EncryptionKeyStoreProvider = encryptionKeyStoreProvider;
        }

        private static readonly SemaphoreSlim CekPropertiesCacheSema = new SemaphoreSlim(1, 1);

        private static readonly SemaphoreSlim EncryptedDatabaseListSema = new SemaphoreSlim(1, 1);

        internal EncryptionKeyStoreProvider EncryptionKeyStoreProvider { get; }

        internal List<string> GetEncryptedDatabaseIds()
        {
            if (EncryptedDatabaseListSema.Wait(-1))
            {
                try
                {
                    return this.encryptedDatabaseIds;
                }
                finally
                {
                    EncryptedDatabaseListSema.Release(1);
                }
            }

            return null;
        }

        internal void SetEncryptedDatabaseIds(string id)
        {
            if (EncryptedDatabaseListSema.Wait(-1))
            {
                try
                {
                    this.encryptedDatabaseIds.Add(id);
                }
                finally
                {
                    EncryptedDatabaseListSema.Release(1);
                }
            }
        }

        internal ClientEncryptionPropertiesRefreshManager ClientEncryptionPolicyRefreshManager { get; }

        private bool isDisposed = false;

        public async Task<ClientEncryptionPolicy> GetOrAddClientEncryptionPolicyAsync(
            Container container,
            bool shouldforceRefresh)
        {
            this.ThrowIfDisposed();

            string cacheKey = container.Database.Id + container.Id;

            // cache it against Database and Container ID key.
            return await this.clientEncryptionPolicyCache.GetAsync(
                 cacheKey,
                 null,
                 async () =>
                 {
                     ContainerResponse containerResponse = await container.ReadContainerAsync();
                     ClientEncryptionPolicy clientEncryptionPolicy = containerResponse.Resource.ClientEncryptionPolicy;
                     return clientEncryptionPolicy;
                 },
                 default,
                 forceRefresh: shouldforceRefresh);
        }

        public async Task<ClientEncryptionKeyProperties> GetOrAddClientEncryptionKeyPropertiessAsync(
            string id,
            Container container,
            bool shouldforceRefresh)
        {
            this.ThrowIfDisposed();

            // we wait unless its a very long previous operation.
            if (await CekPropertiesCacheSema.WaitAsync(-1))
            {
                try
                {
                    return await this.clientEncryptionKeyPropertiesCache.GetAsync(
                         id,
                         null,
                         async () => await this.GetClientEncryptionKeyPropertiesAsync(container, id),
                         default,
                         forceRefresh: shouldforceRefresh);
                }
                finally
                {
                    CekPropertiesCacheSema.Release(1);
                }
            }
            else
            {
                return null;
            }
        }

        public async Task<bool> UpdateClientEncryptionPropertyCacheAsync(
            string id,
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            bool shouldforceRefresh)
        {
            this.ThrowIfDisposed();

            // we wait unless its a very long previous operation.
            if (await CekPropertiesCacheSema.WaitAsync(-1))
            {
                try
                {
                    await this.clientEncryptionKeyPropertiesCache.GetAsync(
                         id,
                         null,
                         async () => await Task.FromResult(clientEncryptionKeyProperties),
                         default,
                         forceRefresh: shouldforceRefresh);
                }
                finally
                {
                    CekPropertiesCacheSema.Release(1);
                }
            }
            else
            {
                return false;
            }

            return true;
        }

        public async Task<ClientEncryptionKeyProperties> GetClientEncryptionKeyPropertiesAsync(Container container, string clientEncryptionKeyId)
        {
            ClientEncryptionKeyProperties clientEncryptionKeyProperties;
            ClientEncryptionKey clientEncryptionKey = container.Database.GetClientEncryptionKey(clientEncryptionKeyId);
            try
            {
                clientEncryptionKeyProperties = await clientEncryptionKey.ReadAsync();
                if (clientEncryptionKeyProperties == null)
                {
                    Debug.Print("Failed to Add Client Encryption Key Properties to the Encryption Cosmos Client Cache.");
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Encryption Based Container without Data Encryption Keys.Please make sure you have created the Client Encryption Keys", ex.Message);
            }

            return await Task.FromResult(clientEncryptionKeyProperties);
        }

        public override CosmosClientOptions ClientOptions => this.cosmosClient.ClientOptions;

        public override CosmosResponseFactory ResponseFactory => this.cosmosClient.ResponseFactory;

        public override Uri Endpoint => this.cosmosClient.Endpoint;

        public override async Task<DatabaseResponse> CreateDatabaseAsync(
            string id,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<DatabaseResponse> databaseResponse = this.cosmosClient.CreateDatabaseAsync(
                id,
                throughput,
                requestOptions,
                cancellationToken);

            EncryptionDatabaseResponse encryptionDatabaseResponse = new EncryptionDatabaseResponse(await databaseResponse, this);
            return encryptionDatabaseResponse;
        }

        public override async Task<DatabaseResponse> CreateDatabaseAsync(
            string id,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<DatabaseResponse> databaseResponse = this.cosmosClient.CreateDatabaseAsync(
                id,
                throughputProperties,
                requestOptions,
                cancellationToken);

            EncryptionDatabaseResponse encryptionDatabaseResponse = new EncryptionDatabaseResponse(await databaseResponse, this);
            return encryptionDatabaseResponse;
        }

        public override async Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<DatabaseResponse> databaseResponse = this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                id,
                throughputProperties,
                requestOptions,
                cancellationToken);

            EncryptionDatabaseResponse encryptionDatabaseResponse = new EncryptionDatabaseResponse(await databaseResponse, this);
            return encryptionDatabaseResponse;
        }

        public override async Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<DatabaseResponse> databaseResponse = this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                id,
                throughput,
                requestOptions,
                cancellationToken);

            EncryptionDatabaseResponse encryptionDatabaseResponse = new EncryptionDatabaseResponse(await databaseResponse, this);
            return encryptionDatabaseResponse;
        }

        public override async Task<ResponseMessage> CreateDatabaseStreamAsync(
            DatabaseProperties databaseProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await this.cosmosClient.CreateDatabaseStreamAsync(
                databaseProperties,
                throughput,
                requestOptions,
                cancellationToken);
        }

        /// <summary>
        /// Gets an Encryption Supported Database.
        /// </summary>
        /// <param name="id"> Database Id </param>
        /// <returns> Database with Encryption support </returns>
        public override Database GetDatabase(string id)
        {
            this.ThrowIfDisposed();
            return new EncryptionDatabase(this.cosmosClient.GetDatabase(id), this);
        }

        public override Container GetContainer(string databaseId, string containerId)
        {
            return new MdeContainer(
                this.cosmosClient.GetContainer(databaseId, containerId),
                this);
        }

        public override FeedIterator<T> GetDatabaseQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.cosmosClient.GetDatabaseQueryIterator<T>(queryDefinition, continuationToken, requestOptions);
        }

        public override FeedIterator<T> GetDatabaseQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.cosmosClient.GetDatabaseQueryIterator<T>(queryText, continuationToken, requestOptions);
        }

        public override FeedIterator GetDatabaseQueryStreamIterator(
           QueryDefinition queryDefinition,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null)
        {
            return this.cosmosClient.GetDatabaseQueryStreamIterator(queryDefinition, continuationToken, requestOptions);
        }

        public override FeedIterator GetDatabaseQueryStreamIterator(
           string queryText = null,
           string continuationToken = null,
           QueryRequestOptions requestOptions = null)
        {
            return this.cosmosClient.GetDatabaseQueryStreamIterator(queryText, continuationToken, requestOptions);
        }

        public override async Task<AccountProperties> ReadAccountAsync()
        {
            return await this.cosmosClient.ReadAccountAsync();
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(nameof(EncryptionCosmosClient));
            }
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            this.ClientEncryptionPolicyRefreshManager.Dispose();
            this.isDisposed = true;
            this.cosmosClient.Dispose();
        }
    }
}
