//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.HybridSearch;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.CrossPartition.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class NonStreamingOrderByQueryTests
    {
        private delegate IReadOnlyList<double> CalculateScores(int componentCount, int documentIndex);

        private const int MaxConcurrency = 10;

        private const int DocumentCount = 420;

        private const int LeafPageCount = 100;

        private const int PageSize = 10;

        private const string ActivityId = "ActivityId";

        private const int QueryCharge = 42;

        private const int GlobalStatisticsQueryCharge = 3032;

        private const string CollectionRid = "1HNeAM-TiQY=";

        private const string RId = "_rid";

        private const string OrderByItems = "orderByItems";

        private const string Payload = "payload";

        private const string ComponentScores = "componentScores";

        private const string Item = "item";

        private const string Text = "text";

        private const string Index = "index";

        private const string IndexString = "indexString";

        private const string DocumentCountPropertyName = "documentCount";

        private const string FullTextStatistics = "fullTextStatistics";

        private const string TotalWordCount = "totalWordCount";

        private const string HitCounts = "hitCounts";

        private static readonly int[] PageSizes = new[] { 1, 10, 100, DocumentCount };

        public TestContext TestContext { get; set; }

        [TestMethod]
        public async Task InMemoryContainerParityTests()
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(DocumentCount);

            IReadOnlyList<OrderByColumn> idColumnAsc = new List<OrderByColumn>
            {
                new OrderByColumn("c.id", SortOrder.Ascending)
            };

            IReadOnlyList<OrderByColumn> idColumnDesc = new List<OrderByColumn>
            {
                new OrderByColumn("c.id", SortOrder.Descending)
            };

            IReadOnlyList<TestCase> testCases = new List<TestCase>
            {
                MakeTest(
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.id}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.id",
                    orderByColumns: idColumnAsc,
                    validate: result => Validate.IndexIsInOrder(result, propertyName: "id", DocumentCount, reversed: false)),
                MakeTest(
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.id}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.id DESC",
                    orderByColumns: idColumnDesc,
                    validate: result => Validate.IndexIsInOrder(result, propertyName: "id",DocumentCount, reversed: true)),

                // Empty result set
                MakeTest(
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.id}] AS orderByItems, c AS payload
                        FROM c
                        WHERE c.doesNotExist = true AND {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.id",
                    orderByColumns: idColumnAsc,
                    validate: result => result.Count == 0),
                MakeTest(
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.id}] AS orderByItems, c AS payload
                        FROM c
                        WHERE c.doesNotExist = true AND {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.id DESC",
                    orderByColumns: idColumnDesc,
                    validate: result => result.Count == 0),
            };

            await RunParityTests(
                documentContainer,
                new NonStreamingDocumentContainer(documentContainer, allowSplits: false),
                testCases);
        }

        [TestMethod]
        public async Task SplittingContainerParityTests()
        {
            IReadOnlyList<OrderByColumn> idColumnAsc = new List<OrderByColumn>
            {
                new OrderByColumn("c.id", SortOrder.Ascending)
            };

            IReadOnlyList<TestCase> testCases = new List<TestCase>
            {
                MakeTest(
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.id}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.id",
                    orderByColumns: idColumnAsc,
                    pageSizes: new int[] { 10 },
                    validate: result => Validate.IndexIsInOrder(result, propertyName: "id", DocumentCount, reversed: false)),
            };

            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(DocumentCount);
            await RunParityTests(
                documentContainer,
                new NonStreamingDocumentContainer(documentContainer, allowSplits: true),
                testCases,
                new TestOptions(validateCharges: false, maxConcurrency: 1));
        }

        [TestMethod]
        public async Task ShufflingContainerParityTests()
        {
            static bool IndexIsInOrder(IReadOnlyList<CosmosElement> result, bool reversed)
            {
                return Validate.IndexIsInOrder(result, propertyName: Index, LeafPageCount * PageSize, reversed);
            }

            IReadOnlyList<ParityTestCase> testCases = new List<ParityTestCase>
            {
                MakeParityTest(
                    feedMode: PartitionedFeedMode.NonStreaming,
                    documentCreationMode: DocumentCreationMode.SingleItem,
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.index}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.index",
                    orderByColumns: new List<OrderByColumn>
                    {
                        new OrderByColumn($"c.{Index}", SortOrder.Ascending)
                    },
                    validate: result => IndexIsInOrder(result, reversed: false)),
                MakeParityTest(
                    feedMode: PartitionedFeedMode.NonStreamingReversed,
                    documentCreationMode: DocumentCreationMode.SingleItem,
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.index}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.index DESC",
                    orderByColumns: new List<OrderByColumn>
                    {
                        new OrderByColumn($"c.{Index}", SortOrder.Descending)
                    },
                    validate: result => IndexIsInOrder(result, reversed: true)),
                MakeParityTest(
                    feedMode: PartitionedFeedMode.NonStreaming,
                    documentCreationMode: DocumentCreationMode.MultiItem,
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.index}, {""item"": c.indexString}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.index, c.indexString",
                    orderByColumns: new List<OrderByColumn>
                    {
                        new OrderByColumn($"c.{Index}", SortOrder.Ascending),
                        new OrderByColumn($"c.{IndexString}", SortOrder.Ascending)
                    },
                    validate: result => IndexIsInOrder(result, reversed: false)),
                MakeParityTest(
                    feedMode: PartitionedFeedMode.NonStreamingReversed,
                    documentCreationMode: DocumentCreationMode.MultiItem,
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.index}, {""item"": c.indexString}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.index DESC, c.indexString DESC",
                    orderByColumns: new List<OrderByColumn>
                    {
                        new OrderByColumn($"c.{Index}", SortOrder.Descending),
                        new OrderByColumn($"c.{IndexString}", SortOrder.Descending)
                    },
                    validate: result => IndexIsInOrder(result, reversed: true)),
                MakeParityTest(
                    feedMode: PartitionedFeedMode.NonStreaming,
                    documentCreationMode: DocumentCreationMode.MultiItemSwapped,
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.indexString}, {""item"": c.index}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.indexString, c.index",
                    orderByColumns: new List<OrderByColumn>
                    {
                        new OrderByColumn($"c.{IndexString}", SortOrder.Ascending),
                        new OrderByColumn($"c.{Index}", SortOrder.Ascending),
                    },
                    validate: result => IndexIsInOrder(result, reversed: false)),
                MakeParityTest(
                    feedMode: PartitionedFeedMode.NonStreamingReversed,
                    documentCreationMode: DocumentCreationMode.MultiItemSwapped,
                    queryText: @"
                        SELECT c._rid AS _rid, [{""item"": c.indexString}, {""item"": c.index}] AS orderByItems, c AS payload
                        FROM c
                        WHERE {documentdb-formattableorderbyquery-filter}
                        ORDER BY c.indexString DESC, c.index DESC",
                    orderByColumns: new List<OrderByColumn>
                    {
                        new OrderByColumn($"c.{IndexString}", SortOrder.Descending),
                        new OrderByColumn($"c.{Index}", SortOrder.Descending),
                    },
                    validate: result => IndexIsInOrder(result, reversed: true)),
            };

            await RunParityTests(testCases);
        }

        [TestMethod]
        public void HybridSearchDefaultScopeIsGlobalTests()
        {
            // Verify that default QueryRequestOptions.FullTextScoreScope is Global
            QueryRequestOptions options = new QueryRequestOptions();
            Assert.AreEqual(FullTextScoreScope.Global, options.FullTextScoreScope);
        }

        [TestMethod]
        public async Task HybridSearchStatisticsCacheScenarios()
        {
            List<StatisticsCacheScenario> scenarios = new List<StatisticsCacheScenario>
            {
                new StatisticsCacheScenario { Name = "Global hit", ExpectedTraceDatum = "Hit" },
                new StatisticsCacheScenario { Name = "Global hit with weighted RRF", Weights = new[] { 1.0, 2.0 }, ExpectedTraceDatum = "Hit" },
                new StatisticsCacheScenario { Name = "Global hit with skip and take", Skip = 5, Take = 20, ExpectedTraceDatum = "Hit" },
                new StatisticsCacheScenario { Name = "Global hit across small pages", PageSize = 7, ExpectedTraceDatum = "Hit" },
                new StatisticsCacheScenario { Name = "Global hit without order-by rewrite", SkipOrderByRewrite = true, ExpectedTraceDatum = "Hit" },
                new StatisticsCacheScenario { Name = "Global hit with a single component", SingleComponent = true, ExpectedTraceDatum = "Hit" },
                new StatisticsCacheScenario { Name = "Global hit with narrower target ranges", MeasuredTargetRangeCount = 1, ExpectedTraceDatum = "Hit" },
                new StatisticsCacheScenario { Name = "Local after Global warm-up", MeasuredScope = FullTextScoreScope.Local },
                new StatisticsCacheScenario { Name = "Local with narrower target ranges", MeasuredScope = FullTextScoreScope.Local, MeasuredTargetRangeCount = 1 },
                new StatisticsCacheScenario { Name = "Cache disabled", CacheEnabled = false },
                new StatisticsCacheScenario { Name = "Missing metadata", MetadataPresent = false },
                new StatisticsCacheScenario { Name = "Expired counts", AdvanceToExpiry = true, ExpectedTraceDatum = "Miss" },
                new StatisticsCacheScenario { Name = "Different container revision", MeasuredETag = "other-etag", ExpectedTraceDatum = "Miss" },
            };
            for (int index = 0; index < scenarios.Count; index++)
            {
                StatisticsCacheScenario scenario = scenarios[index];
                this.TestContext.WriteLine($"Case {index}: {scenario}");
                await VerifyStatisticsCacheScenarioAsync(scenario);
            }
        }

        [TestMethod]
        public async Task HybridSearchStatisticsCacheFailuresAreNotCachedOrRecovered()
        {
            TimeSpan maxAge = TimeSpan.FromMinutes(1);
            HybridSearchStatisticsCache cache = new HybridSearchStatisticsCache(maxAge, 256);
            StatisticsCacheScenario scenario = new StatisticsCacheScenario { Name = "Failures" };

            // A failed statistics collection fails the query and caches nothing.
            CachedHybridSearchRun failedStatistics = await RunCachedHybridSearchAsync(
                scenario, CreateInterceptingContainer(scenario, spec => Task.FromResult(IsGlobalStatisticsQuery(spec))), cache, StatisticsCacheRanges, FullTextScoreScope.Global, "etag", CancellationToken.None);
            Assert.IsNotNull(failedStatistics.Failure, "A statistics failure must fail the query.");
            Assert.AreEqual("Miss", failedStatistics.TraceDatum);

            CachedHybridSearchRun cold = await RunCachedHybridSearchAsync(scenario, cache, StatisticsCacheRanges, FullTextScoreScope.Global, "etag");
            Assert.AreEqual(StatisticsCacheRanges.Count, cold.StatisticsRequestCount, "A failed collection must not populate the cache.");

            // A component failure after a hit propagates and does not evict or bypass the cached statistics.
            CachedHybridSearchRun failedComponents = await RunCachedHybridSearchAsync(
                scenario, CreateInterceptingContainer(scenario, spec => Task.FromResult(!IsGlobalStatisticsQuery(spec))), cache, StatisticsCacheRanges, FullTextScoreScope.Global, "etag", CancellationToken.None);
            Assert.IsNotNull(failedComponents.Failure, "A component failure must propagate on a hit.");
            Assert.AreEqual("Hit", failedComponents.TraceDatum);

            CachedHybridSearchRun warm = await RunCachedHybridSearchAsync(scenario, cache, StatisticsCacheRanges, FullTextScoreScope.Global, "etag");
            Assert.AreEqual(0, warm.StatisticsRequestCount);
            CollectionAssert.AreEqual(cold.Documents.ToList(), warm.Documents.ToList());
        }

        [TestMethod]
        public async Task HybridSearchStatisticsCacheCancelledCollectionIsNotCached()
        {
            HybridSearchStatisticsCache cache = new HybridSearchStatisticsCache(TimeSpan.FromMinutes(1), 256);
            StatisticsCacheScenario scenario = new StatisticsCacheScenario { Name = "Cancellation" };
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            CachedHybridSearchRun cancelled = await RunCachedHybridSearchAsync(
                scenario, CreateStatisticsCacheContainer(scenario), cache, StatisticsCacheRanges, FullTextScoreScope.Global, "etag", cancellationTokenSource.Token);
            Assert.IsInstanceOfType(cancelled.Failure, typeof(OperationCanceledException), "A cancelled query must not complete.");
            CachedHybridSearchRun next = await RunCachedHybridSearchAsync(scenario, cache, StatisticsCacheRanges, FullTextScoreScope.Global, "etag");
            Assert.AreEqual(StatisticsCacheRanges.Count, next.StatisticsRequestCount, "A cancelled collection must not populate the cache.");
        }

        [TestMethod]
        public async Task HybridSearchStatisticsCacheConcurrentColdMissesRunIndependently()
        {
            HybridSearchStatisticsCache cache = new HybridSearchStatisticsCache(TimeSpan.FromMinutes(1), 256);
            StatisticsCacheScenario scenario = new StatisticsCacheScenario { Name = "Concurrent misses" };
            TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> firstArrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> secondArrived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Hold each query's statistics requests until both queries have missed the cache.
            static Func<SqlQuerySpec, Task<bool>> HoldStatistics(TaskCompletionSource<bool> arrived, Task released)
            {
                return async spec =>
                {
                    if (IsGlobalStatisticsQuery(spec))
                    {
                        arrived.TrySetResult(true);
                        await released;
                    }

                    return false;
                };
            }

            StatisticsCacheTestContainer firstContainer = CreateInterceptingContainer(scenario, HoldStatistics(firstArrived, release.Task));
            StatisticsCacheTestContainer secondContainer = CreateInterceptingContainer(scenario, HoldStatistics(secondArrived, release.Task));
            Task<CachedHybridSearchRun> first = RunCachedHybridSearchAsync(scenario, firstContainer, cache, StatisticsCacheRanges, FullTextScoreScope.Global, "etag", CancellationToken.None);
            Task<CachedHybridSearchRun> second = RunCachedHybridSearchAsync(scenario, secondContainer, cache, StatisticsCacheRanges, FullTextScoreScope.Global, "etag", CancellationToken.None);
            await Task.WhenAll(firstArrived.Task, secondArrived.Task).WaitAsync(TimeSpan.FromSeconds(30));
            release.SetResult(true);
            CachedHybridSearchRun[] runs = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(30));

            for (int index = 0; index < runs.Length; index++)
            {
                Assert.IsNull(runs[index].Failure, $"Run {index}");
                Assert.AreEqual("Miss", runs[index].TraceDatum, $"Run {index}");
            }

            CollectionAssert.AreEqual(runs[0].Documents.ToList(), runs[1].Documents.ToList());
            CachedHybridSearchRun warm = await RunCachedHybridSearchAsync(scenario, cache, StatisticsCacheRanges, FullTextScoreScope.Global, "etag");
            Assert.AreEqual(0, warm.StatisticsRequestCount);
        }

        private static readonly IReadOnlyList<FeedRangeEpk> StatisticsCacheRanges = new List<FeedRangeEpk>
        {
            new FeedRangeEpk(new Documents.Routing.Range<string>(string.Empty, "AA", true, false)),
            new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)),
            new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "FF", true, false)),
        };

        private static async Task VerifyStatisticsCacheScenarioAsync(StatisticsCacheScenario scenario)
        {
            TimeSpan maxAge = TimeSpan.FromMinutes(1);
            TimeSpan now = TimeSpan.Zero;
            HybridSearchStatisticsCache cache = scenario.CacheEnabled ? new HybridSearchStatisticsCache(maxAge, 256, () => now) : null;

            // Every scenario first runs a Global query over all ranges to warm the cache.
            CachedHybridSearchRun warmUp = await RunCachedHybridSearchAsync(scenario, cache, StatisticsCacheRanges, FullTextScoreScope.Global, "etag");
            Assert.AreEqual(StatisticsCacheRanges.Count, warmUp.StatisticsRequestCount, scenario.ToString());

            if (scenario.AdvanceToExpiry)
            {
                now = maxAge;
            }

            IReadOnlyList<FeedRangeEpk> targetRanges = StatisticsCacheRanges.Take(scenario.MeasuredTargetRangeCount ?? StatisticsCacheRanges.Count).ToList();
            CachedHybridSearchRun uncached = await RunCachedHybridSearchAsync(scenario, null, targetRanges, scenario.MeasuredScope, scenario.MeasuredETag);
            CachedHybridSearchRun measured = await RunCachedHybridSearchAsync(scenario, cache, targetRanges, scenario.MeasuredScope, scenario.MeasuredETag);

            bool expectHit = scenario.ExpectedTraceDatum == "Hit";
            int statisticsRangeCount = scenario.MeasuredScope == FullTextScoreScope.Global ? StatisticsCacheRanges.Count : targetRanges.Count;
            Assert.AreEqual(expectHit ? 0 : statisticsRangeCount, measured.StatisticsRequestCount, scenario.ToString());
            Assert.AreEqual(statisticsRangeCount, uncached.StatisticsRequestCount, scenario.ToString());
            CollectionAssert.AreEqual(uncached.Documents.ToList(), measured.Documents.ToList(), scenario.ToString());
            Assert.AreEqual(
                expectHit ? uncached.RequestCharge - (statisticsRangeCount * GlobalStatisticsQueryCharge) : uncached.RequestCharge,
                measured.RequestCharge,
                scenario.ToString());
            Assert.AreEqual(scenario.ExpectedTraceDatum, measured.TraceDatum, scenario.ToString());
        }

        private static Task<CachedHybridSearchRun> RunCachedHybridSearchAsync(
            StatisticsCacheScenario scenario,
            HybridSearchStatisticsCache cache,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            FullTextScoreScope fullTextScoreScope,
            string etag)
        {
            return RunCachedHybridSearchAsync(
                scenario,
                CreateStatisticsCacheContainer(scenario),
                cache,
                targetRanges,
                fullTextScoreScope,
                etag,
                CancellationToken.None);
        }

        private static async Task<CachedHybridSearchRun> RunCachedHybridSearchAsync(
            StatisticsCacheScenario scenario,
            StatisticsCacheTestContainer container,
            HybridSearchStatisticsCache cache,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            FullTextScoreScope fullTextScoreScope,
            string etag,
            CancellationToken cancellationToken)
        {
            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                new DocumentContainer(container),
                Create2ItemSqlQuerySpec(),
                targetRanges,
                partitionKey: null,
                queryInfo: null,
                hybridSearchQueryInfo: CreateStatisticsCacheQueryInfo(scenario),
                maxItemCount: scenario.PageSize,
                new ContainerQueryProperties("rid", etag, null, null, null, Cosmos.GeospatialType.Geography, false),
                StatisticsCacheRanges,
                isContinuationExpected: true,
                maxConcurrency: MaxConcurrency,
                fullTextScoreScope: fullTextScoreScope,
                requestContinuationToken: null,
                statisticsCache: cache);
            Assert.IsTrue(tryCreatePipeline.Succeeded, scenario.ToString());

            List<CosmosElement> documents = new List<CosmosElement>();
            double requestCharge = 0;
            Exception failure = null;
            using Cosmos.Tracing.Trace trace = Cosmos.Tracing.Trace.GetRootTrace("Hybrid search statistics cache test");
            await using (IQueryPipelineStage stage = tryCreatePipeline.Result)
            {
                // Failures surface either as a failed page or, for some component pipelines and cancellation, as an exception.
                try
                {
                    while (await stage.MoveNextAsync(trace, cancellationToken))
                    {
                        if (stage.Current.Failed)
                        {
                            failure = stage.Current.Exception;
                            break;
                        }

                        Assert.IsTrue(stage.Current.Result.Documents.Count <= scenario.PageSize, scenario.ToString());
                        documents.AddRange(stage.Current.Result.Documents);
                        requestCharge += stage.Current.Result.RequestCharge;
                    }
                }
                catch (Exception exception) when (!(exception is AssertFailedException))
                {
                    failure = exception;
                }
            }

            trace.Data.TryGetValue("Hybrid Search Statistics Cache", out object traceDatum);
            return new CachedHybridSearchRun(documents, requestCharge, container.StatisticsRequestCount, (string)traceDatum, failure);
        }

        private static StatisticsCacheTestContainer CreateStatisticsCacheContainer(StatisticsCacheScenario scenario)
        {
            return CreateInterceptingContainer(scenario, _ => Task.FromResult(false));
        }

        private static StatisticsCacheTestContainer CreateInterceptingContainer(StatisticsCacheScenario scenario, Func<SqlQuerySpec, Task<bool>> shouldFail)
        {
            MockDocumentContainer inner = MockDocumentContainer.CreateHybridSearchContainer(
                StatisticsCacheRanges,
                new[] { PartitionedFeedMode.NonStreamingReversed, PartitionedFeedMode.NonStreamingReversed },
                leafPageCount: 2,
                backendPageSize: 10,
                returnEmptyGlobalStatistics: false,
                skipOrderByRewrite: scenario.SkipOrderByRewrite,
                calculateScores: CalculateDefaultScores);
            return new StatisticsCacheTestContainer(inner, shouldFail);
        }

        private static HybridSearchQueryInfo CreateStatisticsCacheQueryInfo(StatisticsCacheScenario scenario)
        {
            double[] weights = scenario.Weights?.ToArray();
            HybridSearchQueryInfo hybridSearchQueryInfo = scenario.SkipOrderByRewrite
                ? Create2ItemHybridSearchSkipOrderByRewriteQueryInfo(true, scenario.Skip, scenario.Take, weights)
                : Create2ItemHybridSearchQueryInfo(true, scenario.Skip, scenario.Take, weights);

            if (scenario.SingleComponent)
            {
                hybridSearchQueryInfo.ComponentQueryInfos = new List<QueryInfo> { hybridSearchQueryInfo.ComponentQueryInfos[0] };
                hybridSearchQueryInfo.ComponentWeights = null;
            }

            // The metadata matches the mock statistics: two terms on the first path and one on the second.
            if (scenario.MetadataPresent)
            {
                hybridSearchQueryInfo.FullTextSearchTerms = new List<FullTextSearchTerms>
                {
                    new FullTextSearchTerms
                    {
                        Path = new List<string> { "text" },
                        Terms = new List<FullTextSearchTerm> { new FullTextSearchTerm { Term = "swim" }, new FullTextSearchTerm { Term = "run" } },
                    },
                    new FullTextSearchTerms
                    {
                        Path = new List<string> { "abstract" },
                        Terms = new List<FullTextSearchTerm> { new FullTextSearchTerm { Term = "energy" } },
                    },
                };
            }

            return hybridSearchQueryInfo;
        }

        private sealed class StatisticsCacheScenario
        {
            public string Name { get; set; }

            public bool CacheEnabled { get; set; } = true;

            public bool MetadataPresent { get; set; } = true;

            public bool SkipOrderByRewrite { get; set; }

            public bool SingleComponent { get; set; }

            public double[] Weights { get; set; }

            public uint? Skip { get; set; }

            public uint? Take { get; set; }

            public int PageSize { get; set; } = 1000;

            public FullTextScoreScope MeasuredScope { get; set; } = FullTextScoreScope.Global;

            public int? MeasuredTargetRangeCount { get; set; }

            public string MeasuredETag { get; set; } = "etag";

            public bool AdvanceToExpiry { get; set; }

            public string ExpectedTraceDatum { get; set; }

            public override string ToString() =>
                $"{this.Name}: Enabled={this.CacheEnabled}, Metadata={this.MetadataPresent}, SkipOrderByRewrite={this.SkipOrderByRewrite}, " +
                $"SingleComponent={this.SingleComponent}, Weights={(this.Weights == null ? "<null>" : string.Join(",", this.Weights))}, " +
                $"Skip={this.Skip?.ToString() ?? "<null>"}, Take={this.Take?.ToString() ?? "<null>"}, PageSize={this.PageSize}, " +
                $"Scope={this.MeasuredScope}, TargetRanges={this.MeasuredTargetRangeCount?.ToString() ?? "all"}, ETag={this.MeasuredETag}, " +
                $"Expired={this.AdvanceToExpiry}, ExpectedTraceDatum={this.ExpectedTraceDatum ?? "<none>"}";
        }

        private sealed class CachedHybridSearchRun
        {
            public CachedHybridSearchRun(IReadOnlyList<CosmosElement> documents, double requestCharge, int statisticsRequestCount, string traceDatum, Exception failure)
            {
                this.Documents = documents;
                this.RequestCharge = requestCharge;
                this.StatisticsRequestCount = statisticsRequestCount;
                this.TraceDatum = traceDatum;
                this.Failure = failure;
            }

            public IReadOnlyList<CosmosElement> Documents { get; }

            public double RequestCharge { get; }

            public int StatisticsRequestCount { get; }

            public string TraceDatum { get; }

            public Exception Failure { get; }
        }

        // Counts statistics requests and can fail or delay queries before delegating to the mock container.
        private sealed class StatisticsCacheTestContainer : IMonadicDocumentContainer
        {
            private readonly IMonadicDocumentContainer inner;
            private readonly Func<SqlQuerySpec, Task<bool>> shouldFail;
            private int statisticsRequestCount;

            public StatisticsCacheTestContainer(IMonadicDocumentContainer inner, Func<SqlQuerySpec, Task<bool>> shouldFail)
            {
                this.inner = inner;
                this.shouldFail = shouldFail;
            }

            public int StatisticsRequestCount => Volatile.Read(ref this.statisticsRequestCount);

            public async Task<TryCatch<QueryPage>> MonadicQueryAsync(
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                QueryExecutionOptions queryPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                if (IsGlobalStatisticsQuery(sqlQuerySpec))
                {
                    Interlocked.Increment(ref this.statisticsRequestCount);
                }

                if (await this.shouldFail(sqlQuerySpec))
                {
                    return TryCatch<QueryPage>.FromException(new InvalidOperationException("Injected query failure."));
                }

                return await this.inner.MonadicQueryAsync(sqlQuerySpec, feedRangeState, queryPaginationOptions, trace, cancellationToken);
            }

            public Task<TryCatch<Record>> MonadicCreateItemAsync(
                CosmosObject payload,
                CancellationToken cancellationToken) => this.inner.MonadicCreateItemAsync(payload, cancellationToken);

            public Task<TryCatch<Record>> MonadicReadItemAsync(
                CosmosElement partitionKey,
                string identifer,
                CancellationToken cancellationToken) => this.inner.MonadicReadItemAsync(partitionKey, identifer, cancellationToken);

            public Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(
                FeedRangeState<ReadFeedState> feedRangeState,
                ReadFeedExecutionOptions readFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicReadFeedAsync(feedRangeState, readFeedPaginationOptions, trace, cancellationToken);

            public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
                FeedRangeState<ChangeFeedState> feedRangeState,
                ChangeFeedExecutionOptions changeFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicChangeFeedAsync(feedRangeState, changeFeedPaginationOptions, trace, cancellationToken);

            public Task<TryCatch> MonadicSplitAsync(
                FeedRangeInternal feedRange,
                CancellationToken cancellationToken) => this.inner.MonadicSplitAsync(feedRange, cancellationToken);

            public Task<TryCatch> MonadicMergeAsync(
                FeedRangeInternal feedRange1,
                FeedRangeInternal feedRange2,
                CancellationToken cancellationToken) => this.inner.MonadicMergeAsync(feedRange1, feedRange2, cancellationToken);

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
                FeedRangeInternal feedRange,
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicGetChildRangeAsync(feedRange, trace, cancellationToken);

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicGetFeedRangesAsync(trace, cancellationToken);

            public Task<TryCatch> MonadicRefreshProviderAsync(
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicRefreshProviderAsync(trace, cancellationToken);

            public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(
                ITrace trace,
                CancellationToken cancellationToken) => this.inner.MonadicGetResourceIdentifierAsync(trace, cancellationToken);
        }

        [TestMethod]
        public async Task HybridSearchTests()
        {
            IReadOnlyList<HybridSearchTest> testCases = new List<HybridSearchTest>
            {
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: false,
                    skip: null,
                    take: 100,
                    pageSize: 1000),
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: false,
                    skip: 20,
                    take: 100,
                    pageSize: 1000),
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 20,
                    take: 100,
                    pageSize: 1000),
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 20,
                    take: 100,
                    pageSize: 10),
                MakeHybridSearchTest(
                    leafPageCount: 10,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 20,
                    take: 100,
                    pageSize: 10),
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 100,
                    requiresGlobalStatistics: true,
                    skip: 7,
                    take: 10,
                    pageSize: 1),
                MakeHybridSearchTest(
                    leafPageCount: 0,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 0,
                    take: 10,
                    pageSize: 10,
                    returnEmptyGlobalStatistics: true),

                // Local scope tests - query subset of ranges, statistics also from subset
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: null,
                    take: 100,
                    pageSize: 1000,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 2),
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 10,
                    take: 50,
                    pageSize: 100,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 3),
                // Local scope with single target range
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: null,
                    take: 50,
                    pageSize: 100,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 1),
                // Local scope with larger page size than document count
                MakeHybridSearchTest(
                    leafPageCount: 2,
                    backendPageSize: 5,
                    requiresGlobalStatistics: true,
                    skip: null,
                    take: 100,
                    pageSize: 1000,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 2),
                // Local scope with skip and take
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 5,
                    take: 20,
                    pageSize: 10,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 2),
                // Local scope with small page size (multiple pages)
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: null,
                    take: 50,
                    pageSize: 5,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 3),
                // Local scope with 4 target ranges (majority of 6)
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 10,
                    take: 100,
                    pageSize: 50,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 4),

                // Global scope test - query subset of ranges, statistics from ALL ranges
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: null,
                    take: 100,
                    pageSize: 1000,
                    fullTextScoreScope: FullTextScoreScope.Global,
                    targetRangeCount: 2),
            };

            foreach (HybridSearchTest testCase in testCases)
            {
                await RunHybridSearchTest(testCase);
            }
        }

        [TestMethod]
        public async Task HybridSearchSkipOrderByRewriteTests()
        {
            IReadOnlyList<HybridSearchTest> testCases = new List<HybridSearchTest>
            {
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: false,
                    skip: null,
                    take: 100,
                    pageSize: 1000),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: false,
                    skip: 20,
                    take: 100,
                    pageSize: 1000),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 20,
                    take: 100,
                    pageSize: 1000),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 20,
                    take: 100,
                    pageSize: 10),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 10,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 20,
                    take: 100,
                    pageSize: 10),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 100,
                    requiresGlobalStatistics: true,
                    skip: 7,
                    take: 10,
                    pageSize: 1),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 0,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 0,
                    take: 10,
                    pageSize: 10,
                    returnEmptyGlobalStatistics: true),

                // Local scope test with skip order by rewrite
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 20,
                    take: 100,
                    pageSize: 1000,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 2),
                // Local scope with single target range
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: null,
                    take: 50,
                    pageSize: 100,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 1),
                // Local scope with small page size
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 5,
                    take: 30,
                    pageSize: 5,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 3),
                // Local scope with different skip/take values
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 10,
                    take: 25,
                    pageSize: 10,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 2),
                // Local scope with 4 target ranges
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: null,
                    take: 100,
                    pageSize: 50,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 4),
            };

            foreach (HybridSearchTest testCase in testCases)
            {
                await RunHybridSearchTest(testCase);
            }
        }

        [TestMethod]
        public async Task HybridSearchWeightedRRFTests()
        {
            IReadOnlyList<HybridSearchTest> testCases = new List<HybridSearchTest>
            {
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: false,
                    skip: null,
                    take: 100,
                    weights: new double[] { 1.0, 1.0 },
                    pageSize: 1000),
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: false,
                    skip: 20,
                    take: 100,
                    weights: new double[] { 0.25, 2.1 },
                    pageSize: 1000),
                MakeHybridSearchTest(
                    leafPageCount: 0,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 0,
                    take: 10,
                    weights: new double[] { 1.25, 2.0 },
                    pageSize: 10,
                    returnEmptyGlobalStatistics: true),
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: false,
                    skip: null,
                    take: 100,
                    weights: new double[] { -1.0, -1.0 },
                    pageSize: 1000),
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 100,
                    requiresGlobalStatistics: true,
                    skip: 7,
                    take: 10,
                    weights: new double[] { -1.33, -0.45 },
                    pageSize: 1),
                MakeHybridSearchTest(
                    leafPageCount: 0,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 0,
                    take: 10,
                    weights: new double[] { -1.25, -2.0 },
                    pageSize: 10,
                    returnEmptyGlobalStatistics: true),

                // Local scope test with weighted RRF
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: null,
                    take: 100,
                    weights: new double[] { 1.0, 1.0 },
                    pageSize: 1000,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 2),
                // Local scope with different weights
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 10,
                    take: 50,
                    weights: new double[] { 0.5, 1.5 },
                    pageSize: 100,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 3),
                // Local scope with single target range and weights
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: null,
                    take: 50,
                    weights: new double[] { 2.0, 1.0 },
                    pageSize: 50,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 1),
                // Local scope with small page size and weights
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 5,
                    take: 30,
                    weights: new double[] { 1.0, 2.0 },
                    pageSize: 5,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 2),
                // Local scope with 4 target ranges and weights
                MakeHybridSearchTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: null,
                    take: 100,
                    weights: new double[] { 0.75, 1.25 },
                    pageSize: 25,
                    fullTextScoreScope: FullTextScoreScope.Local,
                    targetRangeCount: 4),
            };

            foreach (HybridSearchTest testCase in testCases)
            {
                await RunHybridSearchTest(testCase);
            }
        }

        [TestMethod]
        public async Task CompetitionRanksDetermineWeightedRrfOrder()
        {
            await AssertCompetitionRankScenarioAsync(
                firstComponentScores: new[] { 10.0, 10.0, 8.0, 7.0, 6.0 },
                expected: new[] { 1, 0, 2, 3, 4 });

            // Verify the regression case where a middle tie group causes the next rank to skip from 2 to 5.
            await AssertCompetitionRankScenarioAsync(
                firstComponentScores: new[] { 10.0, 9.0, 9.0, 9.0, 4.0 },
                expected: new[] { 3, 0, 2, 1, 4 });

            await AssertCompetitionRankScenarioAsync(
                firstComponentScores: new[] { 10.0, 9.0, 8.0, 7.0, 7.0 },
                expected: new[] { 0, 1, 4, 2, 3 });

            await AssertCompetitionRankScenarioAsync(
                firstComponentScores: new[] { 5.0, 5.0, 5.0, 5.0, 5.0 },
                expected: new[] { 4, 3, 2, 1, 0 });
        }

        private static async Task AssertCompetitionRankScenarioAsync(double[] firstComponentScores, int[] expected)
        {
            int[] actual = await RunCompetitionRankScenarioAsync(firstComponentScores);
            if (!expected.SequenceEqual(actual))
            {
                System.Diagnostics.Trace.WriteLine("Mismatch in competition rank results");
                System.Diagnostics.Trace.WriteLine($"Expected: {string.Join(", ", expected)}");
                System.Diagnostics.Trace.WriteLine($"Actual: {string.Join(", ", actual)}");
                Assert.Fail();
            }
        }

        private static async Task<int[]> RunCompetitionRankScenarioAsync(double[] firstComponentScores)
        {
            IReadOnlyList<FeedRangeEpk> feedRanges = new List<FeedRangeEpk>
            {
                new FeedRangeEpk(new Documents.Routing.Range<string>(string.Empty, "FF", true, false)),
            };

            MockDocumentContainer documentContainer = MockDocumentContainer.CreateHybridSearchContainer(
                feedRanges,
                new[]
                {
                    PartitionedFeedMode.NonStreamingReversed,
                    PartitionedFeedMode.NonStreamingReversed,
                },
                leafPageCount: 1,
                backendPageSize: firstComponentScores.Length,
                returnEmptyGlobalStatistics: false,
                skipOrderByRewrite: false,
                calculateScores: (_, index) => new[]
                {
                    firstComponentScores[index],
                    index + 1.0,
                });

            (IReadOnlyList<CosmosElement> results, _) = await CreateAndRunHybridSearchQueryPipelineStage(
                documentContainer,
                targetRanges: feedRanges,
                allRanges: feedRanges,
                requiresGlobalStatistics: false,
                pageSize: firstComponentScores.Length,
                skip: null,
                take: null,
                weights: new[] { 2.0, 1.0 },
                skipOrderByRewrite: false,
                fullTextScoreScope: FullTextScoreScope.Local);

            return results
                .Select(result => (int)Number64.ToLong(((CosmosNumber)((CosmosObject)result)[Index]).Value))
                .ToArray();
        }

        [TestMethod]
        public async Task HybridSearchSkipOrderByRewriteWeightedRRFTests()
        {
            IReadOnlyList<HybridSearchTest> testCases = new List<HybridSearchTest>
            {
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: false,
                    skip: null,
                    take: 100,
                    weights: new double[] { 1.0, 1.0 },
                    pageSize: 1000),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: false,
                    skip: 20,
                    take: 100,
                    weights: new double[] { 0.25, 2.1 },
                    pageSize: 1000),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 0,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 0,
                    take: 10,
                    weights: new double[] { 1.25, 2.0 },
                    pageSize: 10,
                    returnEmptyGlobalStatistics: true),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 10,
                    requiresGlobalStatistics: false,
                    skip: null,
                    take: 100,
                    weights: new double[] { -1.0, -1.0 },
                    pageSize: 1000),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 4,
                    backendPageSize: 100,
                    requiresGlobalStatistics: true,
                    skip: 7,
                    take: 10,
                    weights: new double[] { -1.33, -0.45 },
                    pageSize: 1),
                MakeHybridSearchSkipOrderByRewriteTest(
                    leafPageCount: 0,
                    backendPageSize: 10,
                    requiresGlobalStatistics: true,
                    skip: 0,
                    take: 10,
                    weights: new double[] { -1.25, -2.0 },
                    pageSize: 10,
                    returnEmptyGlobalStatistics: true),
            };

            foreach (HybridSearchTest testCase in testCases)
            {
                await RunHybridSearchTest(testCase);
            }
        }

        private static async Task RunHybridSearchTest(HybridSearchTest testCase)
        {
            IReadOnlyList<FeedRangeEpk> allRanges = new List<FeedRangeEpk>
            {
                new FeedRangeEpk(new Documents.Routing.Range<string>(string.Empty, "AA", true, false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "CC", true, false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("CC", "DD", true, false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("DD", "EE", true, false)),
                new FeedRangeEpk(new Documents.Routing.Range<string>("EE", "FF", true, false)),
            };

            // Determine target ranges based on test case
            IReadOnlyList<FeedRangeEpk> targetRanges = testCase.TargetRangeCount.HasValue
                ? allRanges.Take(testCase.TargetRangeCount.Value).ToList()
                : allRanges;

            int targetRangeCount = targetRanges.Count;
            int allRangeCount = allRanges.Count;

            // Calculate expected indices based on target ranges
            // Documents are distributed across ALL ranges with interleaved indices:
            // Range 0: indices 0, 6, 12, 18, ...
            // Range 1: indices 1, 7, 13, 19, ...
            // etc.
            IEnumerable<int> expectedIndices;
            if (testCase.TargetRangeCount.HasValue && testCase.TargetRangeCount.Value < allRangeCount)
            {
                // Calculate expected indices for subset of ranges
                int docsPerRange = testCase.LeafPageCount * testCase.BackendPageSize;
                expectedIndices = Enumerable.Range(0, docsPerRange)
                    .SelectMany(docInRange =>
                        Enumerable.Range(0, testCase.TargetRangeCount.Value)
                            .Select(rangeIndex => rangeIndex + (docInRange * allRangeCount)))
                    .OrderByDescending(x => x);
            }
            else
            {
                // Original calculation for all ranges
                int documentCount = allRangeCount * testCase.LeafPageCount * testCase.BackendPageSize;
                expectedIndices = Enumerable.Range(0, documentCount).Reverse();
            }

            PartitionedFeedMode[] feedModes = new PartitionedFeedMode[]
            {
                PartitionedFeedMode.NonStreamingReversed,
                PartitionedFeedMode.NonStreamingReversed,
            };

            CalculateScores calculateScores = CalculateDefaultScores;

            if (testCase.Weights != null)
            {
                Assert.IsTrue(testCase.Weights.All(x => x >= 0) || testCase.Weights.All(x => x <= 0));

                if (testCase.Weights[0] <= 0)
                {
                    expectedIndices = expectedIndices.Reverse();

                    PartitionedFeedMode feedMode = testCase.SkipOrderByRewrite?
                        PartitionedFeedMode.NonStreamingReversed:
                        PartitionedFeedMode.NonStreaming;

                    feedModes = new PartitionedFeedMode[] { feedMode, feedMode };
                    calculateScores = testCase.SkipOrderByRewrite ? CalculateNegatedScores : CalculateDefaultScores;
                }
            }

            if (testCase.Skip.HasValue)
            {
                expectedIndices = expectedIndices.Skip(testCase.Skip.Value);
            }

            if (testCase.Take.HasValue)
            {
                expectedIndices = expectedIndices.Take(testCase.Take.Value);
            }

            // Create container with ALL ranges (for statistics if Global scope)
            MockDocumentContainer nonStreamingDocumentContainer = MockDocumentContainer.CreateHybridSearchContainer(
                allRanges,
                feedModes,
                leafPageCount: testCase.LeafPageCount,
                backendPageSize: testCase.BackendPageSize,
                returnEmptyGlobalStatistics: testCase.ReturnEmptyGlobalStatistics,
                skipOrderByRewrite: testCase.SkipOrderByRewrite,
                calculateScores: calculateScores);

            (IReadOnlyList<CosmosElement> results, double requestCharge) = await CreateAndRunHybridSearchQueryPipelineStage(
                documentContainer: nonStreamingDocumentContainer,
                targetRanges: targetRanges,
                allRanges: allRanges,
                requiresGlobalStatistics: testCase.RequiresGlobalStatistics,
                pageSize: testCase.PageSize,
                skip: (uint?)testCase.Skip,
                take: (uint?)testCase.Take,
                weights: testCase.Weights,
                skipOrderByRewrite: testCase.SkipOrderByRewrite,
                fullTextScoreScope: testCase.FullTextScoreScope);

            Assert.AreEqual(expectedIndices.Count(), results.Count);

            List<int> actual = new List<int>(results.Count);
            foreach (CosmosElement result in results)
            {
                CosmosObject cosmosObject = result as CosmosObject;
                CosmosNumber cosmosNumber = cosmosObject[Index] as CosmosNumber;
                Assert.IsTrue(cosmosNumber != null && cosmosNumber.Value.IsInteger);
                actual.Add((int)Number64.ToLong(cosmosNumber.Value));
            }

            if (!expectedIndices.SequenceEqual(actual))
            {
                System.Diagnostics.Trace.WriteLine("Mismatch in query results");
                System.Diagnostics.Trace.WriteLine($"Expected: {string.Join(", ", expectedIndices)}");
                System.Diagnostics.Trace.WriteLine($"Actual: {string.Join(", ", actual)}");
                Assert.Fail();
            }

            Assert.AreEqual(nonStreamingDocumentContainer.TotalRequestCharge, requestCharge);

            // Validate statistics query ranges
            if (testCase.RequiresGlobalStatistics)
            {
                IReadOnlyList<FeedRangeEpk> expectedStatisticsRanges =
                    testCase.FullTextScoreScope == FullTextScoreScope.Global
                        ? allRanges
                        : targetRanges;

                Assert.AreEqual(
                    expectedStatisticsRanges.Count,
                    nonStreamingDocumentContainer.StatisticsQueryRanges.Count,
                    $"Statistics query should have targeted {expectedStatisticsRanges.Count} ranges for {testCase.FullTextScoreScope} scope");

                foreach (FeedRangeEpk expectedRange in expectedStatisticsRanges)
                {
                    Assert.IsTrue(
                        nonStreamingDocumentContainer.StatisticsQueryRanges.Any(r => r.Equals(expectedRange)),
                        $"Statistics query should have targeted range {expectedRange}");
                }
            }
        }

        private static Task RunParityTests(
            IDocumentContainer documentContainer,
            IDocumentContainer nonStreamingDocumentContainer,
            IReadOnlyList<TestCase> testCases)
        {
            return RunParityTests(documentContainer, nonStreamingDocumentContainer, testCases, TestOptions.Default);
        }

        private static async Task RunParityTests(
            IDocumentContainer documentContainer,
            IDocumentContainer nonStreamingDocumentContainer,
            IReadOnlyList<TestCase> testCases,
            TestOptions testOptions)
        {
            foreach (TestCase testCase in testCases)
            {
                foreach (int pageSize in testCase.PageSizes)
                {
                    (IReadOnlyList<CosmosElement> streamingResult, double streamingCharge) = await CreateAndRunPipelineStage(
                        documentContainer: documentContainer,
                        ranges: await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default),
                        queryText: testCase.QueryText,
                        orderByColumns: testCase.OrderByColumns,
                        pageSize: pageSize,
                        nonStreamingOrderBy: false);

                    (IReadOnlyList<CosmosElement> nonStreamingResult, double nonStreamingCharge) = await CreateAndRunPipelineStage(
                        documentContainer: nonStreamingDocumentContainer,
                        ranges: await nonStreamingDocumentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default),
                        queryText: testCase.QueryText,
                        orderByColumns: testCase.OrderByColumns,
                        pageSize: pageSize,
                        nonStreamingOrderBy: true,
                        maxConcurrency: testOptions.MaxConcurrency);

                    if (!streamingResult.SequenceEqual(nonStreamingResult))
                    {
                        Assert.Fail($"Results mismatch for query:\n{testCase.QueryText}\npageSize: {pageSize}");
                    }

                    if (!testCase.Validate(nonStreamingResult))
                    {
                        Assert.Fail($"Could not validate result for query:\n{testCase.QueryText}\npageSize: {pageSize}");
                    }

                    if (testOptions.ValidateCharges && (Math.Abs(streamingCharge - nonStreamingCharge) > 0.0001))
                    {
                        Assert.Fail($"Request charge mismatch for query:\n{testCase.QueryText}\npageSize: {pageSize}" +
                            $"\nStreaming request charge: {streamingCharge} NonStreaming request charge: {nonStreamingCharge}");
                    }
                }
            }
        }

        private static Task<(IReadOnlyList<CosmosElement>, double)> CreateAndRunHybridSearchQueryPipelineStage(
            IDocumentContainer documentContainer,
            IReadOnlyList<FeedRangeEpk> targetRanges,
            IReadOnlyList<FeedRangeEpk> allRanges,
            bool requiresGlobalStatistics,
            int pageSize,
            uint? skip,
            uint? take,
            double[] weights,
            bool skipOrderByRewrite,
            FullTextScoreScope fullTextScoreScope)
        {
            HybridSearchQueryInfo hybridSearchQueryInfo = skipOrderByRewrite ?
                Create2ItemHybridSearchSkipOrderByRewriteQueryInfo(requiresGlobalStatistics, skip, take, weights) :
                Create2ItemHybridSearchQueryInfo(requiresGlobalStatistics, skip, take, weights);

            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                documentContainer,
                Create2ItemSqlQuerySpec(),
                targetRanges,
                partitionKey: null,
                queryInfo: null,
                hybridSearchQueryInfo: hybridSearchQueryInfo,
                maxItemCount: pageSize,
                new ContainerQueryProperties(),
                allRanges,
                isContinuationExpected: true,
                maxConcurrency: MaxConcurrency,
                fullTextScoreScope: fullTextScoreScope,
                requestContinuationToken: null,
                statisticsCache: null);

            Assert.IsTrue(tryCreatePipeline.Succeeded);
            return RunPipelineStage(tryCreatePipeline.Result, pageSize);
        }

        private static Task<(IReadOnlyList<CosmosElement>, double)> CreateAndRunPipelineStage(
            IDocumentContainer documentContainer,
            IReadOnlyList<FeedRangeEpk> ranges,
            string queryText,
            IReadOnlyList<OrderByColumn> orderByColumns,
            int pageSize,
            bool nonStreamingOrderBy)
        {
            return CreateAndRunPipelineStage(
                documentContainer,
                ranges,
                queryText,
                orderByColumns,
                pageSize,
                nonStreamingOrderBy,
                MaxConcurrency);
        }

        private static Task<(IReadOnlyList<CosmosElement>, double)> CreateAndRunPipelineStage(
            IDocumentContainer documentContainer,
            IReadOnlyList<FeedRangeEpk> ranges,
            string queryText,
            IReadOnlyList<OrderByColumn> orderByColumns,
            int pageSize,
            bool nonStreamingOrderBy,
            int maxConcurrency)
        {
            TryCatch<IQueryPipelineStage> pipelineStage = OrderByCrossPartitionQueryPipelineStage.MonadicCreate(
                    documentContainer: documentContainer,
                    sqlQuerySpec: new SqlQuerySpec(queryText),
                    targetRanges: ranges,
                    partitionKey: null,
                    orderByColumns: orderByColumns,
                    queryPaginationOptions: new QueryExecutionOptions(pageSizeHint: pageSize),
                    maxConcurrency: maxConcurrency,
                    nonStreamingOrderBy: nonStreamingOrderBy,
                    continuationToken: null,
                    containerQueryProperties: new Cosmos.Query.Core.QueryClient.ContainerQueryProperties(),
                    emitRawOrderByPayload: false);

            Assert.IsTrue(pipelineStage.Succeeded);

            return RunPipelineStage(pipelineStage.Result, pageSize);
        }

        private static async Task<(IReadOnlyList<CosmosElement>, double)> RunPipelineStage(IQueryPipelineStage stage, int pageSize)
        {
            double totalRequestCharge = 0;
            List<CosmosElement> documents = new List<CosmosElement>();
            while (await stage.MoveNextAsync(NoOpTrace.Singleton, default))
            {
                Assert.IsTrue(stage.Current.Succeeded);
                Assert.IsTrue(stage.Current.Result.Documents.Count <= pageSize);
                DebugTraceHelpers.TracePipelineStagePage(stage.Current.Result);
                documents.AddRange(stage.Current.Result.Documents);
                totalRequestCharge += stage.Current.Result.RequestCharge;
            }

            return (documents, totalRequestCharge);
        }

        private static async Task RunParityTests(IReadOnlyList<ParityTestCase> testCases)
        {
            foreach (ParityTestCase testCase in testCases)
            {
                IReadOnlyList<FeedRangeEpk> ranges = new List<FeedRangeEpk>
                {
                    new FeedRangeEpk(new Documents.Routing.Range<string>(string.Empty, "AA", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("BB", "CC", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("CC", "DD", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("DD", "EE", true, false)),
                    new FeedRangeEpk(new Documents.Routing.Range<string>("EE", "FF", true, false)),
                };

                MockDocumentContainer nonStreamingDocumentContainer = MockDocumentContainer.Create(ranges, testCase.FeedMode, testCase.DocumentCreationMode);

                MockDocumentContainer streamingDocumentContainer = MockDocumentContainer.Create(
                    ranges,
                    testCase.FeedMode & PartitionedFeedMode.StreamingReversed,
                    testCase.DocumentCreationMode);

                foreach (int pageSize in testCase.PageSizes)
                {
                    DebugTraceHelpers.TraceNonStreamingPipelineStarting();
                    (IReadOnlyList<CosmosElement> nonStreamingResult, double nonStreamingCharge) = await CreateAndRunPipelineStage(
                        documentContainer: nonStreamingDocumentContainer,
                        ranges: ranges,
                        queryText: testCase.QueryText,
                        orderByColumns: testCase.OrderByColumns,
                        pageSize: pageSize,
                        nonStreamingOrderBy: true);

                    DebugTraceHelpers.TraceStreamingPipelineStarting();
                    (IReadOnlyList<CosmosElement> streamingResult, double streamingCharge) = await CreateAndRunPipelineStage(
                        documentContainer: streamingDocumentContainer,
                        ranges: ranges,
                        queryText: testCase.QueryText,
                        orderByColumns: testCase.OrderByColumns,
                        pageSize: pageSize,
                        nonStreamingOrderBy: false);

                    if (!streamingResult.SequenceEqual(nonStreamingResult))
                    {
                        Assert.Fail($"Results mismatch for query:\n{testCase.QueryText}\npageSize: {pageSize}");
                    }

                    if (Math.Abs(streamingCharge - nonStreamingCharge) > 0.0001)
                    {
                        Assert.Fail($"Request charge mismatch for query:\n{testCase.QueryText}\npageSize: {pageSize}" +
                            $"\nStreaming request charge: {streamingCharge} NonStreaming request charge: {nonStreamingCharge}");
                    }

                    if (Math.Abs(nonStreamingCharge - nonStreamingDocumentContainer.TotalRequestCharge) > 0.0001)
                    {
                        Assert.Fail($"Request charge mismatch for query:\n{testCase.QueryText}\npageSize: {pageSize}" +
                            $"\nExpected: {nonStreamingDocumentContainer.TotalRequestCharge} Actual NonStreaming request charge: {nonStreamingCharge}");
                    }
                }
            }
        }

        private static TestCase MakeTest(string queryText, IReadOnlyList<OrderByColumn> orderByColumns, Func<IReadOnlyList<CosmosElement>, bool> validate)
        {
            return MakeTest(queryText, orderByColumns, PageSizes, validate);
        }

        private static TestCase MakeTest(
            string queryText,
            IReadOnlyList<OrderByColumn> orderByColumns,
            int[] pageSizes,
            Func<IReadOnlyList<CosmosElement>, bool> validate)
        {
            return new TestCase(queryText, orderByColumns, pageSizes, validate);
        }

        private class TestCase
        {
            public string QueryText { get; }

            public IReadOnlyList<OrderByColumn> OrderByColumns { get; }

            public int[] PageSizes { get; }

            public Func<IReadOnlyList<CosmosElement>, bool> Validate { get; }

            public TestCase(
                string queryText,
                IReadOnlyList<OrderByColumn> orderByColumns,
                int[] pageSizes,
                Func<IReadOnlyList<CosmosElement>, bool> validate)
            {
                this.QueryText = queryText;
                this.OrderByColumns = orderByColumns;
                this.PageSizes = pageSizes;
                this.Validate = validate;
            }
        }

        private static HybridSearchTest MakeHybridSearchTest(
            int leafPageCount,
            int backendPageSize,
            bool requiresGlobalStatistics,
            int? skip,
            int? take,
            int pageSize,
            bool returnEmptyGlobalStatistics = false,
            bool skipOrderByRewrite = false,
            FullTextScoreScope fullTextScoreScope = FullTextScoreScope.Global,
            int? targetRangeCount = null)
        {
            return new HybridSearchTest(
                leafPageCount,
                backendPageSize,
                requiresGlobalStatistics,
                skip,
                take,
                weights: null,
                pageSize,
                returnEmptyGlobalStatistics,
                skipOrderByRewrite,
                fullTextScoreScope,
                targetRangeCount);
        }

        private static HybridSearchTest MakeHybridSearchSkipOrderByRewriteTest(
            int leafPageCount,
            int backendPageSize,
            bool requiresGlobalStatistics,
            int? skip,
            int? take,
            int pageSize,
            bool returnEmptyGlobalStatistics = false,
            FullTextScoreScope fullTextScoreScope = FullTextScoreScope.Global,
            int? targetRangeCount = null)
        {
            return new HybridSearchTest(
                leafPageCount,
                backendPageSize,
                requiresGlobalStatistics,
                skip,
                take,
                weights: null,
                pageSize,
                returnEmptyGlobalStatistics,
                skipOrderByRewrite: true,
                fullTextScoreScope,
                targetRangeCount);
        }

        private static HybridSearchTest MakeHybridSearchTest(
            int leafPageCount,
            int backendPageSize,
            bool requiresGlobalStatistics,
            int? skip,
            int? take,
            double[] weights,
            int pageSize,
            bool returnEmptyGlobalStatistics = false,
            bool skipOrderByRewrite = false,
            FullTextScoreScope fullTextScoreScope = FullTextScoreScope.Global,
            int? targetRangeCount = null)
        {
            return new HybridSearchTest(
                leafPageCount,
                backendPageSize,
                requiresGlobalStatistics,
                skip,
                take,
                weights,
                pageSize,
                returnEmptyGlobalStatistics,
                skipOrderByRewrite,
                fullTextScoreScope,
                targetRangeCount);
        }

        private static HybridSearchTest MakeHybridSearchSkipOrderByRewriteTest(
            int leafPageCount,
            int backendPageSize,
            bool requiresGlobalStatistics,
            int? skip,
            int? take,
            double[] weights,
            int pageSize,
            bool returnEmptyGlobalStatistics = false,
            FullTextScoreScope fullTextScoreScope = FullTextScoreScope.Global,
            int? targetRangeCount = null)
        {
            return new HybridSearchTest(
                leafPageCount,
                backendPageSize,
                requiresGlobalStatistics,
                skip,
                take,
                weights,
                pageSize,
                returnEmptyGlobalStatistics,
                skipOrderByRewrite: true,
                fullTextScoreScope,
                targetRangeCount);
        }

        private class HybridSearchTest
        {
            public int LeafPageCount { get; }

            public int BackendPageSize { get; }

            public bool RequiresGlobalStatistics { get; }

            public int? Skip { get; }

            public int? Take { get; }

            public double[] Weights { get; }

            public int PageSize { get; }

            public bool ReturnEmptyGlobalStatistics { get; }

            public bool SkipOrderByRewrite { get; }

            public FullTextScoreScope FullTextScoreScope { get; }

            // Number of ranges to use as target ranges (subset of all ranges)
            // null means use all ranges (current behavior)
            public int? TargetRangeCount { get; }

            public HybridSearchTest(
                int leafPageCount,
                int backendPageSize,
                bool requiresGlobalStatistics,
                int? skip,
                int? take,
                double[] weights,
                int pageSize,
                bool returnEmptyGlobalStatistics,
                bool skipOrderByRewrite,
                FullTextScoreScope fullTextScoreScope = FullTextScoreScope.Global,
                int? targetRangeCount = null)
            {
                this.LeafPageCount = leafPageCount;
                this.BackendPageSize = backendPageSize;
                this.RequiresGlobalStatistics = requiresGlobalStatistics;
                this.Skip = skip;
                this.Take = take;
                this.Weights = weights;
                this.PageSize = pageSize;
                this.ReturnEmptyGlobalStatistics = returnEmptyGlobalStatistics;
                this.SkipOrderByRewrite = skipOrderByRewrite;
                this.FullTextScoreScope = fullTextScoreScope;
                this.TargetRangeCount = targetRangeCount;
            }
        }

        private static ParityTestCase MakeParityTest(
            PartitionedFeedMode feedMode,
            DocumentCreationMode documentCreationMode,
            string queryText,
            IReadOnlyList<OrderByColumn> orderByColumns,
            Func<IReadOnlyList<CosmosElement>, bool> validate)
        {
            return MakeParityTest(feedMode, documentCreationMode, queryText, orderByColumns, PageSizes, validate);
        }

        private static ParityTestCase MakeParityTest(
            PartitionedFeedMode feedMode,
            DocumentCreationMode documentCreationMode,
            string queryText,
            IReadOnlyList<OrderByColumn> orderByColumns,
            int[] pageSizes,
            Func<IReadOnlyList<CosmosElement>, bool> validate)
        {
            return new ParityTestCase(feedMode, documentCreationMode, queryText, orderByColumns, pageSizes, validate);
        }

        private sealed class ParityTestCase : TestCase
        {
            public PartitionedFeedMode FeedMode { get; }

            public DocumentCreationMode DocumentCreationMode { get; }

            public ParityTestCase(
                PartitionedFeedMode feedMode,
                DocumentCreationMode documentCreationMode,
                string queryText,
                IReadOnlyList<OrderByColumn> orderByColumns,
                int[] pageSizes,
                Func<IReadOnlyList<CosmosElement>, bool> validate)
                : base(queryText, orderByColumns, pageSizes, validate)
            {
                this.FeedMode = feedMode;
                this.DocumentCreationMode = documentCreationMode;
            }
        }

        private static class Validate
        {
            public static bool IndexIsInOrder(IReadOnlyList<CosmosElement> documents, string propertyName, int count, bool reversed)
            {
                List<int> expected = Enumerable
                    .Range(0, count)
                    .ToList();

                if (reversed)
                {
                    expected.Reverse();
                }

                IEnumerable<int> actual = documents
                    .Cast<CosmosObject>()
                    .Select(x => x[propertyName])
                    .Cast<CosmosNumber64>()
                    .Select(x => (int)Number64.ToLong(x.Value));

                return expected.SequenceEqual(actual);
            }
        }

        private sealed class NonStreamingDocumentContainer : IDocumentContainer
        {
            private readonly Random random = new Random();

            private readonly IDocumentContainer inner;

            private readonly bool allowSplits;

            public NonStreamingDocumentContainer(IDocumentContainer inner, bool allowSplits)
            {
                this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
                this.allowSplits = allowSplits;
            }

            public Task<ChangeFeedPage> ChangeFeedAsync(
                FeedRangeState<ChangeFeedState> feedRangeState,
                ChangeFeedExecutionOptions changeFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.ChangeFeedAsync(feedRangeState, changeFeedPaginationOptions, trace, cancellationToken);
            }

            public Task<Record> CreateItemAsync(CosmosObject payload, CancellationToken cancellationToken)
            {
                return this.inner.CreateItemAsync(payload, cancellationToken);
            }

            public Task<List<FeedRangeEpk>> GetChildRangeAsync(
                FeedRangeInternal feedRange,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.GetChildRangeAsync(feedRange, trace, cancellationToken);
            }

            public Task<List<FeedRangeEpk>> GetFeedRangesAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.GetFeedRangesAsync(trace, cancellationToken);
            }

            public Task<string> GetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.GetResourceIdentifierAsync(trace, cancellationToken);
            }

            public Task MergeAsync(FeedRangeInternal feedRange1, FeedRangeInternal feedRange2, CancellationToken cancellationToken)
            {
                return this.inner.MergeAsync(feedRange1, feedRange2, cancellationToken);
            }

            public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(
                FeedRangeState<ChangeFeedState> feedRangeState,
                ChangeFeedExecutionOptions changeFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.MonadicChangeFeedAsync(feedRangeState, changeFeedPaginationOptions, trace, cancellationToken);
            }

            public Task<TryCatch<Record>> MonadicCreateItemAsync(CosmosObject payload, CancellationToken cancellationToken)
            {
                return this.inner.MonadicCreateItemAsync(payload, cancellationToken);
            }

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(
                FeedRangeInternal feedRange,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.MonadicGetChildRangeAsync(feedRange, trace, cancellationToken);
            }

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.MonadicGetFeedRangesAsync(trace, cancellationToken);
            }

            public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.MonadicGetResourceIdentifierAsync(trace, cancellationToken);
            }

            public Task<TryCatch> MonadicMergeAsync(
                FeedRangeInternal feedRange1,
                FeedRangeInternal feedRange2,
                CancellationToken cancellationToken)
            {
                return this.inner.MonadicMergeAsync(feedRange1, feedRange2, cancellationToken);
            }

            public async Task<TryCatch<QueryPage>> MonadicQueryAsync(
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                QueryExecutionOptions queryPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                await this.SplitMergeAsync();

                TryCatch<QueryPage> queryPage = await this.inner.MonadicQueryAsync(sqlQuerySpec, feedRangeState, queryPaginationOptions, trace, cancellationToken);

                if (queryPage.Failed)
                {
                    return queryPage;
                }

                QueryPage page = queryPage.Result;
                DebugTraceHelpers.TraceBackendResponse(feedRangeState.FeedRange, page);

                return TryCatch<QueryPage>.FromResult(new QueryPage(
                    page.Documents,
                    page.RequestCharge,
                    page.ActivityId,
                    page.CosmosQueryExecutionInfo,
                    page.DistributionPlanSpec,
                    page.DisallowContinuationTokenMessage,
                    page.AdditionalHeaders,
                    page.State,
                    streaming: false));
            }

            public Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(
                FeedRangeState<ReadFeedState> feedRangeState,
                ReadFeedExecutionOptions readFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.MonadicReadFeedAsync(feedRangeState, readFeedPaginationOptions, trace, cancellationToken);
            }

            public Task<TryCatch<Record>> MonadicReadItemAsync(
                CosmosElement partitionKey,
                string identifer,
                CancellationToken cancellationToken)
            {
                return this.inner.MonadicReadItemAsync(partitionKey, identifer, cancellationToken);
            }

            public Task<TryCatch> MonadicRefreshProviderAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.MonadicRefreshProviderAsync(trace, cancellationToken);
            }

            public Task<TryCatch> MonadicSplitAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
            {
                return this.inner.MonadicSplitAsync(feedRange, cancellationToken);
            }

            public async Task<QueryPage> QueryAsync(
                SqlQuerySpec sqlQuerySpec,
                FeedRangeState<QueryState> feedRangeState,
                QueryExecutionOptions queryPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                TryCatch<QueryPage> queryPage = await this.MonadicQueryAsync(
                    sqlQuerySpec,
                    feedRangeState,
                    queryPaginationOptions,
                    trace,
                    cancellationToken);
                queryPage.ThrowIfFailed();
                return queryPage.Result;
            }

            public Task<ReadFeedPage> ReadFeedAsync(
                FeedRangeState<ReadFeedState> feedRangeState,
                ReadFeedExecutionOptions readFeedPaginationOptions,
                ITrace trace,
                CancellationToken cancellationToken)
            {
                return this.inner.ReadFeedAsync(feedRangeState, readFeedPaginationOptions, trace, cancellationToken);
            }

            public Task<Record> ReadItemAsync(CosmosElement partitionKey, string identifier, CancellationToken cancellationToken)
            {
                return this.inner.ReadItemAsync(partitionKey, identifier, cancellationToken);
            }

            public Task RefreshProviderAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.inner.RefreshProviderAsync(trace, cancellationToken);
            }

            public Task SplitAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
            {
                return this.inner.SplitAsync(feedRange, cancellationToken);
            }

            private async Task SplitMergeAsync()
            {
                if (!this.allowSplits)
                {
                    return;
                }

                if (this.random.Next() % 2 == 0)
                {
                    // Split
                    await this.inner.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                    List<FeedRangeEpk> ranges = await this.inner.GetFeedRangesAsync(
                        trace: NoOpTrace.Singleton,
                        cancellationToken: default);
                    FeedRangeInternal randomRangeToSplit = ranges[this.random.Next(0, ranges.Count)];
                    await this.inner.SplitAsync(randomRangeToSplit, cancellationToken: default);

                    DebugTraceHelpers.TraceSplit(randomRangeToSplit);
                }
            }
        }

        private static class DebugTraceHelpers
        {
#pragma warning disable CS0162, CS0649 // Unreachable code detected
            private static readonly bool Enabled;

            [Conditional("DEBUG")]
            public static void TraceSplit(FeedRangeInternal feedRange)
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine($"Split range: {feedRange.ToJsonString()}");
                }
            }

            [Conditional("DEBUG")]
            public static void TraceNonStreamingPipelineStarting()
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine("\nStarting non streaming pipeline\n");
                }
            }

            [Conditional("DEBUG")]
            public static void TraceStreamingPipelineStarting()
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine("\nStarting streaming pipeline\n");
                }
            }

            [Conditional("DEBUG")]
            public static void TracePipelineStagePage(QueryPage page)
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine("\nReceived next page from pipeline: ");
                    TracePage(page);
                }
            }

            [Conditional("DEBUG")]
            public static void TraceBackendResponse(FeedRangeInternal feedRange, QueryPage page)
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine($"Serving query from backend: {feedRange}");
                    TracePage(page);
                }
            }

            [Conditional("DEBUG")]
            public static void TracePage(QueryPage page)
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine("Page:");
                    System.Diagnostics.Trace.WriteLine($"    State: {page.State?.Value}");
                    System.Diagnostics.Trace.WriteLine($"    ActivityId: {page.ActivityId}");
                    System.Diagnostics.Trace.WriteLine($"    RequestCharge: {page.RequestCharge}");
                    System.Diagnostics.Trace.WriteLine($"    ActivityId: {page.ActivityId}");

                    System.Diagnostics.Trace.WriteLine($"    AdditionalHeaders: ");
                    foreach (KeyValuePair<string, string> header in page.AdditionalHeaders)
                    {
                        System.Diagnostics.Trace.WriteLine($"        [{header.Key}] = {header.Value}");
                    }

                    System.Diagnostics.Trace.WriteLine($"    Results:");
                    foreach (CosmosElement result in page.Documents)
                    {
                        System.Diagnostics.Trace.WriteLine($"        {result}");
                    }
                }
            }
