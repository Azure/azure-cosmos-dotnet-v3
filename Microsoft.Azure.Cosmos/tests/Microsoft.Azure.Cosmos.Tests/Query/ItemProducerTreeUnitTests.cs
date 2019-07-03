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
        [DataRow(0, 5)]
        [DataRow(1, 5)]
        [DataRow(2, 2)]
        [DataRow(2, 5)]
        public async Task TestMoveNextAsync(int pageSize, int maxPageSize)
        {
            List<int[]> combinations = new List<int[]>()
            {
                // Create all combination with empty pages
                new int[] { pageSize },
                new int[] { pageSize, 0 },
                new int[] { 0, pageSize },
                new int[] { pageSize, pageSize },
                new int[] { pageSize, 0, 0 },
                new int[] {0, pageSize, 0 },
                new int[] {0, 0, pageSize },
                new int[] { pageSize, 0, pageSize },
                new int[] { pageSize, pageSize, pageSize },
            };

            foreach (int[] combination in combinations)
            {
                (ItemProducerTree itemProducerTree, ReadOnlyCollection<ToDoItem> allItems) itemFactory = MockItemProducerFactory.CreateTree(
                    responseMessagesPageSize: combination,
                    maxPageSize: maxPageSize,
                    cancellationToken: this.cancellationToken);

                ItemProducerTree itemProducerTree = itemFactory.itemProducerTree;

                List<ToDoItem> itemsRead = new List<ToDoItem>();

                Assert.IsTrue(itemProducerTree.HasMoreResults);

                while ((await itemProducerTree.MoveNextAsync(this.cancellationToken)).successfullyMovedNext)
                {
                    Assert.IsTrue(itemProducerTree.HasMoreResults);
                    string jsonValue = itemProducerTree.Current.ToString();
                    ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                    itemsRead.Add(item);
                }

                Assert.IsFalse(itemProducerTree.HasMoreResults);

                Assert.AreEqual(itemFactory.allItems.Count, itemsRead.Count);

                CollectionAssert.AreEqual(itemsRead, itemFactory.allItems, new ToDoItemComparer());
            }
        }

        [TestMethod]
        public async Task TestSplitAsync()
        {
            int maxPageSize = 10;
            const string collectionRid = "MockTestSplitContainerRid";
            SqlQuerySpec sqlQuerySpec = MockItemProducerFactory.DefaultQuerySpec;

            // pkRange1 is the original range, newPkRange2/newPkRange3 are the ranges after the split
            PartitionKeyRange pkRange1 = new PartitionKeyRange() { Id = "0", MinInclusive = "", MaxExclusive = "FF" };
            PartitionKeyRange newPkRange2 = new PartitionKeyRange() { Id = "1", MinInclusive = "", MaxExclusive = "BB" };
            PartitionKeyRange newPkRange3 = new PartitionKeyRange() { Id = "2", MinInclusive = "BB", MaxExclusive = "FF" };
            IReadOnlyList<PartitionKeyRange> newPkRanges = new List<PartitionKeyRange>()
            {
                newPkRange2,
                newPkRange3,
            };

            // Setup the necessary partition key range update calls
            Mock<IRoutingMapProvider> mockRoutingMap = new Mock<IRoutingMapProvider>();
            mockRoutingMap.Setup(x =>
                x.TryGetOverlappingRangesAsync(
                    collectionRid,
                    pkRange1.ToRange(),
                    true)).Returns(Task.FromResult(newPkRanges));

            Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
            mockQueryClient.Setup(x => x.GetRoutingMapProviderAsync()).Returns(Task.FromResult(mockRoutingMap.Object));

            Mock<CosmosQueryContext> mockContext = new Mock<CosmosQueryContext>();
            mockContext.Setup(x => x.QueryClient).Returns(mockQueryClient.Object);
            List<ToDoItem> allMockedToDoItems = new List<ToDoItem>();


            // Setup the mocks for the new partitions ranges
            List<ToDoItem> itemsFromPkRange2 = MockItemProducerFactory.MockSinglePartitionKeyRangeContext(
                mockContext,
                responseMessagesPageSize: new int[] { 1 },
                sqlQuerySpec: sqlQuerySpec,
                partitionKeyRange: newPkRange2,
                continuationToken: null,
                maxPageSize: maxPageSize,
                collectionRid: collectionRid,
                responseDelay: null,
                cancellationToken: cancellationToken);
            

            List<ToDoItem> itemsFromPkRange3 = MockItemProducerFactory.MockSinglePartitionKeyRangeContext(
                 mockContext,
                 responseMessagesPageSize: new int[] { 1 },
                 sqlQuerySpec: sqlQuerySpec,
                 partitionKeyRange: newPkRange3,
                 continuationToken: null,
                 maxPageSize: maxPageSize,
                 collectionRid: collectionRid,
                 responseDelay: null,
                 cancellationToken: cancellationToken);
           

            // Setup the item producer tree with a split response
            (ItemProducerTree itemProducerTree, ReadOnlyCollection<ToDoItem> allItems) itemFactory = MockItemProducerFactory.CreateTree(
                mockQueryContext: mockContext,
                responseMessagesPageSize: new int[] { 2, QueryResponseMessageFactory.SPLIT },
                sqlQuerySpec: sqlQuerySpec,
                partitionKeyRange: pkRange1,
                collectionRid: collectionRid,
                maxPageSize: maxPageSize,
                deferFirstPage: true,
                cancellationToken: this.cancellationToken);

            allMockedToDoItems.AddRange(itemFactory.allItems);
            allMockedToDoItems.AddRange(itemsFromPkRange2);
            allMockedToDoItems.AddRange(itemsFromPkRange3);

            ItemProducerTree itemProducerTree = itemFactory.itemProducerTree;

            // Read all the pages from both splits
            List<ToDoItem> itemsRead = new List<ToDoItem>();
            Assert.IsTrue(itemProducerTree.HasMoreResults);

            while ((await itemProducerTree.MoveNextAsync(this.cancellationToken)).successfullyMovedNext)
            {
                Assert.IsTrue(itemProducerTree.HasMoreResults);
                if (itemProducerTree.Current != null)
                {
                    string jsonValue = itemProducerTree.Current.ToString();
                    ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                    itemsRead.Add(item);
                }
            }

            Assert.IsFalse(itemProducerTree.HasMoreResults);

            Assert.AreEqual(allMockedToDoItems.Count, itemsRead.Count);

            CollectionAssert.AreEqual(itemsRead, allMockedToDoItems, new ToDoItemComparer());
        }


        [TestMethod]
        public async Task TestSplitWithExecutionContextAsync()
        {
            int maxPageSize = 10;
            const string collectionRid = "MockTestSplitContainerRid";
            SqlQuerySpec sqlQuerySpec = MockItemProducerFactory.DefaultQuerySpec;

            // pkRange1 is the original range, newPkRange2/newPkRange3 are the ranges after the split
            PartitionKeyRange pkRange1 = new PartitionKeyRange() { Id = "0", MinInclusive = "", MaxExclusive = "FF" };
            PartitionKeyRange newPkRange2 = new PartitionKeyRange() { Id = "1", MinInclusive = "", MaxExclusive = "BB" };
            PartitionKeyRange newPkRange3 = new PartitionKeyRange() { Id = "2", MinInclusive = "BB", MaxExclusive = "FF" };
            IReadOnlyList<PartitionKeyRange> newPkRanges = new List<PartitionKeyRange>()
            {
                newPkRange2,
                newPkRange3,
            };

            // Setup the necessary partition key range update calls
            Mock<IRoutingMapProvider> mockRoutingMap = new Mock<IRoutingMapProvider>();
            mockRoutingMap.Setup(x =>
                x.TryGetOverlappingRangesAsync(
                    collectionRid,
                    pkRange1.ToRange(),
                    true)).Returns(Task.FromResult(newPkRanges));

            Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
            mockQueryClient.Setup(x => x.GetRoutingMapProviderAsync()).Returns(Task.FromResult(mockRoutingMap.Object));

            Mock<CosmosQueryContext> mockContext = new Mock<CosmosQueryContext>();
            mockContext.Setup(x => x.QueryClient).Returns(mockQueryClient.Object);
            mockContext.Setup(x => x.ContainerResourceId).Returns(collectionRid);
            mockContext.Setup(x => x.SqlQuerySpec).Returns(sqlQuerySpec);

            QueryRequestOptions requestOptions = new QueryRequestOptions();
            mockContext.Setup(x => x.QueryRequestOptions).Returns(requestOptions);
            List<ToDoItem> allMockedToDoItems = new List<ToDoItem>();


            // Setup the mocks for the new partitions ranges
            List<ToDoItem> itemsFromPkRange1 = MockItemProducerFactory.MockSinglePartitionKeyRangeContext(
                 mockContext,
                 responseMessagesPageSize: new int[] { 2, QueryResponseMessageFactory.SPLIT },
                 sqlQuerySpec: sqlQuerySpec,
                 partitionKeyRange: pkRange1,
                 continuationToken: null,
                 maxPageSize: maxPageSize,
                 collectionRid: collectionRid,
                 responseDelay: null,
                 cancellationToken: cancellationToken);
            allMockedToDoItems.AddRange(itemsFromPkRange1);

            List<ToDoItem> itemsFromPkRange2 = MockItemProducerFactory.MockSinglePartitionKeyRangeContext(
                mockContext,
                responseMessagesPageSize: new int[] { 1 },
                sqlQuerySpec: sqlQuerySpec,
                partitionKeyRange: newPkRange2,
                continuationToken: null,
                maxPageSize: maxPageSize,
                collectionRid: collectionRid,
                responseDelay: null,
                cancellationToken: cancellationToken);
            allMockedToDoItems.AddRange(itemsFromPkRange2);

            List<ToDoItem> itemsFromPkRange3 = MockItemProducerFactory.MockSinglePartitionKeyRangeContext(
                 mockContext,
                 responseMessagesPageSize: new int[] { 1 },
                 sqlQuerySpec: sqlQuerySpec,
                 partitionKeyRange: newPkRange3,
                 continuationToken: null,
                 maxPageSize: maxPageSize,
                 collectionRid: collectionRid,
                 responseDelay: null,
                 cancellationToken: cancellationToken);
            allMockedToDoItems.AddRange(itemsFromPkRange3);

            CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                    collectionRid,
                    new PartitionedQueryExecutionInfo() { QueryInfo = new QueryInfo() },
                    new List<PartitionKeyRange>() { pkRange1 },
                    maxPageSize,
                    null);

            CosmosParallelItemQueryExecutionContext executionContext = await CosmosParallelItemQueryExecutionContext.CreateAsync(
                mockContext.Object,
                initParams,
                cancellationToken);

            // Read all the pages from both splits
            List<ToDoItem> itemsRead = new List<ToDoItem>();
            Assert.IsTrue(!executionContext.IsDone);

            while (!executionContext.IsDone)
            {
                QueryResponse queryResponse = await executionContext.DrainAsync(maxPageSize, cancellationToken);

                foreach (CosmosElement element in queryResponse.CosmosElements)
                {
                    string jsonValue = element.ToString();
                    ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                    itemsRead.Add(item);
                }
            }

            Assert.AreEqual(allMockedToDoItems.Count, itemsRead.Count);

            CollectionAssert.AreEqual(itemsRead, allMockedToDoItems, new ToDoItemComparer());
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
