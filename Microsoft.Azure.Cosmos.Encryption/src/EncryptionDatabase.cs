// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;

    internal sealed class EncryptionDatabase : Database
    {
        private readonly Database database;

        private Container container;

        public EncryptionDatabase(Database database, EncryptionCosmosClient encryptionCosmosClient)
        {
            this.database = database;
            this.EncryptionCosmosClient = encryptionCosmosClient;
        }

        internal EncryptionCosmosClient EncryptionCosmosClient { get; private set; }

        public override string Id => this.database.Id;

        public override CosmosClient Client => this.database.Client;

        public override Container GetContainer(string id)
        {
            this.container = this.database.GetContainer(id);
            return new MdeContainer(this.container, this.EncryptionCosmosClient);
        }

        public override Task<ClientEncryptionKeyResponse> CreateClientEncryptionKeyAsync(
            ClientEncryptionKey clientEncryptionKey,
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.database.CreateClientEncryptionKeyAsync(
                clientEncryptionKey,
                clientEncryptionKeyProperties,
                requestOptions,
                cancellationToken);
        }

        public override async Task<ContainerResponse> CreateContainerAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<ContainerResponse> containerResponse = this.database.CreateContainerAsync(
                containerProperties,
                throughputProperties,
                requestOptions,
                cancellationToken);

            EncryptionContainerResponse encryptionContainerResponse = new EncryptionContainerResponse(
                await containerResponse,
                new MdeContainer(await containerResponse, this.EncryptionCosmosClient));

            return encryptionContainerResponse;
        }

        public override async Task<ContainerResponse> CreateContainerAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<ContainerResponse> containerResponse = this.database.CreateContainerAsync(
                containerProperties,
                throughput,
                requestOptions,
                cancellationToken);

            EncryptionContainerResponse encryptionContainerResponse = new EncryptionContainerResponse(
                await containerResponse,
                new MdeContainer(await containerResponse, this.EncryptionCosmosClient));

            return encryptionContainerResponse;
        }

        public override async Task<ContainerResponse> CreateContainerAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<ContainerResponse> containerResponse = this.database.CreateContainerAsync(
                id,
                partitionKeyPath,
                throughput,
                requestOptions,
                cancellationToken);

            EncryptionContainerResponse encryptionContainerResponse = new EncryptionContainerResponse(
                await containerResponse,
                new MdeContainer(await containerResponse, this.EncryptionCosmosClient));

            return encryptionContainerResponse;
        }

        public override async Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<ContainerResponse> containerResponse = this.database.CreateContainerIfNotExistsAsync(
                containerProperties,
                throughputProperties,
                requestOptions,
                cancellationToken);

            EncryptionContainerResponse encryptionContainerResponse = new EncryptionContainerResponse(
                await containerResponse,
                new MdeContainer(await containerResponse, this.EncryptionCosmosClient));

            return encryptionContainerResponse;
        }

        public override async Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<ContainerResponse> containerResponse = this.database.CreateContainerIfNotExistsAsync(
                containerProperties,
                throughput,
                requestOptions,
                cancellationToken);

            EncryptionContainerResponse encryptionContainerResponse = new EncryptionContainerResponse(
                await containerResponse,
                new MdeContainer(await containerResponse, this.EncryptionCosmosClient));

            return encryptionContainerResponse;
        }

        public override async Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            Task<ContainerResponse> containerResponse = this.database.CreateContainerIfNotExistsAsync(
                id,
                partitionKeyPath,
                throughput,
                requestOptions,
                cancellationToken);

            EncryptionContainerResponse encryptionContainerResponse = new EncryptionContainerResponse(
                await containerResponse,
                new MdeContainer(await containerResponse, this.EncryptionCosmosClient));

            return encryptionContainerResponse;
        }

        public override Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.database.CreateContainerStreamAsync(
                containerProperties,
                throughputProperties,
                requestOptions,
                cancellationToken);
        }

        public override Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.database.CreateContainerStreamAsync(
                containerProperties,
                throughput,
                requestOptions,
                cancellationToken);
        }

        public override async Task<UserResponse> CreateUserAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return await this.database.CreateUserAsync(id, requestOptions, cancellationToken);
        }

        public override ContainerBuilder DefineContainer(string name, string partitionKeyPath)
        {
            ContainerBuilder containerBuilder = this.database.DefineContainer(name, partitionKeyPath);
            return containerBuilder;
        }

        public override async Task<DatabaseResponse> DeleteAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            // clean up the container and database cache entries.
            await this.DatabaseCacheCleanupAsync();

            EncryptionDatabaseResponse encryptionDatabaseResponse = new EncryptionDatabaseResponse(
                await this.database.DeleteAsync(requestOptions, cancellationToken),
                this.EncryptionCosmosClient);

            return encryptionDatabaseResponse;
        }

        public override async Task<ResponseMessage> DeleteStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            // clean up the container and database cache entries.
            await this.DatabaseCacheCleanupAsync();
            return await this.database.DeleteStreamAsync(requestOptions, cancellationToken);
        }

        internal async Task DatabaseCacheCleanupAsync()
        {
            // clean up all the container specific Client Encryption Policy cache entries.
            FeedIterator<ContainerProperties> feedIterator = this.database.GetContainerQueryIterator<ContainerProperties>();
            while (feedIterator.HasMoreResults)
            {
                foreach (ContainerProperties containerProperties in await feedIterator.ReadNextAsync())
                {
                    // clear the cached policies for this container.
                    this.EncryptionCosmosClient.RemoveClientEncryptionPolicy(this.database.GetContainer(containerProperties.Id));
                }
            }

            // Remove all the Keys cached.
            if (this.EncryptionCosmosClient.GetClientEncryptionKeyList().TryGetValue(this.Id, out HashSet<string> keyList))
            {
                foreach (string keyId in keyList)
                {
                    string cacheKey = this.Id + keyId;
                    this.EncryptionCosmosClient.RemoveClientEncryptionPropertyCache(cacheKey);
                }

                this.EncryptionCosmosClient.RemoveCachedClientEncryptionKeyEntry(this.Id);
            }
        }

        public override ClientEncryptionKey GetClientEncryptionKey(string id)
        {
            return this.database.GetClientEncryptionKey(id);
        }

        public override FeedIterator<ClientEncryptionKeyProperties> GetClientEncryptionKeyIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.database.GetClientEncryptionKeyIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.database.GetContainerQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.database.GetContainerQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator GetContainerQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.database.GetContainerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator GetContainerQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.database.GetContainerQueryStreamIterator(
                queryText,
                continuationToken,
                requestOptions);
        }

        public override User GetUser(string id)
        {
            return this.database.GetUser(id);
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.database.GetUserQueryIterator<T>(
                queryText,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return this.database.GetUserQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override async Task<DatabaseResponse> ReadAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            EncryptionDatabaseResponse encryptionDatabaseResponse = new EncryptionDatabaseResponse(
                await this.database.ReadAsync(requestOptions, cancellationToken),
                this.EncryptionCosmosClient);

            return encryptionDatabaseResponse;
        }

        public override Task<ResponseMessage> ReadStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.database.ReadStreamAsync(requestOptions, cancellationToken);
        }

        public override Task<int?> ReadThroughputAsync(
            CancellationToken cancellationToken = default)
        {
            return this.database.ReadThroughputAsync(cancellationToken);
        }

        public override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            return this.database.ReadThroughputAsync(
                requestOptions,
                cancellationToken);
        }

        public override Task<ClientEncryptionKeyResponse> ReplaceClientEncryptionKeyAsync(
            ClientEncryptionKey clientEncryptionKey,
            ClientEncryptionKeyProperties clientEncryptionKeyProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.database.ReplaceClientEncryptionKeyAsync(
                clientEncryptionKey,
                clientEncryptionKeyProperties,
                requestOptions,
                cancellationToken);
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.database.ReplaceThroughputAsync(
                throughputProperties,
                requestOptions,
                cancellationToken);
        }

        public override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.database.ReplaceThroughputAsync(
                throughput,
                requestOptions,
                cancellationToken);
        }

        public override Task<UserResponse> UpsertUserAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.database.UpsertUserAsync(
                id,
                requestOptions,
                cancellationToken);
        }
    }
}