//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;

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
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.ReadStreamAsync(
                diagnosticsContext: diagnosticsContext,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponse(this, response);
        }

        public async Task<DatabaseResponse> DeleteAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            ResponseMessage response = await this.DeleteStreamAsync(
                diagnosticsContext: diagnosticsContext,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateDatabaseResponse(this, response);
        }

        public async Task<int?> ReadThroughputAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            CancellationToken cancellationToken)
        {
            ThroughputResponse response = await this.ReadThroughputIfExistsAsync(null, cancellationToken);
            return response.Resource?.Throughput;
        }

        public async Task<ThroughputResponse> ReadThroughputAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
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
            CosmosDiagnosticsContext diagnosticsContext,
            int throughput,
            RequestOptions requestOptions,
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
            CosmosDiagnosticsContext diagnosticsContext,
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            return this.ProcessCollectionCreateAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: this.ClientContext.SerializerCore.ToStream(containerProperties),
                throughputProperties: throughputProperties,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public async Task<ContainerResponse> CreateContainerAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            ResponseMessage response = await this.ProcessCollectionCreateAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: this.ClientContext.SerializerCore.ToStream(containerProperties),
                throughputProperties: throughputProperties,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponse(this.GetContainer(containerProperties.Id), response);
        }

        public async Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            ContainerProperties containerProperties,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            ContainerCore container = (ContainerCore)this.GetContainer(containerProperties.Id);
            using (ResponseMessage readResponse = await container.ReadContainerStreamAsync(
                diagnosticsContext: diagnosticsContext,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken))
            {
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
                diagnosticsContext,
                containerProperties,
                throughputProperties,
                requestOptions,
                cancellationToken))
            {
                if (createResponse.StatusCode != HttpStatusCode.Conflict)
                {
                    return this.ClientContext.ResponseFactory.CreateContainerResponse(container, createResponse);
                }
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            using (ResponseMessage readResponseAfterCreate = await container.ReadContainerStreamAsync(
                diagnosticsContext: diagnosticsContext,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken))
            {
                return this.ClientContext.ResponseFactory.CreateContainerResponse(container, readResponseAfterCreate);
            }
        }

        public async Task<ThroughputResponse> ReplaceThroughputAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
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
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessResourceOperationStreamAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: null,
                operationType: OperationType.Read,
                linkUri: this.LinkUri,
                resourceType: ResourceType.Database,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public Task<ResponseMessage> DeleteStreamAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ProcessResourceOperationStreamAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: null,
                operationType: OperationType.Delete,
                linkUri: this.LinkUri,
                resourceType: ResourceType.Database,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public async Task<ContainerResponse> CreateContainerAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            ContainerProperties containerProperties,
            int? throughput,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            ResponseMessage response = await this.ProcessCollectionCreateAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: this.ClientContext.SerializerCore.ToStream(containerProperties),
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponse(this.GetContainer(containerProperties.Id), response);
        }

        public Task<ContainerResponse> CreateContainerAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            string partitionKeyPath,
            int? throughput,
            RequestOptions requestOptions,
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
                diagnosticsContext,
                containerProperties,
                throughput,
                requestOptions,
                cancellationToken);
        }

        public Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            ContainerProperties containerProperties,
            int? throughput,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            return this.CreateContainerIfNotExistsAsync(
                diagnosticsContext: diagnosticsContext,
                containerProperties,
                ThroughputProperties.CreateManualThroughput(throughput),
                requestOptions,
                cancellationToken);
        }

        public Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            string partitionKeyPath,
            int? throughput,
            RequestOptions requestOptions,
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
            return this.CreateContainerIfNotExistsAsync(diagnosticsContext, containerProperties, throughput, requestOptions, cancellationToken);
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
            CosmosDiagnosticsContext diagnosticsContext,
            ContainerProperties containerProperties,
            int? throughput,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ValidateContainerProperties(containerProperties);

            Stream streamPayload = this.ClientContext.SerializerCore.ToStream(containerProperties);
            return this.ProcessCollectionCreateAsync(
                diagnosticsContext,
                streamPayload,
                throughput,
                requestOptions,
                cancellationToken);
        }

        public async Task<UserResponse> CreateUserAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            UserProperties userProperties = new UserProperties(id);

            ResponseMessage response = await this.CreateUserStreamAsync(
                diagnosticsContext: diagnosticsContext,
                userProperties: userProperties,
                requestOptions: requestOptions,
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
            CosmosDiagnosticsContext diagnosticsContext,
            UserProperties userProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (userProperties == null)
            {
                throw new ArgumentNullException(nameof(userProperties));
            }

            this.ClientContext.ValidateResource(userProperties.Id);

            Stream streamPayload = this.ClientContext.SerializerCore.ToStream(userProperties);
            return this.ProcessUserCreateAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: streamPayload,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public async Task<UserResponse> UpsertUserAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            string id,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            this.ClientContext.ValidateResource(id);

            ResponseMessage response = await this.ProcessUserUpsertAsync(
                diagnosticsContext: diagnosticsContext,
                streamPayload: this.ClientContext.SerializerCore.ToStream(new UserProperties(id)),
                requestOptions: requestOptions,
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

        private void ValidateContainerProperties(ContainerProperties containerProperties)
        {
            containerProperties.ValidateRequiredProperties();
            this.ClientContext.ValidateResource(containerProperties.Id);
        }

        private Task<ResponseMessage> ProcessCollectionCreateAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            ThroughputProperties throughputProperties,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.Collection,
               operationType: OperationType.Create,
               cosmosContainerCore: null,
               partitionKey: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputPropertiesHeader(throughputProperties),
               diagnosticsContext: diagnosticsContext,
               cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessCollectionCreateAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            int? throughput,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
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
               diagnosticsContext: diagnosticsContext,
               cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessUserCreateAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
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
               diagnosticsContext: diagnosticsContext,
               cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessUserUpsertAsync(
            CosmosDiagnosticsContext diagnosticsContext,
            Stream streamPayload,
            RequestOptions requestOptions,
            CancellationToken cancellationToken)
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
               diagnosticsContext: diagnosticsContext,
               cancellationToken: cancellationToken);
        }

        internal override async Task<string> GetRIDAsync(CancellationToken cancellationToken)
        {
            DatabaseResponse databaseResponse = await this.ReadAsync(cancellationToken: cancellationToken);
            return databaseResponse?.Resource?.ResourceId;
        }

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
           CosmosDiagnosticsContext diagnosticsContext,
           Stream streamPayload,
           OperationType operationType,
           string linkUri,
           ResourceType resourceType,
           RequestOptions requestOptions,
           CancellationToken cancellationToken)
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
              diagnosticsContext: diagnosticsContext,
              cancellationToken: cancellationToken);
        }
    }
}
