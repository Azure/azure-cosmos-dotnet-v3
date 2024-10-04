// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tracing;

    internal class HybridSearchCrossPartitionQueryPipelineStage : IQueryPipelineStage
    {
        private delegate TryCatch<IQueryPipelineStage> HybridSearchComponentPipelineFactory(QueryInfo queryInfo);

        private const string DisallowContinuationTokenMessage = "Hybrid search does not support continuation tokens";

        private readonly static IReadOnlyList<CosmosElement> EmptyPage = new List<CosmosElement>();

        private readonly static QueryState Continuation = new QueryState(CosmosString.Create("HybridSearchInProgress"));

        private State state;

        private HybridSearchComponentPipelineFactory pipelineFactory;

        private IQueryPipelineStage globalStatisticsPipeline;

        private IReadOnlyList<IQueryPipelineStage> queryPipelineStages;

        public TryCatch<QueryPage> Current => throw new System.NotImplementedException();

        private enum State
        {
            GatherGlobalStatistics,
            RunComponentQueries,
            ReciprocalRankFusion,
            Done,
        }

        private HybridSearchCrossPartitionQueryPipelineStage(
            HybridSearchComponentPipelineFactory pipelineFactory,
            State state,
            IQueryPipelineStage globalStatisticsPipeline,
            IReadOnlyList<IQueryPipelineStage> queryPipelineStages)
        {
            this.state = state;
            this.pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
            this.globalStatisticsPipeline = globalStatisticsPipeline;
            this.queryPipelineStages = queryPipelineStages;
            this.state = State.GatherGlobalStatistics;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            ContainerQueryProperties containerQueryProperties,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            Cosmos.PartitionKey? partitionKey,
            HybridSearchQueryInfo queryInfo,
            IReadOnlyList<FeedRangeEpk> allRanges,
            QueryExecutionOptions queryExecutionOptions,
            int maxConcurrency)
        {
            QueryExecutionOptions rewrittenQueryExecutionOptions = null;

            TryCatch<IQueryPipelineStage> ComponentPipelineFactory(QueryInfo rewrittenQueryInfo)
            {
                SqlQuerySpec rewrittenQuerySpec = new SqlQuerySpec(
                    rewrittenQueryInfo.RewrittenQuery,
                    sqlQuerySpec.Parameters);

                return PipelineFactory.MonadicCreate(
                    documentContainer,
                    rewrittenQuerySpec,
                    targetRanges,
                    partitionKey,
                    rewrittenQueryInfo,
                    PrefetchPolicy.PrefetchAll,
                    rewrittenQueryExecutionOptions,
                    containerQueryProperties,
                    maxConcurrency,
                    requestContinuationToken: null);
            }

            SqlQuerySpec globalStatisticsQuerySpec = new SqlQuerySpec(
                queryInfo.GlobalStatisticsQuery,
                sqlQuerySpec.Parameters);

            State state;
            IQueryPipelineStage globalStatisticsPipeline;
            List<IQueryPipelineStage> queryPipelineStages;
            if (queryInfo.RequiresGlobalStatistics)
            {
                TryCatch<IQueryPipelineStage> tryCatchGlobalStatisticsPipeline = ParallelCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: globalStatisticsQuerySpec,
                    targetRanges: allRanges,
                    queryPaginationOptions: queryExecutionOptions,
                    partitionKey: null,
                    containerQueryProperties: containerQueryProperties,
                    prefetchPolicy: PrefetchPolicy.PrefetchAll,
                    maxConcurrency: maxConcurrency,
                    continuationToken: null);

                if (tryCatchGlobalStatisticsPipeline.Failed)
                {
                    return tryCatchGlobalStatisticsPipeline;
                }

                state = State.GatherGlobalStatistics;
                globalStatisticsPipeline = tryCatchGlobalStatisticsPipeline.Result;
                queryPipelineStages = null;
            }
            else
            {
                TryCatch<List<IQueryPipelineStage>> tryCreatePipelineStages = CreateQueryPipelineStages(
                    queryInfo.ComponentQueryInfos,
                    ComponentPipelineFactory);

                if (tryCreatePipelineStages.Failed)
                {
                    return TryCatch<IQueryPipelineStage>.FromException(tryCreatePipelineStages.Exception);
                }

                state = State.RunComponentQueries;
                globalStatisticsPipeline = null;
                queryPipelineStages = tryCreatePipelineStages.Result;
            }

            return TryCatch<IQueryPipelineStage>.FromResult(
                new HybridSearchCrossPartitionQueryPipelineStage(
                    ComponentPipelineFactory,
                    state,
                    globalStatisticsPipeline,
                    queryPipelineStages));
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private ValueTask<bool> MoveNextAsync_GatherGlobalStatisticsAsync(ITrace trace, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private static TryCatch<List<IQueryPipelineStage>> CreateQueryPipelineStages(
            IReadOnlyList<QueryInfo> queryInfos,
            GlobalFullTextSearchStatistics statistics,
            HybridSearchComponentPipelineFactory pipelineFactory)
        {
            List<QueryInfo> rewrittenQueryInfos = new List<QueryInfo>(queryInfos.Count);
            foreach (QueryInfo queryInfo in queryInfos)
            {
                QueryInfo rewrittenQueryInfo = RewriteQueryInfo(queryInfo, statistics);
                rewrittenQueryInfos.Add(rewrittenQueryInfo);
            }

            return CreateQueryPipelineStages(rewrittenQueryInfos, pipelineFactory);
        }

        private static TryCatch<List<IQueryPipelineStage>> CreateQueryPipelineStages(
            IReadOnlyList<QueryInfo> queryInfos,
            HybridSearchComponentPipelineFactory pipelineFactory)
        {
            List<IQueryPipelineStage> queryPipelineStages = new List<IQueryPipelineStage>(queryInfos.Count);
            foreach (QueryInfo queryInfo in queryInfos)
            {
                TryCatch<IQueryPipelineStage> tryCreatePipeline = pipelineFactory(queryInfo);

                if (tryCreatePipeline.Failed)
                {
                    return TryCatch<List<IQueryPipelineStage>>.FromException(tryCreatePipeline.Exception);
                }

                queryPipelineStages.Add(tryCreatePipeline.Result);
            }

            return TryCatch<List<IQueryPipelineStage>>.FromResult(queryPipelineStages);
        }

        private static QueryInfo RewriteQueryInfo(QueryInfo queryInfo, GlobalFullTextSearchStatistics statistics)
        {
            QueryInfo result = new QueryInfo();
            result.DistinctType = queryInfo.DistinctType;
            result.Top = queryInfo.Top;
            result.Offset = queryInfo.Offset;
            result.Limit = queryInfo.Limit;
            result.OrderBy = queryInfo.OrderBy;

            List<string> orderByExpressions = new List<string>(queryInfo.OrderByExpressions.Count);
            foreach (string orderByExpression in queryInfo.OrderByExpressions)
            {
                string rewrittenOrderByExpression = FormatComponentQueryText(orderByExpression, statistics);
                orderByExpressions.Add(rewrittenOrderByExpression);
            }
            result.OrderByExpressions = orderByExpressions;

            result.GroupByExpressions = queryInfo.GroupByExpressions;
            result.GroupByAliases = queryInfo.GroupByAliases;
            result.Aggregates = queryInfo.Aggregates;
            result.GroupByAliasToAggregateType = queryInfo.GroupByAliasToAggregateType;

            string rewrittenQuery = FormatComponentQueryText(queryInfo.RewrittenQuery, statistics);
            queryInfo.RewrittenQuery = rewrittenQuery;

            result.HasSelectValue = queryInfo.HasSelectValue;
            result.DCountInfo = queryInfo.DCountInfo;
            result.HasNonStreamingOrderBy = queryInfo.HasNonStreamingOrderBy;

            return result;
        }

        private static string FormatComponentQueryText(string format, GlobalFullTextSearchStatistics statistics)
        {
            string query = format.Replace(Placeholders.TotalDocumentCount, statistics.DocumentCount.ToString());

            int count = statistics.FullTextStatistics.Count;
            for (int index = 0; index < count; ++index)
            {
                FullTextStatistics fullTextStatistics = statistics.FullTextStatistics[index];
                query = query.Replace(string.Format(Placeholders.FormattableTotalWordCount, index), fullTextStatistics.TotalWordCount.ToString());

                string hitCountsArray = string.Format("[{0}]", string.Join(",", fullTextStatistics.HitCounts));
                query = query.Replace(string.Format(Placeholders.FormattableHitCountsArray, index), hitCountsArray);
            }

            return query;
        }

        private static async ValueTask<TryCatch<(GlobalFullTextSearchStatistics, QueryPage)>> GatherStatisticsAsync(
            IQueryPipelineStage source,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            QueryPageParameters queryPageParameters = null;
            double requestCharge = 0;
            GlobalStatisticsAggregator globalStatisticsAggregator = null;
            while (await source.MoveNextAsync(trace, cancellationToken))
            {
                TryCatch<QueryPage> tryGetPage = source.Current;
                if (tryGetPage.Failed)
                {
                    return TryCatch<(GlobalFullTextSearchStatistics, QueryPage)>.FromException(tryGetPage.Exception);
                }

                QueryPage page = tryGetPage.Result;
                requestCharge += page.RequestCharge;

                if (queryPageParameters == null)
                {
                    queryPageParameters = new QueryPageParameters(
                        activityId: page.ActivityId,
                        cosmosQueryExecutionInfo: page.CosmosQueryExecutionInfo,
                        distributionPlanSpec: page.DistributionPlanSpec,
                        additionalHeaders: page.AdditionalHeaders);
                }

                Debug.Assert(page.Documents.Count == 1, "There should only be one document per page");

                GlobalFullTextSearchStatistics statistics = new GlobalFullTextSearchStatistics(page.Documents[0]);

                if (globalStatisticsAggregator == null)
                {
                    globalStatisticsAggregator = new GlobalStatisticsAggregator(statistics);
                }
                else
                {
                    globalStatisticsAggregator.Add(statistics);
                }
            }

            QueryPage queryPage = new QueryPage(
                EmptyPage,
                requestCharge,
                queryPageParameters.ActivityId,
                queryPageParameters.CosmosQueryExecutionInfo,
                queryPageParameters.DistributionPlanSpec,
                DisallowContinuationTokenMessage,
                queryPageParameters.AdditionalHeaders,
                Continuation,
                streaming: false);

            return TryCatch<(GlobalFullTextSearchStatistics, QueryPage)>.FromResult((globalStatisticsAggregator.GetResult(), queryPage));
        }

        private sealed class FullTextStatisticsAggregator
        {
            private readonly long[] hitCounts;

            private long totalWordCount;

            public FullTextStatisticsAggregator(FullTextStatistics statistics)
            {
                if (statistics == null)
                {
                    throw new ArgumentNullException(nameof(statistics));
                }

                this.totalWordCount = statistics.TotalWordCount;
                this.hitCounts = statistics.HitCounts.ToArray();
            }

            public void Add(FullTextStatistics fullTextStatistics)
            {
                if (fullTextStatistics == null)
                {
                    throw new ArgumentNullException(nameof(fullTextStatistics));
                }

                Debug.Assert(fullTextStatistics.HitCounts.Length == this.hitCounts.Length, "The dimensions of the hit counts should match");

                this.totalWordCount += fullTextStatistics.TotalWordCount;

                for (int index = 0; index < this.hitCounts.Length; ++index)
                {
                    this.hitCounts[index] += fullTextStatistics.HitCounts.Span[index];
                }
            }

            public FullTextStatistics GetResult()
            {
                return new FullTextStatistics(this.totalWordCount, this.hitCounts);
            }
        }

        private sealed class GlobalStatisticsAggregator
        {
            private readonly IReadOnlyList<FullTextStatisticsAggregator> textStatistics;

            private long documentCount;

            public GlobalStatisticsAggregator(GlobalFullTextSearchStatistics statistics)
            {
                if (statistics == null)
                {
                    throw new ArgumentNullException(nameof(statistics));
                }

                this.documentCount = statistics.DocumentCount;
                this.textStatistics = statistics.FullTextStatistics
                    .Select(x => new FullTextStatisticsAggregator(x))
                    .ToList();
            }

            public void Add(GlobalFullTextSearchStatistics globalStatistics)
            {
                if (globalStatistics == null)
                {
                    throw new ArgumentNullException(nameof(globalStatistics));
                }

                Debug.Assert(globalStatistics.FullTextStatistics.Count == this.textStatistics.Count, "The number of text statistics should match");

                this.documentCount += globalStatistics.DocumentCount;

                for (int index = 0; index < this.textStatistics.Count; ++index)
                {
                    this.textStatistics[index].Add(globalStatistics.FullTextStatistics[index]);
                }
            }

            public GlobalFullTextSearchStatistics GetResult()
            {
                return new GlobalFullTextSearchStatistics(
                    this.documentCount,
                    this.textStatistics.Select(x => x.GetResult()).ToList());
            }
        }

        private static class Placeholders
        {
            public const string TotalDocumentCount = "{documentdb-formattablehybridsearchquery-totaldocumentcount}";

            public const string FormattableTotalWordCount = "{{documentdb-formattablehybridsearchquery-totalwordcount-{0}}}";

            public const string FormattableHitCountsArray = "{{documentdb-formattablehybridsearchquery-hitcountsarray-{0}}}";
        }
    }
}