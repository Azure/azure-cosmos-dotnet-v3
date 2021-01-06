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

        private readonly AsyncCache<string, CachedClientEncryptionProperties> clientEncryptionKeyPropertiesCache;

        private readonly TimeSpan clientEncryptionKeyPropertiesCacheTimeToLive;

        public EncryptionCosmosClient(CosmosClient cosmosClient, EncryptionKeyStoreProvider encryptionKeyStoreProvider)
        {
            this.cosmosClient = cosmosClient;
            this.EncryptionKeyStoreProvider = encryptionKeyStoreProvider;
            this.clientEncryptionPolicyCache = new AsyncCache<string, ClientEncryptionPolicy>();
            this.clientEncryptionKeyPropertiesCache = new AsyncCache<string, CachedClientEncryptionProperties>();
            this.clientEncryptionKeyPropertiesCacheTimeToLive = TimeSpan.FromMinutes(60);
            this.cachedClientEncryptionKeyList = new Dictionary<string, HashSet<string>>();
        }

        private static readonly SemaphoreSlim CekPropertiesCacheSema = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim KeyListSema = new SemaphoreSlim(1, 1);

        internal EncryptionKeyStoreProvider EncryptionKeyStoreProvider { get; }

        private readonly Dictionary<string, HashSet<string>> cachedClientEncryptionKeyList;

        public void UpdateCachedClientEncryptionKeyList(string key, string value)
        {
            if (KeyListSema.Wait(-1))
            {
                try
                {
                    if (this.cachedClientEncryptionKeyList.ContainsKey(key))
                    {
                        HashSet<string> list = this.cachedClientEncryptionKeyList[key];
                        if (list.Contains(value) == false)
                        {
                            list.Add(value);
                        }
                    }
                    else
                    {
                        HashSet<string> list = new HashSet<string>
                        {
                            value,
                        };

                        this.cachedClientEncryptionKeyList.Add(key, list);
                    }
                }
                finally
                {
                    KeyListSema.Release(1);
                }
            }
        }

        public Dictionary<string, HashSet<string>> GetClientEncryptionKeyList()
        {
            if (KeyListSema.Wait(-1))
            {
                try
                {
                    return this.cachedClientEncryptionKeyList;
                }
                finally
                {
                    KeyListSema.Release(1);
                }
            }
            else
            {
                return null;
            }
        }

        public void RemoveCachedClientEncryptionKeyEntry(string key)
        {
            if (KeyListSema.Wait(-1))
            {
                try
                {
                    if (this.cachedClientEncryptionKeyList.ContainsKey(key))
                    {
                        this.cachedClientEncryptionKeyList.Remove(key);
                    }
                }
                finally
                {
                    KeyListSema.Release(1);
                }
            }
        }

        /// <summary>
        /// Gets or Adds ClientEncryptionPolicy. The Cache gets seeded initially either via InitializeEncryptionAsync call on the container,
        /// or during the the first request to create an item.
        /// </summary>
        /// <param name="container"> The container handler to read the policies from.</param>
        /// <param name="cancellationToken"> cancellation token </param>
        /// <param name="shouldforceRefresh"> force refresh the cache </param>
        /// <returns> task result </returns>
        internal async Task<ClientEncryptionPolicy> GetOrAddClientEncryptionPolicyAsync(
            Container container,
            CancellationToken cancellationToken = default,
            bool shouldforceRefresh = false)
        {
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
                 cancellationToken,
                 forceRefresh: shouldforceRefresh);
        }

        internal void RemoveClientEncryptionPolicy(Container container)
        {
            string cacheKey = container.Database.Id + container.Id;
            this.clientEncryptionPolicyCache.Remove(cacheKey);
        }

        internal async Task<CachedClientEncryptionProperties> GetOrAddClientEncryptionKeyPropertiesAsync(
            string clientEncryptionKeyId,
            Container container,
            CancellationToken cancellationToken = default,
            bool shouldforceRefresh = false)
        {
            string cacheKey = container.Database.Id + clientEncryptionKeyId;

            if (await CekPropertiesCacheSema.WaitAsync(-1))
            {
                try
                {
                    return await this.clientEncryptionKeyPropertiesCache.GetAsync(
                         cacheKey,
                         null,
                         async () => await this.GetClientEncryptionKeyPropertiesAsync(container, clientEncryptionKeyId, cancellationToken),
                         cancellationToken,
                         forceRefresh: shouldforceRefresh);
                }
                catch
                {
                    throw;
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

        internal void RemoveClientEncryptionPropertyCache(string id)
        {
            if (CekPropertiesCacheSema.Wait(-1))
            {
                try
                {
                    this.clientEncryptionKeyPropertiesCache.Remove(id);
                }
                finally
                {
                    CekPropertiesCacheSema.Release(1);
                }
            }
        }

        internal async Task<CachedClientEncryptionProperties> GetClientEncryptionKeyPropertiesAsync(
            Container container,
            string clientEncryptionKeyId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ClientEncryptionKeyProperties clientEncryptionKeyProperties;
            ClientEncryptionKey clientEncryptionKey = container.Database.GetClientEncryptionKey(clientEncryptionKeyId);
            try
            {
                clientEncryptionKeyProperties = await clientEncryptionKey.ReadAsync(cancellationToken: cancellationToken);
                if (clientEncryptionKeyProperties == null)
                {
                    Debug.Print("Failed to Add Client Encryption Key Properties to the Encryption Cosmos Client Cache.");
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Encryption Based Container without Data Encryption Keys.Please make sure you have created the Client Encryption Keys", ex.Message);
            }

            CachedClientEncryptionProperties cachedClientEncryptionProperties = new CachedClientEncryptionProperties(
                clientEncryptionKeyProperties,
                DateTime.UtcNow + this.clientEncryptionKeyPropertiesCacheTimeToLive);

            // manage a list of keys associated with a database, required for cleanup.
            this.UpdateCachedClientEncryptionKeyList(container.Database.Id, clientEncryptionKeyId);
            return cachedClientEncryptionProperties;
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

        public override Task<ResponseMessage> CreateDatabaseStreamAsync(
            DatabaseProperties databaseProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.cosmosClient.CreateDatabaseStreamAsync(
                databaseProperties,
                throughput,
                requestOptions,
                cancellationToken);
        }

        public override Database GetDatabase(string id)
        {
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

        public override Task<AccountProperties> ReadAccountAsync()
        {
            return this.cosmosClient.ReadAccountAsync();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            this.cosmosClient.Dispose();
        }
    }
}
