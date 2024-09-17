// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
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
        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            PartitionKey? partitionKey,
            QueryInfo queryInfo,
            QueryExecutionOptions queryPaginationOptions,
            ContainerQueryProperties containerQueryProperties,
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

            if (queryInfo == null)
            {
                throw new ArgumentNullException(nameof(queryInfo));
            }

            sqlQuerySpec = !string.IsNullOrEmpty(queryInfo.RewrittenQuery) ? new SqlQuerySpec(queryInfo.RewrittenQuery, sqlQuerySpec.Parameters) : sqlQuerySpec;

            PrefetchPolicy prefetchPolicy = DeterminePrefetchPolicy(queryInfo);

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
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => SkipQueryPipelineStage.MonadicCreate(
                    queryInfo.Offset.Value,
                    continuationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasLimit)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => TakeQueryPipelineStage.MonadicCreateLimitStage(
                    queryInfo.Limit.Value,
                    continuationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasTop)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => TakeQueryPipelineStage.MonadicCreateTopStage(
                    queryInfo.Top.Value,
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
