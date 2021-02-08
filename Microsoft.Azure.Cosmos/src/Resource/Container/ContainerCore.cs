//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing container by id.
    /// 
    /// <see cref="Cosmos.Database"/> for creating new containers, and reading/querying all containers;
    /// </summary>
    internal abstract partial class ContainerCore : ContainerInternal
    {
        private readonly Lazy<BatchAsyncContainerExecutor> lazyBatchExecutor;
        private static readonly Range<string> allRanges = new Range<string>(
                            PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                            PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                            isMinInclusive: true,
                            isMaxInclusive: false);

        protected ContainerCore(
            CosmosClientContext clientContext,
            DatabaseInternal database,
            string containerId,
            CosmosQueryClient cosmosQueryClient = null)
        {
            this.Id = containerId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: database.LinkUri,
                uriPathSegment: Paths.CollectionsPathSegment,
                id: containerId);

            this.Database = database;
            this.Conflicts = new ConflictsInlineCore(this.ClientContext, this);
            this.Scripts = new ScriptsInlineCore(this, this.ClientContext);
            this.cachedUriSegmentWithoutId = this.GetResourceSegmentUriWithoutId();
            this.queryClient = cosmosQueryClient ?? new CosmosQueryClientCore(this.ClientContext, this);
            this.lazyBatchExecutor = new Lazy<BatchAsyncContainerExecutor>(() => this.ClientContext.GetExecutorForContainer(this));
        }

        public override string Id { get; }

        public override Database Database { get; }

        public override string LinkUri { get; }

        public override CosmosClientContext ClientContext { get; }

        public override BatchAsyncContainerExecutor BatchExecutor => this.lazyBatchExecutor.Value;

        public override Conflicts Conflicts { get; }

        public override Scripts.Scripts Scripts { get; }

        public async Task<ContainerResponse> ReadContainerAsync(
            ITrace trace,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ResponseMessage response = await this.ReadContainerStreamAsync(
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponse(this, response);
        }

        public async Task<ContainerResponse> ReplaceContainerAsync(
            ContainerProperties containerProperties,
            ITrace trace,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ClientContext.ValidateResource(containerProperties.Id);
            ResponseMessage response = await this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(containerProperties),
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponse(this, response);
        }

        public async Task<ContainerResponse> DeleteContainerAsync(
            ITrace trace,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ResponseMessage response = await this.DeleteContainerStreamAsync(
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponse(this, response);
        }

        public async Task<int?> ReadThroughputAsync(
            ITrace trace,
            CancellationToken cancellationToken = default)
        {
            ThroughputResponse response = await this.ReadThroughputIfExistsAsync(null, cancellationToken);
            return response.Resource?.Throughput;
        }

        public async Task<ThroughputResponse> ReadThroughputAsync(
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken = default)
        {
            ThroughputResponse throughputResponse = await this.ReadThroughputIfExistsAsync(
                requestOptions,
                trace,
                cancellationToken);

            if (throughputResponse.StatusCode == HttpStatusCode.NotFound)
            {
                throw CosmosExceptionFactory.CreateNotFoundException(
                    message: $"Throughput is not configured for {this.Id}",
                    headers: throughputResponse.Headers,
                    trace: trace);
            }

            return throughputResponse;
        }

        public Task<ThroughputResponse> ReadThroughputIfExistsAsync(
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken = default)
        {
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return this.OfferRetryHelperForStaleRidCacheAsync(
                (rid) => cosmosOffers.ReadThroughputIfExistsAsync(rid, requestOptions, cancellationToken),
                trace,
                cancellationToken);
        }

        public Task<ThroughputResponse> ReplaceThroughputAsync(
            int throughput,
            ITrace trace,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ReplaceThroughputAsync(
                throughputProperties: ThroughputProperties.CreateManualThroughput(throughput),
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public Task<ThroughputResponse> ReplaceThroughputIfExistsAsync(
            ThroughputProperties throughput,
            ITrace trace,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            CosmosOffers cosmosOffers = new CosmosOffers(this.ClientContext);
            return this.OfferRetryHelperForStaleRidCacheAsync(
                (rid) => cosmosOffers.ReplaceThroughputPropertiesIfExistsAsync(
                    targetRID: rid,
                    throughputProperties: throughput,
                    requestOptions: requestOptions,
                    cancellationToken: cancellationToken),
                trace,
                cancellationToken);
        }

        public async Task<ThroughputResponse> ReplaceThroughputAsync(
            ThroughputProperties throughputProperties,
            ITrace trace,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            ThroughputResponse throughputResponse = await this.ReplaceThroughputIfExistsAsync(
                throughputProperties,
                trace,
                requestOptions,
                cancellationToken);

            if (throughputResponse.StatusCode == HttpStatusCode.NotFound)
            {
                throw CosmosExceptionFactory.CreateNotFoundException(
                    message: $"Throughput is not configured for {this.Id}",
                    headers: throughputResponse.Headers);
            }

            return throughputResponse;
        }

        public Task<ResponseMessage> DeleteContainerStreamAsync(
            ITrace trace,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Delete,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public Task<ResponseMessage> ReadContainerStreamAsync(
            ITrace trace,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public Task<ResponseMessage> ReplaceContainerStreamAsync(
            ContainerProperties containerProperties,
            ITrace trace,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            if (containerProperties == null)
            {
                throw new ArgumentNullException(nameof(containerProperties));
            }

            this.ClientContext.ValidateResource(containerProperties.Id);
            return this.ReplaceStreamInternalAsync(
                streamPayload: this.ClientContext.SerializerCore.ToStream(containerProperties),
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        public async Task<IReadOnlyList<FeedRange>> GetFeedRangesAsync(
            ITrace trace,
            CancellationToken cancellationToken = default)
        {
            PartitionKeyRangeCache partitionKeyRangeCache = await this.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();

            string containerRId;
            containerRId = await this.GetCachedRIDAsync(
                forceRefresh: false,
                trace,
                cancellationToken);

            IReadOnlyList<PartitionKeyRange> partitionKeyRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                containerRId,
                ContainerCore.allRanges,
                trace,
                forceRefresh: true);

            if (partitionKeyRanges == null)
            {
                string refreshedContainerRId;
                refreshedContainerRId = await this.GetCachedRIDAsync(
                    forceRefresh: true,
                    trace,
                    cancellationToken);

                if (string.Equals(containerRId, refreshedContainerRId))
                {
                    throw CosmosExceptionFactory.CreateInternalServerErrorException(
                        $"Container rid {containerRId} did not have a partition key range after refresh",
                        trace: trace);
                }

                partitionKeyRanges = await partitionKeyRangeCache.TryGetOverlappingRangesAsync(
                    containerRId,
                    ContainerCore.allRanges,
                    trace,
                    forceRefresh: true);

                if (partitionKeyRanges == null)
                {
                    throw CosmosExceptionFactory.CreateInternalServerErrorException(
                        $"Container rid {containerRId} returned partitionKeyRanges null after Container RID refresh",
                        trace: trace);
                }
            }

            List<FeedRange> feedTokens = new List<FeedRange>(partitionKeyRanges.Count);
            foreach (PartitionKeyRange partitionKeyRange in partitionKeyRanges)
            {
                feedTokens.Add(new FeedRangeEpk(partitionKeyRange.ToRange()));
            }

            return feedTokens;
        }

        public override FeedIterator GetChangeFeedStreamIterator(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            if (changeFeedStartFrom == null)
            {
                throw new ArgumentNullException(nameof(changeFeedStartFrom));
            }

            if (changeFeedMode == null)
            {
                throw new ArgumentNullException(nameof(changeFeedMode));
            }

            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                this,
                this.queryClient,
                changeFeedRequestOptions: changeFeedRequestOptions);
            DocumentContainer documentContainer = new DocumentContainer(networkAttachedDocumentContainer);

            return new ChangeFeedIteratorCore(
                documentContainer: documentContainer,
                changeFeedStartFrom: changeFeedStartFrom,
                changeFeedMode: changeFeedMode,
                changeFeedRequestOptions: changeFeedRequestOptions);
        }

        public override FeedIterator<T> GetChangeFeedIterator<T>(
            ChangeFeedStartFrom changeFeedStartFrom,
            ChangeFeedMode changeFeedMode,
            ChangeFeedRequestOptions changeFeedRequestOptions = null)
        {
            if (changeFeedStartFrom == null)
            {
                throw new ArgumentNullException(nameof(changeFeedStartFrom));
            }

            if (changeFeedMode == null)
            {
                throw new ArgumentNullException(nameof(changeFeedMode));
            }

            NetworkAttachedDocumentContainer networkAttachedDocumentContainer = new NetworkAttachedDocumentContainer(
                this,
                this.queryClient,
                changeFeedRequestOptions: changeFeedRequestOptions);
            DocumentContainer documentContainer = new DocumentContainer(networkAttachedDocumentContainer);

            ChangeFeedIteratorCore changeFeedIteratorCore = new ChangeFeedIteratorCore(
                documentContainer: documentContainer,
                changeFeedStartFrom: changeFeedStartFrom,
                changeFeedMode: changeFeedMode,
                changeFeedRequestOptions: changeFeedRequestOptions);

            return new FeedIteratorCore<T>(
                changeFeedIteratorCore,
                responseCreator: this.ClientContext.ResponseFactory.CreateChangeFeedUserTypeResponse<T>);
        }

        public override async Task<IEnumerable<string>> GetPartitionKeyRangesAsync(
            FeedRange feedRange,
            CancellationToken cancellationToken = default)
        {
            IRoutingMapProvider routingMapProvider = await this.ClientContext.DocumentClient.GetPartitionKeyRangeCacheAsync();
            string containerRid = await this.GetCachedRIDAsync(
                forceRefresh: false,
                NoOpTrace.Singleton,
                cancellationToken);
            PartitionKeyDefinition partitionKeyDefinition = await this.GetPartitionKeyDefinitionAsync(cancellationToken);

            if (!(feedRange is FeedRangeInternal feedTokenInternal))
            {
                throw new ArgumentException(nameof(feedRange), ClientResources.FeedToken_UnrecognizedFeedToken);
            }

            return await feedTokenInternal.GetPartitionKeyRangesAsync(routingMapProvider, containerRid, partitionKeyDefinition, cancellationToken);
        }

        /// <summary>
        /// Gets the container's Properties by using the internal cache.
        /// In case the cache does not have information about this container, it may end up making a server call to fetch the data.
        /// </summary>
        /// <param name="forceRefresh">Forces the cache to refresh</param>
        /// <param name="trace">The trace.</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="ContainerProperties"/> for this container.</returns>
        public override async Task<ContainerProperties> GetCachedContainerPropertiesAsync(
            bool forceRefresh,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            try
            {
                ClientCollectionCache collectionCache = await this.ClientContext.DocumentClient.GetCollectionCacheAsync(trace);
                return await collectionCache.ResolveByNameAsync(
                    HttpConstants.Versions.CurrentVersion,
                    this.LinkUri,
                    forceRefresh,
                    cancellationToken);
            }
            catch (DocumentClientException ex)
            {
                throw CosmosExceptionFactory.Create(
                    dce: ex,
                    trace: trace);
            }
        }

        // Name based look-up, needs re-computation and can't be cached
        public override async Task<string> GetCachedRIDAsync(
            bool forceRefresh,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            using (ITrace childTrace = trace.StartChild("Get RID", TraceComponent.Routing, TraceLevel.Info))
            {
                ContainerProperties containerProperties = await this.GetCachedContainerPropertiesAsync(
                    forceRefresh,
                    trace,
                    cancellationToken);
                return containerProperties?.ResourceId;
            }
        }

        public override async Task<PartitionKeyDefinition> GetPartitionKeyDefinitionAsync(CancellationToken cancellationToken = default)
        {
            ContainerProperties cachedContainerPropertiesAsync = await this.GetCachedContainerPropertiesAsync(
                forceRefresh: false,
                trace: NoOpTrace.Singleton,
                cancellationToken: cancellationToken);
            return cachedContainerPropertiesAsync?.PartitionKey;
        }

        /// <summary>
        /// Used by typed API only. Exceptions are allowed.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>Returns the partition key path</returns>
        public override async Task<IReadOnlyList<IReadOnlyList<string>>> GetPartitionKeyPathTokensAsync(CancellationToken cancellationToken = default)
        {
            ContainerProperties containerProperties = await this.GetCachedContainerPropertiesAsync(
                forceRefresh: false,
                trace: NoOpTrace.Singleton,
                cancellationToken: cancellationToken);
            if (containerProperties == null)
            {
                throw new ArgumentOutOfRangeException($"Container {this.LinkUri} not found");
            }

            if (containerProperties.PartitionKey?.Paths == null)
            {
                throw new ArgumentOutOfRangeException($"Partition key not defined for container {this.LinkUri}");
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
        public override async Task<PartitionKeyInternal> GetNonePartitionKeyValueAsync(ITrace trace, CancellationToken cancellationToken = default)
        {
            ContainerProperties containerProperties = await this.GetCachedContainerPropertiesAsync(forceRefresh: false, trace, cancellationToken: cancellationToken);
            return containerProperties.GetNoneValue();
        }

        public override async Task<CollectionRoutingMap> GetRoutingMapAsync(CancellationToken cancellationToken)
        {
            string collectionRid = await this.GetCachedRIDAsync(
                forceRefresh: false,
                trace: NoOpTrace.Singleton,
                cancellationToken);

            PartitionKeyRangeCache partitionKeyRangeCache = await this.ClientContext.Client.DocumentClient.GetPartitionKeyRangeCacheAsync();
            CollectionRoutingMap collectionRoutingMap = await partitionKeyRangeCache.TryLookupAsync(
                collectionRid,
                previousValue: null,
                request: null,
                cancellationToken);

            // Not found.
            if (collectionRoutingMap == null)
            {
                collectionRid = await this.GetCachedRIDAsync(
                    forceRefresh: true,
                    trace: NoOpTrace.Singleton,
                    cancellationToken);

                collectionRoutingMap = await partitionKeyRangeCache.TryLookupAsync(
                    collectionRid,
                    previousValue: null,
                    request: null,
                    cancellationToken);
            }

            return collectionRoutingMap;
        }

        private async Task<ThroughputResponse> OfferRetryHelperForStaleRidCacheAsync(
            Func<string, Task<ThroughputResponse>> executeOfferOperation,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            string rid = await this.GetCachedRIDAsync(
                forceRefresh: false,
                trace: trace,
                cancellationToken: cancellationToken);
            ThroughputResponse throughputResponse = await executeOfferOperation(rid);
            if (throughputResponse.StatusCode != HttpStatusCode.NotFound)
            {
                return throughputResponse;
            }

            // Check if RID cache is stale
            ResponseMessage responseMessage = await this.ReadContainerStreamAsync(
                requestOptions: null,
                trace: trace,
                cancellationToken: cancellationToken);

            // Container does not exist
            if (responseMessage.StatusCode == HttpStatusCode.NotFound)
            {
                return new ThroughputResponse(
                    responseMessage.StatusCode,
                    responseMessage.Headers,
                    null,
                    new CosmosTraceDiagnostics(trace));
            }

            responseMessage.EnsureSuccessStatusCode();

            ContainerProperties containerProperties = this.ClientContext.SerializerCore.FromStream<ContainerProperties>(responseMessage.Content);

            // The RIDs match so return the original response.
            if (string.Equals(rid, containerProperties.ResourceId))
            {
                return throughputResponse;
            }

            // Get the offer with the new rid value
            return await executeOfferOperation(containerProperties.ResourceId);
        }

        private Task<ResponseMessage> ReplaceStreamInternalAsync(
            Stream streamPayload,
            ITrace trace,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            RequestOptions requestOptions,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            return this.ProcessResourceOperationStreamAsync(
                streamPayload: streamPayload,
                operationType: operationType,
                linkUri: this.LinkUri,
                resourceType: ResourceType.Collection,
                requestOptions: requestOptions,
                trace: trace,
                cancellationToken: cancellationToken);
        }

        private Task<ResponseMessage> ProcessResourceOperationStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            string linkUri,
            ResourceType resourceType,
            ITrace trace,
            RequestOptions requestOptions = null,
            CancellationToken cancellationToken = default)
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