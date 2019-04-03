//-----------------------------------------------------------------------
// <copyright file="CosmosQueryExecutionContextFactory.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Factory class for creating the appropriate DocumentQueryExecutionContext for the provided type of query.
    /// </summary>
    internal static class CosmosQueryExecutionContextFactory
    {
        private const int PageSizeFactorForTop = 5;

        public static async Task<IDocumentQueryExecutionContext> CreateItemQueryExecutionContextAsync(
            CosmosQueries client,
            ResourceType resourceTypeEnum,
            OperationType operationType,
            Type resourceType,
            SqlQuerySpec sqlQuerySpec,
            CosmosQueryRequestOptions queryRequestOptions,
            Uri resourceLink,
            bool isContinuationExpected,
            CancellationToken cancellationToken,
            Guid correlatedActivityId)
        {
            CosmosContainerSettings collection = null;
            if (resourceTypeEnum.IsCollectionChild())
            {
                CollectionCache collectionCache = await client.GetCollectionCacheAsync();
                using (
                    DocumentServiceRequest request = DocumentServiceRequest.Create(
                        OperationType.Query,
                        resourceTypeEnum,
                        resourceLink.OriginalString,
                        AuthorizationTokenType.Invalid)) //this request doesn't actually go to server
                {
                    collection = await collectionCache.ResolveCollectionAsync(request, cancellationToken);
                }
            }

            CosmosQueryContext cosmosQueryContext = new CosmosQueryContext(
                client: client,
                resourceTypeEnum: resourceTypeEnum,
                operationType: operationType,
                resourceType: resourceType,
                sqlQuerySpecFromUser: sqlQuerySpec,
                queryRequestOptions: queryRequestOptions,
                resourceLink: resourceLink,
                getLazyFeedResponse: isContinuationExpected,
                correlatedActivityId: correlatedActivityId);

            // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop, so need to bypass in that case.
            // We are also now bypassing this for 32 bit host process running even on Windows as there are many 32 bit apps that will not work without this
            if (CustomTypeExtensions.ByPassQueryParsing())
            {
                // We create a ProxyDocumentQueryExecutionContext that will be initialized with DefaultDocumentQueryExecutionContext
                // which will be used to send the query to Gateway and on getting 400(bad request) with 1004(cross partition query not servable), we initialize it with
                // PipelinedDocumentQueryExecutionContext by providing the partition query execution info that's needed(which we get from the exception returned from Gateway).
                CosmosProxyItemQueryExecutionContext proxyQueryExecutionContext =
                    CosmosProxyItemQueryExecutionContext.CreateAsync(
                        queryContext: cosmosQueryContext,
                        token: cancellationToken,
                        collection: collection,
                        isContinuationExpected: isContinuationExpected);

                return proxyQueryExecutionContext;
            }

            //todo:elasticcollections this may rely on information from collection cache which is outdated
            //if collection is deleted/created with same name.
            //need to make it not rely on information from collection cache.
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = await GetPartitionedQueryExecutionInfoAsync(
                cosmosQueryContext.QueryClient,
                sqlQuerySpec,
                collection.PartitionKey,
                true,
                isContinuationExpected,
                cancellationToken);

            List<PartitionKeyRange> targetRanges = await GetTargetPartitionKeyRanges(
                cosmosQueryContext.QueryClient,
                resourceLink.OriginalString,
                partitionedQueryExecutionInfo,
                collection,
                queryRequestOptions);

            return await CreateSpecializedDocumentQueryExecutionContext(
                cosmosQueryContext,
                partitionedQueryExecutionInfo,
                targetRanges,
                collection.ResourceId,
                isContinuationExpected,
                cancellationToken);
        }

        public static async Task<IDocumentQueryExecutionContext> CreateSpecializedDocumentQueryExecutionContext(
            CosmosQueryContext constructorParams,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<PartitionKeyRange> targetRanges,
            string collectionRid,
            bool isContinuationExpected,
            CancellationToken cancellationToken)
        {
            // Figure out the optimal page size.
            long initialPageSize = constructorParams.QueryRequestOptions.MaxItemCount.GetValueOrDefault(ParallelQueryConfig.GetConfig().ClientInternalPageSize);

            if (initialPageSize < -1 || initialPageSize == 0)
            {
                throw new BadRequestException(string.Format(CultureInfo.InvariantCulture, "Invalid MaxItemCount {0}", initialPageSize));
            }

            QueryInfo queryInfo = partitionedQueryExecutionInfo.QueryInfo;

            bool getLazyFeedResponse = queryInfo.HasTop;

            // We need to compute the optimal initial page size for order-by queries
            if (queryInfo.HasOrderBy)
            {
                int top;
                if (queryInfo.HasTop && (top = partitionedQueryExecutionInfo.QueryInfo.Top.Value) > 0)
                {
                    // All partitions should initially fetch about 1/nth of the top value.
                    long pageSizeWithTop = (long)Math.Min(
                        Math.Ceiling(top / (double)targetRanges.Count) * PageSizeFactorForTop,
                        top);

                    if (initialPageSize > 0)
                    {
                        initialPageSize = Math.Min(pageSizeWithTop, initialPageSize);
                    }
                    else
                    {
                        initialPageSize = pageSizeWithTop;
                    }
                }
                else if (isContinuationExpected)
                {
                    if (initialPageSize < 0)
                    {
                        // Max of what the user is willing to buffer and the default (note this is broken if MaxBufferedItemCount = -1)
                        initialPageSize = Math.Max(constructorParams.QueryRequestOptions.MaxBufferedItemCount, ParallelQueryConfig.GetConfig().DefaultMaximumBufferSize);
                    }

                    initialPageSize = (long)Math.Min(
                        Math.Ceiling(initialPageSize / (double)targetRanges.Count) * PageSizeFactorForTop,
                        initialPageSize);
                }
            }

            Debug.Assert(initialPageSize > 0 && initialPageSize <= int.MaxValue,
                string.Format(CultureInfo.InvariantCulture, "Invalid MaxItemCount {0}", initialPageSize));

            return await CosmosPipelinedItemQueryExecutionContext.CreateAsync(
                constructorParams,
                collectionRid,
                partitionedQueryExecutionInfo,
                targetRanges,
                (int)initialPageSize,
                constructorParams.QueryRequestOptions.RequestContinuation,
                cancellationToken);
        }

        /// <summary>
        /// Gets the list of partition key ranges. 
        /// 1. Check partition key range id
        /// 2. Check Partition key
        /// 3. Check the effective partition key
        /// 4. Get the range from the PartitionedQueryExecutionInfo
        /// </summary>
        internal static async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRanges(
            CosmosQueries queryClient,
            string resourceLink,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            CosmosContainerSettings collection,
            CosmosQueryRequestOptions queryRequestOptions)
        {
            List<PartitionKeyRange> targetRanges = null;
            if (queryRequestOptions.PartitionKey != null)
            {
                targetRanges = await GetTargetPartitionKeyRangesByEpkString(
                    queryClient,
                    resourceLink,
                    collection.ResourceId,
                    queryRequestOptions.PartitionKey.InternalKey.GetEffectivePartitionKeyString(collection.PartitionKey));
            }
            else if (TryGetEpkProperty(queryRequestOptions, out string effectivePartitionKeyString))
            {
                targetRanges = await GetTargetPartitionKeyRangesByEpkString(
                    queryClient,
                    resourceLink,
                    collection.ResourceId,
                    effectivePartitionKeyString);
            }
            else
            {
                targetRanges = await GetTargetPartitionKeyRanges(
                    queryClient,
                    resourceLink,
                    collection.ResourceId,
                    partitionedQueryExecutionInfo.QueryRanges);
            }

            return targetRanges;
        }

        public static async Task<PartitionedQueryExecutionInfo> GetPartitionedQueryExecutionInfoAsync(
            CosmosQueries queryClient,
            SqlQuerySpec sqlQuerySpec,
            PartitionKeyDefinition partitionKeyDefinition,
            bool requireFormattableOrderByQuery,
            bool isContinuationExpected,
            CancellationToken cancellationToken)
        {
            // $ISSUE-felixfan-2016-07-13: We should probably get PartitionedQueryExecutionInfo from Gateway in GatewayMode

            QueryPartitionProvider queryPartitionProvider = await queryClient.GetQueryPartitionProviderAsync(cancellationToken);
            return queryPartitionProvider.GetPartitionedQueryExecutionInfo(sqlQuerySpec, partitionKeyDefinition, requireFormattableOrderByQuery, isContinuationExpected);
        }

        internal static Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesByEpkString(
            CosmosQueries queryClient,
            string resourceLink,
            string collectionResourceId,
            string effectivePartitionKeyString)
        {
            return GetTargetPartitionKeyRanges(
                queryClient,
                resourceLink,
                collectionResourceId,
                new List<Range<string>>
                {
                    Range<string>.GetPointRange(effectivePartitionKeyString)
                });
        }

        internal static async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRanges(
            CosmosQueries queryClient,
            string resourceLink,
            string collectionResourceId,
            List<Range<string>> providedRanges)
        {
            if (string.IsNullOrEmpty(nameof(collectionResourceId)))
            {
                throw new ArgumentNullException();
            }

            if (providedRanges == null || !providedRanges.Any())
            {
                throw new ArgumentNullException(nameof(providedRanges));
            }

            IRoutingMapProvider routingMapProvider = await queryClient.GetRoutingMapProviderAsync();

            List<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(collectionResourceId, providedRanges);
            if (ranges == null && PathsHelper.IsNameBased(resourceLink))
            {
                // Refresh the cache and don't try to re-resolve collection as it is not clear what already
                // happened based on previously resolved collection rid.
                // Return NotFoundException this time. Next query will succeed.
                // This can only happen if collection is deleted/created with same name and client was not restarted
                // in between.
                CollectionCache collectionCache = await queryClient.GetCollectionCacheAsync();
                collectionCache.Refresh(resourceLink);
            }

            if (ranges == null)
            {
                throw new NotFoundException($"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}: GetTargetPartitionKeyRanges(collectionResourceId:{collectionResourceId}, providedRanges: {string.Join(",", providedRanges)} failed due to stale cache");
            }

            return ranges;
        }


        private static bool ShouldCreateSpecializedDocumentQueryExecutionContext(
            ResourceType resourceTypeEnum,
            CosmosQueryRequestOptions feedOptions,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            PartitionKeyDefinition partitionKeyDefinition,
            bool isContinuationExpected)
        {
            // We need to aggregate the total results with Pipelined~Context if isContinuationExpected is false.
            return
                (CosmosQueryExecutionContextFactory.IsCrossPartitionQuery(
                    resourceTypeEnum,
                    feedOptions,
                    partitionKeyDefinition,
                    partitionedQueryExecutionInfo) &&
                 (CosmosQueryExecutionContextFactory.IsTopOrderByQuery(partitionedQueryExecutionInfo) ||
                  CosmosQueryExecutionContextFactory.IsAggregateQuery(partitionedQueryExecutionInfo) ||
                  CosmosQueryExecutionContextFactory.IsOffsetLimitQuery(partitionedQueryExecutionInfo) ||
                  CosmosQueryExecutionContextFactory.IsParallelQuery(feedOptions))) ||
                  // Even if it's single partition query we create a specialized context to aggregate the aggregates and distinct of distinct.
                  CosmosQueryExecutionContextFactory.IsAggregateQueryWithoutContinuation(
                      partitionedQueryExecutionInfo,
                      isContinuationExpected) ||
                  CosmosQueryExecutionContextFactory.IsDistinctQuery(partitionedQueryExecutionInfo);
        }

        private static bool IsCrossPartitionQuery(
            ResourceType resourceTypeEnum,
            CosmosQueryRequestOptions feedOptions,
            PartitionKeyDefinition partitionKeyDefinition,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return resourceTypeEnum.IsPartitioned()
                && (feedOptions.PartitionKey == null && feedOptions.EnableCrossPartitionQuery)
                && (partitionKeyDefinition.Paths.Count > 0)
                && !(partitionedQueryExecutionInfo.QueryRanges.Count == 1 && partitionedQueryExecutionInfo.QueryRanges[0].IsSingleValue);
        }

        private static bool IsTopOrderByQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return (partitionedQueryExecutionInfo.QueryInfo != null)
                && (partitionedQueryExecutionInfo.QueryInfo.HasOrderBy || partitionedQueryExecutionInfo.QueryInfo.HasTop);
        }

        private static bool IsAggregateQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return (partitionedQueryExecutionInfo.QueryInfo != null)
                && (partitionedQueryExecutionInfo.QueryInfo.HasAggregates);
        }

        private static bool IsAggregateQueryWithoutContinuation(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo, bool isContinuationExpected)
        {
            return IsAggregateQuery(partitionedQueryExecutionInfo) && !isContinuationExpected;
        }

        private static bool IsDistinctQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return partitionedQueryExecutionInfo.QueryInfo.HasDistinct;
        }

        private static bool IsParallelQuery(CosmosQueryRequestOptions feedOptions)
        {
            return (feedOptions.MaxConcurrency != 0);
        }

        private static bool IsOffsetLimitQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return partitionedQueryExecutionInfo.QueryInfo.HasOffset && partitionedQueryExecutionInfo.QueryInfo.HasLimit;
        }

        private static bool TryGetEpkProperty(
            CosmosQueryRequestOptions queryRequestOptions,
            out string effectivePartitionKeyString)
        {
            if (queryRequestOptions?.Properties != null
                && queryRequestOptions.Properties.TryGetValue(
                   WFConstants.BackendHeaders.EffectivePartitionKeyString,
                   out object effectivePartitionKeyStringObject))
            {
                effectivePartitionKeyString = effectivePartitionKeyStringObject as string;
                if (string.IsNullOrEmpty(effectivePartitionKeyString))
                {
                    throw new ArgumentOutOfRangeException(nameof(effectivePartitionKeyString));
                }

                return true;
            }

            effectivePartitionKeyString = null;
            return false;
        }
    }
}
