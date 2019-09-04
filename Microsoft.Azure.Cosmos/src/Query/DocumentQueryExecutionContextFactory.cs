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
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Common;
    using Microsoft.Azure.Cosmos.Query.ParallelQuery;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Factory class for creating the appropriate DocumentQueryExecutionContext for the provided type of query.
    /// </summary>
    internal static class DocumentQueryExecutionContextFactory
    {
        private const int PageSizeFactorForTop = 5;

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
            ContainerProperties collection = null;
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

                if (feedOptions != null && feedOptions.PartitionKey != null && feedOptions.PartitionKey.Equals(Documents.PartitionKey.None))
                {
                    feedOptions.PartitionKey = Documents.PartitionKey.FromInternalKey(collection.GetNoneValue());
                }
            }

            DocumentQueryExecutionContextBase.InitParams constructorParams = new DocumentQueryExecutionContextBase.InitParams(
                client,
                resourceTypeEnum,
                resourceType,
                expression,
                feedOptions,
                resourceLink,
                false,
                correlatedActivityId);

            // For non-Windows platforms(like Linux and OSX) in .NET Core SDK, we cannot use ServiceInterop, so need to bypass in that case.
            // We are also now bypassing this for 32 bit host process running even on Windows as there are many 32 bit apps that will not work without this
            if (CustomTypeExtensions.ByPassQueryParsing())
            {
                // We create a ProxyDocumentQueryExecutionContext that will be initialized with DefaultDocumentQueryExecutionContext
                // which will be used to send the query to Gateway and on getting 400(bad request) with 1004(cross partition query not servable), we initialize it with
                // PipelinedDocumentQueryExecutionContext by providing the partition query execution info that's needed(which we get from the exception returned from Gateway).
                ProxyDocumentQueryExecutionContext proxyQueryExecutionContext =
                    ProxyDocumentQueryExecutionContext.Create(
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
                constructorParams, isContinuationExpected, token);

            // If isContinuationExpected is false, we want to check if there are aggregates.
            if (
                resourceTypeEnum.IsCollectionChild()
                && resourceTypeEnum.IsPartitioned()
                && (feedOptions.EnableCrossPartitionQuery || !isContinuationExpected))
            {
                //todo:elasticcollections this may rely on information from collection cache which is outdated
                //if collection is deleted/created with same name.
                //need to make it not rely on information from collection cache.
                PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = await queryExecutionContext.GetPartitionedQueryExecutionInfoAsync(
                    partitionKeyDefinition: collection.PartitionKey,
                    requireFormattableOrderByQuery: true,
                    isContinuationExpected: isContinuationExpected,
                    allowNonValueAggregateQuery: true,
                    hasLogicalPartitionKey: feedOptions.PartitionKey != null,
                    cancellationToken: token);

                if (DocumentQueryExecutionContextFactory.ShouldCreateSpecializedDocumentQueryExecutionContext(
                        resourceTypeEnum,
                        feedOptions,
                        partitionedQueryExecutionInfo,
                        collection.PartitionKey,
                        isContinuationExpected))
                {
                    List<PartitionKeyRange> targetRanges = await GetTargetPartitionKeyRangesAsync(
                        queryExecutionContext,
                        partitionedQueryExecutionInfo,
                        collection,
                        feedOptions);

                    // Devnote this will get replace by the new v3 to v2 logic
                    throw new NotSupportedException("v2 query excution context is currently not supported.");
                }
            }

            return queryExecutionContext;
        }

        /// <summary>
        /// Gets the list of partition key ranges. 
        /// 1. Check partition key range id
        /// 2. Check Partition key
        /// 3. Check the effective partition key
        /// 4. Get the range from the PartitionedQueryExecutionInfo
        /// </summary>
        internal static async Task<List<PartitionKeyRange>> GetTargetPartitionKeyRangesAsync(
            DefaultDocumentQueryExecutionContext queryExecutionContext,
            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo,
            ContainerProperties collection,
            FeedOptions feedOptions)
        {
            List<PartitionKeyRange> targetRanges = null;
            if (!string.IsNullOrEmpty(feedOptions.PartitionKeyRangeId))
            {
                targetRanges = new List<PartitionKeyRange>()
                {
                    await queryExecutionContext.GetTargetPartitionKeyRangeByIdAsync(
                                    collection.ResourceId,
                                    feedOptions.PartitionKeyRangeId)
                };
            }
            else if (feedOptions.PartitionKey != null)
            {
                targetRanges = await queryExecutionContext.GetTargetPartitionKeyRangesByEpkStringAsync(
                    collection.ResourceId,
                    feedOptions.PartitionKey.InternalKey.GetEffectivePartitionKeyString(collection.PartitionKey));
            }
            else if (TryGetEpkProperty(feedOptions, out string effectivePartitionKeyString))
            {
                targetRanges = await queryExecutionContext.GetTargetPartitionKeyRangesByEpkStringAsync(
                    collection.ResourceId,
                    effectivePartitionKeyString);
            }
            else
            {
                targetRanges = await queryExecutionContext.GetTargetPartitionKeyRangesAsync(
                    collection.ResourceId,
                    partitionedQueryExecutionInfo.QueryRanges);
            }

            return targetRanges;
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
                  DocumentQueryExecutionContextFactory.IsOffsetLimitQuery(partitionedQueryExecutionInfo) ||
                  DocumentQueryExecutionContextFactory.IsParallelQuery(feedOptions)) ||
                  !string.IsNullOrEmpty(feedOptions.PartitionKeyRangeId)) ||
                  // Even if it's single partition query we create a specialized context to aggregate the aggregates and distinct of distinct.
                  DocumentQueryExecutionContextFactory.IsAggregateQueryWithoutContinuation(
                      partitionedQueryExecutionInfo,
                      isContinuationExpected) ||
                  DocumentQueryExecutionContextFactory.IsDistinctQuery(partitionedQueryExecutionInfo) ||
                  DocumentQueryExecutionContextFactory.IsGroupByQuery(partitionedQueryExecutionInfo);
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

        private static bool IsParallelQuery(FeedOptions feedOptions)
        {
            return (feedOptions.MaxDegreeOfParallelism != 0);
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

        private static bool IsOffsetLimitQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return partitionedQueryExecutionInfo.QueryInfo.HasOffset && partitionedQueryExecutionInfo.QueryInfo.HasLimit;
        }

        private static bool IsDistinctQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return partitionedQueryExecutionInfo.QueryInfo.HasDistinct;
        }

        private static bool IsGroupByQuery(PartitionedQueryExecutionInfo partitionedQueryExecutionInfo)
        {
            return partitionedQueryExecutionInfo.QueryInfo.HasGroupBy;
        }

        private static bool TryGetEpkProperty(
            FeedOptions feedOptions,
            out string effectivePartitionKeyString)
        {
            if (feedOptions?.Properties != null
                && feedOptions.Properties.TryGetValue(
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
