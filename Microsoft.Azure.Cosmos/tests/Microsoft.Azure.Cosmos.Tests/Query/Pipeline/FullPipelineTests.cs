//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Pagination;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.ReadFeed.Pagination;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class FullPipelineTests
    {
        internal static readonly PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
        {
            Paths = new Collection<string>()
            {
                "/pk"
            },
            Kind = PartitionKind.Hash,
            Version = PartitionKeyDefinitionVersion.V2,
        };

        [TestMethod]
        public async Task TestMerge()
        {
            List<CosmosObject> documents = Enumerable
                .Range(0, 100)
                .Select(x => CosmosObject.Parse($"{{\"pk\" : {x} }}"))
                .ToList();

            MergeTestUtil mergeTest = new MergeTestUtil();
            mergeTest.DocumentContainer = await CreateDocumentContainerAsync(
                documents: documents,
                numSplits: 2,
                failureConfigs: new FlakyDocumentContainer.FailureConfigs(
                    inject429s: false,
                    injectEmptyPages: false,
                    shouldReturnFailure: mergeTest.ShouldReturnFailure));

            string query = "SELECT * FROM c ORDER BY c._ts";
            int pageSize = 10;
            IQueryPipelineStage pipelineStage = await CreatePipelineAsync(mergeTest.DocumentContainer, query, pageSize);

            List<CosmosElement> elements = new List<CosmosElement>();
            int iteration = 0;
            while (await pipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                TryCatch<QueryPage> tryGetQueryPage = pipelineStage.Current;
                tryGetQueryPage.ThrowIfFailed();

                elements.AddRange(tryGetQueryPage.Result.Documents);
                ++iteration;

                if (iteration == 1)
                {
                    mergeTest.ShouldMerge = MergeTestUtil.TriState.Ready;
                }
            }

            Assert.AreEqual(expected: documents.Count, actual: elements.Count);
        }

        [TestMethod]
        public async Task SelectStar()
        {
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 250; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i} }}"));
            }

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT * FROM c",
                documents: documents);

            Assert.AreEqual(expected: documents.Count, actual: documentsQueried.Count);
        }

        [TestMethod]
        public async Task OrderBy()
        {
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 250; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i} }}"));
            }

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT * FROM c ORDER BY c._ts",
                documents: documents);

            Assert.AreEqual(expected: documents.Count, actual: documentsQueried.Count);
        }

        [TestMethod]
        [Ignore] // Continuation token for in memory container needs to be updated to suppport this query
        public async Task OrderByWithJoins()
        {
            List<CosmosObject> documents = new List<CosmosObject>()
            {
                CosmosObject.Parse($"{{\"pk\" : {1}, \"children\" : [\"Alice\", \"Bob\", \"Charlie\"]}}"),
                CosmosObject.Parse($"{{\"pk\" : {2}, \"children\" : [\"Dave\", \"Eve\", \"Fancy\"]}}"),
                CosmosObject.Parse($"{{\"pk\" : {3}, \"children\" : [\"George\", \"Henry\", \"Igor\"]}}"),
                CosmosObject.Parse($"{{\"pk\" : {4}, \"children\" : [\"Jack\", \"Kim\", \"Levin\"]}}"),
            };

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT d FROM c JOIN d in c.children ORDER BY c.pk",
                documents: documents,
                pageSize: 2);

            Assert.AreEqual(expected: documents.Count * 3, actual: documentsQueried.Count);
        }

        [TestMethod]
        public async Task Top()
        {
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 250; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i} }}"));
            }

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT TOP 10 * FROM c",
                documents: documents);

            Assert.AreEqual(expected: 10, actual: documentsQueried.Count);
        }

        [TestMethod]
        public async Task OffsetLimit()
        {
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 250; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i} }}"));
            }

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT * FROM c OFFSET 10 LIMIT 103",
                documents: documents);

            Assert.AreEqual(expected: 103, actual: documentsQueried.Count);
        }

        [TestMethod]
        public async Task Aggregates()
        {
            const int DocumentCount = 250;
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < DocumentCount; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i} }}"));
            }

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT VALUE COUNT(1) FROM c",
                documents: documents);

            Assert.AreEqual(expected: 1, actual: documentsQueried.Count);
            if (documentsQueried[0] is CosmosNumber number)
            {
                Assert.AreEqual(expected: DocumentCount, actual: Number64.ToLong(number.Value));
            }
            else
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public async Task DCount()
        {
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 250; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i}, \"val\": {i % 49} }}"));
            }

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT VALUE COUNT(1) FROM (SELECT DISTINCT VALUE c.val FROM c)",
                documents: documents);

            Assert.AreEqual(expected: 1, actual: documentsQueried.Count);
            Assert.IsTrue(documentsQueried[0] is CosmosNumber);
            CosmosNumber result = documentsQueried[0] as CosmosNumber;
            Assert.AreEqual(expected: 49, actual: Number64.ToLong(result.Value));
        }

        [TestMethod]
        [Ignore]
        // Need to implement group by continuation token on the in memory collection.
        public async Task GroupBy()
        {
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 250; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i} }}"));
            }

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT VALUE COUNT(1) FROM c GROUP BY c.pk",
                documents: documents);

            Assert.AreEqual(expected: documents.Count, actual: documentsQueried.Count);
        }

        [TestMethod]
        public async Task Tracing()
        {
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 250; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i} }}"));
            }

            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(documents);
            IQueryPipelineStage pipelineStage = await CreatePipelineAsync(documentContainer, "SELECT * FROM c", pageSize: 10);

            Trace rootTrace;
            int numTraces = (await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, default)).Count;
            using (rootTrace = Trace.GetRootTrace("Cross Partition Query"))
            {
                while (await pipelineStage.MoveNextAsync(rootTrace))
                {
                    TryCatch<QueryPage> tryGetQueryPage = pipelineStage.Current;
                    tryGetQueryPage.ThrowIfFailed();

                    numTraces++;
                }
            }

            Assert.AreEqual(numTraces, rootTrace.Children.Count);
        }

        [TestMethod]
        public async Task OffsetLimitPageSize()
        {
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 1100; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i} }}"));
            }

            MockInMemoryContainer mockInMemoryContainer = new MockInMemoryContainer(new InMemoryContainer(partitionKeyDefinition));
            DocumentContainer documentContainer = await CreateDocumentContainerAsync(documents, mockInMemoryContainer, numSplits: 4);

            // OFFSET/LIMIT with ORDER BY
            await this.TestPageSizeAsync("SELECT c.pk FROM c ORDER BY c.pk OFFSET 0 LIMIT 500", expectedPageSize: 500, expectedResults: 500, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT c.pk FROM c ORDER BY c.pk OFFSET 10000 LIMIT 5000", expectedPageSize: 1000, expectedResults: 0, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT c.pk FROM c ORDER BY c.pk OFFSET 10 LIMIT 100", expectedPageSize: 110, expectedResults: 100, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT c.pk FROM c ORDER BY c.pk OFFSET 0 LIMIT 100", expectedPageSize: 100, expectedResults: 100, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT c.pk FROM c ORDER BY c.pk OFFSET 100 LIMIT 0", expectedPageSize: 1000, expectedResults: 0, mockInMemoryContainer, documentContainer);

            // OFFSET/LIMIT without ORDER BY
            await this.TestPageSizeAsync("SELECT c.pk FROM c OFFSET 10 LIMIT 100", expectedPageSize: 1000, expectedResults: 100, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT c.pk FROM c OFFSET 0 LIMIT 100", expectedPageSize: 1000, expectedResults: 100, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT c.pk FROM c OFFSET 100 LIMIT 0", expectedPageSize: 1000, expectedResults: 0, mockInMemoryContainer, documentContainer);

            // TOP with ORDER BY
            await this.TestPageSizeAsync("SELECT TOP 5 c.pk FROM c ORDER BY c.pk", expectedPageSize: 5, expectedResults: 5, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT TOP 100 c.pk FROM c ORDER BY c.pk", expectedPageSize: 100, expectedResults: 100, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT TOP 5000 c.pk FROM c ORDER BY c.pk", expectedPageSize: 1000, expectedResults: 1100, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT TOP 15000 c.pk FROM c ORDER BY c.pk", expectedPageSize: 1000, expectedResults: 1100, mockInMemoryContainer, documentContainer);

            // TOP without ORDER BY
            await this.TestPageSizeAsync("SELECT TOP 5 c.pk FROM c", expectedPageSize: 1000, expectedResults: 5, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT TOP 5000 c.pk FROM c", expectedPageSize: 1000, expectedResults: 1100, mockInMemoryContainer, documentContainer);
            await this.TestPageSizeAsync("SELECT TOP 15000 c.pk FROM c", expectedPageSize: 1000, expectedResults: 1100, mockInMemoryContainer, documentContainer);
        }

        private async Task TestPageSizeAsync(string query, int expectedPageSize, int expectedResults, MockInMemoryContainer inMemoryContainer, DocumentContainer documentContainer)
        {
            (CosmosQueryExecutionContextFactory.InputParameters inputParameters, CosmosQueryContextCore cosmosQueryContextCore) = CreateInputParamsAndQueryContext(
                query,
                partitionKeyDefinition,
                null,
                new QueryRequestOptions());

            IQueryPipelineStage queryPipelineStage = CosmosQueryExecutionContextFactory.Create(
                documentContainer,
                cosmosQueryContextCore,
                inputParameters,
                NoOpTrace.Singleton);

            List<CosmosElement> elements = new List<CosmosElement>();
            while (await queryPipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                TryCatch<QueryPage> tryGetQueryPage = queryPipelineStage.Current;
                tryGetQueryPage.ThrowIfFailed();

                elements.AddRange(tryGetQueryPage.Result.Documents);
            }

            Assert.AreEqual(expected: expectedPageSize, actual: inMemoryContainer.PageSizeSpecified);
            Assert.AreEqual(expected: expectedResults, actual: elements.Count);
        }

        private static Tuple<CosmosQueryExecutionContextFactory.InputParameters, CosmosQueryContextCore> CreateInputParamsAndQueryContext(
            string query,
            PartitionKeyDefinition partitionKeyDefinition,
            Cosmos.PartitionKey? partitionKeyValue,
            QueryRequestOptions queryRequestOptions)
        {
            CosmosSerializerCore serializerCore = new();
            using StreamReader streamReader = new(serializerCore.ToStreamSqlQuerySpec(new SqlQuerySpec(query), Documents.ResourceType.Document));
            string sqlQuerySpecJsonString = streamReader.ReadToEnd();

            CosmosQueryExecutionContextFactory.InputParameters inputParameters = new CosmosQueryExecutionContextFactory.InputParameters(
                sqlQuerySpec: new SqlQuerySpec(query),
                initialUserContinuationToken: null,
                initialFeedRange: null,
                maxConcurrency: queryRequestOptions.MaxConcurrency,
                maxItemCount: queryRequestOptions.MaxItemCount,
                maxBufferedItemCount: queryRequestOptions.MaxBufferedItemCount,
                partitionKey: partitionKeyValue,
                properties: new Dictionary<string, object>() { { "x-ms-query-partitionkey-definition", partitionKeyDefinition } },
                partitionedQueryExecutionInfo: null,
                executionEnvironment: null,
                returnResultsInDeterministicOrder: null,
                enableOptimisticDirectExecution: queryRequestOptions.EnableOptimisticDirectExecution,
                testInjections: queryRequestOptions.TestSettings);

            string databaseId = "db1234";
            string resourceLink = $"dbs/{databaseId}/colls";
            const string suffix = "-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF-FF";

            List<PartitionKeyRange> partitionKeyRanges = new List<PartitionKeyRange>
            {
                new PartitionKeyRange() { MinInclusive = Documents.Routing.PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey, MaxExclusive = "1F" + suffix },
                new PartitionKeyRange() { MinInclusive = "1F" + suffix, MaxExclusive = "3F" + suffix },
                new PartitionKeyRange() { MinInclusive = "3F" + suffix, MaxExclusive = "5F" + suffix },
                new PartitionKeyRange() { MinInclusive = "5F" + suffix, MaxExclusive = "7F" + suffix },
                new PartitionKeyRange() { MinInclusive = "7F" + suffix, MaxExclusive = "9F" + suffix },
                new PartitionKeyRange() { MinInclusive = "9F" + suffix, MaxExclusive = "BF" + suffix },
                new PartitionKeyRange() { MinInclusive = "BF" + suffix, MaxExclusive = "DF" + suffix },
                new PartitionKeyRange() { MinInclusive = "DF" + suffix, MaxExclusive = Documents.Routing.PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey },
            };

            Mock<CosmosQueryClient> mockClient = new Mock<CosmosQueryClient>();

            mockClient.Setup(x => x.GetTargetPartitionKeyRangesAsync(
                It.IsAny<string>(),
                "HelloWorld",
                It.IsAny<IReadOnlyList<Documents.Routing.Range<string>>>(),
                It.IsAny<bool>(),
                It.IsAny<ITrace>()))
                .Returns((string resourceLink, string collectionResourceId, IReadOnlyList<Documents.Routing.Range<string>> providedRanges, bool forceRefresh, ITrace trace) => Task.FromResult(partitionKeyRanges));

            mockClient.Setup(x => x.TryGetPartitionedQueryExecutionInfoAsync(
                It.IsAny<SqlQuerySpec>(),
                It.IsAny<ResourceType>(),
                It.IsAny<PartitionKeyDefinition>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<Cosmos.GeospatialType>(),
                It.IsAny<CancellationToken>()))
                .Returns((SqlQuerySpec sqlQuerySpec, ResourceType resourceType, PartitionKeyDefinition partitionKeyDefinition, bool requireFormattableOrderByQuery, bool isContinuationExpected, bool allowNonValueAggregateQuery, bool hasLogicalPartitionKey, bool allowDCount, bool useSystemPrefix, Cosmos.GeospatialType geospatialType, CancellationToken cancellationToken) =>
                {
                    CosmosSerializerCore serializerCore = new();
                    using StreamReader streamReader = new(serializerCore.ToStreamSqlQuerySpec(sqlQuerySpec, Documents.ResourceType.Document));
                    string sqlQuerySpecJsonString = streamReader.ReadToEnd();

                    (PartitionedQueryExecutionInfo partitionedQueryExecutionInfo, QueryPartitionProvider queryPartitionProvider) = OptimisticDirectExecutionQueryBaselineTests.GetPartitionedQueryExecutionInfoAndPartitionProvider(sqlQuerySpecJsonString, partitionKeyDefinition);
                    return Task.FromResult(TryCatch<PartitionedQueryExecutionInfo>.FromResult(partitionedQueryExecutionInfo));
                }
                );

            CosmosQueryContextCore cosmosQueryContextCore = new CosmosQueryContextCore(
                 client: new TestCosmosQueryClient(GetQueryPartitionProvider()),
                 resourceTypeEnum: Documents.ResourceType.Document,
                 operationType: Documents.OperationType.Query,
                 resourceType: typeof(QueryResponseCore),
                 resourceLink: resourceLink,
                 isContinuationExpected: true,
                 allowNonValueAggregateQuery: true,
                 useSystemPrefix: false,
                 correlatedActivityId: Guid.NewGuid());

            return Tuple.Create(inputParameters, cosmosQueryContextCore);
        }

        internal static QueryPartitionProvider GetQueryPartitionProvider()
        {
            IDictionary<string, object> DefaultQueryengineConfiguration = new Dictionary<string, object>()
            {
                {"maxSqlQueryInputLength", 30720},
                {"maxJoinsPerSqlQuery", 5},
                {"maxLogicalAndPerSqlQuery", 200},
                {"maxLogicalOrPerSqlQuery", 200},
                {"maxUdfRefPerSqlQuery", 2},
                {"maxInExpressionItemsCount", 8000},
                {"queryMaxInMemorySortDocumentCount", 500},
                {"maxQueryRequestTimeoutFraction", 0.90},
                {"sqlAllowNonFiniteNumbers", false},
                {"sqlAllowAggregateFunctions", true},
                {"sqlAllowSubQuery", true},
                {"sqlAllowScalarSubQuery", false},
                {"allowNewKeywords", true},
                {"sqlAllowLike", true},
                {"sqlAllowGroupByClause", false},
                {"queryEnableMongoNativeRegex", true},
                {"maxSpatialQueryCells", 12},
                {"spatialMaxGeometryPointCount", 256},
                {"sqlDisableOptimizationFlags", 0},
                {"sqlEnableParameterExpansionCheck", true}
            };

            return new QueryPartitionProvider(DefaultQueryengineConfiguration);
        }

        internal static async Task<List<CosmosElement>> ExecuteQueryAsync(
            string query,
            IReadOnlyList<CosmosObject> documents,
            int pageSize = 10)
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(documents);
            return await ExecuteQueryAsync(query, documentContainer, pageSize);
        }

        internal static async Task<List<CosmosElement>> ExecuteQueryAsync(
            string query,
            IDocumentContainer documentContainer,
            int pageSize)
        {
            List<CosmosElement> resultsFromDrainWithoutState = await DrainWithoutStateAsync(query, documentContainer, pageSize);
            List<CosmosElement> resultsFromDrainWithState = await DrainWithStateAsync(query, documentContainer, pageSize);

            Assert.IsTrue(resultsFromDrainWithoutState.SequenceEqual(resultsFromDrainWithState));

            return resultsFromDrainWithoutState;
        }

        [TestMethod]
        public async Task Fuzz()
        {
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 250; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i} }}"));
            }

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT * FROM c ORDER BY c._ts OFFSET 1 LIMIT 500",
                documents: documents);

            Assert.AreEqual(expected: 249, actual: documentsQueried.Count);
        }

        internal static async Task<List<CosmosElement>> DrainWithoutStateAsync(string query, IDocumentContainer documentContainer, int pageSize)
        {
            IQueryPipelineStage pipelineStage = await CreatePipelineAsync(documentContainer, query, pageSize);

            List<CosmosElement> elements = new List<CosmosElement>();
            while (await pipelineStage.MoveNextAsync(NoOpTrace.Singleton))
            {
                TryCatch<QueryPage> tryGetQueryPage = pipelineStage.Current;
                tryGetQueryPage.ThrowIfFailed();

                elements.AddRange(tryGetQueryPage.Result.Documents);
            }

            return elements;
        }

        private static async Task<List<CosmosElement>> DrainWithStateAsync(string query, IDocumentContainer documentContainer, int pageSize)
        {
            IQueryPipelineStage pipelineStage;
            CosmosElement state = null;

            List<CosmosElement> elements = new List<CosmosElement>();
            do
            {
                pipelineStage = await CreatePipelineAsync(documentContainer, query, pageSize, state);

                if (!await pipelineStage.MoveNextAsync(NoOpTrace.Singleton))
                {
                    break;
                }

                TryCatch<QueryPage> tryGetQueryPage = pipelineStage.Current;
                tryGetQueryPage.ThrowIfFailed();

                elements.AddRange(tryGetQueryPage.Result.Documents);
                state = tryGetQueryPage.Result.State?.Value;
            }
            while (state != null);

            return elements;
        }

        internal static Task<DocumentContainer> CreateDocumentContainerAsync(
            IReadOnlyList<CosmosObject> documents,
            int numSplits = 3,
            FlakyDocumentContainer.FailureConfigs failureConfigs = null)
        {
            IMonadicDocumentContainer monadicDocumentContainer = CreateMonadicDocumentContainerAsync(failureConfigs);
            return CreateDocumentContainerAsync(documents, monadicDocumentContainer, numSplits);
        }

        internal static IMonadicDocumentContainer CreateMonadicDocumentContainerAsync(FlakyDocumentContainer.FailureConfigs failureConfigs)
        {
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            return monadicDocumentContainer;
        }

        internal static async Task<DocumentContainer> CreateDocumentContainerAsync(
            IReadOnlyList<CosmosObject> documents,
            IMonadicDocumentContainer monadicDocumentContainer,
            int numSplits)
        {
            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < numSplits; i++)
            {
                IReadOnlyList<FeedRangeInternal> ranges = await documentContainer.GetFeedRangesAsync(
                    trace: NoOpTrace.Singleton,
                    cancellationToken: default);
                foreach (FeedRangeInternal range in ranges)
                {
                    await documentContainer.SplitAsync(range, cancellationToken: default);
                }

                await documentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
            }

            foreach (CosmosObject document in documents)
            {
                while (true)
                {
                    TryCatch<Record> monadicCreateRecord = await documentContainer.MonadicCreateItemAsync(
                        document,
                        cancellationToken: default);
                    if (monadicCreateRecord.Succeeded)
                    {
                        break;
                    }
                }
            }

            return documentContainer;
        }

        private static async Task<IQueryPipelineStage> CreatePipelineAsync(
            IDocumentContainer documentContainer,
            string query,
            int pageSize,
            CosmosElement state = null)
        {
            IReadOnlyList<FeedRangeEpk> feedRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);

            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                ExecutionEnvironment.Client,
                documentContainer,
                new SqlQuerySpec(query),
                feedRanges,
                partitionKey: null,
                GetQueryPlan(query),
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: pageSize),
                containerQueryProperties: new ContainerQueryProperties(),
                maxConcurrency: 10,
                requestCancellationToken: default,
                requestContinuationToken: state);

            tryCreatePipeline.ThrowIfFailed();

            return tryCreatePipeline.Result;
        }

        private static QueryInfo GetQueryPlan(string query)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> info = QueryPartitionProviderTestInstance.Object.TryGetPartitionedQueryExecutionInfoInternal(
                JsonConvert.SerializeObject(new SqlQuerySpec(query)),
                partitionKeyDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                allowDCount: true,
                hasLogicalPartitionKey: false,
                useSystemPrefix: false,
                geospatialType: Cosmos.GeospatialType.Geography);

            info.ThrowIfFailed();
            return info.Result.QueryInfo;
        }

        private class MergeTestUtil
        {
            public enum TriState { NotReady, Ready, Done };

            public IDocumentContainer DocumentContainer { get; set; }

            public TriState ShouldMerge { get; set; }

            public async Task<Exception> ShouldReturnFailure()
            {
                if (this.ShouldMerge == TriState.Ready)
                {
                    await this.DocumentContainer.RefreshProviderAsync(NoOpTrace.Singleton, cancellationToken: default);
                    List<FeedRangeEpk> ranges = await this.DocumentContainer.GetFeedRangesAsync(
                        trace: NoOpTrace.Singleton,
                        cancellationToken: default);

                    await this.DocumentContainer.MergeAsync(ranges[0], ranges[1], default);
                    this.ShouldMerge = TriState.Done;

                    return new CosmosException(
                        message: "PKRange was split/merged",
                        statusCode: System.Net.HttpStatusCode.Gone,
                        subStatusCode: (int)Documents.SubStatusCodes.PartitionKeyRangeGone,
                        activityId: "BC0CCDA5-D378-4922-B8B0-D51D745B9139",
                        requestCharge: 0.0);
                }
                else
                {
                    return null;
                }
            }
        }

        private class MockInMemoryContainer : IMonadicDocumentContainer
        {
            public int? PageSizeSpecified { get; private set; }
            public IMonadicDocumentContainer MonadicDocumentContainer { get; }

            public MockInMemoryContainer(IMonadicDocumentContainer documentContainer)
            {
                this.MonadicDocumentContainer = documentContainer;
                this.PageSizeSpecified = null;
            }

            public void Reset()
            {
                this.PageSizeSpecified = null;
            }

            public Task<TryCatch<ChangeFeedPage>> MonadicChangeFeedAsync(FeedRangeState<ChangeFeedState> feedRangeState, ChangeFeedPaginationOptions changeFeedPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                return this.MonadicDocumentContainer.MonadicChangeFeedAsync(feedRangeState, changeFeedPaginationOptions, trace, cancellationToken);
            }

            public Task<TryCatch<Record>> MonadicCreateItemAsync(CosmosObject payload, CancellationToken cancellationToken)
            {
                return this.MonadicDocumentContainer.MonadicCreateItemAsync(payload, cancellationToken);
            }

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetChildRangeAsync(FeedRangeInternal feedRange, ITrace trace, CancellationToken cancellationToken)
            {
                return this.MonadicDocumentContainer.MonadicGetChildRangeAsync(feedRange, trace, cancellationToken);
            }

            public Task<TryCatch<List<FeedRangeEpk>>> MonadicGetFeedRangesAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.MonadicDocumentContainer.MonadicGetFeedRangesAsync(trace, cancellationToken);
            }

            public Task<TryCatch<string>> MonadicGetResourceIdentifierAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.MonadicDocumentContainer.MonadicGetResourceIdentifierAsync(trace, cancellationToken);
            }

            public Task<TryCatch> MonadicMergeAsync(FeedRangeInternal feedRange1, FeedRangeInternal feedRange2, CancellationToken cancellationToken)
            {
                return this.MonadicDocumentContainer.MonadicMergeAsync(feedRange1, feedRange2, cancellationToken);
            }

            public Task<TryCatch<QueryPage>> MonadicQueryAsync(SqlQuerySpec sqlQuerySpec, FeedRangeState<QueryState> feedRangeState, QueryPaginationOptions queryPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                this.PageSizeSpecified = queryPaginationOptions.PageSizeLimit;
                
                return this.MonadicDocumentContainer.MonadicQueryAsync(sqlQuerySpec, feedRangeState, queryPaginationOptions, trace, cancellationToken);
            }

            public Task<TryCatch<ReadFeedPage>> MonadicReadFeedAsync(FeedRangeState<ReadFeedState> feedRangeState, ReadFeedPaginationOptions readFeedPaginationOptions, ITrace trace, CancellationToken cancellationToken)
            {
                return this.MonadicDocumentContainer.MonadicReadFeedAsync(feedRangeState, readFeedPaginationOptions, trace, cancellationToken);
            }

            public Task<TryCatch<Record>> MonadicReadItemAsync(CosmosElement partitionKey, string identifier, CancellationToken cancellationToken)
            {
                return this.MonadicDocumentContainer.MonadicReadItemAsync(partitionKey, identifier, cancellationToken);
            }

            public Task<TryCatch> MonadicRefreshProviderAsync(ITrace trace, CancellationToken cancellationToken)
            {
                return this.MonadicDocumentContainer.MonadicRefreshProviderAsync(trace, cancellationToken);
            }

            public Task<TryCatch> MonadicSplitAsync(FeedRangeInternal feedRange, CancellationToken cancellationToken)
            {
                return this.MonadicDocumentContainer.MonadicSplitAsync(feedRange, cancellationToken);
            }
        }
    }
}
