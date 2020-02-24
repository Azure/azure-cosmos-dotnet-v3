//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.ItemProducers;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Metrics;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class ItemProducerTreeUnitTests
    {
        private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

        [TestMethod]
        [DataRow(null)]
        [DataRow("SomeRandomContinuationToken")]
        public async Task TestMoveNextWithEmptyPagesAsync(string initialContinuationToken)
        {
            int maxPageSize = 5;

            List<MockPartitionResponse[]> mockResponses = MockQueryFactory.GetAllCombinationWithEmptyPage();
            foreach (MockPartitionResponse[] mockResponse in mockResponses)
            {
                Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
                IList<ToDoItem> allItems = MockQueryFactory.GenerateAndMockResponse(
                    mockQueryClient,
                    isOrderByQuery: false,
                    sqlQuerySpec: MockQueryFactory.DefaultQuerySpec,
                    containerRid: MockQueryFactory.DefaultCollectionRid,
                    initContinuationToken: initialContinuationToken,
                    maxPageSize: maxPageSize,
                    mockResponseForSinglePartition: mockResponse,
                    cancellationTokenForMocks: this.cancellationToken);

                CosmosQueryContext context = MockQueryFactory.CreateContext(
                    mockQueryClient.Object);

                ItemProducerTree itemProducerTree = new ItemProducerTree(
                   queryContext: context,
                   querySpecForInit: MockQueryFactory.DefaultQuerySpec,
                   partitionKeyRange: mockResponse[0].PartitionKeyRange,
                   produceAsyncCompleteCallback: MockItemProducerFactory.DefaultTreeProduceAsyncCompleteDelegate,
                   itemProducerTreeComparer: DeterministicParallelItemProducerTreeComparer.Singleton,
                   equalityComparer: CosmosElementEqualityComparer.Value,
                   testSettings: new TestInjections(simulate429s: false, simulateEmptyPages: false),
                   deferFirstPage: true,
                   collectionRid: MockQueryFactory.DefaultCollectionRid,
                   initialPageSize: maxPageSize,
                   initialContinuationToken: initialContinuationToken);

                Assert.IsTrue(itemProducerTree.HasMoreResults);

                List<ToDoItem> itemsRead = new List<ToDoItem>();
                while ((await itemProducerTree.TryMoveNextPageAsync(this.cancellationToken)).movedToNextPage)
                {
                    while (itemProducerTree.TryMoveNextDocumentWithinPage())
                    {
                        Assert.IsTrue(itemProducerTree.HasMoreResults);
                        string jsonValue = itemProducerTree.Current.ToString();
                        ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                        itemsRead.Add(item);
                    }
                }

                Assert.IsFalse(itemProducerTree.HasMoreResults);

                Assert.AreEqual(allItems.Count, itemsRead.Count);

                CollectionAssert.AreEqual(itemsRead, allItems.ToList(), new ToDoItemComparer());
            }
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("SomeRandomContinuationToken")]
        public async Task TestMoveNextWithEmptyPagesAndSplitAsync(string initialContinuationToken)
        {
            int maxPageSize = 5;

            List<MockPartitionResponse[]> mockResponsesScenario = MockQueryFactory.GetSplitScenarios();
            foreach (MockPartitionResponse[] mockResponse in mockResponsesScenario)
            {
                Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
                IList<ToDoItem> allItems = MockQueryFactory.GenerateAndMockResponse(
                    mockQueryClient,
                    isOrderByQuery: false,
                    sqlQuerySpec: MockQueryFactory.DefaultQuerySpec,
                    containerRid: MockQueryFactory.DefaultCollectionRid,
                    initContinuationToken: initialContinuationToken,
                    maxPageSize: maxPageSize,
                    mockResponseForSinglePartition: mockResponse,
                    cancellationTokenForMocks: this.cancellationToken);

                CosmosQueryContext context = MockQueryFactory.CreateContext(
                    mockQueryClient.Object);

                ItemProducerTree itemProducerTree = new ItemProducerTree(
                   context,
                   MockQueryFactory.DefaultQuerySpec,
                   mockResponse[0].PartitionKeyRange,
                   MockItemProducerFactory.DefaultTreeProduceAsyncCompleteDelegate,
                   DeterministicParallelItemProducerTreeComparer.Singleton,
                   CosmosElementEqualityComparer.Value,
                   new TestInjections(simulate429s: false, simulateEmptyPages: false),
                   true,
                   MockQueryFactory.DefaultCollectionRid,
                   maxPageSize,
                   initialContinuationToken: initialContinuationToken);

                Assert.IsTrue(itemProducerTree.HasMoreResults);

                List<ToDoItem> itemsRead = new List<ToDoItem>();
                while ((await itemProducerTree.TryMoveNextPageAsync(this.cancellationToken)).movedToNextPage)
                {
                    while (itemProducerTree.TryMoveNextDocumentWithinPage())
                    {
                        Assert.IsTrue(itemProducerTree.HasMoreResults);
                        if (itemProducerTree.Current != null)
                        {
                            string jsonValue = itemProducerTree.Current.ToString();
                            ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                            itemsRead.Add(item);
                        }
                    }

                    itemProducerTree.UpdatePriority();
                }

                Assert.IsFalse(itemProducerTree.HasMoreResults);

                Assert.AreEqual(allItems.Count, itemsRead.Count);
                List<ToDoItem> exepected = allItems.OrderBy(x => x.id).ToList();
                List<ToDoItem> actual = itemsRead.OrderBy(x => x.id).ToList();

                CollectionAssert.AreEqual(exepected, actual, new ToDoItemComparer());
            }
        }

        [TestMethod]
        public async Task TestItemProducerTreeWithFailure()
        {
            int callBackCount = 0;
            Mock<CosmosQueryContext> mockQueryContext = new Mock<CosmosQueryContext>();

            SqlQuerySpec sqlQuerySpec = new SqlQuerySpec("Select * from t");
            PartitionKeyRange partitionKeyRange = new PartitionKeyRange { Id = "0", MinInclusive = "A", MaxExclusive = "B" };
            void produceAsyncCompleteCallback(
                ItemProducerTree producer,
                int itemsBuffered,
                double resourceUnitUsage,
                IReadOnlyCollection<QueryPageDiagnostics> queryPageDiagnostics,
                long responseLengthBytes,
                CancellationToken token)
            { callBackCount++; }

            Mock<IComparer<ItemProducerTree>> comparer = new Mock<IComparer<ItemProducerTree>>();
            Mock<IEqualityComparer<CosmosElement>> cosmosElementComparer = new Mock<IEqualityComparer<CosmosElement>>();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            IReadOnlyList<CosmosElement> cosmosElements = new List<CosmosElement>()
            {
                new Mock<CosmosElement>(CosmosElementType.Object).Object
            };

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create();
            diagnosticsContext.AddDiagnosticsInternal(new PointOperationStatistics(
                Guid.NewGuid().ToString(),
                System.Net.HttpStatusCode.OK,
                subStatusCode: SubStatusCodes.Unknown,
                requestCharge: 42,
                errorMessage: null,
                method: HttpMethod.Post,
                requestUri: new Uri("http://localhost.com"),
                requestSessionToken: null,
                responseSessionToken: null,
                clientSideRequestStatistics: null));

            QueryPageDiagnostics diagnostics = new QueryPageDiagnostics(
                partitionKeyRangeId: "0",
                queryMetricText: "SomeRandomQueryMetricText",
                indexUtilizationText: null,
                diagnosticsContext: diagnosticsContext,
                schedulingStopwatch: new SchedulingStopwatch());
            IReadOnlyCollection<QueryPageDiagnostics> pageDiagnostics = new List<QueryPageDiagnostics>() { diagnostics };

            mockQueryContext.Setup(x => x.ContainerResourceId).Returns("MockCollectionRid");
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(
                sqlQuerySpec,
                It.IsAny<string>(),
                It.IsAny<PartitionKeyRangeIdentity>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<SchedulingStopwatch>(),
                cancellationTokenSource.Token)).Returns(
                Task.FromResult(QueryResponseCore.CreateSuccess(
                    result: cosmosElements,
                    requestCharge: 42,
                    activityId: "AA470D71-6DEF-4D61-9A08-272D8C9ABCFE",
                    diagnostics: pageDiagnostics,
                    pipelineDiagnostics: null,
                    responseLengthBytes: 500,
                    disallowContinuationTokenMessage: null,
                    continuationToken: "TestToken")));

            ItemProducerTree itemProducerTree = new ItemProducerTree(
                queryContext: mockQueryContext.Object,
                querySpecForInit: sqlQuerySpec,
                partitionKeyRange: partitionKeyRange,
                produceAsyncCompleteCallback: produceAsyncCompleteCallback,
                itemProducerTreeComparer: comparer.Object,
                equalityComparer: cosmosElementComparer.Object,
                testSettings: new TestInjections(simulate429s: false, simulateEmptyPages: false),
                deferFirstPage: false,
                collectionRid: "collectionRid",
                initialContinuationToken: null,
                initialPageSize: 50);

            // Buffer to success responses
            await itemProducerTree.BufferMoreDocumentsAsync(cancellationTokenSource.Token);
            await itemProducerTree.BufferMoreDocumentsAsync(cancellationTokenSource.Token);

            CosmosDiagnosticsContext diagnosticsContextInternalServerError = CosmosDiagnosticsContext.Create();
            diagnosticsContextInternalServerError.AddDiagnosticsInternal(new PointOperationStatistics(
                Guid.NewGuid().ToString(),
                System.Net.HttpStatusCode.InternalServerError,
                subStatusCode: SubStatusCodes.Unknown,
                requestCharge: 10.2,
                errorMessage: "Error message",
                method: HttpMethod.Post,
                requestUri: new Uri("http://localhost.com"),
                requestSessionToken: null,
                responseSessionToken: null,
                clientSideRequestStatistics: null));

            diagnostics = new QueryPageDiagnostics(
                partitionKeyRangeId: "0",
                queryMetricText: null,
                indexUtilizationText: null,
                diagnosticsContext: diagnosticsContextInternalServerError,
                schedulingStopwatch: new SchedulingStopwatch());
            pageDiagnostics = new List<QueryPageDiagnostics>() { diagnostics };

            // Buffer a failure
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(
                sqlQuerySpec,
                It.IsAny<string>(),
                It.IsAny<PartitionKeyRangeIdentity>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<SchedulingStopwatch>(),
                cancellationTokenSource.Token)).Returns(
                Task.FromResult(QueryResponseCore.CreateFailure(
                    statusCode: HttpStatusCode.InternalServerError,
                    subStatusCodes: null,
                    errorMessage: "Error message",
                    requestCharge: 10.2,
                    activityId: Guid.NewGuid().ToString(),
                    diagnostics: pageDiagnostics,
                    pipelineDiagnostics: null)));

            await itemProducerTree.BufferMoreDocumentsAsync(cancellationTokenSource.Token);

            // First item should be a success
            {
                (bool movedToNextPage, QueryResponseCore? failureResponse) = await itemProducerTree.TryMoveNextPageAsync(cancellationTokenSource.Token);
                Assert.IsTrue(movedToNextPage);
                Assert.IsNull(failureResponse);
                Assert.IsTrue(itemProducerTree.TryMoveNextDocumentWithinPage());
                Assert.IsFalse(itemProducerTree.TryMoveNextDocumentWithinPage());
                Assert.IsTrue(itemProducerTree.HasMoreResults);
            }

            // Second item should be a success
            {
                (bool movedToNextPage, QueryResponseCore? failureResponse) = await itemProducerTree.TryMoveNextPageAsync(cancellationTokenSource.Token);
                Assert.IsTrue(movedToNextPage);
                Assert.IsNull(failureResponse);
                Assert.IsTrue(itemProducerTree.TryMoveNextDocumentWithinPage());
                Assert.IsFalse(itemProducerTree.TryMoveNextDocumentWithinPage());
                Assert.IsTrue(itemProducerTree.HasMoreResults);
            }

            // Third item should be a failure
            {
                (bool movedToNextPage, QueryResponseCore? failureResponse) = await itemProducerTree.TryMoveNextPageAsync(cancellationTokenSource.Token);
                Assert.IsFalse(movedToNextPage);
                Assert.IsNotNull(failureResponse);
                Assert.IsFalse(itemProducerTree.HasMoreResults);
            }

            // Try to buffer after failure. It should return the previous cached failure and not try to buffer again.
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(
                sqlQuerySpec,
                It.IsAny<string>(),
                It.IsAny<PartitionKeyRangeIdentity>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                It.IsAny<SchedulingStopwatch>(),
                cancellationTokenSource.Token)).
                Throws(new Exception("Previous buffer failed. Operation should return original failure and not try again"));

            await itemProducerTree.BufferMoreDocumentsAsync(cancellationTokenSource.Token);
            Assert.IsFalse(itemProducerTree.HasMoreResults);
        }
    }
}