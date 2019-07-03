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

        internal async Task<int?> ReadProvisionedThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosOfferResult offerResult = await this.ReadProvisionedThroughputIfExistsAsync(cancellationToken);
            if (offerResult.StatusCode == HttpStatusCode.OK || offerResult.StatusCode == HttpStatusCode.NotFound)
            {
                return offerResult.Throughput;
            }

            throw offerResult.CosmosException;
        }

        internal async Task ReplaceProvisionedThroughputAsync(
            int throughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosOfferResult offerResult = await this.ReplaceProvisionedThroughputIfExistsAsync(throughput, cancellationToken);
            if (offerResult.StatusCode != HttpStatusCode.OK)
            {
                throw offerResult.CosmosException;
            }
        }

        public async override Task<int?> ReadThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ThroughputResponse response = await this.ReadThroughputAsync(null, cancellationToken);
            return response.Resource?.Throughput;
        }

        public async override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            OfferV2 offerV2 = await this.GetRIDAsync(cancellationToken)
            .ContinueWith(task => this.ClientContext.Client.Offers.GetOfferV2Async(task.Result, cancellationToken),
                cancellationToken)
            .Unwrap();

            if (offerV2 == null)
            {
                return new ThroughputResponse(httpStatusCode: HttpStatusCode.NotFound, headers: null, throughputProperties: null);
            }

            Task<ResponseMessage> response = this.ProcessResourceOperationStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                linkUri: new Uri(offerV2.SelfLink, UriKind.Relative),
                resourceType: ResourceType.Offer,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return await this.ClientContext.ResponseFactory.CreateThroughputResponseAsync(response);
        }

        public async override Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            OfferV2 offerV2 = await this.GetRIDAsync(cancellationToken)
             .ContinueWith(task => this.ClientContext.Client.Offers.GetOfferV2Async(task.Result, cancellationToken),
                 cancellationToken)
             .Unwrap();

            if (offerV2 == null)
            {
                return new ThroughputResponse(httpStatusCode: HttpStatusCode.NotFound, headers: null, throughputProperties: null);
            }

            OfferV2 newOffer = new OfferV2(offerV2, throughput);

            Task<ResponseMessage> response = this.ProcessResourceOperationStreamAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(newOffer),
                operationType: OperationType.Replace,
                linkUri: new Uri(offerV2.SelfLink, UriKind.Relative),
                resourceType: ResourceType.Offer,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return await this.ClientContext.ResponseFactory.CreateThroughputResponseAsync(response);
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
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(containerProperties),
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
            ContainerResponse cosmosContainerResponse = await container.ReadContainerAsync(cancellationToken: cancellationToken);
            if (cosmosContainerResponse.StatusCode != HttpStatusCode.NotFound)
            {
                return cosmosContainerResponse;
            }

            cosmosContainerResponse = await this.CreateContainerAsync(containerProperties, throughput, requestOptions, cancellationToken: cancellationToken);
            if (cosmosContainerResponse.StatusCode != HttpStatusCode.Conflict)
            {
                return cosmosContainerResponse;
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

            return new ContainerCore(
                    this.ClientContext,
                    this,
                    id);
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

            Stream streamPayload = this.ClientContext.PropertiesSerializer.ToStream(containerProperties);
            return this.CreateContainerStreamInternalAsync(streamPayload,
                throughput,
                requestOptions,
                cancellationToken);
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
            return new FeedStatelessIteratorCore(
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
            FeedIterator databaseStreamIterator = this.GetContainerQueryStreamIterator(
                queryDefinition,
                continuationToken,
                requestOptions);

            return new FeedStatelessIteratorCore<T>(
                databaseStreamIterator,
                this.ClientContext.ResponseFactory.CreateResultSetQueryResponse<T>);
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
               cancellationToken: cancellationToken);
        }

        internal Task<CosmosOfferResult> ReadProvisionedThroughputIfExistsAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetRIDAsync(cancellationToken)
                .ContinueWith(task => this.ClientContext.Client.Offers.ReadProvisionedThroughputIfExistsAsync(task.Result, cancellationToken), cancellationToken)
                .Unwrap();
        }

        internal Task<CosmosOfferResult> ReplaceProvisionedThroughputIfExistsAsync(
            int throughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<string> rid = this.GetRIDAsync(cancellationToken);
            return rid.ContinueWith(task => this.ClientContext.Client.Offers.ReplaceThroughputIfExistsAsync(task.Result, throughput, cancellationToken), cancellationToken)
                .Unwrap();
        }

        internal virtual Task<string> GetRIDAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ReadAsync(cancellationToken: cancellationToken)
                .ContinueWith(task =>
                {
                    DatabaseResponse response = task.Result;
                    return response.Resource.ResourceId;
                }, cancellationToken);
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

        private Task<ResponseMessage> ProcessAsync(
            OperationType operationType,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ProcessResourceOperationStreamAsync(
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
              cancellationToken: cancellationToken);
        }
    }
}
