//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

#if PREVIEW
    using System.Collections.Generic;
#endif

    /// <summary>
    /// Operations for reading or deleting an existing database.
    ///
    /// <see cref="Client"/> for or creating new databases, and reading/querying all databases; use `client.Databases`.
    /// </summary>
    internal abstract class DatabaseCore : DatabaseInternal
    {
        protected DatabaseCore(
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

        public override CosmosClient Client => this.ClientContext.Client;

        internal override string LinkUri { get; }

        internal override CosmosClientContext ClientContext { get; }

        public async Task<DatabaseResponse> ReadAsync(
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ReadStreamAsync(
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponse(this, response);
        }

        public async Task<DatabaseResponse> DeleteAsync(
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.DeleteStreamAsync(
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponse(this, response);
        }

        public async Task<int?> ReadThroughputAsync(
            ITrace trace,
            CancellationToken cancellationToken)
        {
            ThroughputResponse response = await this.ReadThroughputIfExistsAsync(null, cancellationToken);
            return response.Resource?.Throughput;
        }

        public async Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReadThroughputAsync(
                targetRID: rid,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal override async Task<ThroughputResponse> ReadThroughputIfExistsAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReadThroughputIfExistsAsync(targetRID: rid, requestOptions: requestOptions, cancellationToken: cancellationToken);
        }

        public async Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReplaceThroughputAsync(
                targetRID: rid,
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal override async Task<ThroughputResponse> ReplaceThroughputIfExistsAsync(
            int throughput,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReplaceThroughputIfExistsAsync(
                targetRID: rid,
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            return this.ProcessCollectionCreateAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(containerProperties),
                throughputProperties: throughputProperties,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<ContainerResponse> CreateContainerAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken = default)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            ResponseMessage response = await this.ProcessCollectionCreateAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(containerProperties),
                throughputProperties: throughputProperties,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponse(this.GetContainer(containerProperties.Id), response);
        }

        public async Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            double totalRequestCharge = 0;
            ContainerCore container = (ContainerCore)this.GetContainer(containerProperties.Id);
            using (ResponseMessage readResponse = await container.ReadContainerStreamAsync(
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken))
            {
                totalRequestCharge = readResponse.Headers.RequestCharge;

                if (readResponse.StatusCode != HttpStatusCode.NotFound)
                {
                    ContainerResponse retrivedContainerResponse = this.ClientContext.ResponseFactory.CreateContainerResponse(
                        container,
                        readResponse);

                    if (containerProperties.PartitionKey.Kind != Documents.PartitionKind.MultiHash)
                    {
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
                    }
#if PREVIEW
                    else
                    {
                        IReadOnlyList<string> retrivedPartitionKeyPaths = retrivedContainerResponse.Resource.PartitionKeyPaths;
                        IReadOnlyList<string> receivedPartitionKeyPaths = containerProperties.PartitionKeyPaths;
                        
                        if (retrivedPartitionKeyPaths.Count != receivedPartitionKeyPaths.Count || !Enumerable.SequenceEqual(retrivedPartitionKeyPaths, receivedPartitionKeyPaths))
                        {
                            throw new ArgumentException(
                                string.Format(
                                    ClientResources.PartitionKeyPathConflict,
                                    string.Join(",", containerProperties.PartitionKeyPaths),
                                    containerProperties.Id,
                                    string.Join(",", retrivedContainerResponse.Resource.PartitionKeyPaths)),
                                nameof(containerProperties.PartitionKey));
                        }
                    }
#endif
                    return retrivedContainerResponse;
                }
            }

            this.ValidateContainerProperties(containerProperties);
            using (ResponseMessage createResponse = await this.CreateContainerStreamAsync(
                containerProperties,
                throughputProperties,
                requestOptions,
                trace,
                cancellationToken))
            {
                totalRequestCharge += createResponse.Headers.RequestCharge;
                createResponse.Headers.RequestCharge = totalRequestCharge;

                if (createResponse.StatusCode != HttpStatusCode.Conflict)
                {
                    return this.ClientContext.ResponseFactory.CreateContainerResponse(container, createResponse);
                }
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            using (ResponseMessage readResponseAfterCreate = await container.ReadContainerStreamAsync(
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken))
            {
                totalRequestCharge += readResponseAfterCreate.Headers.RequestCharge;
                readResponseAfterCreate.Headers.RequestCharge = totalRequestCharge;

                return this.ClientContext.ResponseFactory.CreateContainerResponse(container, readResponseAfterCreate);
            }
        }

        public async Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken = default)
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReplaceThroughputPropertiesAsync(
                targetRID: rid,
                throughputProperties: throughputProperties,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal override async Task<ThroughputResponse> ReplaceThroughputPropertiesIfExistsAsync(
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReplaceThroughputPropertiesIfExistsAsync(
                targetRID: rid,
                throughputProperties: throughputProperties,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<ResponseMessage> ReadStreamAsync(
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ProcessResourceOperationStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                linkUri: this.LinkUri,
                resourceType: ResourceType.Database,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public Task<ResponseMessage> DeleteStreamAsync(
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ProcessResourceOperationStreamAsync(
                streamPayload: null,
                operationType: OperationType.Delete,
                linkUri: this.LinkUri,
                resourceType: ResourceType.Database,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<ContainerResponse> CreateContainerAsync(
            ContainerProperties containerProperties,
            int? throughput,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            ResponseMessage response = await this.ProcessCollectionCreateAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(containerProperties),
                throughput: throughput,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponse(this.GetContainer(containerProperties.Id), response);
        }

        public Task<ContainerResponse> CreateContainerAsync(
            string id,
            string partitionKeyPath,
            int? throughput,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
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
                trace,
                cancellationToken);
        }

        public Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            ContainerProperties containerProperties,
            int? throughput,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            return this.CreateContainerIfNotExistsAsync(
                containerProperties,
                ThroughputProperties.CreateManualThroughput(throughput),
                requestOptions,
                trace,
                cancellationToken);
        }

        public Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? throughput,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
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
            return this.CreateContainerIfNotExistsAsync(containerProperties, throughput, requestOptions, trace, cancellationToken);
        }

        public override Container GetContainer(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new ContainerInlineCore(
                    this.ClientContext,
                    this,
                    id);
        }

        public Task<ResponseMessage> CreateContainerStreamAsync(
            ContainerProperties containerProperties,
            int? throughput,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            Stream streamPayload = this.ClientContext.SerializerCore.ToStream(containerProperties);
            return this.ProcessCollectionCreateAsync(
                streamPayload,
                throughput,
                requestOptions,
                trace,
                cancellationToken);
        }

        public async Task<UserResponse> CreateUserAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            UserProperties userProperties = new UserProperties(id);

            ResponseMessage response = await this.CreateUserStreamAsync(
                userProperties: userProperties,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponse(this.GetUser(userProperties.Id), response);
        }

        public override User GetUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new UserInlineCore(
                    this.ClientContext,
                    this,
                    id);
        }

        public Task<ResponseMessage> CreateUserStreamAsync(
            UserProperties userProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
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
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<UserResponse> UpsertUserAsync(
            string id,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            this.ClientContext.ValidateResource(id);

            ResponseMessage response = await this.ProcessUserUpsertAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(new UserProperties(id)),
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateUserResponse(this.GetUser(id), response);
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
               clientContext: this.ClientContext,
               resourceLink: this.LinkUri,
               resourceType: ResourceType.Collection,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
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

        public override FeedIterator<T> GetUserQueryIterator<T>(
            QueryDefinition queryDefinition,
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

        public override FeedIterator GetUserQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
               clientContext: this.ClientContext,
               resourceLink: this.LinkUri,
               resourceType: ResourceType.User,
               queryDefinition: queryDefinition,
               continuationToken: continuationToken,
               options: requestOptions);
        }

        public override FeedIterator<T> GetUserQueryIterator<T>(
            string queryText = null,
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

        public override FeedIterator GetUserQueryStreamIterator(
            string queryText = null,
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
            ClientEncryptionKey GetClientEncryptionKey(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new ClientEncryptionKeyInlineCore(
                    this.ClientContext,
                    this,
                    id);
        }

#if PREVIEW
        public override
#else
        internal virtual
#endif
            FeedIterator<ClientEncryptionKeyProperties> GetClientEncryptionKeyQueryIterator(
                QueryDefinition queryDefinition,
                string continuationToken = null,
                QueryRequestOptions requestOptions = null)
        {
            if (!(this.GetClientEncryptionKeyQueryStreamIterator(
                    queryDefinition: queryDefinition,
                    continuationToken: continuationToken,
                    requestOptions: requestOptions) is FeedIteratorInternal cekStreamIterator))
            {
                throw new InvalidOperationException($"Expected FeedIteratorInternal.");
            }

            return new FeedIteratorCore<ClientEncryptionKeyProperties>(
                cekStreamIterator,
                (responseMessage) =>
                {
                    FeedResponse<ClientEncryptionKeyProperties> results = this.ClientContext.ResponseFactory.CreateQueryFeedResponse<ClientEncryptionKeyProperties>(responseMessage, ResourceType.ClientEncryptionKey);
                    return results;
                });
        }

        private FeedIterator GetClientEncryptionKeyQueryStreamIterator(
            QueryDefinition queryDefinition,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
                clientContext: this.ClientContext,
                resourceLink: this.LinkUri,
                resourceType: ResourceType.ClientEncryptionKey,
                queryDefinition: queryDefinition,
                continuationToken: continuationToken,
                options: requestOptions);
        }

#if PREVIEW
        public
#else
        internal virtual
#endif
            async Task<ClientEncryptionKeyResponse> CreateClientEncryptionKeyAsync(
                ITrace trace,
                ClientEncryptionKeyProperties clientEncryptionKeyProperties,
                RequestOptions requestOptions = null,
                CancellationToken cancellationToken = default)
        {
            Stream streamPayload = this.ClientContext.SerializerCore.ToStream(clientEncryptionKeyProperties);
            ResponseMessage responseMessage = await this.CreateClientEncryptionKeyStreamAsync(
                trace: trace,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            ClientEncryptionKeyResponse cekResponse = this.ClientContext.ResponseFactory.CreateClientEncryptionKeyResponse(
                this.GetClientEncryptionKey(clientEncryptionKeyProperties.Id),
                responseMessage);

            Debug.Assert(cekResponse.Resource != null);

            return cekResponse;
        }

        private void ValidateContainerProperties(ContainerProperties containerProperties)
        {
            containerProperties.ValidateRequiredProperties();
            this.ClientContext.ValidateResource(containerProperties.Id);
        }

        private Task<ResponseMessage> ProcessCollectionCreateAsync(
            Stream streamPayload,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.Collection,
               operationType: OperationType.Create,
               cosmosContainerCore: null,
               feedRange: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputPropertiesHeader(throughputProperties),
               trace: trace,
               cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessCollectionCreateAsync(
            Stream streamPayload,
            int? throughput,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.Collection,
               operationType: OperationType.Create,
               cosmosContainerCore: null,
               feedRange: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputHeader(throughput),
               trace: trace,
               cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessUserCreateAsync(
            Stream streamPayload,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.User,
               operationType: OperationType.Create,
               cosmosContainerCore: null,
               feedRange: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: null,
               trace: trace,
               cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessUserUpsertAsync(
            Stream streamPayload,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.User,
               operationType: OperationType.Upsert,
               cosmosContainerCore: null,
               feedRange: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: null,
               trace: trace,
               cancellationToken: cancellationToken);
        }

        internal override async Task<string> GetRIDAsync(CancellationToken cancellationToken)
        {
            DatabaseResponse databaseResponse = await this.ReadAsync(cancellationToken: cancellationToken);
            return databaseResponse?.Resource?.ResourceId;
        }

        private Task<ResponseMessage> CreateClientEncryptionKeyStreamAsync(
            ITrace trace,
            Stream streamPayload,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
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
                feedRange: null,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                requestEnricher: null,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
           Stream streamPayload,
           OperationType operationType,
           string linkUri,
           ResourceType resourceType,
           RequestOptions requestOptions,
           ITrace trace,
           CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
              resourceUri: linkUri,
              resourceType: resourceType,
              operationType: operationType,
              cosmosContainerCore: null,
              feedRange: null,
              streamPayload: streamPayload,
              requestOptions: requestOptions,
              requestEnricher: null,
              trace: trace,
              cancellationToken: cancellationToken);
        }
    }
}
