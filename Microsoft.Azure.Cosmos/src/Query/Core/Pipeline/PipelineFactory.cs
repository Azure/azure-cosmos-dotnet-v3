// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.DCount;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.GroupBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Skip;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Take;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;

    internal static class PipelineFactory
    {
        private const int PageSizeFactorForTop = 5;

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            PartitionKey? partitionKey,
            QueryInfo queryInfo,
            HybridSearchQueryInfo hybridSearchQueryInfo,
            int maxItemCount,
            ContainerQueryProperties containerQueryProperties,
            IReadOnlyList<FeedRangeEpk> allRanges,
            bool isContinuationExpected,
            int maxConcurrency,
            CosmosElement requestContinuationToken)
        {
            if (documentContainer == null)
            {
                throw new ArgumentNullException(nameof(documentContainer));
            }

            if (sqlQuerySpec == null)
            {
                throw new ArgumentNullException(nameof(sqlQuerySpec));
            }

            if (targetRanges == null)
            {
                throw new ArgumentNullException(nameof(targetRanges));
            }

            if (targetRanges.Count == 0)
            {
                throw new ArgumentException($"{nameof(targetRanges)} must not be empty.");
            }

            if (queryInfo == null && hybridSearchQueryInfo == null)
            {
                throw new ArgumentNullException($"{nameof(queryInfo)} and {nameof(hybridSearchQueryInfo)} cannot both be null.");
            }

            if (queryInfo != null && hybridSearchQueryInfo != null)
            {
                throw new ArgumentException($"{nameof(queryInfo)} and {nameof(hybridSearchQueryInfo)} cannot both be non-null.");
            }

            if (hybridSearchQueryInfo != null && requestContinuationToken != null)
            {
                throw new ArgumentException($"Continuation tokens are not supported for hybrid search.");
            }

            if (queryInfo != null)
            {
                return MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: sqlQuerySpec,
                    targetRanges: targetRanges,
                    partitionKey: partitionKey,
                    queryInfo: queryInfo,
                    prefetchPolicy: DeterminePrefetchPolicy(queryInfo),
                    containerQueryProperties: containerQueryProperties,
                    maxItemCount: maxItemCount,
                    isContinuationExpected: true,
                    emitRawOrderByPayload: false,
                    maxConcurrency: maxConcurrency,
                    requestContinuationToken: requestContinuationToken);
            }
            else
            {
                MonadicCreatePipelineStage monadicCreatePipelineStage = (_) => HybridSearchCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    containerQueryProperties: containerQueryProperties,
                    sqlQuerySpec: sqlQuerySpec,
                    targetRanges: targetRanges,
                    partitionKey: partitionKey,
                    queryInfo: hybridSearchQueryInfo,
                    allRanges: allRanges,
                    maxItemCount: maxItemCount,
                    isContinuationExpected: isContinuationExpected,
                    maxConcurrency: maxConcurrency);

                if (hybridSearchQueryInfo.Skip != null)
                {
                    Debug.Assert(hybridSearchQueryInfo.Skip.Value <= int.MaxValue, "PipelineFactory Assert!", "Skip value must be <= int.MaxValue");

                    int skipCount = (int)hybridSearchQueryInfo.Skip.Value;

                    MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                    monadicCreatePipelineStage = (continuationToken) => SkipQueryPipelineStage.MonadicCreate(
                        skipCount,
                        continuationToken,
                        monadicCreateSourceStage);
                }

                if (hybridSearchQueryInfo.Take != null)
                {
                    Debug.Assert(hybridSearchQueryInfo.Take.Value <= int.MaxValue, "PipelineFactory Assert!", "Take value must be <= int.MaxValue");

                    int takeCount = (int)hybridSearchQueryInfo.Take.Value;

                    MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                    monadicCreatePipelineStage = (continuationToken) => TakeQueryPipelineStage.MonadicCreateLimitStage(
                        takeCount,
                        requestContinuationToken,
                        monadicCreateSourceStage);
                }

                // Allow hybrid search to emit empty pages for now
                // If we decide to change this in the future, we can wrap the stage in a SkipEmptyPageQueryPipelineStage
                // similar to how we do for regular queries (see below)
                return monadicCreatePipelineStage(requestContinuationToken);
            }
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            PartitionKey? partitionKey,
            QueryInfo queryInfo,
            PrefetchPolicy prefetchPolicy,
            ContainerQueryProperties containerQueryProperties,
            int maxItemCount,
            bool emitRawOrderByPayload,
            bool isContinuationExpected,
            int maxConcurrency,
            CosmosElement requestContinuationToken)
        {
            // We need to compute the optimal initial page size for order-by queries
            long optimalPageSize = maxItemCount;
            if (queryInfo.HasOrderBy)
            {
                uint top;
                if (queryInfo.HasTop && (queryInfo.Top.Value > 0))
                {
                    top = queryInfo.Top.Value;
                }
                else if (queryInfo.HasLimit && (queryInfo.Limit.Value > 0))
                {
                    top = (queryInfo.Offset ?? 0) + queryInfo.Limit.Value;
                }
                else
                {
                    top = 0;
                }

                if (top > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException(nameof(queryInfo.Top.Value));
                }

                if (top > 0)
                {
                    // All partitions should initially fetch about 1/nth of the top value.
                    long pageSizeWithTop = (long)Math.Min(
                        Math.Ceiling(top / (double)targetRanges.Count) * PageSizeFactorForTop,
                        top);

                    optimalPageSize = Math.Min(pageSizeWithTop, optimalPageSize);
                }
                else if (isContinuationExpected)
                {
                    optimalPageSize = (long)Math.Min(
                        Math.Ceiling(optimalPageSize / (double)targetRanges.Count) * PageSizeFactorForTop,
                        optimalPageSize);
                }
            }

            QueryExecutionOptions queryPaginationOptions = new QueryExecutionOptions(pageSizeHint: (int)optimalPageSize);

            Debug.Assert(
                (optimalPageSize > 0) && (optimalPageSize <= int.MaxValue),
                $"Invalid MaxItemCount {optimalPageSize}");

            sqlQuerySpec = !string.IsNullOrEmpty(queryInfo.RewrittenQuery) ? new SqlQuerySpec(queryInfo.RewrittenQuery, sqlQuerySpec.Parameters) : sqlQuerySpec;

            MonadicCreatePipelineStage monadicCreatePipelineStage;
            if (queryInfo.HasOrderBy)
            {
                monadicCreatePipelineStage = (continuationToken) => OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: sqlQuerySpec,
                    targetRanges: targetRanges,
                    partitionKey: partitionKey,
                    orderByColumns: queryInfo
                        .OrderByExpressions
                        .Zip(queryInfo.OrderBy, (expression, sortOrder) => new OrderByColumn(expression, sortOrder)).ToList(),
                    queryPaginationOptions: queryPaginationOptions,
                    maxConcurrency: maxConcurrency,
                    nonStreamingOrderBy: queryInfo.HasNonStreamingOrderBy,
                    emitRawOrderByPayload: emitRawOrderByPayload,
                    continuationToken: continuationToken,
                    containerQueryProperties: containerQueryProperties);
            }
            else
            {
                monadicCreatePipelineStage = (continuationToken) => ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: sqlQuerySpec,
                    targetRanges: targetRanges,
                    queryPaginationOptions: queryPaginationOptions,
                    partitionKey: partitionKey,
                    containerQueryProperties: containerQueryProperties,
                    prefetchPolicy: prefetchPolicy,
                    maxConcurrency: maxConcurrency,
                    continuationToken: continuationToken);
            }

            if (queryInfo.HasAggregates && !queryInfo.HasGroupBy)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => AggregateQueryPipelineStage.MonadicCreate(
                    queryInfo.Aggregates,
                    queryInfo.GroupByAliasToAggregateType,
                    queryInfo.GroupByAliases,
                    queryInfo.HasSelectValue,
                    continuationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasDistinct)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => DistinctQueryPipelineStage.MonadicCreate(
                    continuationToken,
                    monadicCreateSourceStage,
                    queryInfo.DistinctType);
            }

            if (queryInfo.HasGroupBy)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => GroupByQueryPipelineStage.MonadicCreate(
                    continuationToken,
                    monadicCreateSourceStage,
                    queryInfo.Aggregates,
                    queryInfo.GroupByAliasToAggregateType,
                    queryInfo.GroupByAliases,
                    queryInfo.HasSelectValue,
                    (queryPaginationOptions ?? QueryExecutionOptions.Default).PageSizeLimit.GetValueOrDefault(int.MaxValue));
            }

            if (queryInfo.HasOffset)
            {
                Debug.Assert(queryInfo.Offset.Value <= int.MaxValue, "PipelineFactory Assert!", "Offset value must be <= int.MaxValue");

                int offsetCount = (int)queryInfo.Offset.Value;

                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => SkipQueryPipelineStage.MonadicCreate(
                    offsetCount,
                    continuationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasLimit)
            {
                Debug.Assert(queryInfo.Limit.Value <= int.MaxValue, "PipelineFactory Assert!", "Limit value must be <= int.MaxValue");

                int limitCount = (int)queryInfo.Limit.Value;

                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => TakeQueryPipelineStage.MonadicCreateLimitStage(
                    limitCount,
                    continuationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasTop)
            {
                Debug.Assert(queryInfo.Top.Value <= int.MaxValue, "PipelineFactory Assert!", "Top value must be <= int.MaxValue");

                int topCount = (int)queryInfo.Top.Value;

                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => TakeQueryPipelineStage.MonadicCreateTopStage(
                    topCount,
                    continuationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasDCount)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => DCountQueryPipelineStage.MonadicCreate(
                    queryInfo.DCountInfo,
                    continuationToken,
                    monadicCreateSourceStage);
            }

            return monadicCreatePipelineStage(requestContinuationToken)
                .Try<IQueryPipelineStage>(onSuccess: stage => new SkipEmptyPageQueryPipelineStage(stage));
        }

        private static PrefetchPolicy DeterminePrefetchPolicy(QueryInfo queryInfo)
        {
            if (queryInfo.HasDCount || queryInfo.HasAggregates || queryInfo.HasGroupBy)
            {
                return PrefetchPolicy.PrefetchAll;
            }

            return PrefetchPolicy.PrefetchSinglePage;
        }
    }
}
