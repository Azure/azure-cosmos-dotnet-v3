//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for reading or deleting an existing database.
    ///
    /// <see cref="CosmosClient"/> for or creating new databases, and reading/querying all databases; use `client.Databases`.
    /// </summary>
    internal class DatabaseCore : Database
    {
        /// <summary>
        /// Only used for unit testing
        /// </summary>
        internal DatabaseCore()
        {
        }

        internal DatabaseCore(
            CosmosClientContext clientContext,
            string databaseId)
        {
            this.Id = databaseId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: null,
                uriPathSegment: Paths.DatabasesPathSegment,
                id: databaseId);
        }

        public override string Id { get; }

        internal virtual Uri LinkUri { get; }

        internal CosmosClientContext ClientContext { get; }

        public override Task<DatabaseResponse> ReadAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> response = this.ReadStreamAsync(
                        requestOptions: requestOptions,
                        cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(this, response);
        }

        public override Task<DatabaseResponse> DeleteAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<ResponseMessage> response = this.DeleteStreamAsync(
                        requestOptions: requestOptions,
                        cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponseAsync(this, response);
        }

        public async override Task<int?> ReadThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ThroughputResponse response = await this.ReadThroughputIfExistsAsync(null, cancellationToken);
            return response.Resource?.Throughput;
        }

        public async override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReadThroughputAsync(
                targetRID: rid,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal async Task<ThroughputResponse> ReadThroughputIfExistsAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReadThroughputIfExistsAsync(targetRID: rid, requestOptions: requestOptions, cancellationToken: cancellationToken);
        }

        public async override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReplaceThroughputAsync(
                targetRID: rid,
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal async Task<ThroughputResponse> ReplaceThroughputIfExistsAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReplaceThroughputIfExistsAsync(
                targetRID: rid,
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<ResponseMessage> ReadStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                OperationType.Read,
                requestOptions,
                cancellationToken);
        }

        public override Task<ResponseMessage> DeleteStreamAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                OperationType.Delete,
                requestOptions,
                cancellationToken);
        }

        public override Task<ContainerResponse> CreateContainerAsync(
                    ContainerProperties containerProperties,
                    int? throughput = null,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            Task<ResponseMessage> response = this.CreateContainerStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(containerProperties),
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponseAsync(this.GetContainer(containerProperties.Id), response);
        }

        public override Task<ContainerResponse> CreateContainerAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(partitionKeyPath))
            {
                throw new ArgumentNullException(nameof(partitionKeyPath));
            }

            ContainerProperties containerProperties = new ContainerProperties(id, partitionKeyPath);

            return this.CreateContainerAsync(
                containerProperties,
                throughput,
                requestOptions,
                cancellationToken);
        }

        public override async Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            Container container = this.GetContainer(containerProperties.Id);
            ResponseMessage response = await container.ReadContainerStreamAsync(cancellationToken: cancellationToken);
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                ContainerResponse retrivedContainerResponse = await this.ClientContext.ResponseFactory.CreateContainerResponseAsync(container, Task.FromResult(response));
                if (!retrivedContainerResponse.Resource.PartitionKeyPath.Equals(containerProperties.PartitionKeyPath))
                {
                    throw new ArgumentException(
                        string.Format(
                            ClientResources.PartitionKeyPathConflict,
                            containerProperties.PartitionKeyPath,
                            containerProperties.Id,
                            retrivedContainerResponse.Resource.PartitionKeyPath),
                        nameof(containerProperties.PartitionKey));
                }

                return retrivedContainerResponse;
            }

            this.ValidateContainerProperties(containerProperties);
            response = await this.CreateContainerStreamAsync(containerProperties, throughput, requestOptions, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Conflict)
            {
                return await this.ClientContext.ResponseFactory.CreateContainerResponseAsync(container, Task.FromResult(response));
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            return await container.ReadContainerAsync(cancellationToken: cancellationToken);
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(partitionKeyPath))
            {
                throw new ArgumentNullException(nameof(partitionKeyPath));
            }

            ContainerProperties containerProperties = new ContainerProperties(id, partitionKeyPath);
            return this.CreateContainerIfNotExistsAsync(containerProperties, throughput, requestOptions, cancellationToken);
        }

        public override Container GetContainer(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new ContainerInlineCore(new ContainerCore(
                    this.ClientContext,
                    this,
                    id));
        }

        public override Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            Stream streamPayload = this.ClientContext.SerializerCore.ToStream(containerProperties);
            return this.CreateContainerStreamInternalAsync(streamPayload,
                throughput,
                requestOptions,
                cancellationToken);
        }

        public override Task<UserResponse> CreateUserAsync(
            string id,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            UserProperties userProperties = new UserProperties(id);

            Task<ResponseMessage> response = this.CreateUserStreamAsync(
                userProperties: userProperties,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this.GetUser(userProperties.Id), response);
        }

        public override User GetUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new UserInlineCore(new UserCore(
                    this.ClientContext,
                    this,
                    id));
        }

        public Task<ResponseMessage> CreateUserStreamAsync(
            UserProperties userProperties,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (userProperties == null)
            {
                throw new ArgumentNullException(nameof(userProperties));
            }

            this.ClientContext.ValidateResource(userProperties.Id);

            Stream streamPayload = this.ClientContext.SerializerCore.ToStream(userProperties);
            return this.ProcessUserCreateAsync(
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<UserResponse> UpsertUserAsync(string id,
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            this.ClientContext.ValidateResource(id);

            Task<ResponseMessage> response = this.ProcessUserUpsertAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(new UserProperties(id)),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this.GetUser(id), response);
        }

        public override FeedIterator GetContainerQueryStreamIterator(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetContainerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
            string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetContainerQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override FeedIterator GetContainerQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               this.ClientContext,
               this.LinkUri,
               ResourceType.Collection,
               queryDefinition,
               continuationToken,
               requestOptions);
        }

        public override FeedIterator<T> GetContainerQueryIterator<T>(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            if (!(this.GetContainerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions) is FeedIteratorInternal containerStreamIterator))
            {
                // This class should inherit from DatabaseInteral to avoid the downcasting hacks.
                throw new InvalidOperationException($"Expected FeedIteratorInternal.");
            }

            return new FeedIteratorCore<T>(
                containerStreamIterator,
                (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                    responseMessage: response,
                    resourceType: ResourceType.Collection));
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            if (!(this.GetUserQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions) is FeedIteratorInternal userStreamIterator))
            {
                // This class should inherit from DatabaseInteral to avoid the downcasting hacks.
                throw new InvalidOperationException($"Expected FeedIteratorInternal.");
            }

            return new FeedIteratorCore<T>(
                userStreamIterator,
                (response) => this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>(
                    responseMessage: response,
                    resourceType: ResourceType.User));
        }

        public FeedIterator GetUserQueryStreamIterator(QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               this.ClientContext,
               this.LinkUri,
               ResourceType.User,
               queryDefinition,
               continuationToken,
               requestOptions);
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetUserQueryIterator<T>(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public FeedIterator GetUserQueryStreamIterator(string queryText = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            QueryDefinition queryDefinition = null;
            if (queryText != null)
            {
                queryDefinition = new QueryDefinition(queryText);
            }

            return this.GetUserQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);
        }

        public override ContainerBuilder DefineContainer(
            string name,
            string partitionKeyPath)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (string.IsNullOrEmpty(partitionKeyPath))
            {
                throw new ArgumentNullException(nameof(partitionKeyPath));
            }

            return new ContainerBuilder(this, this.ClientContext, name, partitionKeyPath);
        }

