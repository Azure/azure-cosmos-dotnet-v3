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
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Routing;

    internal sealed class CosmosContainerImpl : CosmosContainer
    {
        internal CosmosContainerImpl(
            CosmosDatabase database,
            string containerId)
        {
            this.Id = containerId;
            this.Client = database.Client;
            this.LinkUri =  GetLink(database.LinkUri.OriginalString, Paths.CollectionsPathSegment);
            this.Database = database;
            this.Items = new CosmosItemsImpl(this);
            this.StoredProcedures = new CosmosStoredProceduresImpl(this);
            this.DocumentClient = this.Client.DocumentClient;
            this.Triggers = new CosmosTriggers(this);
            this.UserDefinedFunctions = new CosmosUserDefinedFunctions(this);
        }

        public override string Id { get; }

        public override CosmosDatabase Database { get; }

        public override CosmosItems Items { get; }

        public override CosmosStoredProcedures StoredProcedures { get; }

        internal override CosmosTriggers Triggers { get; }

        internal override CosmosUserDefinedFunctions UserDefinedFunctions { get; }

        internal DocumentClient DocumentClient { get; private set; }

        internal override CosmosClient Client { get; }

        internal override Uri LinkUri { get; }

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
            this.Client.DocumentClient.ValidateResource(containerSettings);

            Task<CosmosResponseMessage> response = this.ReplaceStreamAsync(
                streamPayload: containerSettings.GetResourceStream(),
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
                    this.Client.Offers.ReadProvisionedThroughputIfExistsAsync(task.Result, cancellationToken),
                    cancellationToken)
                .Unwrap();
        }

        internal Task<CosmosOfferResult> ReplaceProvisionedThroughputIfExistsAsync(
            int targetThroughput,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return this.GetRID(cancellationToken)
                 .ContinueWith(task => 
                    this.Client.Offers.ReplaceThroughputIfExistsAsync(
                        task.Result, 
                        targetThroughput, 
                        cancellationToken), 
                    cancellationToken)
                 .Unwrap();
        }

        internal Task<CosmosContainerSettings> GetCachedContainerSettingsAsync(CancellationToken cancellationToken)
        {
            return this.DocumentClient.GetCollectionCacheAsync()
                .ContinueWith(collectionCacheTask => 
                    collectionCacheTask.Result.ResolveByNameAsync(
                        this.LinkUri.OriginalString,
                        cancellationToken),
                    cancellationToken)
                .Unwrap();
        }

        // Name based look-up, needs re-computation and can't be cached
        internal Task<string> GetRID(CancellationToken cancellationToken)
        {
            return this.GetCachedContainerSettingsAsync(cancellationToken)
                            .ContinueWith(containerSettingsTask => 
                                containerSettingsTask.Result?.ResourceId, 
                                cancellationToken);
        }

        internal Task<PartitionKeyDefinition> GetPartitionKeyDefinitionAsync(CancellationToken cancellationToken)
        {
            return this.GetCachedContainerSettingsAsync(cancellationToken)
                            .ContinueWith(containerSettingsTask => 
                                containerSettingsTask.Result?.PartitionKey, 
                                cancellationToken);
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
                            false,
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
