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
    using Microsoft.Azure.Documents;

    internal class HybridSearchCrossPartitionQueryPipelineStage : IQueryPipelineStage
    {
        private delegate TryCatch<IQueryPipelineStage> HybridSearchComponentPipelineFactory(QueryInfo queryInfo);

        private const string DisallowContinuationTokenMessage = "Hybrid search does not support continuation tokens";

        private const int RRFConstant = 60;

        private readonly static IReadOnlyList<CosmosElement> EmptyPage = new List<CosmosElement>();

        private readonly static QueryState Continuation = new QueryState(CosmosString.Create("HybridSearchInProgress"));

        private readonly HybridSearchComponentPipelineFactory pipelineFactory;

        private readonly IQueryPipelineStage globalStatisticsPipeline;

        private State state;

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
                    emitRawOrderByPayload: true,
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

        private static async ValueTask<TryCatch<(IReadOnlyList<HybridSearchQueryResult>, double)>> CollateSortedPipelineStageResultsAsync(
            IReadOnlyList<IQueryPipelineStage> queryPipelineStages,
            int maxConcurrency,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            // Collate results should return an IEnumerable<HybridSearchQueryResult>
            // Sort and coalesce the results on _rid
            // After sorting, each HybridSearchQueryResult has a fixed index in the list
            // This index can be used as the key for the ranking array
            // Now create an array (per dimension) of tuples (score, index) and sort it by score
            // The index of the tuple in the sorted array is the rank of the document
            // Create an array of tuples of ranks for each dimension
            // Create an array of tuples of (RRF scores, index) for each document using the ranks
            // Sort the array by RRF scores
            // Emit the documents in the sorted order by using the index in the tuple

            TryCatch<(List<HybridSearchQueryResult> queryResults, double requestCharge)> tryGetResults = await PrefetchInParallelAsync(
                queryPipelineStages,
                maxConcurrency,
                trace,
                cancellationToken);

            if (tryGetResults.Failed)
            {
                return TryCatch<(IReadOnlyList<HybridSearchQueryResult>, double)>.FromException(tryGetResults.Exception);
            }

            (List<HybridSearchQueryResult> queryResults, double requestCharge) = tryGetResults.Result;

            queryResults.Sort((x, y) => string.CompareOrdinal(x.Rid.Value, y.Rid.Value));

            UniqueRids(queryResults);

            IReadOnlyList<List<ScoreTuple>> componentScores = RetrieveComponentScores(queryResults, queryPipelineStages.Count);

            foreach (List<ScoreTuple> scoreTuples in componentScores)
            {
                scoreTuples.Sort((x, y) => (-1) * x.Score.CompareTo(y.Score)); // sort descending
            }

            int[,] ranks = ComputeRanks(componentScores);

            ComputeRRFScores(ranks, queryResults);

            queryResults.Sort((x, y) => (-1) * x.Score.CompareTo(y.Score)); // higher scores are better

            return TryCatch<(IReadOnlyList<HybridSearchQueryResult>, double)>.FromResult((queryResults, requestCharge));
        }

        private static async ValueTask<TryCatch<(List<HybridSearchQueryResult> queryResults, double requestCharge)>> PrefetchInParallelAsync(
            IReadOnlyList<IQueryPipelineStage> queryPipelineStages,
            int maxConcurrency,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            List<QueryPipelineStagePrefetcher> prefetchers = new List<QueryPipelineStagePrefetcher>(queryPipelineStages.Count);
            foreach (IQueryPipelineStage queryPipelineStage in queryPipelineStages)
            {
                prefetchers.Add(new QueryPipelineStagePrefetcher(queryPipelineStage));
            }

            await ParallelPrefetch.PrefetchInParallelAsync(prefetchers, maxConcurrency, trace, cancellationToken);

            double requestCharge = 0;
            List<HybridSearchQueryResult> queryResults = new List<HybridSearchQueryResult>();
            foreach (QueryPipelineStagePrefetcher prefetcher in prefetchers)
            {
                TryCatch<IReadOnlyList<QueryPage>> tryGetResults = prefetcher.Result;
                if (tryGetResults.Failed)
                {
                    return TryCatch<(List<HybridSearchQueryResult> queryResults, double requestCharge)>.FromException(tryGetResults.Exception);
                }

                foreach (QueryPage queryPage in tryGetResults.Result)
                {
                    requestCharge += queryPage.RequestCharge;
                    foreach (CosmosElement document in queryPage.Documents)
                    {
                        HybridSearchQueryResult hybridSearchQueryResult = HybridSearchQueryResult.Create(document);
                        queryResults.Add(hybridSearchQueryResult);
                    }
                }
            }

            return TryCatch<(List<HybridSearchQueryResult> queryResults, double requestCharge)>.FromResult((queryResults, requestCharge));
        }

        private static async ValueTask<TryCatch<IReadOnlyList<QueryPage>>> RunQueryPipelineStageAsync(
            IQueryPipelineStage pipelineStage,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            List<QueryPage> result = new List<QueryPage>();
            while (await pipelineStage.MoveNextAsync(trace, cancellationToken))
            {
                TryCatch<QueryPage> tryCatchQueryPage = pipelineStage.Current;
                if (tryCatchQueryPage.Failed)
                {
                    return TryCatch<IReadOnlyList<QueryPage>>.FromException(tryCatchQueryPage.Exception);
                }

                result.Add(tryCatchQueryPage.Result);
            }

            return TryCatch<IReadOnlyList<QueryPage>>.FromResult(result);
        }

        private static void UniqueRids(List<HybridSearchQueryResult> queryResults)
        {
            int writeIndex = 0;
            for (int readIndex = 1; readIndex < queryResults.Count; ++readIndex)
            {
                if (queryResults[readIndex].Rid.Value != queryResults[readIndex - 1].Rid.Value)
                {
                    ++writeIndex;
                    queryResults[writeIndex] = queryResults[readIndex];
                }
            }

            queryResults.RemoveRange(writeIndex + 1, queryResults.Count - writeIndex);
        }

        private static IReadOnlyList<List<ScoreTuple>> RetrieveComponentScores(IReadOnlyList<HybridSearchQueryResult> queryResults, int componentCount)
        {
            List<List<ScoreTuple>> componentScores = new List<List<ScoreTuple>>(componentCount);
            for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
            {
                componentScores.Add(new List<ScoreTuple>(queryResults.Count));
            }

            for (int index = 0; index < queryResults.Count; ++index)
            {
                CosmosArray componentScoresArray = queryResults[index].ComponentScores;

                for (int componentScoreindex = 0; componentScoreindex < componentScoresArray.Count; ++componentScoreindex)
                {
                    if (!(componentScoresArray[componentScoreindex] is CosmosNumber cosmosNumber))
                    {
                        throw new InternalServerErrorException($"componentScores must be an array of numbers.");
                    }

                    ScoreTuple scoreTuple = new ScoreTuple(Number64.ToDouble(cosmosNumber.Value), index);
                    componentScores[componentScoreindex].Add(scoreTuple);
                }
            }

            return componentScores;
        }

        private static int[,] ComputeRanks(IReadOnlyList<List<ScoreTuple>> componentScores)
        {
            int[,] ranks = new int[componentScores.Count, componentScores[0].Count];
            for (int componentIndex = 0; componentIndex < componentScores.Count; ++componentIndex)
            {
                for (int rank = 0; rank < componentScores[componentIndex].Count; ++rank)
                {
                    ranks[componentIndex, componentScores[componentIndex][rank].Index] = rank;
                }
            }

            return ranks;
        }

        private static void ComputeRRFScores(
            int[,] ranks,
            List<HybridSearchQueryResult> queryResults)
        {
            int componentCount = ranks.GetLength(0);

            for (int index = 0; index < queryResults.Count; ++index)
            {
                double rrfScore = 0;
                for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
                {
                    rrfScore += 1.0 / (RRFConstant + ranks[componentIndex, index]);
                }

                queryResults[index] = queryResults[index].WithScore(rrfScore);
            }
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

        private readonly struct ScoreTuple
        {
            public double Score { get; }

            public int Index { get; }

            public ScoreTuple(double score, int index)
            {
                this.Score = score;
                this.Index = index;
            }
        }

        private sealed class QueryPipelineStagePrefetcher : IPrefetcher
        {
            private readonly IQueryPipelineStage queryPipelineStage;

            public TryCatch<IReadOnlyList<QueryPage>> Result { get; private set; }

            public QueryPipelineStagePrefetcher(IQueryPipelineStage queryPipelineStage)
            {
                this.queryPipelineStage = queryPipelineStage ?? throw new ArgumentNullException(nameof(queryPipelineStage));
            }

            public async ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken)
            {
                this.Result = await RunQueryPipelineStageAsync(this.queryPipelineStage, trace, cancellationToken);
            }
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