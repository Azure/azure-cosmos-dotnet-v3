// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Cache;
    using System.Threading;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.GroupBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Skip;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Take;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Documents;

    internal static class PipelineFactory
    {
        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            ExecutionEnvironment executionEnvironment,
            IDocumentContainer documentContainer,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<PartitionKeyRange> targetRanges,
            QueryInfo queryInfo,
            int pageSize,
            int maxConcurrency,
            CosmosElement requestContinuationToken,
            CancellationToken requestCancellationToken)
        {
            MonadicCreatePipelineStage monadicCreatePipelineStage;
            if (queryInfo.HasOrderBy)
            {
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: sqlQuerySpec,
                    targetRanges: targetRanges,
                    orderByColumns: queryInfo
                        .OrderByExpressions
                        .Zip(queryInfo.OrderBy, (expression, sortOrder) => new OrderByColumn(expression, sortOrder)).ToList(),
                    pageSize: pageSize,
                    maxConcurrency: maxConcurrency,
                    continuationToken: requestContinuationToken,
                    cancellationToken: cancellationToken);
            }
            else
            {
                monadicCreatePipelineStage = (continuationToken, cancellationToken) => ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: sqlQuerySpec,
                    targetRanges: targetRanges,
                    pageSize: pageSize,
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
                    queryInfo.GroupByAliasToAggregateType,
                    queryInfo.GroupByAliases,
                    queryInfo.HasSelectValue);
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

            return monadicCreatePipelineStage(requestContinuationToken, requestCancellationToken)
                .Try<IQueryPipelineStage>(onSuccess: (stage) => new SkipEmptyPageQueryPipelineStage(stage, requestCancellationToken));
        }
    }
}
