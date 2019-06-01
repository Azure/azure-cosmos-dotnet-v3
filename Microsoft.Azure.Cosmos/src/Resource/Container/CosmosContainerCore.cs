//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Operations for reading, replacing, or deleting a specific, existing cosmosContainer by id.
    /// 
    /// <see cref="CosmosDatabase"/> for creating new containers, and reading/querying all containers;
    /// </summary>
    internal partial class CosmosContainerCore : CosmosContainer
    {
        /// <summary>
        /// Only used for unit testing
        /// </summary>
        internal CosmosContainerCore()
        {
        }

        internal CosmosContainerCore(
            CosmosClientContext clientContext,
            CosmosDatabaseCore database,
            string containerId)
        {
            this.Id = containerId;
            this.ClientContext = clientContext;
            this.LinkUri = clientContext.CreateLink(
                parentLink: database.LinkUri.OriginalString,
                uriPathSegment: Paths.CollectionsPathSegment,
                id: containerId);

            this.Database = database;
            this.Conflicts = new CosmosConflictsCore(this.ClientContext, this);
            this.cachedUriSegmentWithoutId = this.GetResourceSegmentUriWithoutId();
            this.queryClient = queryClient ?? new CosmosQueryClientCore(this.ClientContext, this);
        }

        public override string Id { get; }

        public override CosmosDatabase Database { get; }

        public override CosmosConflicts Conflicts { get; }

        internal virtual Uri LinkUri { get; }

        internal virtual CosmosClientContext ClientContext { get; }

        public override Task<ContainerResponse> ReadAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.ReadAsStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponseAsync(this, response);
        }

        public override Task<ContainerResponse> ReplaceAsync(
            CosmosContainerSettings containerSettings,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerSettings == null)
            {
                throw new ArgumentNullException(nameof(containerSettings));
            }

            this.ClientContext.ValidateResource(containerSettings.Id);
            Task<CosmosResponseMessage> response = this.ReplaceAsStreamInternalAsync(
                streamPayload: this.ClientContext.SettingsSerializer.ToStream(containerSettings),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponseAsync(this, response);
        }

        public override Task<ContainerResponse> DeleteAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.DeleteAsStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponseAsync(this, response);
        }

        public override async Task<int?> ReadProvisionedThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosOfferResult offerResult = await this.ReadProvisionedThroughputIfExistsAsync(cancellationToken);
            if (offerResult.StatusCode == HttpStatusCode.OK || offerResult.StatusCode == HttpStatusCode.NotFound)
            {
                return offerResult.RequestUnits;
            }

            throw offerResult.CosmosException;
        }

        public override async Task ReplaceProvisionedThroughputAsync(
            int requestUnits,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosOfferResult offerResult = await this.ReplaceProvisionedThroughputIfExistsAsync(requestUnits, cancellationToken);
            if (offerResult.StatusCode != HttpStatusCode.OK)
            {
                throw offerResult.CosmosException;
            }
        }

        public override Task<CosmosResponseMessage> DeleteAsStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsStreamAsync(
               streamPayload: null,
               operationType: OperationType.Delete,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);
        }

        public override Task<CosmosResponseMessage> ReadAsStreamAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<CosmosResponseMessage> ReplaceAsStreamAsync(
            CosmosContainerSettings containerSettings, 
            ContainerRequestOptions requestOptions = null, 
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (containerSettings == null)
            {
                throw new ArgumentNullException(nameof(containerSettings));
            }

            this.ClientContext.ValidateResource(containerSettings.Id);
            return this.ReplaceAsStreamInternalAsync(
                streamPayload: this.ClientContext.SettingsSerializer.ToStream(containerSettings),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal Task<CosmosOfferResult> ReadProvisionedThroughputIfExistsAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetRIDAsync(cancellationToken)
                .ContinueWith(task => task.Result == null ?
                    Task.FromResult(new CosmosOfferResult(
                        statusCode: HttpStatusCode.Found,
                        cosmosRequestException: new CosmosException(
                            message: RMResources.NotFound,
                            statusCode: HttpStatusCode.Found,
                            subStatusCode: (int)SubStatusCodes.Unknown,
                            activityId: null,
                            requestCharge: 0))) :
                    this.ClientContext.Client.Offers.ReadProvisionedThroughputIfExistsAsync(task.Result, cancellationToken),
                    cancellationToken)
                .Unwrap();
        }

        internal Task<CosmosOfferResult> ReplaceProvisionedThroughputIfExistsAsync(
            int targetRequestUnits,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetRIDAsync(cancellationToken)
                 .ContinueWith(task => this.ClientContext.Client.Offers.ReplaceThroughputIfExistsAsync(task.Result, targetRequestUnits, cancellationToken), cancellationToken)
                 .Unwrap();
        }

        /// <summary>
        /// Gets the container's settings by using the internal cache.
        /// In case the cache does not have information about this container, it may end up making a server call to fetch the data.
        /// </summary>
        /// <param name="cancellationToken"><see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="CosmosContainerSettings"/> for this container.</returns>
        internal async Task<CosmosContainerSettings> GetCachedContainerSettingsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            ClientCollectionCache collectionCache = await this.ClientContext.DocumentClient.GetCollectionCacheAsync();
            return await collectionCache.ResolveByNameAsync(HttpConstants.Versions.CurrentVersion, this.LinkUri.OriginalString, cancellationToken);
        }

        // Name based look-up, needs re-computation and can't be cached
        internal Task<string> GetRIDAsync(CancellationToken cancellationToken)
        {
            return this.GetCachedContainerSettingsAsync(cancellationToken)
                            .ContinueWith(containerSettingsTask => containerSettingsTask.Result?.ResourceId, cancellationToken);
        }

        internal Task<PartitionKeyDefinition> GetPartitionKeyDefinitionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetCachedContainerSettingsAsync(cancellationToken)
                            .ContinueWith(containerSettingsTask => containerSettingsTask.Result?.PartitionKey, cancellationToken);
        }

        /// <summary>
        /// Used by typed API only. Exceptions are allowed.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>Returns the partition key path</returns>
        internal virtual async Task<string[]> GetPartitionKeyPathTokensAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosContainerSettings containerSettings = await this.GetCachedContainerSettingsAsync(cancellationToken);
            if (containerSettings == null)
            {
                throw new ArgumentOutOfRangeException($"Container {this.LinkUri.ToString()} not found");
            }
            
            if (containerSettings.PartitionKey?.Paths == null)
            {
                throw new ArgumentOutOfRangeException($"Partition key not defined for container {this.LinkUri.ToString()}");
            }

            return containerSettings.PartitionKeyPathTokens;
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
            CosmosContainerSettings containerSettings = await this.GetCachedContainerSettingsAsync(cancellationToken);
            return containerSettings.GetNoneValue();
        }

        internal Task<CollectionRoutingMap> GetRoutingMapAsync(CancellationToken cancellationToken)
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
        
        private Task<CosmosResponseMessage> ReplaceAsStreamInternalAsync(
            Stream streamPayload,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessAsStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        private Task<CosmosResponseMessage> ProcessAsStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationAsStreamAsync(
              resourceUri: this.LinkUri,
              resourceType: ResourceType.Collection,
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