#if PREVIEW
        public override
#else
        internal virtual
#endif
        DataEncryptionKey GetDataEncryptionKey(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new DataEncryptionKeyInlineCore(
                new DataEncryptionKeyCore(
                    this.ClientContext,
                    this,
                    id));
        }

#if PREVIEW
        public override
#else
        internal virtual
#endif
            FeedIterator<DataEncryptionKeyProperties> GetDataEncryptionKeyIterator(
                string startId = null,
                string endId = null,
                bool isDescending = false,
                string continuationToken = null,
                QueryRequestOptions requestOptions = null)
        {
            if (!(this.GetDataEncryptionKeyStreamIterator(
                startId,
                endId,
                isDescending,
                continuationToken,
                requestOptions) is FeedIteratorInternal dekStreamIterator))
            {
                throw new InvalidOperationException($"Expected FeedIteratorInternal.");
            }

            return new FeedIteratorCore<DataEncryptionKeyProperties>(
                dekStreamIterator,
                (responseMessage) =>
                {
                    FeedResponse<DataEncryptionKeyProperties> results = this.ClientContext.ResponseFactory.CreateQueryFeedResponse<DataEncryptionKeyProperties>(responseMessage, ResourceType.ClientEncryptionKey);
                    foreach (DataEncryptionKeyProperties result in results)
                    {
                        Uri dekUri = DataEncryptionKeyCore.CreateLinkUri(this.ClientContext, this, result.Id);
                        this.ClientContext.DekCache.Set(this.Id, dekUri, result);
                    }

                    return results;
                });
        }

        internal FeedIterator GetDataEncryptionKeyStreamIterator(
            string startId = null,
            string endId = null,
            bool isDescending = false,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            if (startId != null || endId != null)
            {
                if (requestOptions == null)
                {
                    requestOptions = new QueryRequestOptions();
                }

                requestOptions.StartId = startId;
                requestOptions.EndId = endId;
                requestOptions.EnumerationDirection = isDescending ? EnumerationDirection.Reverse : EnumerationDirection.Forward;
            }

            return new FeedIteratorCore(
                clientContext: this.ClientContext,
                resourceLink: this.LinkUri,
                resourceType: ResourceType.ClientEncryptionKey,
                queryDefinition: null,
                continuationToken: continuationToken,
                options: requestOptions,
                usePropertySerializer: true);
        }

