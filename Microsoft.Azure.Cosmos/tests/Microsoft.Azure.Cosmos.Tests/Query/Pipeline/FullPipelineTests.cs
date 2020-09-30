//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Pipeline
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Pagination;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
    using Microsoft.Azure.Cosmos.Tests.Pagination;
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

        private static async Task<List<CosmosElement>> ExecuteQueryAsync(
            string query,
            IReadOnlyList<CosmosObject> documents)
        {
            IDocumentContainer documentContainer = await CreateDocumentContainerAsync(documents);

            List<CosmosElement> resultsFromDrainWithoutState = await DrainWithoutStateAsync(query, documentContainer);
            List<CosmosElement> resultsFromDrainWithState = await DrainWithStateAsync(query, documentContainer);

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

        private static async Task<List<CosmosElement>> DrainWithoutStateAsync(string query, IDocumentContainer documentContainer)
        {
            IQueryPipelineStage pipelineStage = CreatePipeline(documentContainer, query);

            List<CosmosElement> elements = new List<CosmosElement>();
            while (await pipelineStage.MoveNextAsync())
            {
                TryCatch<QueryPage> tryGetQueryPage = pipelineStage.Current;
                tryGetQueryPage.ThrowIfFailed();

                elements.AddRange(tryGetQueryPage.Result.Documents);
            }

            return elements;
        }

        private static async Task<List<CosmosElement>> DrainWithStateAsync(string query, IDocumentContainer documentContainer)
        {
            IQueryPipelineStage pipelineStage;
            CosmosElement state = null;

            List<CosmosElement> elements = new List<CosmosElement>();
            do
            {
                pipelineStage = CreatePipeline(documentContainer, query, state);

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

            await documentContainer.SplitAsync(partitionKeyRangeId: 0, cancellationToken: default);

            await documentContainer.SplitAsync(partitionKeyRangeId: 1, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 2, cancellationToken: default);

            await documentContainer.SplitAsync(partitionKeyRangeId: 3, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 4, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 5, cancellationToken: default);
            await documentContainer.SplitAsync(partitionKeyRangeId: 6, cancellationToken: default);

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

        private static IQueryPipelineStage CreatePipeline(IDocumentContainer documentContainer, string query, CosmosElement state = null)
        {
            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                ExecutionEnvironment.Compute,
                documentContainer,
                new SqlQuerySpec(query),
                documentContainer.GetFeedRangesAsync(default(CancellationToken)).Result,
                GetQueryPlan(query),
                pageSize: 10,
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
                hasLogicalPartitionKey: false);

            info.ThrowIfFailed();
            return info.Result.QueryInfo;
        }
    }
}