#pragma warning restore CS0162 // Unreachable code detected
        }

        private class MockDocumentContainer : IDocumentContainer
        {
            private readonly IReadOnlyList<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>> pages;

            private readonly bool streaming;

            private readonly Func<SqlQuerySpec, int> componentSelector;

            private readonly Func<SqlQuerySpec, bool> isGlobalStatisticsQuery;

            private readonly double totalRequestCharge;

            private readonly bool returnEmptyGlobalStatistics;

            private readonly List<FeedRange> statisticsQueryRanges = new List<FeedRange>();

            private int statisticsQueryCount;

            private int queryCount;

            public IReadOnlyList<FeedRange> StatisticsQueryRanges => this.statisticsQueryRanges;

            public double TotalRequestCharge
            {
                get
                {
                    if (this.totalRequestCharge > 0)
                    {
                        return this.totalRequestCharge;
                    }
                    else
                    {
                        int queryCount = Interlocked.CompareExchange(ref this.queryCount, 0, 0);
                        int statisticsQueryCount = Interlocked.CompareExchange(ref this.statisticsQueryCount, 0, 0);
                        double requestCharge = (queryCount * QueryCharge) + (statisticsQueryCount * GlobalStatisticsQueryCharge);
                        return requestCharge;
                    }
                }
            }

            public static MockDocumentContainer CreateHybridSearchContainer(
                IReadOnlyList<FeedRangeEpk> feedRanges,
                PartitionedFeedMode[] feedModes,
                int leafPageCount,
                int backendPageSize,
                bool returnEmptyGlobalStatistics,
                bool skipOrderByRewrite,
                CalculateScores calculateScores = null)
            {
                Assert.IsTrue(feedModes.All(x => x.HasFlag(PartitionedFeedMode.NonStreaming)) || feedModes.All(x => !x.HasFlag(PartitionedFeedMode.NonStreaming)));

                IReadOnlyList<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>> pages = CreateHybridSearchPartitionedFeed(
                    feedRanges,
                    feedModes,
                    leafPageCount,
                    backendPageSize,
                    skipOrderByRewrite,
                    calculateScores ?? CalculateDefaultScores);

                return new MockDocumentContainer(
                    pages,
                    streaming: !feedModes[0].HasFlag(PartitionedFeedMode.NonStreaming),
                    componentSelector: GetOrderByScoreKind,
                    isGlobalStatisticsQuery: IsGlobalStatisticsQuery,
                    totalRequestCharge: 0,
                    returnEmptyGlobalStatistics: returnEmptyGlobalStatistics);
            }

            public static MockDocumentContainer Create(IReadOnlyList<FeedRangeEpk> feedRanges, PartitionedFeedMode feedMode, DocumentCreationMode documentCreationMode)
            {
                IReadOnlyList<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>> pages = CreatePartitionedFeed(
                    feedRanges,
                    LeafPageCount,
                    PageSize,
                    feedMode,
                    (index) => CreateDocument(index, documentCreationMode));
                double totalRequestCharge = feedRanges.Count * LeafPageCount * QueryCharge;

                return new MockDocumentContainer(
                    pages,
                    streaming: !feedMode.HasFlag(PartitionedFeedMode.NonStreaming),
                    componentSelector: _ => 0,
                    isGlobalStatisticsQuery: _ => false,
                    totalRequestCharge,
                    returnEmptyGlobalStatistics: false);
            }

            private MockDocumentContainer(
                IReadOnlyList<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>> pages,
                bool streaming,
                Func<SqlQuerySpec, int> componentSelector,
                Func<SqlQuerySpec, bool> isGlobalStatisticsQuery,
                double totalRequestCharge,
                bool returnEmptyGlobalStatistics)
            {
                this.pages = pages ?? throw new ArgumentNullException(nameof(pages));
                this.streaming = streaming;
                this.componentSelector = componentSelector;
                this.isGlobalStatisticsQuery = isGlobalStatisticsQuery;
                this.totalRequestCharge = totalRequestCharge;
                this.returnEmptyGlobalStatistics = returnEmptyGlobalStatistics;
            }

            public Task<ChangeFeedPage> ChangeFeedAsync(FeedRangeState<ChangeFeedState> feedRangeState, ChangeFeedExecutionOptions changeFeedPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<Record> CreateItemAsync(CosmosObject payload, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<List<FeedRangeEpk>> GetChildRangeAsync(FeedRangeInternal feedRange, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<List<FeedRangeEpk>> GetFeedRangesAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return Task.FromResult(this.pages[0].Keys.Cast<FeedRangeEpk>().ToList());
            }

            public Task<string> GetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task MergeAsync(FeedRangeInternal feedRange1, FeedRangeInternal feedRange2, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(FeedRangeState<ChangeFeedState> feedRangeState, ChangeFeedExecutionOptions changeFeedPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<Record>> MonadicCreateItemAsync(CosmosObject payload, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(FeedRangeInternal feedRange, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return Task.FromResult(TryCatch<List<FeedRangeEpk>>.FromResult(this.pages[0].Keys.Cast<FeedRangeEpk>().ToList()));
            }

            public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch> MonadicMergeAsync(FeedRangeInternal feedRange1, FeedRangeInternal feedRange2, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<QueryPage>> MonadicQueryAsync(SqlQuerySpec sqlQuerySpec, FeedRangeState<QueryState> feedRangeState, QueryExecutionOptions queryPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                if (this.isGlobalStatisticsQuery(sqlQuerySpec))
                {
                    lock (this.statisticsQueryRanges)
                    {
                        this.statisticsQueryRanges.Add(feedRangeState.FeedRange);
                    }

                    QueryPage globalStatisticsPage = new QueryPage(
                        documents: new List<CosmosElement> { this.returnEmptyGlobalStatistics ? CreateEmptyHybridSearchGlobalStatistics() : CreateHybridSearchGlobalStatistics() },
                        requestCharge: GlobalStatisticsQueryCharge,
                        activityId: ActivityId,
                        cosmosQueryExecutionInfo: null,
                        distributionPlanSpec: null,
                        disallowContinuationTokenMessage: null,
                        additionalHeaders: null,
                        state: null,
                        streaming: false);

                    Interlocked.Increment(ref this.statisticsQueryCount);
                    return Task.FromResult(TryCatch<QueryPage>.FromResult(globalStatisticsPage));
                }

                int componentIndex = this.componentSelector(sqlQuerySpec);
                IReadOnlyList<IReadOnlyList<CosmosElement>> feedRangePages = this.pages[componentIndex][feedRangeState.FeedRange];
                int index = feedRangeState.State == null ? 0 : int.Parse(((CosmosString)feedRangeState.State.Value).Value);
                IReadOnlyList<CosmosElement> documents = index < feedRangePages.Count ? feedRangePages[index] : Enumerable.Empty<CosmosElement>().ToList();

                QueryState state = index < feedRangePages.Count - 1 ? new QueryState(CosmosString.Create((index + 1).ToString())) : null;
                QueryPage queryPage = new QueryPage(
                    documents: documents,
                    requestCharge: QueryCharge,
                    activityId: ActivityId,
                    cosmosQueryExecutionInfo: null,
                    distributionPlanSpec: null,
                    disallowContinuationTokenMessage: null,
                    additionalHeaders: null,
                    state: state,
                    streaming: this.streaming);

                DebugTraceHelpers.TraceBackendResponse(feedRangeState.FeedRange, queryPage);
                Interlocked.Increment(ref this.queryCount);

                return Task.FromResult(TryCatch<QueryPage>.FromResult(queryPage));
            }

            public Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(FeedRangeState<ReadFeedState> feedRangeState, ReadFeedExecutionOptions readFeedPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch<Record>> MonadicReadItemAsync(CosmosElement partitionKey, string identifer, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch> MonadicRefreshProviderAsync(ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<TryCatch> MonadicSplitAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public async Task<QueryPage> QueryAsync(SqlQuerySpec sqlQuerySpec, FeedRangeState<QueryState> feedRangeState, QueryExecutionOptions queryPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                TryCatch<QueryPage> queryPage = await this.MonadicQueryAsync(sqlQuerySpec, feedRangeState, queryPaginationOptions, trace, cancellationToken);
                return queryPage.Result;
            }

            public Task<ReadFeedPage> ReadFeedAsync(FeedRangeState<ReadFeedState> feedRangeState, ReadFeedExecutionOptions readFeedPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<Record> ReadItemAsync(CosmosElement partitionKey, string identifier, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task RefreshProviderAsync(ITrace trace, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task SplitAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        [Flags]
        enum PartitionedFeedMode
        {
            Streaming = 0,
            NonStreaming = 1,
            Reversed = 2,

            StreamingReversed = Streaming | Reversed,
            NonStreamingReversed = NonStreaming | Reversed,
        }

        private static int GetOrderByScoreKind(SqlQuerySpec sqlQuerySpec)
        {
            string queryText = sqlQuerySpec.QueryText;
            if (queryText.Contains(@"ORDER BY _FullTextScore(c.text"))
            {
                return 0;
            }
            else if (queryText.Contains(@"ORDER BY _FullTextScore(c.abstract"))
            {
                return 1;
            }
            else if (queryText.Contains(@"ORDER BY _FullTextScore(c.image"))
            {
                return 2;
            }
            else
            {
                throw new ArgumentException("Unknown query text");
            }
        }

        private static bool IsGlobalStatisticsQuery(SqlQuerySpec sqlQuerySpec)
        {
            return sqlQuerySpec.QueryText.Contains("COUNT(1) AS documentCount") && sqlQuerySpec.QueryText.Contains("] AS fullTextStatistics");
        }

        private static IReadOnlyList<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>> CreatePartitionedFeed(
            IReadOnlyList<FeedRangeEpk> feedRanges,
            int leafPageCount,
            int pageSize,
            PartitionedFeedMode mode,
            Func<int, CosmosElement> createDocument)
        {
            IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>> pages = CreatePartitionedFeed(
                feedRanges,
                leafPageCount,
                pageSize,
                mode,
                componentIndex: 0,
                (_, index) => createDocument(index));

            return new List<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>>
            {
                pages
            };
        }

        private static IReadOnlyList<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>> CreateHybridSearchPartitionedFeed(
            IReadOnlyList<FeedRangeEpk> feedRanges,
            PartitionedFeedMode[] feedModes,
            int leafPageCount,
            int pageSize,
            bool skipOrderByRewrite,
            CalculateScores calculateScores)
        {
            int componentCount = feedModes.Length;
            List<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>> componentPages = new List<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>>(componentCount);
            for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
            {
                IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>> pages = CreatePartitionedFeed(
                    feedRanges,
                    leafPageCount,
                    pageSize,
                    feedModes[componentIndex],
                    componentIndex,
                    (componentIndex, index) => CreateHybridSearchDocument(
                        componentCount: componentCount,
                        index: index,
                        componentIndex: componentIndex,
                        skipOrderByRewrite: skipOrderByRewrite,
                        calculateScores: calculateScores));

                componentPages.Add(pages);
            }

            return componentPages;
        }

        private static IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>> CreatePartitionedFeed(
            IReadOnlyList<FeedRangeEpk> feedRanges,
            int leafPageCount,
            int pageSize,
            PartitionedFeedMode mode,
            int componentIndex,
            Func<int, int, CosmosElement> createDocument)
        {
            int feedRangeIndex = 0;
            Dictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>> pages = new Dictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>();
            foreach (FeedRangeEpk feedRange in feedRanges)
            {
                int index = feedRangeIndex;
                List<IReadOnlyList<CosmosElement>> leafPages = new List<IReadOnlyList<CosmosElement>>(leafPageCount);
                for (int pageIndex = 0; pageIndex < leafPageCount; ++pageIndex)
                {
                    List<CosmosElement> documents = new List<CosmosElement>(pageSize);
                    for (int documentCount = 0; documentCount < pageSize; ++documentCount)
                    {
                        documents.Add(createDocument(componentIndex, index));
                        index += feedRanges.Count;
                    }

                    if (mode.HasFlag(PartitionedFeedMode.Reversed))
                    {
                        documents.Reverse();
                    }

                    leafPages.Add(documents);
                }

                if (mode.HasFlag(PartitionedFeedMode.NonStreaming))
                {
                    FischerYatesShuffle(leafPages);
                }

                if (mode == PartitionedFeedMode.StreamingReversed)
                {
                    leafPages.Reverse();
                }

                pages.Add(feedRange, leafPages);
                ++feedRangeIndex;
            }

            return pages;
        }

        [Flags]
        enum DocumentCreationMode
        {
            SingleItem = 0,
            MultiItem = 1,
            Swapped = 2,

            MultiItemSwapped = MultiItem | Swapped,
        }

        private static CosmosElement CreateEmptyHybridSearchGlobalStatistics()
        {
            List<CosmosElement> statistics = new List<CosmosElement>
            {
                CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    [TotalWordCount] = CosmosNumber64.Create(0),
                    [HitCounts] = CosmosArray.Create(new List<CosmosElement>
                    {
                        CosmosNumber64.Create(0),
                        CosmosNumber64.Create(0),
                    }),
                }),
                CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    [TotalWordCount] = CosmosNumber64.Create(0),
                    [HitCounts] = CosmosArray.Create(new List<CosmosElement>
                    {
                        CosmosNumber64.Create(0),
                    }),
                }),
            };

            CosmosObject globalStatistics = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                [DocumentCountPropertyName] = CosmosNumber64.Create(0),
                [FullTextStatistics] = CosmosArray.Create(statistics),
            });

            return globalStatistics;
        }

        private static CosmosElement CreateHybridSearchGlobalStatistics()
        {
            List<CosmosElement> statistics = new List<CosmosElement>
            {
                CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    [TotalWordCount] = CosmosNumber64.Create(10124),
                    [HitCounts] = CosmosArray.Create(new List<CosmosElement>
                    {
                        CosmosNumber64.Create(100),
                        CosmosNumber64.Create(200),
                    }),
                }),
                CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    [TotalWordCount] = CosmosNumber64.Create(1024),
                    [HitCounts] = CosmosArray.Create(new List<CosmosElement>
                    {
                        CosmosNumber64.Create(300),
                    }),
                }),
            };

            CosmosObject globalStatistics = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                [DocumentCountPropertyName] = CosmosNumber64.Create(DocumentCount),
                [FullTextStatistics] = CosmosArray.Create(statistics),
            });

            return globalStatistics;
        }

        private static CosmosElement CreateHybridSearchDocument(
            int componentCount,
            int index,
            int componentIndex,
            bool skipOrderByRewrite,
            CalculateScores calculateScores)
        {
            CosmosElement indexElement = CosmosNumber64.Create(index);
            CosmosElement indexStringElement = CosmosString.Create(index.ToString("D4"));
            IReadOnlyList<double> scores = calculateScores(componentCount, index);
            Assert.AreEqual(componentCount, scores.Count);

            List<CosmosElement> orderByItems = new List<CosmosElement>
            {
                CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    [Item] = CosmosNumber64.Create(scores[componentIndex])
                })
            };

            Dictionary<string, CosmosElement> payload = new Dictionary<string, CosmosElement>
            {
                [Payload] = CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    [Text] = indexStringElement,
                    [Index] = indexElement,
                }),
                [ComponentScores] = CosmosArray.Create(scores.Select(score => CosmosNumber64.Create(score))),
            };

            Documents.ResourceId resourceId = Documents.ResourceId.NewCollectionChildResourceId(
                CollectionRid,
                (ulong)index,
                Documents.ResourceType.Document);

            if (skipOrderByRewrite)
            {
                payload.Add(RId, CosmosString.Create(resourceId.ToString()));
            }
            else
            {
                payload = new Dictionary<string, CosmosElement>
                {
                    [RId] = CosmosString.Create(resourceId.ToString()),
                    [OrderByItems] = CosmosArray.Create(orderByItems),
                    [Payload] = CosmosObject.Create(payload)
                };
            }

            CosmosElement document = CosmosObject.Create(payload);
            return document;
        }

        private static IReadOnlyList<double> CalculateDefaultScores(int componentCount, int documentIndex)
        {
            double delta = 0.1;
            return Enumerable.Range(0, componentCount)
                .Select(componentIndex => documentIndex + ((componentIndex + 1) * delta))
                .ToArray();
        }

        private static IReadOnlyList<double> CalculateNegatedScores(int componentCount, int documentIndex)
        {
            return CalculateDefaultScores(componentCount, documentIndex)
                .Select(score => -score)
                .ToArray();
        }

        private static CosmosElement CreateDocument(int index, DocumentCreationMode mode)
        {
            CosmosElement indexElement = CosmosNumber64.Create(index);
            CosmosElement indexStringElement = CosmosString.Create(index.ToString("D4"));

            List<CosmosElement> orderByItems = new List<CosmosElement>
            {
                CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    [Item] = indexElement
                })
            };

            if (mode.HasFlag(DocumentCreationMode.MultiItem))
            {
                orderByItems.Add(CosmosObject.Create(new Dictionary<string, CosmosElement>
                {
                    [Item] = indexStringElement
                }));
            }

            if (mode.HasFlag(DocumentCreationMode.Swapped))
            {
                orderByItems.Reverse();
            }

            Dictionary<string, CosmosElement> payload = new Dictionary<string, CosmosElement>
            {
                [Index] = indexElement
            };

            if (mode.HasFlag(DocumentCreationMode.MultiItem))
            {
                payload.Add(IndexString, indexStringElement);
            }

            Documents.ResourceId resourceId = Documents.ResourceId.NewCollectionChildResourceId(
                CollectionRid,
                (ulong)index,
                Documents.ResourceType.Document);

            CosmosElement document = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                [RId] = CosmosString.Create(resourceId.ToString()),
                [OrderByItems] = CosmosArray.Create(orderByItems),
                [Payload] = CosmosObject.Create(payload)
            });

            return document;
        }

        private static void FischerYatesShuffle<T>(IList<T> list)
        {
            Random random = new Random();
            for (int index = list.Count - 1; index > 0; --index)
            {
                int other = random.Next(index + 1);
                T temp = list[index];
                list[index] = list[other];
                list[other] = temp;
            }
        }

        private static HybridSearchQueryInfo Create2ItemHybridSearchQueryInfo(bool requiresGlobalStatistics, uint? skip, uint? take, double[] weights)
        {
            SortOrder[] sortOrders = new SortOrder[] { SortOrder.Descending, SortOrder.Descending };
            string[] sortOrderText = new string[] { "DESC", "DESC" };
            if (weights != null)
            {
                Assert.AreEqual(2, weights.Length);

                for (int i = 0; i < weights.Length; ++i)
                {
                    sortOrders[i] = weights[i] < 0 ? SortOrder.Ascending : SortOrder.Descending;
                    sortOrderText[i] = weights[i] < 0 ? "ASC" : "DESC";
                    weights[i] = Math.Abs(weights[i]);
                }
            }

            return new HybridSearchQueryInfo
            {
                GlobalStatisticsQuery = @"
                    SELECT 
                        COUNT(1) AS documentCount,
                        [
                            {
                                totalWordCount: SUM(_FullTextWordCount(c.text)),
                                hitCounts: [
                                    COUNTIF(FullTextContains(c.text, ""swim"")),
                                    COUNTIF(FullTextContains(c.text, ""run""))
                                ]
                            },
                            {
                                totalWordCount: SUM(_FullTextWordCount(c.abstract)),
                                hitCounts: [
                                    COUNTIF(FullTextContains(c.abstract, ""energy""))
                                ]
                            }
                        ] AS fullTextStatistics
                    FROM c",

                ComponentQueryInfos = new List<QueryInfo>
                {
                    new QueryInfo
                    {
                        DistinctType = DistinctQueryType.None,
                        Top = 200,
                        OrderBy = new List<SortOrder>{ sortOrders[0] },
                        OrderByExpressions = new List<string>
                        {
                            "_FullTextScore(c.text, [\"swim\", \"run\"], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})",
                        },
                        HasSelectValue = false,
                        RewrittenQuery = @"
                            SELECT TOP 200 
                                c._rid,
                                [
                                    {
                                        item: _FullTextScore(c.text, [""swim"", ""run""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0})
                                    }
                                ] AS orderByItems,
                                {
                                    payload: {
                                        text: c.text,
                                        abstract: c.abstract
                                    },
                                    componentScores: [
                                        _FullTextScore(c.text, [""swim"", ""run""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}),
                                        _FullTextScore(c.abstract, [""energy""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-1}, {documentdb-formattablehybridsearchquery-hitcountsarray-1})
                                    ]
                                } AS payload
                            FROM c
                            WHERE {documentdb-formattableorderbyquery-filter}
                            ORDER BY _FullTextScore(c.text, [""swim"", ""run""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) " + sortOrderText[0],
                        HasNonStreamingOrderBy = true,
                    },

                    new QueryInfo
                    {
                        DistinctType = DistinctQueryType.None,
                        Top = 200,
                        OrderBy = new List<SortOrder>{ sortOrders[1] },
                        OrderByExpressions = new List<string>
                        {
                            "_FullTextScore(c.abstract, [\"energy\"], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-1}, {documentdb-formattablehybridsearchquery-hitcountsarray-1})",
                        },
                        HasSelectValue = false,
                        RewrittenQuery = @"
                            SELECT TOP 200 
                                c._rid,
                                [
                                    {
                                        item: _FullTextScore(c.abstract, [""energy""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-1}, {documentdb-formattablehybridsearchquery-hitcountsarray-1})
                                    }
                                ] AS orderByItems,
                                {
                                    payload: {
                                        text: c.text,
                                        abstract: c.abstract
                                    },
                                    componentScores: [
                                        _FullTextScore(c.text, [""swim"", ""run""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}),
                                        _FullTextScore(c.abstract, [""energy""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-1}, {documentdb-formattablehybridsearchquery-hitcountsarray-1})
                                    ]
                                } AS payload
                            FROM c
                            WHERE {documentdb-formattableorderbyquery-filter}
                            ORDER BY _FullTextScore(c.abstract, [""energy""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-1}, {documentdb-formattablehybridsearchquery-hitcountsarray-1}) " + sortOrderText[1],
                        HasNonStreamingOrderBy = true,
                    },
                },

                Skip = skip,
                Take = take,
                ComponentWeights = weights?.Select(x => x).ToList(),
                RequiresGlobalStatistics = requiresGlobalStatistics,
            };
        }

        private static HybridSearchQueryInfo Create2ItemHybridSearchSkipOrderByRewriteQueryInfo(bool requiresGlobalStatistics, uint? skip, uint? take, double[] weights)
        {
            if (weights != null)
            {
                Assert.AreEqual(2, weights.Length);

                for (int i = 0; i < weights.Length; ++i)
                {
                    weights[i] = Math.Abs(weights[i]);
                }
            }

            return new HybridSearchQueryInfo
            {
                GlobalStatisticsQuery = @"
                    SELECT 
                        COUNT(1) AS documentCount,
                        [
                            {
                                totalWordCount: SUM(_FullTextWordCount(c.text)),
                                hitCounts: [
                                    COUNTIF(FullTextContains(c.text, ""swim"")),
                                    COUNTIF(FullTextContains(c.text, ""run""))
                                ]
                            },
                            {
                                totalWordCount: SUM(_FullTextWordCount(c.abstract)),
                                hitCounts: [
                                    COUNTIF(FullTextContains(c.abstract, ""energy""))
                                ]
                            }
                        ] AS fullTextStatistics
                    FROM c",

                ComponentQueryInfos = new List<QueryInfo>
                {
                    new QueryInfo
                    {
                        DistinctType = DistinctQueryType.None,
                        HasSelectValue = false,
                        RewrittenQuery = @"
                            SELECT TOP 200 
                                c._rid,
                                {
                                    text: c.text,
                                    abstract: c.abstract
                                } AS payload,
                                [
                                    (_FullTextScore(c.text, [""swim"", ""run""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1),
                                    (_FullTextScore(c.abstract, [""energy""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-1}, {documentdb-formattablehybridsearchquery-hitcountsarray-1}) ?? -1)
                                ] AS componentScores
                            FROM c
                            ORDER BY _FullTextScore(c.text, [""swim"", ""run""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC",
                        HasNonStreamingOrderBy = false,
                    },

                    new QueryInfo
                    {
                        DistinctType = DistinctQueryType.None,
                        HasSelectValue = false,
                        RewrittenQuery = @"
                            SELECT TOP 200 
                                c._rid,
                                {
                                    text: c.text,
                                    abstract: c.abstract
                                } AS payload,
                                [
                                    (_FullTextScore(c.text, [""swim"", ""run""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) ?? -1),
                                    (_FullTextScore(c.abstract, [""energy""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-1}, {documentdb-formattablehybridsearchquery-hitcountsarray-1}) ?? -1)
                                ] AS componentScores
                            FROM c
                            ORDER BY _FullTextScore(c.abstract, [""energy""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-1}, {documentdb-formattablehybridsearchquery-hitcountsarray-1}) DESC",
                        HasNonStreamingOrderBy = false,
                    },
                },

                Skip = skip,
                Take = take,
                ComponentWeights = weights?.ToList(),
                RequiresGlobalStatistics = requiresGlobalStatistics,
            };
        }

        private static SqlQuerySpec Create2ItemSqlQuerySpec(double[] weights = null)
        {
            if (weights != null)
            {
                Assert.AreEqual(2, weights.Length);

                CosmosArray weightsArray = CosmosArray.Create(new List<CosmosElement>
                {
                    CosmosNumber64.Create(weights[0]),
                    CosmosNumber64.Create(weights[1]),
                });

                return new SqlQuerySpec(@$"
                  SELECT TOP 100 c.text, c.abstract
                  FROM c
                  ORDER BY RANK RRF(FullTextScore(c.text, ['swim', 'run']), FullTextScore(c.abstract, ['energy']), {weightsArray})");
            }

            return new SqlQuerySpec(@"
              SELECT TOP 100 c.text, c.abstract
              FROM c
              ORDER BY RANK RRF(FullTextScore(c.text, ['swim', 'run']), FullTextScore(c.abstract, ['energy']))");
        }

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(int documentCount)
        {
            Documents.PartitionKeyDefinition partitionKeyDefinition = new Documents.PartitionKeyDefinition()
            {
                Paths = new System.Collections.ObjectModel.Collection<string>()
                {
                    "/id"
                },
                Kind = Documents.PartitionKind.Hash,
                Version = Documents.PartitionKeyDefinitionVersion.V2,
            };

            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < 3; i++)
            {
                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }
            }

            for (int i = 0; i < documentCount; i++)
            {
                // Insert an item
                CosmosObject item = CosmosObject.Parse($"{{\"id\": {i}, \"repeated\": {i % 5} }}");
                TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(item, cancellationToken: default);
                Assert.IsTrue(monadicCreateRecord.Succeeded);
            }

            return documentContainer;
        }

        private sealed class TestOptions
        {
            public static readonly TestOptions Default = new TestOptions(
                validateCharges: true,
                maxConcurrency: NonStreamingOrderByQueryTests.MaxConcurrency);

            public bool ValidateCharges { get; }

            public int MaxConcurrency { get; }

            public TestOptions(bool validateCharges, int maxConcurrency)
            {
                this.ValidateCharges = validateCharges;
                this.MaxConcurrency = maxConcurrency;
            }
        }
    }
}