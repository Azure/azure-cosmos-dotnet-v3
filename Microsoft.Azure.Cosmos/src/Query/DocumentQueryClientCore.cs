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
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using static Microsoft.Azure.Documents.RuntimeConstants;

    internal class DocumentQueryClientCore : CosmosQueryClient
    {
        private readonly DocumentClient documentClient;
        private readonly SemaphoreSlim semaphore;
        private QueryPartitionProvider queryPartitionProvider;

        internal DocumentQueryClientCore(
            DocumentClient documentClient)
        {
            this.documentClient = documentClient;
            this.semaphore = new SemaphoreSlim(1, 1);
        }

        internal override Action<IQueryable> OnExecuteScalarQueryCallback => this.documentClient.OnExecuteScalarQueryCallback;

        internal override async Task<CollectionCache> GetCollectionCacheAsync()
        {
            return await this.documentClient.GetCollectionCacheAsync();
        }

        internal override async Task<ContainerProperties> GetCachedContainerPropertiesAsync(
            Uri containerLink,
            CancellationToken cancellationToken)
        {
            ClientCollectionCache collectionCache = await this.documentClient.GetCollectionCacheAsync();

            return await collectionCache.ResolveByNameAsync(
                HttpConstants.Versions.CurrentVersion,
                containerLink.OriginalString,
                cancellationToken);
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
            RequestMessage requestMessage = new RequestMessage();
            requestOptions.PopulateRequestOptions(requestMessage);
            INameValueCollection headers = requestMessage.Headers.CosmosMessageHeaders;
            if (continuationToken != null)
            {
                headers[HttpConstants.HttpHeaders.Continuation] = continuationToken;
            }

            headers[HttpConstants.HttpHeaders.IsContinuationExpected] = isContinuationExpected.ToString();
            headers[HttpConstants.HttpHeaders.PageSize] = pageSize.ToString(CultureInfo.InvariantCulture);

            if (requestOptions.ConsistencyLevel.HasValue)
            {
                Documents.ConsistencyLevel defaultConsistencyLevel = (Documents.ConsistencyLevel)await this.documentClient.GetDefaultConsistencyLevelAsync();
                Documents.ConsistencyLevel? desiredConsistencyLevel = await this.documentClient.GetDesiredConsistencyLevelAsync();
                if (!string.IsNullOrEmpty(requestOptions.SessionToken) && !ReplicatedResourceClient.IsReadingFromMaster(resourceType, OperationType.ReadFeed))
                {
                    if (defaultConsistencyLevel == Documents.ConsistencyLevel.Session || (desiredConsistencyLevel.HasValue && desiredConsistencyLevel.Value == Documents.ConsistencyLevel.Session))
                    {
                        // Query across partitions is not supported today. Master resources (for e.g., database) 
                        // can span across partitions, whereas server resources (viz: collection, document and attachment)
                        // don't span across partitions. Hence, session token returned by one partition should not be used 
                        // when quering resources from another partition. 
                        // Since master resources can span across partitions, don't send session token to the backend.
                        // As master resources are sync replicated, we should always get consistent query result for master resources,
                        // irrespective of the chosen replica.
                        // For server resources, which don't span partitions, specify the session token 
                        // for correct replica to be chosen for servicing the query result.
                        headers.Add(HttpConstants.HttpHeaders.SessionToken, requestOptions.SessionToken);
                    }
                }
            }

            ResponseMessage responseMessage = null;
            using (DocumentServiceRequest request = DocumentQueryExecutionContextBase.CreateQueryDocumentServiceRequest(
                headers,
                sqlQuerySpec,
                this.documentClient.QueryCompatibilityMode,
                resourceType,
                resourceUri.OriginalString))
            {
                if (requestOptions.PartitionKey == null)
                {
                    if (resourceType.IsPartitioned())
                    {
                        request.RouteTo(partitionKeyRange);
                    }
                }

                DocumentServiceResponse dsr = await this.documentClient.ExecuteQueryAsync(
                    request,
                    new NonRetriableInvalidPartitionExceptionRetryPolicy(await this.GetCollectionCacheAsync(), this.documentClient.ResetSessionTokenRetryPolicy.GetRequestPolicy()),
                    cancellationToken);

                responseMessage = dsr.ToCosmosResponseMessage(requestMessage);
            }

            return this.GetCosmosElementResponse(requestOptions, resourceType, containerResourceId, responseMessage);
        }

        internal override Task<PartitionedQueryExecutionInfo> ExecuteQueryPlanRequestAsync(
            Uri resourceUri,
            ResourceType resourceType,
            OperationType operationType,
            SqlQuerySpec sqlQuerySpec,
            Action<RequestMessage> requestEnricher,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException("V2 Document Client does not currently support execute query plan request operations.");
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

            IRoutingMapProvider routingMapProvider = await this.documentClient.GetPartitionKeyRangeCacheAsync();

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
            ISessionContainer sessionContainer = this.documentClient.sessionContainer;
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

        internal override Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(
            string collectionResourceId,
            Range<string> range,
            bool forceRefresh = false)
        {
            throw new NotImplementedException();
        }
    }
}