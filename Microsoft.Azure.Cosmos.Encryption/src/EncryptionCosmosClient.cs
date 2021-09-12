// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;

    /// <summary>
    /// CosmosClient with Encryption support.
    /// </summary>
    internal sealed class EncryptionCosmosClient : CosmosClient
    {
        private readonly CosmosClient cosmosClient;

        private readonly AsyncCache<string, ClientEncryptionKeyProperties> clientEncryptionKeyPropertiesCacheByKeyId;

        public EncryptionCosmosClient(CosmosClient cosmosClient, EncryptionKeyStoreProvider encryptionKeyStoreProvider)
        {
            this.cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
            this.EncryptionKeyStoreProvider = encryptionKeyStoreProvider ?? throw new ArgumentNullException(nameof(encryptionKeyStoreProvider));
            this.clientEncryptionKeyPropertiesCacheByKeyId = new AsyncCache<string, ClientEncryptionKeyProperties>();
        }

        public EncryptionKeyStoreProvider EncryptionKeyStoreProvider { get; }

        public override CosmosClientOptions ClientOptions => this.cosmosClient.ClientOptions;

        public override CosmosResponseFactory ResponseFactory => this.cosmosClient.ResponseFactory;

        public override Uri Endpoint => this.cosmosClient.Endpoint;

        public override async Task<DatabaseResponse> CreateDatabaseAsync(
            string id,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            EncryptionDatabaseResponse encryptionDatabaseResponse = new EncryptionDatabaseResponse(
                await this.cosmosClient.CreateDatabaseAsync(
                    id,
                    throughput,
                    requestOptions,
                    cancellationToken),
                this);

            return encryptionDatabaseResponse;
        }

        public override async Task<DatabaseResponse> CreateDatabaseAsync(
            string id,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            EncryptionDatabaseResponse encryptionDatabaseResponse = new EncryptionDatabaseResponse(
                await this.cosmosClient.CreateDatabaseAsync(
                    id,
                    throughputProperties,
                    requestOptions,
                    cancellationToken),
                this);

            return encryptionDatabaseResponse;
        }

        public override async Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            EncryptionDatabaseResponse encryptionDatabaseResponse = new EncryptionDatabaseResponse(
                await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                    id,
                    throughputProperties,
                    requestOptions,
                    cancellationToken),
                this);

            return encryptionDatabaseResponse;
        }

        public override async Task<DatabaseResponse> CreateDatabaseIfNotExistsAsync(
            string id,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            EncryptionDatabaseResponse encryptionDatabaseResponse = new EncryptionDatabaseResponse(
                await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                    id,
                    throughput,
                    requestOptions,
                    cancellationToken),
                this);

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
            return new EncryptionContainer(
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

        public async Task<ClientEncryptionKeyProperties> GetClientEncryptionKeyPropertiesAsync(
            string clientEncryptionKeyId,
            EncryptionContainer encryptionContainer,
            string databaseRid,
            CancellationToken cancellationToken = default,
            bool shouldForceRefresh = false)
        {
            if (encryptionContainer == null)
            {
                throw new ArgumentNullException(nameof(encryptionContainer));
            }

            if (string.IsNullOrEmpty(databaseRid))
            {
                throw new ArgumentNullException(nameof(databaseRid));
            }

            if (string.IsNullOrEmpty(clientEncryptionKeyId))
            {
                throw new ArgumentNullException(nameof(clientEncryptionKeyId));
            }

            // Client Encryption key Id is unique within a Database.
            string cacheKey = databaseRid + "|" + clientEncryptionKeyId;

            return await this.clientEncryptionKeyPropertiesCacheByKeyId.GetAsync(
                     cacheKey,
                     obsoleteValue: null,
                     async () => await this.FetchClientEncryptionKeyPropertiesAsync(encryptionContainer, clientEncryptionKeyId, cancellationToken),
                     cancellationToken,
                     forceRefresh: shouldForceRefresh);
        }

        private async Task<ClientEncryptionKeyProperties> FetchClientEncryptionKeyPropertiesAsync(
            EncryptionContainer encryptionContainer,
            string clientEncryptionKeyId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ClientEncryptionKey clientEncryptionKey = encryptionContainer.Database.GetClientEncryptionKey(clientEncryptionKeyId);
            try
            {
                return await clientEncryptionKey.ReadAsync(cancellationToken: cancellationToken);
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new InvalidOperationException($"Encryption Based Container without Client Encryption Keys. Please make sure you have created the Client Encryption Keys:{ex.Message}. Please refer to https://aka.ms/CosmosClientEncryption for more details. ");
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
