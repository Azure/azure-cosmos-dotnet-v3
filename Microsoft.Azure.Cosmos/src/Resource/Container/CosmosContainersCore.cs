//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Operations for creating new containers, and reading/querying all containers
    ///
    /// <see cref="CosmosContainer"/> for reading, replacing, or deleting an existing container.
    /// </summary>
    internal class CosmosContainersCore : CosmosContainers
    {
        private readonly CosmosDatabaseCore database;
        private readonly CosmosClientContext clientContext;
        private readonly ConcurrentDictionary<string, CosmosContainer> containerCache;

        internal CosmosContainersCore(
            CosmosClientContext clientContext,
            CosmosDatabaseCore database)
        {
            this.database = database;
            this.clientContext = clientContext;
            this.containerCache = new ConcurrentDictionary<string, CosmosContainer>();
        }

        public override Task<CosmosContainerResponse> CreateContainerAsync(
                    CosmosContainerSettings containerSettings,
                    int? throughput = null,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerSettings == null)
            {
                throw new ArgumentNullException(nameof(containerSettings));
            }

            this.ValidateContainerSettings(containerSettings);

            Task<CosmosResponseMessage> response = this.CreateContainerStreamAsync(
                streamPayload: CosmosResource.ToStream(containerSettings),
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.clientContext.ResponseFactory.CreateContainerResponse(this[containerSettings.Id], response);
        }
        
        public override Task<CosmosContainerResponse> CreateContainerAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosContainerSettings settings = new CosmosContainerSettings(id, partitionKeyPath);

            return this.CreateContainerAsync(
                settings,
                throughput,
                requestOptions,
                cancellationToken);
        }
        
        public override async Task<CosmosContainerResponse> CreateContainerIfNotExistsAsync(
            CosmosContainerSettings containerSettings,
            int? throughput = null,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerSettings == null)
            {
                throw new ArgumentNullException(nameof(containerSettings));
            }

            this.ValidateContainerSettings(containerSettings);

            CosmosContainer cosmosContainer = this[containerSettings.Id];
            CosmosContainerResponse cosmosContainerResponse = await cosmosContainer.ReadAsync(cancellationToken: cancellationToken);
            if (cosmosContainerResponse.StatusCode != HttpStatusCode.NotFound)
            {
                return cosmosContainerResponse;
            }

            cosmosContainerResponse = await this.CreateContainerAsync(containerSettings, throughput, requestOptions, cancellationToken: cancellationToken);
            if (cosmosContainerResponse.StatusCode != HttpStatusCode.Conflict)
            {
                return cosmosContainerResponse;
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            return await cosmosContainer.ReadAsync(cancellationToken: cancellationToken);
        }

        public override Task<CosmosContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? throughput = null,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosContainerSettings settings = new CosmosContainerSettings(id, partitionKeyPath);
            return this.CreateContainerIfNotExistsAsync(settings, throughput, requestOptions, cancellationToken);
        }

        public override CosmosFeedIterator<CosmosContainerSettings> GetContainerIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new CosmosDefaultResultSetIterator<CosmosContainerSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.ContainerFeedRequestExecutor);
        }
        
        public override CosmosContainer this[string id] =>
                this.containerCache.GetOrAdd(
                    id,
                    keyName => new CosmosContainerCore(
                        this.clientContext, 
                        this.database, 
                        keyName));

        public override Task<CosmosResponseMessage> CreateContainerStreamAsync(
                    Stream streamPayload,
                    int? throughput = null,
                    CosmosRequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessCollectionCreateAsync(
                streamPayload: streamPayload,
                throughput: throughput,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override CosmosFeedIterator GetContainerStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null,
            CosmosQueryRequestOptions requestOptions = null)
        {
            return new CosmosResultSetIteratorCore(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.ContainerStreamFeedRequestExecutor);
        }

        internal void ValidateContainerSettings(CosmosContainerSettings containerSettings)
        {
            containerSettings.ValidateRequiredProperties();
            this.clientContext.ValidateResource(containerSettings.Id);
        }

        internal Task<CosmosResponseMessage> ProcessCollectionCreateAsync(
            Stream streamPayload,
            int? throughput,
            CosmosRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.clientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.database.LinkUri,
               resourceType: ResourceType.Collection,
               operationType: OperationType.Create,
               cosmosContainerCore: null,
               partitionKey: null,
               streamPayload: streamPayload,
               requestOptions: requestOptions,
               requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputHeader(throughput),
               cancellationToken: cancellationToken);
        }

        private Task<CosmosResponseMessage> ContainerStreamFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions requestOptions,
            object state,
            CancellationToken cancellationToken)
        {
            return this.clientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.database.LinkUri,
               resourceType: ResourceType.Collection,
               operationType: OperationType.ReadFeed,
               cosmosContainerCore: null,
               partitionKey: null,
               streamPayload: null,
               requestOptions: requestOptions,
               requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
               cancellationToken: cancellationToken);
        }

        private Task<CosmosFeedResponse<CosmosContainerSettings>> ContainerFeedRequestExecutor(
            int? maxItemCount,
            string continuationToken,
            CosmosRequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            return this.clientContext.ProcessResourceOperationAsync<CosmosFeedResponse<CosmosContainerSettings>>(
                resourceUri: this.database.LinkUri,
                resourceType: ResourceType.Collection,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    CosmosQueryRequestOptions.FillContinuationToken(request, continuationToken);
                    CosmosQueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.clientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosContainerSettings>(response),
                cancellationToken: cancellationToken);
        }
    }
}
