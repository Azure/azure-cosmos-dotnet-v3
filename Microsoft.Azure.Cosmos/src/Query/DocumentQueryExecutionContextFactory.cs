//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Cosmos.Routing;

    internal static class DocumentQueryExecutionContextFactory
    {
        private const int PageSizeFactorForTop = 5;

        public static async Task<IDocumentQueryExecutionContext> CreateDocumentQueryExecutionContextAsync(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            IEnumerable<string> documentFeedLinks,
            bool isContinuationExpected,
            CancellationToken token,
            Guid correlatedActivityId)
        {
            return await MultiCollectionDocumentQueryExecutionContext.CreateAsync(
                client,
                resourceTypeEnum,
                resourceType,
                expression,
                feedOptions,
                documentFeedLinks,
                isContinuationExpected,
                token,
                correlatedActivityId);
        }

        public static async Task<IDocumentQueryExecutionContext> CreateDocumentQueryExecutionContextAsync(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            string resourceLink,
            bool isContinuationExpected,
            CancellationToken token,
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
                        resourceLink,
                        AuthorizationTokenType.Invalid)) //this request doesnt actually go to server
                {
                    collection = await collectionCache.ResolveCollectionAsync(request, token);
                }
            }

            // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop, so need to bypass in that case.
            // We are also now bypassing this for 32 bit host process running even on Windows as there are many 32 bit apps that will not work without this
            if (CustomTypeExtensions.ByPassQueryParsing())
            {
                // We create a ProxyDocumentQueryExecutionContext that will be initialized with DefaultDocumentQueryExecutionContext
                // which will be used to send the query to Gateway and on getting 400(bad request) with 1004(cross parition query not servable), we initialize it with
                // PipelinedDocumentQueryExecutionContext by providing the parition query execution info that's needed(which we get from the exception returned from Gateway).
                ProxyDocumentQueryExecutionContext proxyQueryExecutionContext =
                    await ProxyDocumentQueryExecutionContext.CreateAsync(
                        client,
                        resourceTypeEnum,
                        resourceType,
                        expression,
                        feedOptions,
                        resourceLink,
                        token,
                        collection,
                        isContinuationExpected,
                        correlatedActivityId);

                return proxyQueryExecutionContext;
            }

            DefaultDocumentQueryExecutionContext queryExecutionContext = await DefaultDocumentQueryExecutionContext.CreateAsync(
                client,
                resourceTypeEnum,
                resourceType,
                expression,
                feedOptions,
                resourceLink,
                isContinuationExpected,
                token,
                correlatedActivityId);

            // If isContinuationExpected is false, we want to check if there are aggregates.
            if (resourceTypeEnum.IsCollectionChild()
                && resourceTypeEnum.IsPartitioned()
                && (feedOptions.EnableCrossPartitionQuery || !isContinuationExpected))
            {
                //todo:elasticcollections this may rely on information from collection cache which is outdated
                //if collection is deleted/created with same name.
                //need to make it not rely on information from collection cache.
                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo =
                    (await queryExecutionContext.GetPartitionedQueryExecutionInfoAsync(
                    collection.PartitionKey,
                    true,
                    isContinuationExpected,
                    token));

                if (DocumentQueryExecutionContextFactory.ShouldCreateSpecializedDocumentQueryExecutionContext(
                        resourceTypeEnum,
                        feedOptions,
                        partitionedQueryExecutionInfo,
                        collection.PartitionKey,
                        isContinuationExpected))
                {
                    List<PartitionKeyRange> targetRanges;
                    if (!string.IsNullOrEmpty(feedOptions.PartitionKeyRangeId))
                    {
                        targetRanges =
                            new List<PartitionKeyRange>
                            {
                                await queryExecutionContext.GetTargetPartitionKeyRangeById(
                                    collection.ResourceId,
                                    feedOptions.PartitionKeyRangeId)
                            };
                    }
                    else
                    {
                        List<Range<string>> queryRanges = partitionedQueryExecutionInfo.QueryRanges;
                        if (feedOptions.PartitionKey != null)
                        {
                            queryRanges = new List<Range<string>>
                            {
                                Range<string>.GetPointRange(
                                    feedOptions.PartitionKey.InternalKey.GetEffectivePartitionKeyString(
                                        collection.PartitionKey))
                            };
                        }

                        targetRanges =
                            await queryExecutionContext.GetTargetPartitionKeyRanges(collection.ResourceId, queryRanges);
                    }

                    return await CreateSpecializedDocumentQueryExecutionContext(
                        client,
                        resourceTypeEnum,
                        resourceType,
                        expression,
                        feedOptions,
                        resourceLink,
                        isContinuationExpected,
                        partitionedQueryExecutionInfo,
                        targetRanges,
                        collection.ResourceId,
                        token,
                        correlatedActivityId);
                }
            }

            return queryExecutionContext;
        }

        public static async Task<IDocumentQueryExecutionContext> CreateSpecializedDocumentQueryExecutionContext(
            IDocumentQueryClient client,
            ResourceType resourceTypeEnum,
            Type resourceType,
            Expression expression,
            FeedOptions feedOptions,
            string resourceLink,
            bool isContinuationExpected,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            List<PartitionKeyRange> targetRanges,
            string collectionRid,
            CancellationToken token,
            Guid correlatedActivityId)
        {
            long initialPageSize = feedOptions.MaxItemCount.GetValueOrDefault(ParallelQueryConfig.GetConfig().ClientInternalPageSize);

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
                        initialPageSize = (long)Math.Max(feedOptions.MaxBufferedItemCount, ParallelQueryConfig.GetConfig().DefaultMaximumBufferSize);
                    }

                    initialPageSize = (long)Math.Min(
                        Math.Ceiling(initialPageSize / (double)targetRanges.Count) * PageSizeFactorForTop,
                        initialPageSize);
                }
            }

            Debug.Assert(initialPageSize > 0 && initialPageSize <= int.MaxValue,
                string.Format(CultureInfo.InvariantCulture, "Invalid MaxItemCount {0}", initialPageSize));

            return await PipelinedDocumentQueryExecutionContext.CreateAsync(
                        client,
                        resourceTypeEnum,
                        resourceType,
                        expression,
                        feedOptions,
                        resourceLink,
                        collectionRid,
                        partitionedQueryExecutionInfo,
                        targetRanges,
                        (int)initialPageSize,
                        isContinuationExpected,
                        getLazyFeedResponse,
                        token,
                        correlatedActivityId);
        }

        private static bool ShouldCreateSpecializedDocumentQueryExecutionContext(
            ResourceType resourceTypeEnum,
            FeedOptions feedOptions,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            PartitionKeyDefinition partitionKeyDefinition,
            bool isContinuationExpected)
        {
            // We need to aggregate the total results with Pipelined~Context if isContinuationExpected is false.
            return
                (DocumentQueryExecutionContextFactory.IsCrossPartitionQuery(
                    resourceTypeEnum,
                    feedOptions,
                    partitionKeyDefinition,
                    partitionedQueryExecutionInfo) &&
                 (DocumentQueryExecutionContextFactory.IsTopOrderByQuery(partitionedQueryExecutionInfo) ||
                  DocumentQueryExecutionContextFactory.IsAggregateQuery(partitionedQueryExecutionInfo) ||
                  DocumentQueryExecutionContextFactory.IsParallelQuery(feedOptions)) ||
                  !string.IsNullOrEmpty(feedOptions.PartitionKeyRangeId)) ||

                  // Even if it's single partition query we create a specialized context to aggregate the aggregates and distinct of distinct.
                  DocumentQueryExecutionContextFactory.IsAggregateQueryWithoutContinuation(
                    partitionedQueryExecutionInfo,
                    isContinuationExpected) ||
                  DocumentQueryExecutionContextFactory.IsDistinctQuery(partitionedQueryExecutionInfo);
        }

        private static bool IsCrossPartitionQuery(
            ResourceType resourceTypeEnum,
            FeedOptions feedOptions,
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

        private static bool IsParallelQuery(FeedOptions feedOptions)
        {
            return (feedOptions.MaxDegreeOfParallelism != 0);
        }

        private static bool IsDistinctQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return partitionedQueryExecutionInfo.QueryInfo.HasDistinct;
        }
    }
}
