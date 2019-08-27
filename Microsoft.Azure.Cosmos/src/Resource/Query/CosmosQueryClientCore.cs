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
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    internal class CosmosQueryClientCore : CosmosQueryClient
    {
        private readonly CosmosClientContext clientContext;
        private readonly ContainerCore cosmosContainerCore;
        private readonly DocumentClient documentClient;
        private readonly SemaphoreSlim semaphore;
        private QueryPartitionProvider queryPartitionProvider;

        internal CosmosQueryClientCore(
            CosmosClientContext clientContext,
            ContainerCore cosmosContainerCore)
        {
            this.clientContext = clientContext ?? throw new ArgumentException(nameof(clientContext));
            this.cosmosContainerCore = cosmosContainerCore;
            this.documentClient = this.clientContext.DocumentClient;
            this.semaphore = new SemaphoreSlim(1, 1);
        }

        internal override Action<IQueryable> OnExecuteScalarQueryCallback => this.documentClient.OnExecuteScalarQueryCallback;

        internal override async Task<CollectionCache> GetCollectionCacheAsync()
        {
            return await this.documentClient.GetCollectionCacheAsync();
        }

        internal override Task<ContainerProperties> GetCachedContainerPropertiesAsync(
            Uri containerLink,
            CancellationToken cancellationToken)
        {
            return this.clientContext.GetCachedContainerPropertiesAsync(
                containerLink.OriginalString, 
                cancellationToken);
        }

        internal override async Task<IRoutingMapProvider> GetRoutingMapProviderAsync()
        {
            return await this.documentClient.GetPartitionKeyRangeCacheAsync();
        }

        internal override async Task<PartitionedQueryExecutionInfo> GetPartitionedQueryExecutionInfoAsync(
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            bool allowNonValueAggregateQuery,
            bool hasLogicalPartitionKey,
            CancellationToken cancellationToken)
        {
            if (this.queryPartitionProvider == null)
            {
                try
                {
                    await this.semaphore.WaitAsync(cancellationToken);

                    if (this.queryPartitionProvider == null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        IDictionary<string, object> queryConfiguration = await this.documentClient.GetQueryEngineConfigurationAsync();
                        this.queryPartitionProvider = new QueryPartitionProvider(queryConfiguration);
                    }
                }
                finally
                {
                    this.semaphore.Release();
                }
            }

            return this.queryPartitionProvider.GetPartitionedQueryExecutionInfo(
                sqlQuerySpec,
                partitionKeyDefinition,
                requireFormattableOrderByQuery,
                isContinuationExpected,
                allowNonValueAggregateQuery,
                hasLogicalPartitionKey);
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

        internal override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync()
        {
            return this.documentClient.GetPartitionKeyRangeCacheAsync();
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
                    requestOptions.CosmosSerializationFormatOptions);

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

        internal override async Task ForceRefreshCollectionCacheAsync(string collectionLink, CancellationToken cancellationToken)
        {
            this.ClearSessionTokenCache(collectionLink);

            CollectionCache collectionCache = await this.GetCollectionCacheAsync();
            using (Documents.DocumentServiceRequest request = Documents.DocumentServiceRequest.Create(
               Documents.OperationType.Query,
               Documents.ResourceType.Collection,
               collectionLink,
               Documents.AuthorizationTokenType.Invalid)) //this request doesn't actually go to server
            {
                request.ForceNameCacheRefresh = true;
                await collectionCache.ResolveCollectionAsync(request, cancellationToken);
            }
        }
    }
}
