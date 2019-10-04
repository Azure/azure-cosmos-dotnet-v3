//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
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

        public override Task<Response<DatabaseProperties>> ReadAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<Response> response = this.ReadStreamAsync(
                        requestOptions: requestOptions,
                        cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<DatabaseProperties>(response, cancellationToken);
        }

        public override Task<Response<DatabaseProperties>> DeleteAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<Response> response = this.DeleteStreamAsync(
                        requestOptions: requestOptions,
                        cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateItemResponseAsync<DatabaseProperties>(response, cancellationToken);
        }

        //public async override Task<int?> ReadThroughputAsync(
        //    CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    Response<ThroughputProperties> response = await this.ReadThroughputIfExistsAsync(null, cancellationToken);
        //    return response.Value?.Throughput;
        //}

        //public async override Task<Response<ThroughputProperties>> ReadThroughputAsync(
        //    RequestOptions requestOptions,
        //    CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    string rid = await this.GetRIDAsync(cancellationToken);
        //    CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
        //    return await cosmosOffers.ReadThroughputAsync(
        //        targetRID: rid,
        //        requestOptions: requestOptions,
        //        cancellationToken: cancellationToken);
        //}

        //internal async Task<ThroughputResponse> ReadThroughputIfExistsAsync(
        //    RequestOptions requestOptions,
        //    CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    string rid = await this.GetRIDAsync(cancellationToken);
        //    CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
        //    return await cosmosOffers.ReadThroughputIfExistsAsync(targetRID: rid, requestOptions: requestOptions, cancellationToken: cancellationToken);
        //}

        //public async override Task<ThroughputResponse> ReplaceThroughputAsync(
        //    int throughput,
        //    RequestOptions requestOptions = null,
        //    CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    string rid = await this.GetRIDAsync(cancellationToken);
        //    CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
        //    return await cosmosOffers.ReplaceThroughputAsync(
        //        targetRID: rid,
        //        throughput: throughput,
        //        requestOptions: requestOptions,
        //        cancellationToken: cancellationToken);
        //}

        //internal async Task<ThroughputResponse> ReplaceThroughputIfExistsAsync(
        //    int throughput,
        //    RequestOptions requestOptions = null,
        //    CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    string rid = await this.GetRIDAsync(cancellationToken);
        //    CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
        //    return await cosmosOffers.ReplaceThroughputIfExistsAsync(
        //        targetRID: rid,
        //        throughput: throughput,
        //        requestOptions: requestOptions,
        //        cancellationToken: cancellationToken);
        //}

        public override Task<Response> ReadStreamAsync(
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                OperationType.Read,
                requestOptions,
                cancellationToken);
        }

        public override Task<Response> DeleteStreamAsync(
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsync(
                OperationType.Delete,
                requestOptions,
                cancellationToken);
        }

        public override async Task<Response<ContainerProperties>> CreateContainerAsync(
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

            Task<Response> response = this.CreateContainerStreamInternalAsync(
                streamPayload: await this.ClientContext.PropertiesSerializer.ToStreamAsync(containerProperties, cancellationToken),
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return await this.ClientContext.ResponseFactory.CreateItemResponseAsync<ContainerProperties>(response, cancellationToken);
        }

        public override Task<Response<ContainerProperties>> CreateContainerAsync(
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

        public override async Task<Response<ContainerProperties>> CreateContainerIfNotExistsAsync(
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
            Response response = await container.ReadContainerStreamAsync(cancellationToken: cancellationToken);
            if (response.Status != (int)HttpStatusCode.NotFound)
            {
                Response<ContainerProperties> retrivedContainerResponse = await this.ClientContext.ResponseFactory.CreateItemResponseAsync<ContainerProperties>(Task.FromResult(response), cancellationToken);
                if (!retrivedContainerResponse.Value.PartitionKeyPath.Equals(containerProperties.PartitionKeyPath))
                {
                    throw new ArgumentException(
                        string.Format(
                            ClientResources.PartitionKeyPathConflict,
                            containerProperties.PartitionKeyPath,
                            containerProperties.Id,
                            retrivedContainerResponse.Value.PartitionKeyPath),
                        nameof(containerProperties.PartitionKey));
                }

                return retrivedContainerResponse;
            }

            this.ValidateContainerProperties(containerProperties);
            response = await this.CreateContainerStreamAsync(containerProperties, throughput, requestOptions, cancellationToken);
            if (response.Status != (int)HttpStatusCode.Conflict)
            {
                return await this.ClientContext.ResponseFactory.CreateItemResponseAsync<ContainerProperties>(Task.FromResult(response), cancellationToken);
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            return await container.ReadContainerAsync(cancellationToken: cancellationToken);
        }

        public override Task<Response<ContainerProperties>> CreateContainerIfNotExistsAsync(
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

            return new ContainerCore(
                    this.ClientContext,
                    this,
                    id);
        }

        public override async Task<Response> CreateContainerStreamAsync(
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

            Stream streamPayload = await this.ClientContext.PropertiesSerializer.ToStreamAsync(containerProperties, cancellationToken);
            return await this.CreateContainerStreamInternalAsync(streamPayload,
                throughput,
                requestOptions,
                cancellationToken);
        }

        //public override Task<UserResponse> CreateUserAsync(
        //    string id,
        //    RequestOptions requestOptions = null,
        //    CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    if (string.IsNullOrEmpty(id))
        //    {
        //        throw new ArgumentNullException(nameof(id));
        //    }

        //    UserProperties userProperties = new UserProperties(id);

        //    Task<ResponseMessage> response = this.CreateUserStreamAsync(
        //        userProperties: userProperties,
        //        requestOptions: requestOptions,
        //        cancellationToken: cancellationToken);

        //    return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this.GetUser(userProperties.Id), response);
        //}

        //public override User GetUser(string id)
        //{
        //    if (string.IsNullOrEmpty(id))
        //    {
        //        throw new ArgumentNullException(nameof(id));
        //    }

        //    return new UserCore(
        //            this.ClientContext,
        //            this,
        //            id);
        //}

        //public Task<ResponseMessage> CreateUserStreamAsync(
        //    UserProperties userProperties,
        //    RequestOptions requestOptions = null,
        //    CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    if (userProperties == null)
        //    {
        //        throw new ArgumentNullException(nameof(userProperties));
        //    }

        //    this.ClientContext.ValidateResource(userProperties.Id);

        //    Stream streamPayload = this.ClientContext.PropertiesSerializer.ToStream(userProperties);
        //    return this.ProcessUserCreateAsync(
        //        streamPayload: streamPayload,
        //        requestOptions: requestOptions,
        //        cancellationToken: cancellationToken);
        //}

        //public override Task<UserResponse> UpsertUserAsync(string id,
        //    RequestOptions requestOptions,
        //    CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    if (string.IsNullOrEmpty(id))
        //    {
        //        throw new ArgumentNullException(nameof(id));
        //    }

        //    this.ClientContext.ValidateResource(id);

        //    Task<ResponseMessage> response = this.ProcessUserUpsertAsync(
        //        streamPayload: this.ClientContext.PropertiesSerializer.ToStream(new UserProperties(id)),
        //        requestOptions: requestOptions,
        //        cancellationToken: cancellationToken);

        //    return this.ClientContext.ResponseFactory.CreateUserResponseAsync(this.GetUser(id), response);
        //}

        //public override FeedIterator GetContainerQueryStreamIterator(
        //    string queryText = null,
        //    string continuationToken = null,
        //    QueryRequestOptions requestOptions = null)
        //{
        //    QueryDefinition queryDefinition = null;
        //    if (queryText != null)
        //    {
        //        queryDefinition = new QueryDefinition(queryText);
        //    }

        //    return this.GetContainerQueryStreamIterator(
        //        queryDefinition,
        //        continuationToken,
        //        requestOptions);
        //}

        //public override FeedIterator<T> GetContainerQueryIterator<T>(
        //    string queryText = null,
        //    string continuationToken = null,
        //    QueryRequestOptions requestOptions = null)
        //{
        //    QueryDefinition queryDefinition = null;
        //    if (queryText != null)
        //    {
        //        queryDefinition = new QueryDefinition(queryText);
        //    }

        //    return this.GetContainerQueryIterator<T>(
        //        queryDefinition,
        //        continuationToken,
        //        requestOptions);
        //}

        //public override FeedIterator GetContainerQueryStreamIterator(
        //    QueryDefinition queryDefinition,
        //    string continuationToken = null,
        //    QueryRequestOptions requestOptions = null)
        //{
        //    return new FeedIteratorCore(
        //       this.ClientContext,
        //       this.LinkUri,
        //       ResourceType.Collection,
        //       queryDefinition,
        //       continuationToken,
        //       requestOptions);
        //}

        //public override FeedIterator<T> GetContainerQueryIterator<T>(
        //    QueryDefinition queryDefinition,
        //    string continuationToken = null,
        //    QueryRequestOptions requestOptions = null)
        //{
        //    FeedIterator containerStreamIterator = this.GetContainerQueryStreamIterator(
        //        queryDefinition,
        //        continuationToken,
        //        requestOptions);

        //    return new FeedIteratorCore<T>(
        //        containerStreamIterator,
        //        this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>);
        //}

        //public override FeedIterator<T> GetUserQueryIterator<T>(QueryDefinition queryDefinition,
        //    string continuationToken = null,
        //    QueryRequestOptions requestOptions = null)
        //{
        //    FeedIterator userStreamIterator = this.GetUserQueryStreamIterator(
        //        queryDefinition,
        //        continuationToken,
        //        requestOptions);

        //    return new FeedIteratorCore<T>(
        //        userStreamIterator,
        //        this.ClientContext.ResponseFactory.CreateQueryFeedResponse<T>);
        //}

        //public FeedIterator GetUserQueryStreamIterator(QueryDefinition queryDefinition,
        //    string continuationToken = null,
        //    QueryRequestOptions requestOptions = null)
        //{
        //    return new FeedIteratorCore(
        //       this.ClientContext,
        //       this.LinkUri,
        //       ResourceType.User,
        //       queryDefinition,
        //       continuationToken,
        //       requestOptions);
        //}

        //public override FeedIterator<T> GetUserQueryIterator<T>(string queryText = null,
        //    string continuationToken = null,
        //    QueryRequestOptions requestOptions = null)
        //{
        //    QueryDefinition queryDefinition = null;
        //    if (queryText != null)
        //    {
        //        queryDefinition = new QueryDefinition(queryText);
        //    }

        //    return this.GetUserQueryIterator<T>(
        //        queryDefinition,
        //        continuationToken,
        //        requestOptions);
        //}

        //public FeedIterator GetUserQueryStreamIterator(string queryText = null,
        //    string continuationToken = null,
        //    QueryRequestOptions requestOptions = null)
        //{
        //    QueryDefinition queryDefinition = null;
        //    if (queryText != null)
        //    {
        //        queryDefinition = new QueryDefinition(queryText);
        //    }

        //    return this.GetUserQueryStreamIterator(
        //        queryDefinition,
        //        continuationToken,
        //        requestOptions);
        //}

        //public override ContainerBuilder DefineContainer(
        //    string name,
        //    string partitionKeyPath)
        //{
        //    if (string.IsNullOrEmpty(name))
        //    {
        //        throw new ArgumentNullException(nameof(name));
        //    }

        //    if (string.IsNullOrEmpty(partitionKeyPath))
        //    {
        //        throw new ArgumentNullException(nameof(partitionKeyPath));
        //    }

        //    return new ContainerBuilder(this, this.ClientContext, name, partitionKeyPath);
        //}

        internal void ValidateContainerProperties(ContainerProperties containerProperties)
        {
            containerProperties.ValidateRequiredProperties();
            this.ClientContext.ValidateResource(containerProperties.Id);
        }

        internal Task<Response> ProcessCollectionCreateAsync(
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
               cancellationToken: cancellationToken);
        }

        internal Task<Response> ProcessUserCreateAsync(
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
               cancellationToken: cancellationToken);
        }

        internal Task<Response> ProcessUserUpsertAsync(
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
               cancellationToken: cancellationToken);
        }

        internal virtual async Task<string> GetRIDAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Response<DatabaseProperties> databaseResponse = await this.ReadAsync(cancellationToken: cancellationToken);
            return databaseResponse.Value?.ResourceId;
        }

        private Task<Response> CreateContainerStreamInternalAsync(
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

        private Task<Response> ProcessAsync(
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

        private Task<Response> ProcessResourceOperationStreamAsync(
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
              cancellationToken: cancellationToken);
        }
    }
}