#if PREVIEW
        public override
#else
        internal virtual
#endif
            async Task<DataEncryptionKeyResponse> CreateDataEncryptionKeyAsync(
                string id,
                CosmosEncryptionAlgorithm encryptionAlgorithmId,
                EncryptionKeyWrapMetadata encryptionKeyWrapMetadata,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (encryptionAlgorithmId != CosmosEncryptionAlgorithm.AE_AES_256_CBC_HMAC_SHA_256_RANDOMIZED)
            {
                throw new ArgumentException(string.Format("Unsupported Encryption Algorithm {0}", encryptionAlgorithmId), nameof(encryptionAlgorithmId));
            }

            if (encryptionKeyWrapMetadata == null)
            {
                throw new ArgumentNullException(nameof(encryptionKeyWrapMetadata));
            }

            this.ClientContext.ValidateResource(id);

            DataEncryptionKeyCore newDek = (DataEncryptionKeyInlineCore)this.GetDataEncryptionKey(id);

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(requestOptions);

            byte[] rawDek = newDek.GenerateKey(encryptionAlgorithmId);

            (byte[] wrappedDek, EncryptionKeyWrapMetadata updatedMetadata, InMemoryRawDek inMemoryRawDek) = await newDek.WrapAsync(
                rawDek,
                encryptionAlgorithmId,
                encryptionKeyWrapMetadata,
                diagnosticsContext,
                cancellationToken);

            DataEncryptionKeyProperties dekProperties = new DataEncryptionKeyProperties(id, encryptionAlgorithmId, wrappedDek, updatedMetadata);
            Stream streamPayload = this.ClientContext.SerializerCore.ToStream(dekProperties);
            Task<ResponseMessage> responseMessage = this.CreateDataEncryptionKeyStreamAsync(
                streamPayload,
                requestOptions,
                cancellationToken);

            DataEncryptionKeyResponse dekResponse = await this.ClientContext.ResponseFactory.CreateDataEncryptionKeyResponseAsync(newDek, responseMessage);
            Debug.Assert(dekResponse.Resource != null);

            this.ClientContext.DekCache.Set(this.Id, newDek.LinkUri, dekResponse.Resource);
            this.ClientContext.DekCache.SetRawDek(dekResponse.Resource.SelfLink, inMemoryRawDek);
            return dekResponse;
        }

        internal void ValidateContainerProperties(ContainerProperties containerProperties)
        {
            containerProperties.ValidateRequiredProperties();
            this.ClientContext.ValidateResource(containerProperties.Id);
        }

        internal Task<ResponseMessage> ProcessCollectionCreateAsync(
            Stream streamPayload,
            int? throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.Collection,
               operationType: OperationType.Create,
               cosmosContainerCore: null,
               partitionKey: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputHeader(throughput),
               diagnosticsScope: null,
               cancellationToken: cancellationToken);
        }

        internal Task<ResponseMessage> ProcessUserCreateAsync(
            Stream streamPayload,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.User,
               operationType: OperationType.Create,
               cosmosContainerCore: null,
               partitionKey: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: null,
               diagnosticsScope: null,
               cancellationToken: cancellationToken);
        }

        internal Task<ResponseMessage> ProcessUserUpsertAsync(
            Stream streamPayload,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.User,
               operationType: OperationType.Upsert,
               cosmosContainerCore: null,
               partitionKey: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: null,
               diagnosticsScope: null,
               cancellationToken: cancellationToken);
        }

        internal virtual async Task<string> GetRIDAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            DatabaseResponse databaseResponse = await this.ReadAsync(cancellationToken: cancellationToken);
            return databaseResponse?.Resource?.ResourceId;
        }

        private Task<ResponseMessage> CreateContainerStreamInternalAsync(
            Stream streamPayload,
            int? throughput = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessCollectionCreateAsync(
                streamPayload: streamPayload,
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> CreateDataEncryptionKeyStreamAsync(
            Stream streamPayload,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (streamPayload == null)
            {
                throw new ArgumentNullException(nameof(streamPayload));
            }

            return this.ClientContext.ProcessResourceOperationStreamAsync(
                resourceUri: this.LinkUri,
                resourceType: ResourceType.ClientEncryptionKey,
                operationType: OperationType.Create,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                requestEnricher: null,
                diagnosticsScope: null,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessAsync(
            OperationType operationType,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessResourceOperationStreamAsync(
                streamPayload: null,
                operationType: operationType,
                linkUri: this.LinkUri,
                resourceType: ResourceType.Database,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
           Stream streamPayload,
           OperationType operationType,
           Uri linkUri,
           ResourceType resourceType,
           RequestOptions requestOptions = null,
           CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
              resourceUri: linkUri,
              resourceType: resourceType,
              operationType: operationType,
              cosmosContainerCore: null,
              partitionKey: null,
              streamPayload: streamPayload,
              requestOptions: requestOptions,
              requestEnricher: null,
              diagnosticsScope: null,
              cancellationToken: cancellationToken);
        }
    }
}
