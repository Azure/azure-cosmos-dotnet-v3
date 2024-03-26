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
            ExecutionEnvironment executionEnvironment,
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            PartitionKey? partitionKey,
            QueryInfo queryInfo,
            QueryPaginationOptions queryPaginationOptions,
            ContainerQueryProperties containerQueryProperties,
            int maxConcurrency,
            CosmosElement requestContinuationToken,
            CancellationToken requestCancellationToken)
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
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: sqlQuerySpec,
                    targetRanges: targetRanges,
                    partitionKey: partitionKey,
                    orderByColumns: queryInfo
                        .OrderByExpressions
                        .Zip(queryInfo.OrderBy, (expression, sortOrder) => new OrderByColumn(expression, sortOrder)).ToList(),
                    queryPaginationOptions: queryPaginationOptions,
                    maxConcurrency: maxConcurrency,
                    continuationToken: continuationToken,
                    cancellationToken: cancellationToken);
            }
            else
            {
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: sqlQuerySpec,
                    targetRanges: targetRanges,
                    queryPaginationOptions: queryPaginationOptions,
                    partitionKey: partitionKey,
                    containerQueryProperties: containerQueryProperties,
                    prefetchPolicy: prefetchPolicy,
                    maxConcurrency: maxConcurrency,
                    continuationToken: continuationToken,
                    cancellationToken: cancellationToken);
            }

            if (queryInfo.HasAggregates && !queryInfo.HasGroupBy)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => AggregateQueryPipelineStage.MonadicCreate(
                    executionEnvironment,
                    queryInfo.Aggregates,
                    queryInfo.GroupByAliasToAggregateType,
                    queryInfo.GroupByAliases,
                    queryInfo.HasSelectValue,
                    continuationToken,
                    cancellationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasDistinct)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => DistinctQueryPipelineStage.MonadicCreate(
                    executionEnvironment,
                    continuationToken,
                    cancellationToken,
                    monadicCreateSourceStage,
                    queryInfo.DistinctType);
            }

            if (queryInfo.HasGroupBy)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => GroupByQueryPipelineStage.MonadicCreate(
                    executionEnvironment,
                    continuationToken,
                    cancellationToken,
                    monadicCreateSourceStage,
                    queryInfo.Aggregates,
                    queryInfo.GroupByAliasToAggregateType,
                    queryInfo.GroupByAliases,
                    queryInfo.HasSelectValue,
                    (queryPaginationOptions ?? QueryPaginationOptions.Default).PageSizeLimit.GetValueOrDefault(int.MaxValue));
            }

            if (queryInfo.HasOffset)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => SkipQueryPipelineStage.MonadicCreate(
                    executionEnvironment,
                    queryInfo.Offset.Value,
                    continuationToken,
                    cancellationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasLimit)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => TakeQueryPipelineStage.MonadicCreateLimitStage(
                    executionEnvironment,
                    queryInfo.Limit.Value,
                    continuationToken,
                    cancellationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasTop)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => TakeQueryPipelineStage.MonadicCreateTopStage(
                    executionEnvironment,
                    queryInfo.Top.Value,
                    continuationToken,
                    cancellationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasDCount)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => DCountQueryPipelineStage.MonadicCreate(
                    executionEnvironment,
                    queryInfo.DCountInfo,
                    continuationToken,
                    cancellationToken,
                    monadicCreateSourceStage);
            }

            return monadicCreatePipelineStage(requestContinuationToken, requestCancellationToken)
                .Try<IQueryPipelineStage>(onSuccess: (stage) => new SkipEmptyPageQueryPipelineStage(stage, requestCancellationToken));
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
