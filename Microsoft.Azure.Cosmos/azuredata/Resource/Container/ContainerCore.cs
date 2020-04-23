//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Query;
    using Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing container by id.
    /// 
    /// <see cref="Azure.Cosmos.CosmosDatabase"/> for creating new containers, and reading/querying all containers;
    /// </summary>
    internal partial class ContainerCore : CosmosContainer
    {
        /// <summary>
        /// Only used for unit testing
        /// </summary>
        internal ContainerCore()
        {
        }

        internal ContainerCore(
            CosmosClientContext clientContext,
            DatabaseCore database,
            string containerId,
            CosmosQueryClient cosmosQueryClient = null)
        {
            this.Id = containerId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: database.LinkUri.OriginalString,
                uriPathSegment: Paths.CollectionsPathSegment,
                id: containerId);

            this.Database = database;
            this.Conflicts = new ConflictsCore(this.ClientContext, this);
            this.Scripts = new ScriptsCore(this, this.ClientContext);
            this.cachedUriSegmentWithoutId = this.GetResourceSegmentUriWithoutId();
            this.queryClient = cosmosQueryClient ?? new CosmosQueryClientCore(this.ClientContext, this);
            //this.BatchExecutor = this.InitializeBatchExecutorForContainer();
        }

        public override string Id { get; }

        public CosmosDatabase Database { get; }

        internal virtual Uri LinkUri { get; }

        internal virtual CosmosClientContext ClientContext { get; }

        //internal virtual BatchAsyncContainerExecutor BatchExecutor { get; }

        public override CosmosConflicts Conflicts { get; }

        public override Scripts.CosmosScripts Scripts { get; }

        public override async Task<CosmosContainerResponse> ReadContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<Response> response = this.ReadContainerStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return await this.ClientContext.ResponseFactory.CreateContainerResponseAsync(this, response, cancellationToken);
        }

        public override Task<CosmosContainerResponse> ReplaceContainerAsync(
            CosmosContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ClientContext.ValidateResource(containerProperties.Id);
            Task<Response> response = this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(containerProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponseAsync(this, response, cancellationToken);
        }

        public override Task<CosmosContainerResponse> DeleteContainerAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<Response> response = this.DeleteContainerStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponseAsync(this, response, cancellationToken);
        }

        public async override Task<int?> ReadThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ThroughputResponse response = await this.ReadThroughputIfExistsAsync(null, cancellationToken);
            return response.Value?.Throughput;
        }

        public async override Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReadThroughputAsync(rid, requestOptions, cancellationToken);
        }

        internal async Task<ThroughputResponse> ReadThroughputIfExistsAsync(
            RequestOptions requestOptions,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string rid = await this.GetRIDAsync(cancellationToken);
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return await cosmosOffers.ReadThroughputIfExistsAsync(rid, requestOptions, cancellationToken);
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

        public override async Task<Response> DeleteContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ProcessStreamAsync(
               streamPayload: null,
               operationType: OperationType.Delete,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);
        }

        public override async Task<Response> ReadContainerStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return await this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override async Task<Response> ReplaceContainerStreamAsync(
            CosmosContainerProperties containerProperties,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ClientContext.ValidateResource(containerProperties.Id);
            return await this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.PropertiesSerializer.ToStream(containerProperties),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets the container's Properties by using the internal cache.
        /// In case the cache does not have information about this container, it may end up making a server call to fetch the data.
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="CosmosContainerProperties"/> for this container.</returns>
        internal async Task<CosmosContainerProperties> GetCachedContainerPropertiesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ClientCollectionCache collectionCache = await this.ClientContext.DocumentClient.GetCollectionCacheAsync();
            try
            {
                return await collectionCache.ResolveByNameAsync(HttpConstants.Versions.CurrentVersion, this.LinkUri.OriginalString, cancellationToken);
            }
            catch (DocumentClientException ex)
            {
                throw new CosmosException(ex.ToCosmosResponseMessage(null), ex.Message, ex.Error);
            }
        }

        // Name based look-up, needs re-computation and can't be cached
        internal async Task<string> GetRIDAsync(CancellationToken cancellationToken)
        {
            CosmosContainerProperties containerProperties = await this.GetCachedContainerPropertiesAsync(cancellationToken);
            return containerProperties?.ResourceId;
        }

        internal virtual Task<PartitionKeyDefinition> GetPartitionKeyDefinitionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetCachedContainerPropertiesAsync(cancellationToken)
                            .ContinueWith(containerPropertiesTask => containerPropertiesTask.Result?.PartitionKey, cancellationToken);
        }

        /// <summary>
        /// Used by typed API only. Exceptions are allowed.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>Returns the partition key path</returns>
        internal virtual async Task<string[]> GetPartitionKeyPathTokensAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosContainerProperties containerProperties = await this.GetCachedContainerPropertiesAsync(cancellationToken);
            if (containerProperties == null)
            {
                throw new ArgumentOutOfRangeException($"Container {this.LinkUri.ToString()} not found");
            }

            if (containerProperties.PartitionKey?.Paths == null)
            {
                throw new ArgumentOutOfRangeException($"Partition key not defined for container {this.LinkUri.ToString()}");
            }

            return containerProperties.PartitionKeyPathTokens;
        }

        /// <summary>
        /// Instantiates a new instance of the <see cref="PartitionKeyInternal"/> object.
        /// </summary>
        /// <remarks>
        /// The function selects the right partition key constant for inserting documents that don't have
        /// a value for partition key. The constant selection is based on whether the collection is migrated
        /// or user partitioned
        /// 
        /// For non-existing container will throw <see cref="DocumentClientException"/> with 404 as status code
        /// </remarks>
        internal async Task<PartitionKeyInternal> GetNonePartitionKeyValueAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosContainerProperties containerProperties = await this.GetCachedContainerPropertiesAsync(cancellationToken);
            return containerProperties.GetNoneValue();
        }

        internal virtual Task<CollectionRoutingMap> GetRoutingMapAsync(CancellationToken cancellationToken)
        {
            string collectionRID = null;
            return this.GetRIDAsync(cancellationToken)
                .ContinueWith(ridTask =>
                {
                    collectionRID = ridTask.Result;
                    return this.ClientContext.Client.DocumentClient.GetPartitionKeyRangeCacheAsync();
                })
                .Unwrap()
                .ContinueWith(partitionKeyRangeCachetask =>
                {
                    PartitionKeyRangeCache partitionKeyRangeCache = partitionKeyRangeCachetask.Result;
                    return partitionKeyRangeCache.TryLookupAsync(
                            collectionRID,
                            null,
                            null,
                            cancellationToken);
                })
                .Unwrap();
        }

        //internal virtual BatchAsyncContainerExecutor InitializeBatchExecutorForContainer()
        //{
        //    if (!this.ClientContext.ClientOptions.AllowBulkExecution)
        //    {
        //        return null;
        //    }

        //    return new BatchAsyncContainerExecutor(
        //        this,
        //        this.ClientContext,
        //        Constants.MaxOperationsInDirectModeBatchRequest,
        //        Constants.MaxDirectModeBatchRequestBodySizeInBytes);
        //}

        private Task<Response> ReplaceStreamInternalAsync(
            Stream streamPayload,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<Response> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessResourceOperationStreamAsync(
                streamPayload: streamPayload,
                operationType: operationType,
                linkUri: this.LinkUri,
                resourceType: ResourceType.Collection,
                requestOptions: requestOptions,
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
