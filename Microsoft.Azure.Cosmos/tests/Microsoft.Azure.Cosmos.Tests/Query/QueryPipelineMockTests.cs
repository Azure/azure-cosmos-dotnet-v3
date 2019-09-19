//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;

    [TestClass]
    public class QueryPipelineMockTests
    {
        private CancellationToken cancellationToken = new CancellationTokenSource().Token;

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TestCosmosCrossPartitionQueryExecutionContextWithEmptyPagesAndSplitAsync(bool createInitialContinuationToken)
        {
            int maxPageSize = 5;

            List<MockPartitionResponse[]> mockResponsesScenario = MockQueryFactory.GetSplitScenarios();
            foreach (MockPartitionResponse[] mockResponse in mockResponsesScenario)
            {
                string initialContinuationToken = null;
                string fullConitnuationToken = null;
                if (createInitialContinuationToken)
                {
                    initialContinuationToken = " - RID:02FYAIvUH1kCAAAAAAAAAA ==#RT:1#TRC:1";
                    CompositeContinuationToken compositeContinuation = new CompositeContinuationToken()
                    {
                        Range = new Documents.Routing.Range<string>(
                            min: MockQueryFactory.DefaultPartitionKeyRange.MinInclusive,
                            max: MockQueryFactory.DefaultPartitionKeyRange.MaxExclusive,
                            isMaxInclusive: false,
                            isMinInclusive: true),
                        Token = initialContinuationToken
                    };

                    fullConitnuationToken = JsonConvert.SerializeObject(new CompositeContinuationToken[] { compositeContinuation });
                }

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

                CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                    sqlQuerySpec: MockQueryFactory.DefaultQuerySpec,
                    collectionRid: MockQueryFactory.DefaultCollectionRid,
                    partitionedQueryExecutionInfo: new PartitionedQueryExecutionInfo() { QueryInfo = new QueryInfo() },
                    partitionKeyRanges: new List<PartitionKeyRange>() { MockQueryFactory.DefaultPartitionKeyRange },
                    initialPageSize: maxPageSize,
                    maxConcurrency: null,
                    maxItemCount: maxPageSize,
                    maxBufferedItemCount: null);

                CosmosParallelItemQueryExecutionContext executionContext = await CosmosParallelItemQueryExecutionContext.CreateAsync(
                    context,
                    initParams,
                    fullConitnuationToken,
                    this.cancellationToken);

                // Read all the pages from both splits
                List<ToDoItem> itemsRead = new List<ToDoItem>();
                Assert.IsTrue(!executionContext.IsDone);

                while (!executionContext.IsDone)
                {
                    QueryResponseCore queryResponse = await executionContext.DrainAsync(maxPageSize, this.cancellationToken);
                    string responseContinuationToken = queryResponse.ContinuationToken;
                    foreach (CosmosElement element in queryResponse.CosmosElements)
                    {
                        string jsonValue = element.ToString();
                        ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                        itemsRead.Add(item);
                    }
                }

                Assert.AreEqual(allItems.Count, itemsRead.Count);
                List<ToDoItem> exepected = allItems.OrderBy(x => x.id).ToList();
                List<ToDoItem> actual = itemsRead.OrderBy(x => x.id).ToList();

                CollectionAssert.AreEqual(exepected, actual, new ToDoItemComparer());
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TestCosmosOrderByQueryExecutionContextWithEmptyPagesAndSplitAsync(bool createInitialContinuationToken)
        {
            int maxPageSize = 5;

            List<MockPartitionResponse[]> mockResponsesScenario = MockQueryFactory.GetSplitScenarios();

            foreach (MockPartitionResponse[] mockResponse in mockResponsesScenario)
            {
                string initialContinuationToken = null;
                string fullConitnuationToken = null;
                if (createInitialContinuationToken)
                {
                    ToDoItem itemToRepresentPreviousQuery = ToDoItem.CreateItems(
                        1,
                        "itemToRepresentPreviousQuery",
                        MockQueryFactory.DefaultCollectionRid).First();

                    initialContinuationToken = $" - RID:{itemToRepresentPreviousQuery._rid} ==#RT:1#TRC:1";
                    CompositeContinuationToken compositeContinuation = new CompositeContinuationToken()
                    {
                        Range = new Documents.Routing.Range<string>(
                            min: MockQueryFactory.DefaultPartitionKeyRange.MinInclusive,
                            max: MockQueryFactory.DefaultPartitionKeyRange.MaxExclusive,
                            isMaxInclusive: false,
                            isMinInclusive: true),
                        Token = initialContinuationToken
                    };

                    List<OrderByItem> orderByItems = new List<OrderByItem>()
                    {
                        new OrderByItem(CosmosObject.Create(Encoding.UTF8.GetBytes("{\"item\":\"2c4ce711-13c3-4c93-817c-49287b71b6c3\"}")))
                    };

                    OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                        compositeContinuationToken: compositeContinuation,
                        orderByItems: orderByItems,
                        rid: itemToRepresentPreviousQuery._rid,
                        skipCount: 0,
                        filter: null);

                    fullConitnuationToken = JsonConvert.SerializeObject(new OrderByContinuationToken[] { orderByContinuationToken });
                }

                Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
                IList<ToDoItem> allItems = MockQueryFactory.GenerateAndMockResponse(
                    mockQueryClient,
                    isOrderByQuery: true,
                    sqlQuerySpec: MockQueryFactory.DefaultQuerySpec,
                    containerRid: MockQueryFactory.DefaultCollectionRid,
                    initContinuationToken: initialContinuationToken,
                    maxPageSize: maxPageSize,
                    mockResponseForSinglePartition: mockResponse,
                    cancellationTokenForMocks: this.cancellationToken);

                // Order by drains the partitions until it finds an item
                // If there are no items then it's not possible to have a continuation token
                if(allItems.Count == 0 && createInitialContinuationToken)
                {
                    continue;
                }

                CosmosQueryContext context = MockQueryFactory.CreateContext(
                    mockQueryClient.Object);

                QueryInfo queryInfo = new QueryInfo()
                {
                    OrderBy = new SortOrder[] { SortOrder.Ascending },
                    OrderByExpressions = new string[] { "id" }
                };

                CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                    sqlQuerySpec: MockQueryFactory.DefaultQuerySpec,
                    collectionRid: MockQueryFactory.DefaultCollectionRid,
                    partitionedQueryExecutionInfo: new PartitionedQueryExecutionInfo() { QueryInfo = queryInfo },
                    partitionKeyRanges: new List<PartitionKeyRange>() { MockQueryFactory.DefaultPartitionKeyRange },
                    initialPageSize: maxPageSize,
                    maxConcurrency: null,
                    maxItemCount: maxPageSize,
                    maxBufferedItemCount: null);

                CosmosOrderByItemQueryExecutionContext executionContext = await CosmosOrderByItemQueryExecutionContext.CreateAsync(
                    context,
                    initParams,
                    fullConitnuationToken,
                    this.cancellationToken);

                // For order by it will drain all the pages till it gets a value.
                if (allItems.Count == 0)
                {
                    Assert.IsTrue(executionContext.IsDone);
                    continue;
                }

                Assert.IsTrue(!executionContext.IsDone);

                // Read all the pages from both splits
                List<ToDoItem> itemsRead = new List<ToDoItem>();
                while (!executionContext.IsDone)
                {
                    QueryResponseCore queryResponse = await executionContext.DrainAsync(
                        maxPageSize,
                        this.cancellationToken);

                    string responseContinuationToken = queryResponse.ContinuationToken;
                    foreach (CosmosElement element in queryResponse.CosmosElements)
                    {
                        string jsonValue = element.ToString();
                        ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                        itemsRead.Add(item);
                    }
                }

                Assert.AreEqual(allItems.Count, itemsRead.Count);

                CollectionAssert.AreEqual(allItems.ToList(), itemsRead, new ToDoItemComparer());
            }
        }
    }
}
