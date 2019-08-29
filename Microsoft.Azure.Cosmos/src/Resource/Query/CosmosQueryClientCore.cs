//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    internal class CosmosQueryClientCore : CosmosQueryClient
    {
        private readonly CosmosClientContext clientContext;
        private readonly ContainerCore cosmosContainerCore;
        private readonly IDocumentQueryClient DocumentQueryClient;

        internal CosmosQueryClientCore(
            CosmosClientContext clientContext,
            ContainerCore cosmosContainerCore)
        {
            this.clientContext = clientContext ?? throw new ArgumentException(nameof(clientContext));
            this.cosmosContainerCore = cosmosContainerCore ?? throw new ArgumentException(nameof(cosmosContainerCore));
            this.DocumentQueryClient = clientContext.DocumentQueryClient ?? throw new ArgumentException(nameof(clientContext));
        }

        internal override Action<IQueryable> OnExecuteScalarQueryCallback => this.DocumentQueryClient.OnExecuteScalarQueryCallback;

        internal override Task<CollectionCache> GetCollectionCacheAsync()
        {
            return this.DocumentQueryClient.GetCollectionCacheAsync();
        }

        internal override Task<ContainerProperties> GetCachedContainerPropertiesAsync(CancellationToken cancellationToken)
        {
            return this.cosmosContainerCore.GetCachedContainerPropertiesAsync(cancellationToken);
        }

        internal override Task<IRoutingMapProvider> GetRoutingMapProviderAsync()
        {
            return this.DocumentQueryClient.GetRoutingMapProviderAsync();
        }

        internal override async Task<PartitionedQueryExecutionInfo> GetPartitionedQueryExecutionInfoAsync(
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionkey,
            CancellationToken cancellationToken)
        {
            QueryPartitionProvider queryPartitionProvider = await this.DocumentQueryClient.GetQueryPartitionProviderAsync(cancellationToken);
            return queryPartitionProvider.GetPartitionedQueryExecutionInfo(
                sqlQuerySpec,
                partitionKeyDefinition,
                requireFormattableOrderByQuery,
                isContinuationExpected,
                allowNonValueAggregateQuery,
                hasLogicalPartitionkey);
        }

        internal override async Task<QueryResponse> ExecuteItemQueryAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            string containerResourceId,
            QueryRequestOptions requestOptions,
            SqlQuerySpec sqlQuerySpec,
            string continuationToken,
            PartitionKeyRangeIdentity partitionKeyRange,
            bool isContinuationExpected,
            int pageSize,
            CancellationToken cancellationToken)
        {
            ResponseMessage message = await this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: requestOptions,
                partitionKey: requestOptions.PartitionKey,
                cosmosContainerCore: this.cosmosContainerCore,
                streamPayload: this.clientContext.SqlQuerySpecSerializer.ToStream(sqlQuerySpec),
                requestEnricher: (cosmosRequestMessage) =>
                {
                    this.PopulatePartitionKeyRangeInfo(cosmosRequestMessage, partitionKeyRange);
                    cosmosRequestMessage.Headers.Add(
                        HttpConstants.HttpHeaders.IsContinuationExpected,
                        isContinuationExpected.ToString());
                    QueryRequestOptions.FillContinuationToken(
                        cosmosRequestMessage,
                        continuationToken);
                    QueryRequestOptions.FillMaxItemCount(
                        cosmosRequestMessage,
                        pageSize);
                    cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.ContentType, MediaTypes.QueryJson);
                    cosmosRequestMessage.Headers.Add(HttpConstants.HttpHeaders.IsQuery, bool.TrueString);
                },
                cancellationToken: cancellationToken);

            return this.GetCosmosElementResponse(
                requestOptions,
                resourceType,
                containerResourceId,
                message);
        }

        internal override async Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            Action<RequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo;
            using (ResponseMessage message = await this.clientContext.ProcessResourceOperationStreamAsync(
                resourceUri: resourceUri,
                resourceType: resourceType,
                operationType: operationType,
                requestOptions: null,
                partitionKey: null,
                cosmosContainerCore: this.cosmosContainerCore,
                streamPayload: this.clientContext.SqlQuerySpecSerializer.ToStream(sqlQuerySpec),
                requestEnricher: requestEnricher,
                cancellationToken: cancellationToken))
            {
                // Syntax exception are argument exceptions and thrown to the user.
                message.EnsureSuccessStatusCode();
                partitionedQueryExecutionInfo = this.clientContext.CosmosSerializer.FromStream<PartitionedQueryExecutionInfo>(message.Content);
            }

            return partitionedQueryExecutionInfo;
        }

        internal override Task<Documents.ConsistencyLevel> GetDefaultConsistencyLevelAsync()
        {
            return this.DocumentQueryClient.GetDefaultConsistencyLevelAsync();
        }

        internal override Task<Documents.ConsistencyLevel?> GetDesiredConsistencyLevelAsync()
        {
            return this.DocumentQueryClient.GetDesiredConsistencyLevelAsync();
        }

        internal override Task EnsureValidOverwriteAsync(Documents.ConsistencyLevel desiredConsistencyLevel)
        {
            return this.DocumentQueryClient.EnsureValidOverwriteAsync(desiredConsistencyLevel);
        }

        internal override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync()
        {
            return this.DocumentQueryClient.GetPartitionKeyRangeCacheAsync();
        }

        internal override Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkStringAsync(
            string resourceLink,
            string collectionResourceId,
            string effectivePartitionKeyString)
        {
            return this.GetTargetPartitionKeyRangesAsync(
                resourceLink,
                collectionResourceId,
                new List<Range<string>>
                {
                    Range<string>.GetPointRange(effectivePartitionKeyString)
                });
        }

        internal override async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(
            string resourceLink,
            string collectionResourceId,
            List<Range<string>> providedRanges)
        {
            if (string.IsNullOrEmpty(collectionResourceId))
            {
                throw new ArgumentNullException(nameof(collectionResourceId));
            }

            if (providedRanges == null ||
                !providedRanges.Any() ||
                providedRanges.Any(x => x == null))
            {
                throw new ArgumentNullException(nameof(providedRanges));
            }

            IRoutingMapProvider routingMapProvider = await this.GetRoutingMapProviderAsync();

            List<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(collectionResourceId, providedRanges);
            if (ranges == null && PathsHelper.IsNameBased(resourceLink))
            {
                // Refresh the cache and don't try to re-resolve collection as it is not clear what already
                // happened based on previously resolved collection rid.
                // Return NotFoundException this time. Next query will succeed.
                // This can only happen if collection is deleted/created with same name and client was not restarted
                // in between.
                CollectionCache collectionCache = await this.GetCollectionCacheAsync();
                collectionCache.Refresh(resourceLink);
            }

            if (ranges == null)
            {
                throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: GetTargetPartitionKeyRanges(collectionResourceId:{collectionResourceId}, providedRanges: {string.Join(",", providedRanges)} failed due to stale cache");
            }

            return ranges;
        }

        internal override bool ByPassQueryParsing()
        {
            return CustomTypeExtensions.ByPassQueryParsing();
        }

        internal override void ClearSessionTokenCache(string collectionFullName)
        {
            ISessionContainer sessionContainer = this.clientContext.DocumentClient.sessionContainer;
            sessionContainer.ClearTokenByCollectionFullname(collectionFullName);
        }

        private QueryResponse GetCosmosElementResponse(
            QueryRequestOptions requestOptions,
            ResourceType resourceType,
            string containerResourceId,
            ResponseMessage cosmosResponseMessage)
        {
            using (cosmosResponseMessage)
            {
                if (!cosmosResponseMessage.IsSuccessStatusCode)
                {
                    return QueryResponse.CreateFailure(
                        CosmosQueryResponseMessageHeaders.ConvertToQueryHeaders(cosmosResponseMessage.Headers, resourceType, containerResourceId),
                        cosmosResponseMessage.StatusCode,
                        cosmosResponseMessage.RequestMessage,
                        cosmosResponseMessage.ErrorMessage,
                        cosmosResponseMessage.Error);
                }

                MemoryStream memoryStream = cosmosResponseMessage.Content as MemoryStream;
                if (memoryStream == null)
                {
                    memoryStream = new MemoryStream();
                    cosmosResponseMessage.Content.CopyTo(memoryStream);
                }

                long responseLengthBytes = memoryStream.Length;
                CosmosArray cosmosArray = CosmosElementSerializer.ToCosmosElements(
                    memoryStream,
                    resourceType,
                    requestOptions.CosmosSerializationOptions);

                int itemCount = cosmosArray.Count;
                return QueryResponse.CreateSuccess(
                    result: cosmosArray,
                    count: itemCount,
                    responseHeaders: CosmosQueryResponseMessageHeaders.ConvertToQueryHeaders(cosmosResponseMessage.Headers, resourceType, containerResourceId),
                    responseLengthBytes: responseLengthBytes);
            }
        }

        private void PopulatePartitionKeyRangeInfo(
            RequestMessage request,
            PartitionKeyRangeIdentity partitionKeyRangeIdentity)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.ResourceType.IsPartitioned())
            {
                // If the request already has the logical partition key,
                // then we shouldn't add the physical partition key range id.

                bool hasPartitionKey = request.Headers.PartitionKey != null;
                if (!hasPartitionKey)
                {
                    request
                        .ToDocumentServiceRequest()
                        .RouteTo(partitionKeyRangeIdentity);
                }
            }
        }
    }
}
