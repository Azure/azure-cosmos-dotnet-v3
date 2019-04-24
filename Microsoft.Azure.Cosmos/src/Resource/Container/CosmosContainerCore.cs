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
    /// <see cref="CosmosContainers"/> for creating new containers, and reading/querying all containers;
    /// </summary>
    internal class CosmosContainerCore : CosmosContainer
    {
        internal CosmosContainerCore(
            CosmosDatabase database,
            string containerId)
        {
            this.Id = containerId;
            base.Initialize(
                client: database.Client,
                parentLink: database.LinkUri.OriginalString,
                uriPathSegment: Paths.CollectionsPathSegment);

            this.Database = database;
            this.Items = new CosmosItemsCore(this);
            this.StoredProcedures = new CosmosStoredProceduresCore(this);
            this.DocumentClient = this.Client.DocumentClient;
            this.Triggers = new CosmosTriggers(this);
            this.UserDefinedFunctions = new CosmosUserDefinedFunctions(this);
        }

        public override string Id { get; }

        public override CosmosDatabase Database { get; }

        public override CosmosItems Items { get; }

        public override CosmosStoredProcedures StoredProcedures { get; }

        internal CosmosTriggers Triggers { get; }

        internal CosmosUserDefinedFunctions UserDefinedFunctions { get; }

        internal DocumentClient DocumentClient { get; private set; }

        public override Task<CosmosContainerResponse> ReadAsync(
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.ReadStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.Client.ResponseFactory.CreateContainerResponse(this, response);
        }

        public override Task<CosmosContainerResponse> ReplaceAsync(
            CosmosContainerSettings containerSettings,
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            this.Client.DocumentClient.ValidateResource(containerSettings.Id);

            Task<CosmosResponseMessage> response = this.ReplaceStreamAsync(
                streamPayload: CosmosResource.ToStream(containerSettings),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.Client.ResponseFactory.CreateContainerResponse(this, response);
        }

        public override Task<CosmosContainerResponse> DeleteAsync(
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.DeleteStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.Client.ResponseFactory.CreateContainerResponse(this, response);
        }

        public override async Task<int?> ReadProvisionedThroughputAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosOfferResult offerResult = await this.ReadProvisionedThroughputIfExistsAsync(cancellationToken);
            if (offerResult.StatusCode == HttpStatusCode.OK || offerResult.StatusCode == HttpStatusCode.NotFound)
            {
                return offerResult.Throughput;
            }

            throw offerResult.CosmosException;
        }

        public override async Task ReplaceProvisionedThroughputAsync(
            int throughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosOfferResult offerResult = await this.ReplaceProvisionedThroughputIfExistsAsync(throughput, cancellationToken);
            if (offerResult.StatusCode != HttpStatusCode.OK)
            {
                throw offerResult.CosmosException;
            }
        }

        public override Task<CosmosResponseMessage> DeleteStreamAsync(
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
               streamPayload: null,
               operationType: OperationType.Delete,
               requestOptions: requestOptions,
               cancellationToken: cancellationToken);
        }

        public override Task<CosmosResponseMessage> ReplaceStreamAsync(
            Stream streamPayload,
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<CosmosResponseMessage> ReadStreamAsync(
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: null,
                operationType: OperationType.Read,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        internal Task<CosmosOfferResult> ReadProvisionedThroughputIfExistsAsync(
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetRID(cancellationToken)
                .ContinueWith(task => task.Result == null ?
                    Task.FromResult(new CosmosOfferResult(
                        statusCode: HttpStatusCode.Found,
                        cosmosRequestException: new CosmosException(
                            message: RMResources.NotFound,
                            statusCode: HttpStatusCode.Found,
                            subStatusCode: (int)SubStatusCodes.Unknown,
                            activityId: null,
                            requestCharge: 0))) :
                    this.Database.Client.Offers.ReadProvisionedThroughputIfExistsAsync(task.Result, cancellationToken),
                    cancellationToken)
                .Unwrap();
        }

        internal Task<CosmosOfferResult> ReplaceProvisionedThroughputIfExistsAsync(
            int targetThroughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetRID(cancellationToken)
                 .ContinueWith(task => this.Database.Client.Offers.ReplaceThroughputIfExistsAsync(task.Result, targetThroughput, cancellationToken), cancellationToken)
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
            ClientCollectionCache collectionCache = await this.DocumentClient.GetCollectionCacheAsync();
            return await collectionCache.GetByNameAsync(HttpConstants.Versions.CurrentVersion, this.LinkUri.OriginalString, cancellationToken);
        }

        // Name based look-up, needs re-computation and can't be cached
        internal Task<string> GetRID(CancellationToken cancellationToken)
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
        /// Instantiates a new instance of the <see cref="PartitionKeyInternal"/> object.
        /// </summary>
        /// <remarks>
        /// The function selects the right partition key constant for inserting documents that don't have
        /// a value for partition key. The constant selection is based on whether the collection is migrated
        /// or user partitioned
        /// </remarks>
        internal async Task<PartitionKeyInternal> GetNonePartitionKeyValue(CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosContainerSettings containerSettings = await this.GetCachedContainerSettingsAsync(cancellationToken);
            return containerSettings.GetNoneValue();
        }

        internal Task<CollectionRoutingMap> GetRoutingMapAsync(CancellationToken cancellationToken)
        {
            string collectionRID = null;
            return this.GetRID(cancellationToken)
                .ContinueWith(ridTask =>
                {
                    collectionRID = ridTask.Result;
                    return this.DocumentClient.GetPartitionKeyRangeCacheAsync();
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

        private Task<CosmosResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            CosmosContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return ExecUtils.ProcessResourceOperationStreamAsync(
              client: this.Client,
              resourceUri: this.LinkUri,
              resourceType: ResourceType.Collection,
              operationType: operationType,
              partitionKey: null,
              streamPayload: streamPayload,
              requestOptions: requestOptions,
              requestEnricher: null,
              cancellationToken: cancellationToken);
        }
    }
}
