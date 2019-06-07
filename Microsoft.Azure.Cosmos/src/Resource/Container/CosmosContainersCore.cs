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
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Operations for creating new containers, and reading/querying all containers
    ///
    /// <see cref="CosmosContainer"/> for reading, replacing, or deleting an existing container.
    /// </summary>
    internal partial class CosmosDatabaseCore
    {
        public override Task<ContainerResponse> CreateContainerAsync(
                    CosmosContainerSettings containerSettings,
                    int? requestUnitsPerSecond = null,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerSettings == null)
            {
                throw new ArgumentNullException(nameof(containerSettings));
            }

            this.ValidateContainerSettings(containerSettings);

            Task<CosmosResponseMessage> response = this.CreateContainerStreamInternalAsync(
                streamPayload: this.ClientContext.SettingsSerializer.ToStream(containerSettings),
                requestUnitsPerSecond: requestUnitsPerSecond,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponseAsync(this.GetContainer(containerSettings.Id), response);
        }
        
        public override Task<ContainerResponse> CreateContainerAsync(
            string id,
            string partitionKeyPath,
            int? requestUnitsPerSecond = null,
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

            CosmosContainerSettings settings = new CosmosContainerSettings(id, partitionKeyPath);

            return this.CreateContainerAsync(
                settings,
                requestUnitsPerSecond,
                requestOptions,
                cancellationToken);
        }
        
        public override async Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            CosmosContainerSettings containerSettings,
            int? requestUnitsPerSecond = null,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerSettings == null)
            {
                throw new ArgumentNullException(nameof(containerSettings));
            }

            this.ValidateContainerSettings(containerSettings);

            CosmosContainer cosmosContainer = this.GetContainer(containerSettings.Id);
            ContainerResponse cosmosContainerResponse = await cosmosContainer.ReadAsync(cancellationToken: cancellationToken);
            if (cosmosContainerResponse.StatusCode != HttpStatusCode.NotFound)
            {
                return cosmosContainerResponse;
            }

            cosmosContainerResponse = await this.CreateContainerAsync(containerSettings, requestUnitsPerSecond, requestOptions, cancellationToken: cancellationToken);
            if (cosmosContainerResponse.StatusCode != HttpStatusCode.Conflict)
            {
                return cosmosContainerResponse;
            }

            // This second Read is to handle the race condition when 2 or more threads have Read the database and only one succeeds with Create
            // so for the remaining ones we should do a Read instead of throwing Conflict exception
            return await cosmosContainer.ReadAsync(cancellationToken: cancellationToken);
        }

        public override Task<ContainerResponse> CreateContainerIfNotExistsAsync(
            string id,
            string partitionKeyPath,
            int? requestUnitsPerSecond = null,
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

            CosmosContainerSettings settings = new CosmosContainerSettings(id, partitionKeyPath);
            return this.CreateContainerIfNotExistsAsync(settings, requestUnitsPerSecond, requestOptions, cancellationToken);
        }

        public override FeedIterator<CosmosContainerSettings> GetContainersIterator(
            int? maxItemCount = null,
            string continuationToken = null)
        {
            return new FeedIteratorCore<CosmosContainerSettings>(
                maxItemCount,
                continuationToken,
                null,
                this.ContainerFeedRequestExecutorAsync);
        }

        public override CosmosContainer GetContainer(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            return new CosmosContainerCore(
                    this.ClientContext,
                    this,
                    id);
        }

        public override Task<CosmosResponseMessage> CreateContainerStreamAsync(
            CosmosContainerSettings containerSettings, 
            int? requestUnitsPerSecond = null, 
            RequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerSettings == null)
            {
                throw new ArgumentNullException(nameof(containerSettings));
            }

            this.ValidateContainerSettings(containerSettings);

            Stream streamPayload = this.ClientContext.SettingsSerializer.ToStream(containerSettings);
            return this.CreateContainerStreamInternalAsync(streamPayload,
                requestUnitsPerSecond,
                requestOptions,
                cancellationToken);
        }

        public override FeedIterator GetContainersStreamIterator(
            int? maxItemCount = null,
            string continuationToken = null,
            QueryRequestOptions requestOptions = null)
        {
            return new FeedIteratorCore(
                maxItemCount,
                continuationToken,
                requestOptions,
                this.ContainerStreamFeedRequestExecutorAsync);
        }

        public override CosmosContainerFluentDefinitionForCreate DefineContainer(
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

            return new CosmosContainerFluentDefinitionForCreate(this, name, partitionKeyPath);
        }

        internal void ValidateContainerSettings(CosmosContainerSettings containerSettings)
        {
            containerSettings.ValidateRequiredProperties();
            this.ClientContext.ValidateResource(containerSettings.Id);
        }

        internal Task<CosmosResponseMessage> ProcessCollectionCreateAsync(
            Stream streamPayload,
            int? requestUnitsPerSecond,
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
               requestEnricher: (httpRequestMessage) => httpRequestMessage.AddThroughputHeader(requestUnitsPerSecond),
               cancellationToken: cancellationToken);
        }

        private Task<CosmosResponseMessage> CreateContainerStreamInternalAsync(
                    Stream streamPayload,
                    int? requestUnitsPerSecond = null,
                    RequestOptions requestOptions = null,
                    CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessCollectionCreateAsync(
                streamPayload: streamPayload,
                requestUnitsPerSecond: requestUnitsPerSecond,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<CosmosResponseMessage> ContainerStreamFeedRequestExecutorAsync(
            int? maxItemCount,
            string continuationToken,
            RequestOptions requestOptions,
            object state,
            CancellationToken cancellationToken)
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
               resourceUri: this.LinkUri,
               resourceType: ResourceType.Collection,
               operationType: OperationType.ReadFeed,
               cosmosContainerCore: null,
               partitionKey: null,
               streamPayload: null,
               requestOptions: requestOptions,
               requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
               cancellationToken: cancellationToken);
        }

        private Task<FeedResponse<CosmosContainerSettings>> ContainerFeedRequestExecutorAsync(
            int? maxItemCount,
            string continuationToken,
            RequestOptions options,
            object state,
            CancellationToken cancellationToken)
        {
            Debug.Assert(state == null);

            return this.ClientContext.ProcessResourceOperationAsync<FeedResponse<CosmosContainerSettings>>(
                resourceUri: this.LinkUri,
                resourceType: ResourceType.Collection,
                operationType: OperationType.ReadFeed,
                requestOptions: options,
                cosmosContainerCore: null,
                partitionKey: null,
                streamPayload: null,
                requestEnricher: request =>
                {
                    QueryRequestOptions.FillContinuationToken(request, continuationToken);
                    QueryRequestOptions.FillMaxItemCount(request, maxItemCount);
                },
                responseCreator: response => this.ClientContext.ResponseFactory.CreateResultSetQueryResponse<CosmosContainerSettings>(response),
                cancellationToken: cancellationToken);
        }
    }
}
