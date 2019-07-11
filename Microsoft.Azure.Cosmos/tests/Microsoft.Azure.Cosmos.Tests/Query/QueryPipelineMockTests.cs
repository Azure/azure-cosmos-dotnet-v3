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
    public class QueryPipelineMockTests
    {
        private CancellationToken cancellationToken = new CancellationTokenSource().Token;
        private CosmosSerializer cosmosSerializer = new CosmosJsonSerializerCore();

        [TestMethod]
        public async Task TestQueryPipelineSplitAsync()
        {
            int maxPageSize = 10;
            const string collectionRid = "UipSALL8vxE";
            Uri resourceLink = new Uri("dbs/test/colls/collTest", UriKind.Relative);
            SqlQuerySpec sqlQuerySpec = MockItemProducerFactory.DefaultQuerySpec;
            ContainerProperties containerProperties = ContainerProperties.CreateWithResourceId(collectionRid);
            containerProperties.Id = "SplitContainerId";
            containerProperties.PartitionKeyPath = "/pk";

            // pkRange1 is the original range, newPkRange2/newPkRange3 are the ranges after the split
            PartitionKeyRange pkRange0 = new PartitionKeyRange() { Id = "0", MinInclusive = "", MaxExclusive = "FF" };
            PartitionKeyRange newPkRange1 = new PartitionKeyRange() { Id = "1", MinInclusive = "", MaxExclusive = "BB" };
            PartitionKeyRange newPkRange2 = new PartitionKeyRange() { Id = "2", MinInclusive = "BB", MaxExclusive = "FF" };
            IReadOnlyList<PartitionKeyRange> newPkRanges = new List<PartitionKeyRange>()
            {
                newPkRange1,
                newPkRange2,
            };

            PartitionedQueryExecutionInfo partitionedQueryExecutionInfo = new PartitionedQueryExecutionInfo()
            {
                QueryInfo = new QueryInfo(),
                QueryRanges = new List<Microsoft.Azure.Documents.Routing.Range<string>>()
                {
                    new Microsoft.Azure.Documents.Routing.Range<string>(
                        min: pkRange0.MinInclusive,
                        max: pkRange0.MaxExclusive,
                        isMinInclusive: true,
                        isMaxInclusive: true)
                }
            };

            // Setup the necessary partition key range update calls
            Mock<IRoutingMapProvider> mockRoutingMap = new Mock<IRoutingMapProvider>();
            mockRoutingMap.Setup(x =>
                x.TryGetOverlappingRangesAsync(
                    collectionRid,
                    pkRange0.ToRange(),
                    true)).Returns(Task.FromResult(newPkRanges));

            Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
            mockQueryClient.Setup(x => x.GetRoutingMapProviderAsync()).Returns(Task.FromResult(mockRoutingMap.Object));
            mockQueryClient.Setup(x => x.GetCachedContainerPropertiesAsync(cancellationToken)).Returns(Task.FromResult(containerProperties));
            mockQueryClient.Setup(x => x.GetPartitionedQueryExecutionInfoAsync(
                sqlQuerySpec,
                containerProperties.PartitionKey,
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                cancellationToken)).Returns(Task.FromResult(partitionedQueryExecutionInfo));

            mockQueryClient.Setup(x => x.GetTargetPartitionKeyRangesAsync(
                resourceLink.OriginalString,
                collectionRid,
                partitionedQueryExecutionInfo.QueryRanges))
                .Returns(Task.FromResult(new List<PartitionKeyRange>() { pkRange0 }));

            Mock<CosmosQueryContext> mockContext = new Mock<CosmosQueryContext>();
            mockContext.Setup(x => x.QueryClient).Returns(mockQueryClient.Object);
            mockContext.Setup(x => x.ContainerResourceId).Returns(collectionRid);
            mockContext.Setup(x => x.SqlQuerySpec).Returns(sqlQuerySpec);

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = maxPageSize
            };
  
            mockContext.Setup(x => x.QueryRequestOptions).Returns(requestOptions);
            List<ToDoItem> allMockedToDoItems = new List<ToDoItem>();

            // Setup the mocks for the new partitions ranges
            List<ToDoItem> itemsFromPkRange1 = MockItemProducerFactory.MockSinglePartitionKeyRangeContext(
                 mockQueryClient,
                 responseMessagesPageSize: new int[] { 2, QueryResponseMessageFactory.SPLIT },
                 sqlQuerySpec: sqlQuerySpec,
                 partitionKeyRange: pkRange0,
                 continuationToken: null,
                 maxPageSize: maxPageSize,
                 collectionRid: collectionRid,
                 responseDelay: null,
                 cancellationToken: cancellationToken);
            allMockedToDoItems.AddRange(itemsFromPkRange1);

            List<ToDoItem> itemsFromPkRange2 = MockItemProducerFactory.MockSinglePartitionKeyRangeContext(
                mockQueryClient,
                responseMessagesPageSize: new int[] { 1 },
                sqlQuerySpec: sqlQuerySpec,
                partitionKeyRange: newPkRange1,
                continuationToken: null,
                maxPageSize: maxPageSize,
                collectionRid: collectionRid,
                responseDelay: null,
                cancellationToken: cancellationToken);
            allMockedToDoItems.AddRange(itemsFromPkRange2);

            List<ToDoItem> itemsFromPkRange3 = MockItemProducerFactory.MockSinglePartitionKeyRangeContext(
                 mockQueryClient,
                 responseMessagesPageSize: new int[] { 1 },
                 sqlQuerySpec: sqlQuerySpec,
                 partitionKeyRange: newPkRange2,
                 continuationToken: null,
                 maxPageSize: maxPageSize,
                 collectionRid: collectionRid,
                 responseDelay: null,
                 cancellationToken: cancellationToken);
            allMockedToDoItems.AddRange(itemsFromPkRange3);

            FeedIterator executionContext = new CosmosQueryExecutionContextFactory(
                client: mockQueryClient.Object,
                resourceTypeEnum: ResourceType.Document,
                operationType: OperationType.Query,
                resourceType: typeof(ToDoItem),
                sqlQuerySpec: sqlQuerySpec,
                continuationToken: null,
                queryRequestOptions: requestOptions,
                resourceLink: resourceLink,
                isContinuationExpected: true,
                allowNonValueAggregateQuery: true,
                correlatedActivityId: Guid.NewGuid());

            // Read all the pages from both splits
            List<ToDoItem> itemsRead = new List<ToDoItem>();
            Assert.IsTrue(executionContext.HasMoreResults);

            while (executionContext.HasMoreResults)
            {
                using (ResponseMessage response = await executionContext.ReadNextAsync(cancellationToken))
                {
                    Collection<ToDoItem> items = this.cosmosSerializer.FromStream<CosmosFeedResponseUtil<ToDoItem>>(response.Content).Data;
                    itemsRead.AddRange(items);
                }
            }

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
    }
}
