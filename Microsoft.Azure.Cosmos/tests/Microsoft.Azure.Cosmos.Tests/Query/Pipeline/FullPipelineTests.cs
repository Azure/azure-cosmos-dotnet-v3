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
        public async Task TestMerge()
        {
            List<CosmosObject> documents = Enumerable
                .Range(0, 100)
                .Select(x => CosmosObject.Parse($"{{\"pk\" : {x} }}"))
                .ToList();

            MergeTestUtil mergeTest = new MergeTestUtil();
            mergeTest.DocumentContainer = await CreateDocumentContainerAsync(
                documents: documents,
                numPartitions: 2,
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

        private static async Task<List<CosmosElement>> ExecuteQueryAsync(
            string query,
            IReadOnlyList<CosmosObject> documents,
            IDocumentContainer documentContainer = null,
            int pageSize = 10)
        {
            if (documentContainer == null)
            {
                documentContainer = await CreateDocumentContainerAsync(documents);
            }

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

        private static async Task<List<CosmosElement>> DrainWithStateAsync(string query, IDocumentContainer documentContainer, int pageSize = 10)
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

        private static async Task<IDocumentContainer> CreateDocumentContainerAsync(
            IReadOnlyList<CosmosObject> documents,
            int numPartitions = 3,
            FlakyDocumentContainer.FailureConfigs failureConfigs = null)
        {
            IMonadicDocumentContainer monadicDocumentContainer = new InMemoryContainer(partitionKeyDefinition);
            if (failureConfigs != null)
            {
                monadicDocumentContainer = new FlakyDocumentContainer(monadicDocumentContainer, failureConfigs);
            }

            DocumentContainer documentContainer = new DocumentContainer(monadicDocumentContainer);

            for (int i = 0; i < numPartitions; i++)
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
            int pageSize = 10,
            CosmosElement state = null)
        {
            IReadOnlyList<FeedRangeEpk> feedRanges = await documentContainer.GetFeedRangesAsync(NoOpTrace.Singleton, cancellationToken: default);

            TryCatch<IQueryPipelineStage> tryCreatePipeline = PipelineFactory.MonadicCreate(
                ExecutionEnvironment.Compute,
                documentContainer,
                new SqlQuerySpec(query),
                feedRanges,
                partitionKey: null,
                GetQueryPlan(query),
                queryPaginationOptions: new QueryPaginationOptions(pageSizeHint: pageSize),
                maxConcurrency: 10,
                requestCancellationToken: default,
                requestContinuationToken: state);

            tryCreatePipeline.ThrowIfFailed();

            return tryCreatePipeline.Result;
        }

        private static QueryInfo GetQueryPlan(string query)
        {
            TryCatch<PartitionedQueryExecutionInfoInternal> info = QueryPartitionProviderTestInstance.Object.TryGetPartitionedQueryExecutionInfoInternal(
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
    }
}
