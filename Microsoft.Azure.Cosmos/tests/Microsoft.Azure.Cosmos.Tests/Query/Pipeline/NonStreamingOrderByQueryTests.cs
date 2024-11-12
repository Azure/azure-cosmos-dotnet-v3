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
            };

            foreach (HybridSearchTest testCase in testCases)
            {
                await RunHybridSearchTest(testCase);
            }
        }

        private static async Task RunHybridSearchTest(HybridSearchTest testCase)
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

            int feedRangeCount = ranges.Count;
            int documentCount = feedRangeCount * testCase.LeafPageCount * testCase.BackendPageSize;

            IEnumerable<int> expectedIndices = Enumerable
                .Range(0, documentCount)
                .Reverse();

            if (testCase.Skip.HasValue)
            {
                expectedIndices = expectedIndices.Skip(testCase.Skip.Value);
            }

            if (testCase.Take.HasValue)
            {
                expectedIndices = expectedIndices.Take(testCase.Take.Value);
            }

            MockDocumentContainer nonStreamingDocumentContainer = MockDocumentContainer.Create(
                ranges,
                PartitionedFeedMode.NonStreamingReversed,
                componentCount: 2,
                leafPageCount: testCase.LeafPageCount,
                backendPageSize: testCase.BackendPageSize);

            (IReadOnlyList<CosmosElement> results, double requestCharge) = await CreateAndRunHybridSearchQueryPipelineStage(
                documentContainer: nonStreamingDocumentContainer,
                ranges: ranges,
                requiresGlobalStatistics: testCase.RequiresGlobalStatistics,
                pageSize: testCase.PageSize,
                skip: testCase.Skip,
                take: testCase.Take);

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
            IReadOnlyList<FeedRangeEpk> ranges,
            bool requiresGlobalStatistics,
            int pageSize,
            int? skip,
            int? take)
        {
            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                documentContainer,
                Create2ItemSqlQuerySpec(),
                ranges,
                partitionKey: null,
                queryInfo: null,
                Create2ItemHybridSearchQueryInfo(requiresGlobalStatistics, skip, take),
                maxItemCount: pageSize,
                new ContainerQueryProperties(),
                ranges,
                isContinuationExpected: true,
                maxConcurrency: MaxConcurrency,
                requestContinuationToken: null);

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

        private static HybridSearchTest MakeHybridSearchTest(int leafPageCount, int backendPageSize, bool requiresGlobalStatistics, int? skip, int? take, int pageSize)
        {
            return new HybridSearchTest(leafPageCount, backendPageSize, requiresGlobalStatistics, skip, take, pageSize);
        }

        private class HybridSearchTest
        {
            public int LeafPageCount { get; }

            public int BackendPageSize { get; }

            public bool RequiresGlobalStatistics { get; }

            public int? Skip { get; }

            public int? Take { get; }

            public int PageSize { get; }

            public HybridSearchTest(int leafPageCount, int backendPageSize, bool requiresGlobalStatistics, int? skip, int? take, int pageSize)
            {
                this.LeafPageCount = leafPageCount;
                this.BackendPageSize = backendPageSize;
                this.RequiresGlobalStatistics = requiresGlobalStatistics;
                this.Skip = skip;
                this.Take = take;
                this.PageSize = pageSize;
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
                DebugTraceHelpers.TraceBackendResponse(page);

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
            public static void TraceBackendResponse(QueryPage page)
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine("Serving query from backend: ");
                    TracePage(page);
                }
            }

            [Conditional("DEBUG")]
            public static void TracePage(QueryPage page)
            {
                if (Enabled)
                {
                    System.Diagnostics.Trace.WriteLine("Page:");
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

            private int statisticsQueryCount;

            private int queryCount;

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

            public static MockDocumentContainer Create(
                IReadOnlyList<FeedRangeEpk> feedRanges,
                PartitionedFeedMode feedMode,
                int componentCount,
                int leafPageCount,
                int backendPageSize)
            {
                IReadOnlyList<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>> pages = CreateHybridSearchPartitionedFeed(
                    componentCount,
                    feedRanges,
                    feedMode,
                    leafPageCount,
                    backendPageSize);

                return new MockDocumentContainer(
                    pages,
                    streaming: !feedMode.HasFlag(PartitionedFeedMode.NonStreaming),
                    componentSelector: GetOrderByScoreKind,
                    isGlobalStatisticsQuery: IsGlobalStatisticsQuery,
                    totalRequestCharge: 0);
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
                    totalRequestCharge);
            }

            private MockDocumentContainer(
                IReadOnlyList<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>> pages,
                bool streaming,
                Func<SqlQuerySpec, int> componentSelector,
                Func<SqlQuerySpec, bool> isGlobalStatisticsQuery,
                double totalRequestCharge)
            {
                this.pages = pages ?? throw new ArgumentNullException(nameof(pages));
                this.streaming = streaming;
                this.componentSelector = componentSelector;
                this.isGlobalStatisticsQuery = isGlobalStatisticsQuery;
                this.totalRequestCharge = totalRequestCharge;
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
                    QueryPage globalStatisticsPage = new QueryPage(
                        documents: new List<CosmosElement> { CreateHybridSearchGlobalStatistics() },
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
                IReadOnlyList<CosmosElement> documents = feedRangePages[index];

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

                DebugTraceHelpers.TraceBackendResponse(queryPage);
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
            int componentCount,
            IReadOnlyList<FeedRangeEpk> feedRanges,
            PartitionedFeedMode feedMode,
            int leafPageCount,
            int pageSize)
        {
            List<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>> componentPages = new List<IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>>>(componentCount);
            for (int componentIndex = 0; componentIndex < componentCount; ++componentIndex)
            {
                IReadOnlyDictionary<FeedRange, IReadOnlyList<IReadOnlyList<CosmosElement>>> pages = CreatePartitionedFeed(
                    feedRanges,
                    leafPageCount,
                    pageSize,
                    feedMode,
                    componentIndex,
                    (componentIndex, index) => CreateHybridSearchDocument(componentCount, index, componentIndex));

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

        private static CosmosElement CreateHybridSearchDocument(int componentCount, int index, int componentIndex)
        {
            CosmosElement indexElement = CosmosNumber64.Create(index);
            CosmosElement indexStringElement = CosmosString.Create(index.ToString("D4"));
            double[] scores = new double[componentCount];
            double delta = 0.1;
            for (int scoreIndex = 0; scoreIndex < componentCount; ++scoreIndex)
            {
                scores[scoreIndex] = index + ((1 + scoreIndex) * delta);
            }

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

            CosmosElement document = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                [RId] = CosmosString.Create(resourceId.ToString()),
                [OrderByItems] = CosmosArray.Create(orderByItems),
                [Payload] = CosmosObject.Create(payload)
            });

            return document;
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

        private static HybridSearchQueryInfo Create2ItemHybridSearchQueryInfo(bool requiresGlobalStatistics, int? skip, int? take)
        {
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
                        OrderBy = new List<SortOrder>{ SortOrder.Descending },
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
                            ORDER BY _FullTextScore(c.text, [""swim"", ""run""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-0}, {documentdb-formattablehybridsearchquery-hitcountsarray-0}) DESC",
                        HasNonStreamingOrderBy = true,
                    },

                    new QueryInfo
                    {
                        DistinctType = DistinctQueryType.None,
                        Top = 200,
                        OrderBy = new List<SortOrder>{ SortOrder.Descending },
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
                            ORDER BY _FullTextScore(c.abstract, [""energy""], {documentdb-formattablehybridsearchquery-totaldocumentcount}, {documentdb-formattablehybridsearchquery-totalwordcount-1}, {documentdb-formattablehybridsearchquery-hitcountsarray-1}) DESC",
                        HasNonStreamingOrderBy = true,
                    },
                },

                Skip = skip,
                Take = take,
                RequiresGlobalStatistics = requiresGlobalStatistics,
            };
        }

        private static SqlQuerySpec Create2ItemSqlQuerySpec()
        {
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