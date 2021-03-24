//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FullPipelineTests
    {
        private static readonly Dictionary<string, object> DefaultQueryEngineConfiguration = new Dictionary<string, object>()
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
            {"sqlAllowLike", false},
            {"sqlAllowGroupByClause", false},
            {"maxSpatialQueryCells", 12},
            {"spatialMaxGeometryPointCount", 256},
            {"sqlDisableQueryILOptimization", false},
            {"sqlDisableFilterPlanOptimization", false}
        };

        private static readonly QueryPartitionProvider queryPartitionProvider = new QueryPartitionProvider(DefaultQueryEngineConfiguration);
        private static readonly PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition()
        {
            Paths = new Collection<string>()
            {
                "/pk"
            },
            Kind = PartitionKind.Hash,
            Version = PartitionKeyDefinitionVersion.V2,
        };

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
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 250; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i} }}"));
            }

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT VALUE COUNT(1) FROM c",
                documents: documents);

            Assert.AreEqual(expected: 1, actual: documentsQueried.Count);
        }

        [TestMethod]
        [Ignore("[TODO]: ndeshpan enable after ServiceInterop.dll is refreshed")]
        public async Task DCount()
        {
            List<CosmosObject> documents = new List<CosmosObject>();
            for (int i = 0; i < 250; i++)
            {
                documents.Add(CosmosObject.Parse($"{{\"pk\" : {i}, \"val\": {i % 50} }}"));
            }

            List<CosmosElement> documentsQueried = await ExecuteQueryAsync(
                query: "SELECT VALUE COUNT(1) FROM (SELECT DISTINCT VALUE c.val FROM c)",
                documents: documents);

            Assert.AreEqual(expected: 1, actual: documentsQueried.Count);
            Assert.IsTrue(documentsQueried[0] is CosmosNumber);
            CosmosNumber result = documentsQueried[0] as CosmosNumber;
            Assert.AreEqual(expected: 50, actual: result);
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
            IQueryPipelineStage pipelineStage = CreatePipeline(documentContainer, "SELECT * FROM c", pageSize: 10);

            Trace rootTrace;
            int numTraces = 1;
            using (rootTrace = Trace.GetRootTrace("Cross Partition Query"))
            {
                while (await pipelineStage.MoveNextAsync(rootTrace))
                {
                    TryCatch<QueryPage> tryGetQueryPage = pipelineStage.Current;
                    tryGetQueryPage.ThrowIfFailed();

                    numTraces++;
                }
            }

            string traceString = TraceWriter.TraceToText(rootTrace);

            Console.WriteLine(traceString);

            Assert.AreEqual(numTraces, rootTrace.Children.Count);
        }

        private static async Task<List<CosmosElement>> ExecuteQueryAsync(
            string query,
            IReadOnlyList<CosmosObject> documents,
            int pageSize = 10)
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(documents);

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

        private static async Task<List<CosmosElement>> DrainWithoutStateAsync(string query, IDocumentContainer documentContainer, int pageSize = 10)
        {
            IQueryPipelineStage pipelineStage = CreatePipeline(documentContainer, query, pageSize);

            List<CosmosElement> elements = new List<CosmosElement>();
            while (await pipelineStage.MoveNextAsync())
            {
                TryCatch<QueryPage> tryGetQueryPage = pipelineStage.Current;
                tryGetQueryPage.ThrowIfFailed();

                elements.AddRange(tryGetQueryPage.Result.Documents);
            }

            return elements;
        }

        private static async Task<List<CosmosElement>> DrainWithStateAsync(string query, IDocumentContainer documentContainer, int pageSize = 10)
        {
            IQueryPipelineStage pipelineStage;
            CosmosElement state = null;

            List<CosmosElement> elements = new List<CosmosElement>();
            do
            {
                pipelineStage = CreatePipeline(documentContainer, query, pageSize, state);

                if (!await pipelineStage.MoveNextAsync())
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

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            IReadOnlyList<CosmosObject> documents,
            FlakyDocumentContainer.FailureConfigs failureConfigs = null)
        {
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < 3; i++)
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

        private static IQueryPipelineStage CreatePipeline(IDocumentContainer documentContainer, string query, int pageSize = 10, CosmosElement state = null)
        {
            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                ExecutionEnvironment.Compute,
                documentContainer,
                new SqlQuerySpec(query),
                documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default).Result,
                partitionKey: null,
                GetQueryPlan(query),
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: 10),
                maxConcurrency: 10,
                requestCancellationToken: default,
                requestContinuationToken: state);

            tryCreatePipeline.ThrowIfFailed();

            return tryCreatePipeline.Result;
        }

        private static QueryInfo GetQueryPlan(string query)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> info = queryPartitionProvider.TryGetPartitionedQueryExecutionInfoInternal(
                new SqlQuerySpec(query),
                partitionKeyDefinition,
                requireFormattableOrderByQuery: true,
                isContinuationExpected: false,
                allowNonValueAggregateQuery: true,
                allowDCount: true,
                hasLogicalPartitionKey: false);

            info.ThrowIfFailed();
            return info.Result.QueryInfo;
        }
    }
}
