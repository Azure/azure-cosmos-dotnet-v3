//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
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
    internal partial class CosmosContainerCore : CosmosContainer
    {
        /// <summary>
        /// Only used for unit testing
        /// </summary>
        internal CosmosContainerCore() { }

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
            this.Items = new CosmosItemsCore(this.ClientContext, this);
            this.StoredProcedures = new CosmosStoredProceduresCore(this.ClientContext, this);
            this.Triggers = new CosmosTriggers(this.ClientContext, this);
            this.UserDefinedFunctions = new CosmosUserDefinedFunctions(this.ClientContext, this);
        }

        public override string Id { get; }

        public override CosmosDatabase Database { get; }

        public override CosmosItems Items { get; }

        public override CosmosStoredProcedures StoredProcedures { get; }

        internal virtual Uri LinkUri { get; }

        internal CosmosTriggers Triggers { get; }

        internal CosmosUserDefinedFunctions UserDefinedFunctions { get; }

        internal virtual CosmosClientContext ClientContext { get; }        

        public override Task<ContainerResponse> ReadAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.ReadStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponse(this, response);
        }

        public override Task<ContainerResponse> ReplaceAsync(
            CosmosContainerSettings containerSettings,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            this.ClientContext.ValidateResource(containerSettings.Id);

            Task<CosmosResponseMessage> response = this.ReplaceStreamAsync(
                streamPayload: CosmosResource.ToStream(containerSettings),
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponse(this, response);
        }

        public override Task<ContainerResponse> DeleteAsync(
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task<CosmosResponseMessage> response = this.DeleteStreamAsync(
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);

            return this.ClientContext.ResponseFactory.CreateContainerResponse(this, response);
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
            ContainerRequestOptions requestOptions = null,
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
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ProcessStreamAsync(
                streamPayload: streamPayload,
                operationType: OperationType.Replace,
                requestOptions: requestOptions,
                cancellationToken: cancellationToken);
        }

        public override Task<CosmosResponseMessage> ReadStreamAsync(
            ContainerRequestOptions requestOptions = null,
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
                    this.ClientContext.Client.Offers.ReadProvisionedThroughputIfExistsAsync(task.Result, cancellationToken),
                    cancellationToken)
                .Unwrap();
        }

        internal Task<CosmosOfferResult> ReplaceProvisionedThroughputIfExistsAsync(
            int targetThroughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetRID(cancellationToken)
                 .ContinueWith(task => this.ClientContext.Client.Offers.ReplaceThroughputIfExistsAsync(task.Result, targetThroughput, cancellationToken), cancellationToken)
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
        internal Task<string> GetRID(CancellationToken cancellationToken)
        {
            return this.GetCachedContainerSettingsAsync(cancellationToken)
                            .ContinueWith(containerSettingsTask => containerSettingsTask.Result?.ResourceId, cancellationToken);
        }

        internal virtual Task<PartitionKeyDefinition> GetPartitionKeyDefinitionAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetCachedContainerSettingsAsync(cancellationToken)
                            .ContinueWith(containerSettingsTask => containerSettingsTask.Result?.PartitionKey, cancellationToken);
        }

        internal virtual async Task<Collection<string>> GetPartitionKeyPathsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            CosmosContainerSettings containerSettings = await this.GetCachedContainerSettingsAsync(cancellationToken);
            return containerSettings?.PartitionKey?.Paths;            
        }

        internal async Task<string[]> GetPartitionKeyPathTokensAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            Collection<string> partitionKeyPaths = await this.GetPartitionKeyPathsAsync(cancellationToken);

            if(partitionKeyPaths.Count != 1)
            {
                throw new InvalidOperationException("Multiple partition keys not supported.");
            }

            string path = partitionKeyPaths[0];
            
            return path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
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
        internal async Task<PartitionKeyInternal> GetNonePartitionKeyValueAsync(CancellationToken cancellation = default(CancellationToken))
        {
            CosmosContainerSettings containerSettings = await this.GetCachedContainerSettingsAsync(cancellation);
            return containerSettings.GetNoneValue();
        }

        internal Task<CollectionRoutingMap> GetRoutingMapAsync(CancellationToken cancellationToken)
        {
            string collectionRID = null;
            return this.GetRID(cancellationToken)
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
        
        private Task<CosmosResponseMessage> ProcessStreamAsync(
            Stream streamPayload,
            OperationType operationType,
            ContainerRequestOptions requestOptions = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.ClientContext.ProcessResourceOperationStreamAsync(
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
