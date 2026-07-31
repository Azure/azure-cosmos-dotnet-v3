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
        /// <summary>
        /// Page size factor used for a cross-partition ORDER BY query that has no TOP or LIMIT clause.
        /// With continuations, it is expected that all pages will be consumed, so each range can fetch a
        /// larger multiple of its share: documents fetched beyond the current page are served by later
        /// pages rather than discarded, and the larger buffer reduces the number of network calls.
        /// </summary>
        private const int PageSizeFactorForContinuation = 5;

        /// <summary>
        /// Over-fetch factor used for a cross-partition ORDER BY query that has a TOP or LIMIT clause
        /// </summary>
        private static readonly int PageSizeFactorForTop = ConfigurationManager.GetPageSizeFactorForTop();

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
            FullTextScoreScope fullTextScoreScope,
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
                return TryCatch<IQueryPipelineStage>.FromResult(new EmptyQueryPipelineStage());
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
                    maxConcurrency: maxConcurrency,
                    fullTextScoreScope: fullTextScoreScope);

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
            long optimalPageSize = ComputeOptimalPageSize(
                queryInfo: queryInfo,
                targetRangeCount: targetRanges.Count,
                maxItemCount: maxItemCount,
                isContinuationExpected: isContinuationExpected,
                pageSizeFactorForTop: PageSizeFactorForTop);

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

        /// <summary>
        /// Computes the initial per-partition page size requested from the backend.
        /// </summary>
        /// <remarks>
        /// Only cross-partition ORDER BY queries are adjusted. Because the results have to be merge sorted across
        /// partitions, every partition is asked for roughly <c>1/n</c> of the required documents, multiplied by an
        /// over-fetch factor that absorbs an uneven distribution of the sort key. When the query carries a TOP or
        /// LIMIT clause the result set is capped, so anything fetched past that cap is loaded by the backend and
        /// then discarded, which is why that branch uses the smaller factor.
        /// </remarks>
        internal static long ComputeOptimalPageSize(
            QueryInfo queryInfo,
            int targetRangeCount,
            int maxItemCount,
            bool isContinuationExpected,
            int pageSizeFactorForTop)
        {
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
                    top = Math.Min((queryInfo.Offset ?? 0) + queryInfo.Limit.Value, int.MaxValue);
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
                    // Each targeted range initially fetches its 1/nth share of the top value, scaled by an
                    // over-fetch factor to absorb an uneven distribution of the sort key across ranges.
                    // Anything fetched beyond top is discarded, so the factor is kept small.
                    long pageSizeWithTop = (long)Math.Min(
                        Math.Ceiling(top / (double)targetRangeCount) * pageSizeFactorForTop,
                        top);

                    optimalPageSize = Math.Min(pageSizeWithTop, optimalPageSize);
                }
                else if (isContinuationExpected)
                {
                    optimalPageSize = (long)Math.Min(
                        Math.Ceiling(optimalPageSize / (double)targetRangeCount) * PageSizeFactorForContinuation,
                        optimalPageSize);
                }
            }

            return optimalPageSize;
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
