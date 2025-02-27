// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
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

        private const int RrfConstant = 60;

        private const int MaximumPageSize = 2048;

        private readonly static IReadOnlyList<CosmosElement> EmptyPage = new List<CosmosElement>();

        private readonly static QueryState Continuation = new QueryState(CosmosString.Create("HybridSearchInProgress"));

        private readonly HybridSearchQueryInfo hybridSearchQueryInfo;

        private readonly int pageSize;

        private readonly int maxConcurrency;

        private readonly HybridSearchComponentPipelineFactory pipelineFactory;

        private readonly IQueryPipelineStage globalStatisticsPipeline;

        private State state;

        private IReadOnlyList<IQueryPipelineStage> queryPipelineStages;

        private IReadOnlyList<HybridSearchQueryResult> bufferedResults;

        private IEnumerator<HybridSearchQueryResult> enumerator;

        private QueryPageParameters queryPageParameters;

        public TryCatch<QueryPage> Current { get; private set; }

        private enum State
        {
            Uninitialized,
            Initialized,
            Draining,
            Done,
        }

        private HybridSearchCrossPartitionQueryPipelineStage(
            HybridSearchQueryInfo hybridSearchQueryInfo,
            HybridSearchComponentPipelineFactory pipelineFactory,
            int pageSize,
            int maxConcurrency,
            State state,
            IQueryPipelineStage globalStatisticsPipeline,
            IReadOnlyList<IQueryPipelineStage> queryPipelineStages)
        {
            this.hybridSearchQueryInfo = hybridSearchQueryInfo ?? throw new ArgumentNullException(nameof(hybridSearchQueryInfo));
            this.pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
            this.pageSize = pageSize;
            this.maxConcurrency = maxConcurrency;
            this.state = state;
            this.globalStatisticsPipeline = globalStatisticsPipeline;
            this.queryPipelineStages = queryPipelineStages;
        }

        public static TryCatch<IQueryPipelineStage> MonadicCreate(
            IDocumentContainer documentContainer,
            ContainerQueryProperties containerQueryProperties,
            SqlQuerySpec sqlQuerySpec,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            Cosmos.PartitionKey? partitionKey,
            HybridSearchQueryInfo queryInfo,
            IReadOnlyList<FeedRangeEpk> allRanges,
            int maxItemCount,
            bool isContinuationExpected,
            int maxConcurrency)
        {
            TryCatch<IQueryPipelineStage> ComponentPipelineFactory(QueryInfo rewrittenQueryInfo)
            {
                return PipelineFactory.MonadicCreate(
                    documentContainer,
                    sqlQuerySpec,
                    targetRanges,
                    partitionKey,
                    rewrittenQueryInfo,
                    PrefetchPolicy.PrefetchAll,
                    containerQueryProperties,
                    maxItemCount: maxItemCount,
                    emitRawOrderByPayload: true,
                    isContinuationExpected: isContinuationExpected,
                    maxConcurrency: maxConcurrency,
                    requestContinuationToken: null);
            }

            State state;
            IQueryPipelineStage globalStatisticsPipeline;
            List<IQueryPipelineStage> queryPipelineStages;
            if (queryInfo.RequiresGlobalStatistics)
            {
                QueryExecutionOptions queryExecutionOptions = new QueryExecutionOptions(pageSizeHint: maxItemCount);

                SqlQuerySpec globalStatisticsQuerySpec = new SqlQuerySpec(
                    queryInfo.GlobalStatisticsQuery,
                    sqlQuerySpec.Parameters);

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

                state = State.Uninitialized;
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

                state = State.Initialized;
                globalStatisticsPipeline = null;
                queryPipelineStages = tryCreatePipelineStages.Result;
            }

            int pageSize = maxItemCount > 0 ?
                    Math.Min(MaximumPageSize, maxItemCount) :
                    MaximumPageSize;

            return TryCatch<IQueryPipelineStage>.FromResult(
                new HybridSearchCrossPartitionQueryPipelineStage(
                    queryInfo,
                    ComponentPipelineFactory,
                    pageSize,
                    maxConcurrency,
                    state,
                    globalStatisticsPipeline,
                    queryPipelineStages));
        }

        public ValueTask DisposeAsync()
        {
            List<Task> tasks = new List<Task>();
            if (this.globalStatisticsPipeline != null)
            {
                tasks.Add(this.globalStatisticsPipeline.DisposeAsync().AsTask());
            }

            if (this.queryPipelineStages != null)
            {
                foreach (IQueryPipelineStage queryPipelineStage in this.queryPipelineStages)
                {
                    tasks.Add(queryPipelineStage.DisposeAsync().AsTask());
                }
            }

            Task task = Task.WhenAll(tasks);
            return new ValueTask(task);
        }

        public ValueTask<bool> MoveNextAsync(ITrace trace, CancellationToken cancellationToken)
        {
            return this.state switch
            {
                State.Uninitialized => this.MoveNextAsync_GatherGlobalStatisticsAsync(trace, cancellationToken),
                State.Initialized => this.MoveNextAsync_RunComponentQueriesAsync(trace, cancellationToken),
                State.Draining => this.MoveNextAsync_DrainPageAsync(trace, cancellationToken),
                State.Done => new ValueTask<bool>(false),
                _ => throw new ArgumentOutOfRangeException($"Unknown {nameof(State)}: {this.state}"),
            };
        }

        private async ValueTask<bool> MoveNextAsync_GatherGlobalStatisticsAsync(ITrace trace, CancellationToken cancellationToken)
        {
            TryCatch<(GlobalFullTextSearchStatistics, QueryPage)> tryGatherStatistics = await GatherStatisticsAsync(
                this.globalStatisticsPipeline,
                trace,
                cancellationToken);
            if (tryGatherStatistics.Failed)
            {
                this.Current = TryCatch<QueryPage>.FromException(tryGatherStatistics.Exception);
                this.state = State.Done;
                return true;
            }

            (GlobalFullTextSearchStatistics statistics, QueryPage queryPage) = tryGatherStatistics.Result;

            TryCatch<List<IQueryPipelineStage>> tryCreateQueryPipelineStages = CreateQueryPipelineStages(
                this.hybridSearchQueryInfo.ComponentQueryInfos,
                statistics,
                this.pipelineFactory);
            if (tryCreateQueryPipelineStages.Failed)
            {
                this.Current = TryCatch<QueryPage>.FromException(tryCreateQueryPipelineStages.Exception);
                this.state = State.Done;
                return true;
            }

            this.queryPipelineStages = tryCreateQueryPipelineStages.Result;
            this.state = State.Initialized;
            this.Current = TryCatch<QueryPage>.FromResult(queryPage);
            return true;
        }

        private async ValueTask<bool> MoveNextAsync_RunComponentQueriesAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (this.queryPipelineStages.Count == 1)
            {
                return await this.MoveNextAsync_DrainSingletonComponentAsync(trace, cancellationToken);
            }

            TryCatch<(IReadOnlyList<HybridSearchQueryResult>, QueryPage)> tryCollateSortedPipelineStageResults = await CollateSortedPipelineStageResultsAsync(
                this.queryPipelineStages,
                this.maxConcurrency,
                trace,
                cancellationToken);
            if (tryCollateSortedPipelineStageResults.Failed)
            {
                this.Current = TryCatch<QueryPage>.FromException(tryCollateSortedPipelineStageResults.Exception);
                this.state = State.Done;
                return true;
            }

            (IReadOnlyList<HybridSearchQueryResult> queryResults, QueryPage emptyPage) = tryCollateSortedPipelineStageResults.Result;

            this.queryPageParameters = new QueryPageParameters(
                activityId: emptyPage.ActivityId,
                cosmosQueryExecutionInfo: emptyPage.CosmosQueryExecutionInfo,
                distributionPlanSpec: emptyPage.DistributionPlanSpec,
                additionalHeaders: emptyPage.AdditionalHeaders);
            this.bufferedResults = queryResults;
            this.enumerator = queryResults.GetEnumerator();
            this.Current = TryCatch<QueryPage>.FromResult(emptyPage);
            this.state = State.Draining;
            return true;
        }

        private async ValueTask<bool> MoveNextAsync_DrainSingletonComponentAsync(ITrace trace, CancellationToken cancellationToken)
        {
            Debug.Assert(this.queryPipelineStages != null && this.queryPipelineStages.Count == 1);
            IQueryPipelineStage sourceStage = this.queryPipelineStages[0];

            if (await sourceStage.MoveNextAsync(trace, cancellationToken))
            {
                if (sourceStage.Current.Failed)
                {
                    this.Current = sourceStage.Current;
                    this.state = State.Done;
                    return true;
                }

                QueryPage page = sourceStage.Current.Result;

                List<CosmosElement> documents = new List<CosmosElement>(page.Documents.Count);
                foreach (CosmosElement cosmosElement in page.Documents)
                {
                    HybridSearchQueryResult hybridSearchQueryResult = HybridSearchQueryResult.Create(cosmosElement);
                    HybridSearchDebugTraceHelpers.TraceQueryResult(hybridSearchQueryResult);
                    documents.Add(hybridSearchQueryResult.Payload);
                }

                this.Current = TryCatch<QueryPage>.FromResult(new QueryPage(
                    documents,
                    page.RequestCharge,
                    page.ActivityId,
                    page.CosmosQueryExecutionInfo,
                    page.DistributionPlanSpec,
                    DisallowContinuationTokenMessages.HybridSearch,
                    page.AdditionalHeaders,
                    page.State,
                    streaming: false));
                this.state = State.Draining;
                return true;
            }
            else
            {
                this.state = State.Done;
                return false;
            }
        }

        private ValueTask<bool> MoveNextAsync_DrainPageAsync(ITrace trace, CancellationToken cancellationToken)
        {
            if (this.queryPipelineStages.Count == 1)
            {
                return this.MoveNextAsync_DrainSingletonComponentAsync(trace, cancellationToken);
            }
            else
            {
                return new ValueTask<bool>(this.MoveNextAsync_DrainPage());
            }
        }

        private bool MoveNextAsync_DrainPage()
        {
            List<CosmosElement> documents = new List<CosmosElement>(this.pageSize);
            while (documents.Count < this.pageSize && this.enumerator.MoveNext())
            {
                documents.Add(this.enumerator.Current.Payload);
            }

            if (documents.Count > 0)
            {
                QueryPage queryPage = new QueryPage(
                    documents,
                    requestCharge: 0,
                    this.queryPageParameters.ActivityId,
                    this.queryPageParameters.CosmosQueryExecutionInfo,
                    this.queryPageParameters.DistributionPlanSpec,
                    DisallowContinuationTokenMessages.HybridSearch,
                    this.queryPageParameters.AdditionalHeaders,
                    state: Continuation,
                    streaming: false);

                this.Current = TryCatch<QueryPage>.FromResult(queryPage);
                this.state = State.Draining;
                return true;
            }
            else
            {
                this.state = State.Done;
                return false;
            }
        }

        private static TryCatch<List<IQueryPipelineStage>> CreateQueryPipelineStages(
            IReadOnlyList<QueryInfo> queryInfos,
            GlobalFullTextSearchStatistics statistics,
            HybridSearchComponentPipelineFactory pipelineFactory)
        {
            List<QueryInfo> rewrittenQueryInfos = new List<QueryInfo>(queryInfos.Count);
            foreach (QueryInfo queryInfo in queryInfos)
            {
                QueryInfo rewrittenQueryInfo = RewriteOrderByQueryInfo(queryInfo, statistics, queryInfos.Count);
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

        private static async ValueTask<TryCatch<(IReadOnlyList<HybridSearchQueryResult>, QueryPage)>> CollateSortedPipelineStageResultsAsync(
            IReadOnlyList<IQueryPipelineStage> queryPipelineStages,
            int maxConcurrency,
            ITrace trace,
            CancellationToken cancellationToken)
        {
            // Sort and coalesce the results on _rid
            // After sorting, each HybridSearchQueryResult has a fixed index in the list
            // This index can be used as the key for the ranking array
            // Now create an array (per dimension) of tuples (score, index) and sort it by score
            // We can use these sorted arrays to compute the ranks. Identical scores get the same rank
            // Create an array of tuples of (RRF scores, index) for each document using the ranks
            // Use the ranks array to compute the RRF scores
            // Sort the array by RRF scores

            TryCatch<(List<HybridSearchQueryResult> queryResults, QueryPage emptyPage)> tryGetResults = await PrefetchInParallelAsync(
                queryPipelineStages,
                maxConcurrency,
                trace,
                cancellationToken);

            if (tryGetResults.Failed)
            {
                return TryCatch<(IReadOnlyList<HybridSearchQueryResult>, QueryPage emptyPage)>.FromException(tryGetResults.Exception);
            }

            (List<HybridSearchQueryResult> queryResults, QueryPage emptyPage) = tryGetResults.Result;
            if (queryResults.Count == 0 || queryResults.Count == 1)
            {
                return TryCatch<(IReadOnlyList<HybridSearchQueryResult>, QueryPage emptyPage)>.FromResult((queryResults, emptyPage));
            }

            HybridSearchDebugTraceHelpers.TraceQueryResults(queryResults, queryPipelineStages.Count);

            queryResults.Sort((x, y) => string.CompareOrdinal(x.Rid.Value, y.Rid.Value));

            CoalesceDuplicateRids(queryResults);

            TryCatch<IReadOnlyList<List<ScoreTuple>>> tryGetComponentScores = RetrieveComponentScores(queryResults, queryPipelineStages.Count);
            if (tryGetComponentScores.Failed)
            {
                return TryCatch<(IReadOnlyList<HybridSearchQueryResult>, QueryPage)>.FromException(tryGetComponentScores.Exception);
            }

            IReadOnlyList<List<ScoreTuple>> componentScores = tryGetComponentScores.Result;

            foreach (List<ScoreTuple> scoreTuples in componentScores)
            {
                scoreTuples.Sort((x, y) => (-1) * x.Score.CompareTo(y.Score)); // sort descending, since higher scores are better
            }

            int[,] ranks = ComputeRanks(componentScores);

            ComputeRrfScores(ranks, queryResults);

            HybridSearchDebugTraceHelpers.TraceQueryResultsWithRanks(queryResults, ranks);

            queryResults.Sort((x, y) => (-1) * x.Score.CompareTo(y.Score)); // higher scores are better

            HybridSearchDebugTraceHelpers.TraceQueryResults(queryResults, queryPipelineStages.Count);

            return TryCatch<(IReadOnlyList<HybridSearchQueryResult>, QueryPage)>.FromResult((queryResults, emptyPage));
        }

        private static async ValueTask<TryCatch<(List<HybridSearchQueryResult> queryResults, QueryPage emptyPage)>> PrefetchInParallelAsync(
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

            int queryResultCount = 0;
            List<IReadOnlyList<QueryPage>> prefetchedPageLists = new List<IReadOnlyList<QueryPage>>(prefetchers.Count);
            QueryPageParameters queryPageParameters = null;
            foreach (QueryPipelineStagePrefetcher prefetcher in prefetchers)
            {
                TryCatch<(IReadOnlyList<QueryPage>, int)> tryGetResults = await prefetcher.GetResultAsync(trace, cancellationToken);
                if (tryGetResults.Failed)
                {
                    return TryCatch<(List<HybridSearchQueryResult> queryResults, QueryPage emptyPage)>.FromException(tryGetResults.Exception);
                }

                (IReadOnlyList<QueryPage> queryPages, int documentCount) = tryGetResults.Result;
                prefetchedPageLists.Add(queryPages);
                queryResultCount += documentCount;

                if (queryPageParameters == null && queryPages.Count > 0)
                {
                    QueryPage queryPage = queryPages[0];
                    queryPageParameters = new QueryPageParameters(
                            activityId: queryPage.ActivityId,
                            cosmosQueryExecutionInfo: queryPage.CosmosQueryExecutionInfo,
                            distributionPlanSpec: queryPage.DistributionPlanSpec,
                            additionalHeaders: queryPage.AdditionalHeaders);
                }
            }

            List<HybridSearchQueryResult> queryResults = new List<HybridSearchQueryResult>(queryResultCount);
            double requestCharge = 0;
            foreach (IReadOnlyList<QueryPage> queryPages in prefetchedPageLists)
            {
                foreach (QueryPage queryPage in queryPages)
                {
                    requestCharge += queryPage.RequestCharge;
                    foreach (CosmosElement document in queryPage.Documents)
                    {
                        HybridSearchQueryResult hybridSearchQueryResult = HybridSearchQueryResult.Create(document);
                        queryResults.Add(hybridSearchQueryResult);
                    }
                }
            }

            QueryPage emptyPage = new QueryPage(
                EmptyPage,
                requestCharge,
                queryPageParameters.ActivityId,
                queryPageParameters.CosmosQueryExecutionInfo,
                queryPageParameters.DistributionPlanSpec,
                DisallowContinuationTokenMessages.HybridSearch,
                queryPageParameters.AdditionalHeaders,
                Continuation,
                streaming: false);

            return TryCatch<(List<HybridSearchQueryResult> queryResults, QueryPage emptyPage)>.FromResult((queryResults, emptyPage));
        }

        private static void CoalesceDuplicateRids(List<HybridSearchQueryResult> queryResults)
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

            queryResults.RemoveRange(writeIndex + 1, queryResults.Count - writeIndex - 1);
        }

        private static TryCatch<IReadOnlyList<List<ScoreTuple>>> RetrieveComponentScores(IReadOnlyList<HybridSearchQueryResult> queryResults, int componentCount)
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
                        DocumentClientException exception = new InternalServerErrorException($"componentScores must be an array of numbers.");
                        return TryCatch<IReadOnlyList<List<ScoreTuple>>>.FromException(exception);
                    }

                    ScoreTuple scoreTuple = new ScoreTuple(Number64.ToDouble(cosmosNumber.Value), index);
                    componentScores[componentScoreindex].Add(scoreTuple);
                }
            }

            return TryCatch<IReadOnlyList<List<ScoreTuple>>>.FromResult(componentScores);
        }

        private static int[,] ComputeRanks(IReadOnlyList<List<ScoreTuple>> componentScores)
        {
            int[,] ranks = new int[componentScores.Count, componentScores[0].Count];
            for (int componentIndex = 0; componentIndex < componentScores.Count; ++componentIndex)
            {
                int rank = 1; // ranks are 1 based
                for (int index = 0; index < componentScores[componentIndex].Count; ++index)
                {
                    // Identical scores should have the same rank
                    if ((index > 0) && (componentScores[componentIndex][index].Score < componentScores[componentIndex][index - 1].Score))
                    {
                        ++rank;
                    }

                    ranks[componentIndex, componentScores[componentIndex][index].Index] = rank;
                }
            }

            return ranks;
        }

        private static void ComputeRrfScores(
            int[,] ranks,
            List<HybridSearchQueryResult> queryResults)
        {
            int componentCount = ranks.GetLength(0);

            for (int index = 0; index < queryResults.Count; ++index)
            {
                double rrfScore = 0;
                for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
                {
                    rrfScore += 1.0 / (RrfConstant + ranks[componentIndex, index]);
                }

                queryResults[index] = queryResults[index].WithScore(rrfScore);
            }
        }

        private static QueryInfo RewriteOrderByQueryInfo(QueryInfo queryInfo, GlobalFullTextSearchStatistics statistics, int componentCount)
        {
            Debug.Assert(queryInfo.HasOrderBy, "The component query should have an order by");
            Debug.Assert(queryInfo.HasNonStreamingOrderBy, "The component query is a non streaming order by");

            List<string> rewrittenOrderByExpressions = new List<string>(queryInfo.OrderByExpressions.Count);
            foreach (string orderByExpression in queryInfo.OrderByExpressions)
            {
                string rewrittenOrderByExpression = FormatComponentQueryTextWorkaround(orderByExpression, statistics, componentCount);
                rewrittenOrderByExpressions.Add(rewrittenOrderByExpression);
            }

            string rewrittenQuery = FormatComponentQueryTextWorkaround(queryInfo.RewrittenQuery, statistics, componentCount);

            QueryInfo result = new QueryInfo()
            {
                DistinctType = queryInfo.DistinctType,
                Top = queryInfo.Top,
                Offset = queryInfo.Offset,
                Limit = queryInfo.Limit,

                OrderBy = queryInfo.OrderBy,
                OrderByExpressions = rewrittenOrderByExpressions,

                GroupByExpressions = queryInfo.GroupByExpressions,
                GroupByAliases = queryInfo.GroupByAliases,
                Aggregates = queryInfo.Aggregates,
                GroupByAliasToAggregateType = queryInfo.GroupByAliasToAggregateType,

                RewrittenQuery = rewrittenQuery,

                HasSelectValue = queryInfo.HasSelectValue,
                DCountInfo = queryInfo.DCountInfo,

                HasNonStreamingOrderBy = queryInfo.HasNonStreamingOrderBy,
            };

            return result;
        }

        // This method is unused currently, but we will switch back to using this
        // once the gateway has been redeployed with the fix for placeholder indexes
        private static string FormatComponentQueryText(string format, GlobalFullTextSearchStatistics statistics)
        {
            string query = format.Replace(Placeholders.TotalDocumentCount, statistics.DocumentCount.ToString());

            int count = statistics.FullTextStatistics.Count;
            for (int index = 0; index < count; ++index)
            {
                FullTextStatistics fullTextStatistics = statistics.FullTextStatistics[index];
                query = query.Replace(string.Format(Placeholders.FormattableTotalWordCount, index), fullTextStatistics.TotalWordCount.ToString());

                string hitCountsArray = string.Format("[{0}]", string.Join(",", fullTextStatistics.HitCounts.ToArray())); // ReadOnlyMemory<long> does not implement IEnumerable<long>
                query = query.Replace(string.Format(Placeholders.FormattableHitCountsArray, index), hitCountsArray);
            }

            return query;
        }

        private static string FormatComponentQueryTextWorkaround(string format, GlobalFullTextSearchStatistics statistics, int componentCount)
        {
            string query = format.Replace(Placeholders.TotalDocumentCount, statistics.DocumentCount.ToString());

            int statisticsIndex = 0;
            for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
            {
                string totalWordCountPlaceholder = string.Format(Placeholders.FormattableTotalWordCount, componentIndex);
                string hitCountsArrayPlaceholder = string.Format(Placeholders.FormattableHitCountsArray, componentIndex);

                if (query.IndexOf(totalWordCountPlaceholder) == -1)
                {
                    continue;
                }

                FullTextStatistics fullTextStatistics = statistics.FullTextStatistics[statisticsIndex];
                query = query.Replace(totalWordCountPlaceholder, fullTextStatistics.TotalWordCount.ToString());

                string hitCountsArray = string.Format("[{0}]", string.Join(",", fullTextStatistics.HitCounts.ToArray()));
                query = query.Replace(hitCountsArrayPlaceholder, hitCountsArray);

                ++statisticsIndex;
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
                DisallowContinuationTokenMessages.HybridSearch,
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

            private TryCatch<(IReadOnlyList<QueryPage>, int)> result;

            private bool prefetched;

            public QueryPipelineStagePrefetcher(IQueryPipelineStage queryPipelineStage)
            {
                this.queryPipelineStage = queryPipelineStage ?? throw new ArgumentNullException(nameof(queryPipelineStage));
            }

            public async ValueTask PrefetchAsync(ITrace trace, CancellationToken cancellationToken)
            {
                int documentCount = 0;
                List<QueryPage> pages = new List<QueryPage>();
                while (await this.queryPipelineStage.MoveNextAsync(trace, cancellationToken))
                {
                    TryCatch<QueryPage> tryCatchQueryPage = this.queryPipelineStage.Current;
                    if (tryCatchQueryPage.Failed)
                    {
                        this.result = TryCatch<(IReadOnlyList<QueryPage>, int)>.FromException(tryCatchQueryPage.Exception);
                        this.prefetched = true;
                        return;
                    }

                    pages.Add(tryCatchQueryPage.Result);
                    documentCount += tryCatchQueryPage.Result.Documents.Count;
                }

                this.result = TryCatch<(IReadOnlyList<QueryPage>, int)>.FromResult((pages, documentCount));
                this.prefetched = true;
            }

            public async ValueTask<TryCatch<(IReadOnlyList<QueryPage>, int)>> GetResultAsync(ITrace trace, CancellationToken cancellationToken)
            {
                if (!this.prefetched)
                {
                    await this.PrefetchAsync(trace, cancellationToken);
                }

                return this.result;
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

        private static class HybridSearchDebugTraceHelpers
        {
            private const bool Enabled = false;
#pragma warning disable CS0162 // Unreachable code detected

            [Conditional("DEBUG")]
            public static void TraceQueryResults(IReadOnlyList<HybridSearchQueryResult> queryResults, int componentCount)
            {
                if (Enabled)
                {
                    TraceQueryResultTSVHeader(componentCount);

                    foreach (HybridSearchQueryResult queryResult in queryResults)
                    {
                        StringBuilder builder = new StringBuilder();
                        AppendQueryResult(queryResult, builder);
                        string row = builder.ToString();
                        System.Diagnostics.Trace.WriteLine(row);
                    }
                }
            }

            [Conditional("DEBUG")]
            public static void TraceQueryResultsWithRanks(IReadOnlyList<HybridSearchQueryResult> queryResults, int[,] ranks)
            {
                if (Enabled)
                {
                    int componentCount = ranks.GetLength(0);
                    TraceFullDebugTSVHeader(componentCount);

                    for (int index = 0; index < queryResults.Count; ++index)
                    {
                        StringBuilder builder = new StringBuilder();

                        AppendQueryResult(queryResults[index], builder);
                        builder.Append("\t");

                        for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
                        {
                            builder.Append(ranks[componentIndex, index]);
                            builder.Append("\t");
                        }

                        builder.Remove(builder.Length - 1, 1); // remove extra tab

                        string row = builder.ToString();
                        System.Diagnostics.Trace.WriteLine(row);
                    }
                }
            }

            [Conditional("DEBUG")]
            public static void TraceQueryResult(HybridSearchQueryResult queryResult)
            {
                if (Enabled)
                {
                    StringBuilder builder = new StringBuilder();
                    AppendQueryResult(queryResult, builder);
                    string row = builder.ToString();
                    System.Diagnostics.Trace.WriteLine(row);
                }
            }

            private static StringBuilder AppendQueryResult(HybridSearchQueryResult queryResult, StringBuilder builder)
            {
                builder.Append(queryResult.Rid.Value.ToString());
                builder.Append("\t");
                builder.Append(queryResult.Payload.ToString());
                builder.Append("\t");

                CosmosArray componentScores = queryResult.ComponentScores;
                for (int componentScoreIndex = 0; componentScoreIndex < componentScores.Count; ++componentScoreIndex)
                {
                    builder.Append(componentScores[componentScoreIndex].ToString());
                    builder.Append("\t");
                }

                builder.Append(queryResult.Score);
                return builder;
            }

            private static void TraceQueryResultTSVHeader(int componentCount)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("_rid");
                builder.Append("\t");
                builder.Append("Payload");
                builder.Append("\t");

                for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
                {
                    builder.Append($"Score{componentIndex}");
                    builder.Append("\t");
                }

                builder.Append("RRFScore");
                builder.Append("\t");

                builder.Remove(builder.Length - 1, 1); // remove extra tab

                string header = builder.ToString();
                System.Diagnostics.Trace.WriteLine(header);
            }

            private static void TraceFullDebugTSVHeader(int componentCount)
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("_rid");
                builder.Append("\t");
                builder.Append("Payload");
                builder.Append("\t");

                for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
                {
                    builder.Append($"Score{componentIndex}");
                    builder.Append("\t");
                }

                builder.Append("RRFScore");
                builder.Append("\t");

                for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
                {
                    builder.Append($"Rank{componentIndex}");
                    builder.Append("\t");
                }

                builder.Remove(builder.Length - 1, 1); // remove extra tab

                string header = builder.ToString();
                System.Diagnostics.Trace.WriteLine(header);
            }
#pragma warning restore CS0162 // Unreachable code detected
        }
    }
}