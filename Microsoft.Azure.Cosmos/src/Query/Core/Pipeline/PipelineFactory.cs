// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.GroupBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Remote.Parallel;
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
            CosmosElement requestContinuationToken)
        {
            MonadicCreatePipelineStage monadicCreatePipelineStage;
            if (queryInfo.HasOrderBy)
            {
                monadicCreatePipelineStage = (continuationToken) => OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: sqlQuerySpec,
                    targetRanges: targetRanges,
                    orderByColumns: queryInfo
                        .OrderByExpressions
                        .Zip(queryInfo.OrderBy, (expression, sortOrder) => new OrderByColumn(expression, sortOrder)).ToList(),
                    pageSize: pageSize,
                    maxConcurrency: maxConcurrency,
                    continuationToken: requestContinuationToken);
            }
            else
            {
                monadicCreatePipelineStage = (continuationToken) => ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: sqlQuerySpec,
                    pageSize: pageSize,
                    maxConcurrency: maxConcurrency,
                    continuationToken: continuationToken);
            }

            if (queryInfo.HasAggregates && !queryInfo.HasGroupBy)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => AggregateQueryPipelineStage.MonadicCreate(
                    executionEnvironment,
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
                    executionEnvironment,
                    continuationToken,
                    monadicCreateSourceStage,
                    queryInfo.DistinctType);
            }

            if (queryInfo.HasGroupBy)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => GroupByQueryPipelineStage.MonadicCreate(
                    executionEnvironment,
                    continuationToken,
                    monadicCreateSourceStage,
                    queryInfo.GroupByAliasToAggregateType,
                    queryInfo.GroupByAliases,
                    queryInfo.HasSelectValue);
            }

            if (queryInfo.HasOffset)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => SkipQueryPipelineStage.MonadicCreate(
                    executionEnvironment,
                    queryInfo.Offset.Value,
                    continuationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasLimit)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => TakeQueryPipelineStage.MonadicCreateLimitStage(
                    executionEnvironment,
                    queryInfo.Limit.Value,
                    continuationToken,
                    monadicCreateSourceStage);
            }

            if (queryInfo.HasTop)
            {
                MonadicCreatePipelineStage monadicCreateSourceStage = monadicCreatePipelineStage;
                monadicCreatePipelineStage = (continuationToken) => TakeQueryPipelineStage.MonadicCreateTopStage(
                    executionEnvironment,
                    queryInfo.Top.Value,
                    continuationToken,
                    monadicCreateSourceStage);
            }

            return monadicCreatePipelineStage(requestContinuationToken)
                .Try<IQueryPipelineStage>(onSuccess: (stage) => new SkipEmptyPageQueryPipelineStage(stage));
        }
    }
}
