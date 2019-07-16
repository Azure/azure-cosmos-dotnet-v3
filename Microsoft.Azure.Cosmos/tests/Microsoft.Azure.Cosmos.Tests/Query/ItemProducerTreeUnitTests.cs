//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Query.ExecutionComponent;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Query;
    using Moq;
    using System.Threading;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using System.Collections.ObjectModel;
    using Newtonsoft.Json;
    using Microsoft.Azure.Cosmos.Routing;

    [TestClass]
    public class ItemProducerTreeUnitTests
    {
        private CancellationToken cancellationToken = new CancellationTokenSource().Token;

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
                    cancellationTokenForMocks: cancellationToken);

                CosmosQueryContext context = MockQueryFactory.CreateContext(
                    mockQueryClient.Object);

                ItemProducerTree itemProducerTree = new ItemProducerTree(
                   queryContext: context,
                   querySpecForInit: MockQueryFactory.DefaultQuerySpec,
                   partitionKeyRange: mockResponse[0].PartitionKeyRange,
                   produceAsyncCompleteCallback: MockItemProducerFactory.DefaultTreeProduceAsyncCompleteDelegate,
                   itemProducerTreeComparer: new ParallelItemProducerTreeComparer(),
                   equalityComparer: CosmosElementEqualityComparer.Value,
                   deferFirstPage: true,
                   collectionRid: MockQueryFactory.DefaultCollectionRid,
                   initialPageSize: maxPageSize,
                   initialContinuationToken: initialContinuationToken);

                Assert.IsTrue(itemProducerTree.HasMoreResults);

                List<ToDoItem> itemsRead = new List<ToDoItem>();
                while ((await itemProducerTree.MoveNextAsync(this.cancellationToken)).successfullyMovedNext)
                {
                    Assert.IsTrue(itemProducerTree.HasMoreResults);
                    string jsonValue = itemProducerTree.Current.ToString();
                    ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                    itemsRead.Add(item);
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
                    cancellationTokenForMocks: cancellationToken);

                CosmosQueryContext context = MockQueryFactory.CreateContext(
                    mockQueryClient.Object);

                ItemProducerTree itemProducerTree = new ItemProducerTree(
                   context,
                   MockQueryFactory.DefaultQuerySpec,
                   mockResponse[0].PartitionKeyRange,
                   MockItemProducerFactory.DefaultTreeProduceAsyncCompleteDelegate,
                   new ParallelItemProducerTreeComparer(),
                   CosmosElementEqualityComparer.Value,
                   true,
                   MockQueryFactory.DefaultCollectionRid,
                   maxPageSize,
                   initialContinuationToken: initialContinuationToken);

                Assert.IsTrue(itemProducerTree.HasMoreResults);

                List<ToDoItem> itemsRead = new List<ToDoItem>();
                while ((await itemProducerTree.MoveNextAsync(this.cancellationToken)).successfullyMovedNext)
                {
                    Assert.IsTrue(itemProducerTree.HasMoreResults);
                    if(itemProducerTree.Current != null)
                    {
                        string jsonValue = itemProducerTree.Current.ToString();
                        ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                        itemsRead.Add(item);
                    }
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
            ItemProducerTree.ProduceAsyncCompleteDelegate produceAsyncCompleteCallback = (
                ItemProducerTree producer,
                int itemsBuffered,
                double resourceUnitUsage,
                QueryMetrics queryMetrics,
                long responseLengthBytes,
                CancellationToken token) =>
            { callBackCount++; };

            Mock<IComparer<ItemProducerTree>> comparer = new Mock<IComparer<ItemProducerTree>>();
            Mock<IEqualityComparer<CosmosElement>> cosmosElementComparer = new Mock<IEqualityComparer<CosmosElement>>();
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            IEnumerable<CosmosElement> cosmosElements = new List<CosmosElement>()
            {
                new Mock<CosmosElement>(CosmosElementType.Object).Object
            };

            CosmosQueryResponseMessageHeaders headers = new CosmosQueryResponseMessageHeaders("TestToken", null, ResourceType.Document, "ContainerRid")
            {
                ActivityId = "AA470D71-6DEF-4D61-9A08-272D8C9ABCFE",
                RequestCharge = 42
            };

            mockQueryContext.Setup(x => x.ContainerResourceId).Returns("MockCollectionRid");
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(
                sqlQuerySpec,
                It.IsAny<string>(),
                It.IsAny<PartitionKeyRangeIdentity>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                cancellationTokenSource.Token)).Returns(
                Task.FromResult(QueryResponse.CreateSuccess(cosmosElements, 1, 500, headers)));

            ItemProducerTree itemProducerTree = new ItemProducerTree(
                queryContext: mockQueryContext.Object,
                querySpecForInit: sqlQuerySpec,
                partitionKeyRange: partitionKeyRange,
                produceAsyncCompleteCallback: produceAsyncCompleteCallback,
                itemProducerTreeComparer: comparer.Object,
                equalityComparer: cosmosElementComparer.Object,
                deferFirstPage: false,
                collectionRid: "collectionRid",
                initialContinuationToken: null,
                initialPageSize: 50);

            // Buffer to success responses
            await itemProducerTree.BufferMoreDocumentsAsync(cancellationTokenSource.Token);
            await itemProducerTree.BufferMoreDocumentsAsync(cancellationTokenSource.Token);

            // Buffer a failure
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(
                sqlQuerySpec,
                It.IsAny<string>(),
                It.IsAny<PartitionKeyRangeIdentity>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                cancellationTokenSource.Token)).Returns(
                Task.FromResult(QueryResponse.CreateFailure(headers, HttpStatusCode.InternalServerError, null, "Error message", null)));

            await itemProducerTree.BufferMoreDocumentsAsync(cancellationTokenSource.Token);

            // First item should be a success
            var result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsTrue(result.successfullyMovedNext);
            Assert.IsNull(result.failureResponse);
            Assert.IsTrue(itemProducerTree.HasMoreResults);

            // Second item should be a success
            result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsTrue(result.successfullyMovedNext);
            Assert.IsNull(result.failureResponse);
            Assert.IsTrue(itemProducerTree.HasMoreResults);

            // Third item should be a failure
            result = await itemProducerTree.MoveNextAsync(cancellationTokenSource.Token);
            Assert.IsFalse(result.successfullyMovedNext);
            Assert.IsNotNull(result.failureResponse);
            Assert.IsFalse(itemProducerTree.HasMoreResults);

            // Try to buffer after failure. It should return the previous cached failure and not try to buffer again.
            mockQueryContext.Setup(x => x.ExecuteQueryAsync(
                sqlQuerySpec,
                It.IsAny<string>(),
                It.IsAny<PartitionKeyRangeIdentity>(),
                It.IsAny<bool>(),
                It.IsAny<int>(),
                cancellationTokenSource.Token)).
                Throws(new Exception("Previous buffer failed. Operation should return original failure and not try again"));

            await itemProducerTree.BufferMoreDocumentsAsync(cancellationTokenSource.Token);
            Assert.IsFalse(result.successfullyMovedNext);
            Assert.IsNotNull(result.failureResponse);
            Assert.IsFalse(itemProducerTree.HasMoreResults);
        }
    }
}
