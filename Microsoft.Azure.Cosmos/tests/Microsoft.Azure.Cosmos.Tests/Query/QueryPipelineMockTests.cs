//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
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
        private readonly CancellationToken cancellationToken = new CancellationTokenSource().Token;

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
        public async Task TestCosmosCrossPartitionQueryExecutionContextWithFailuresAsync(bool createInitialContinuationToken)
        {
            int maxPageSize = 5;

            List<MockPartitionResponse[]> mockResponsesScenario = MockQueryFactory.GetFailureScenarios();
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

                QueryResponseCore? failure = null;
                while (!executionContext.IsDone)
                {
                    QueryResponseCore queryResponse = await executionContext.DrainAsync(maxPageSize, this.cancellationToken);
                    if (queryResponse.IsSuccess)
                    {
                        string responseContinuationToken = queryResponse.ContinuationToken;
                        foreach (CosmosElement element in queryResponse.CosmosElements)
                        {
                            string jsonValue = element.ToString();
                            ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                            itemsRead.Add(item);
                        }
                    }
                    else
                    {
                        Assert.IsNull(failure, "There should only be one error");
                        failure = queryResponse;
                    }
                }

                Assert.IsNotNull(failure);
                Assert.AreEqual((HttpStatusCode)429, failure.Value.StatusCode);
                Assert.IsNull(failure.Value.ErrorMessage);

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

            Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
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
                        new OrderByItem(CosmosObject.CreateFromBuffer(Encoding.UTF8.GetBytes("{\"item\":\"2c4ce711-13c3-4c93-817c-49287b71b6c3\"}")))
                    };

                    OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                        queryClient: mockQueryClient.Object,
                        compositeContinuationToken: compositeContinuation,
                        orderByItems: orderByItems,
                        rid: itemToRepresentPreviousQuery._rid,
                        skipCount: 0,
                        filter: null);

                    fullConitnuationToken = JsonConvert.SerializeObject(new OrderByContinuationToken[] { orderByContinuationToken });
                }


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
                if (allItems.Count == 0 && createInitialContinuationToken)
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

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task TestCosmosOrderByQueryExecutionContextWithFailurePageAsync(bool createInitialContinuationToken)
        {
            int maxPageSize = 5;

            List<MockPartitionResponse[]> mockResponsesScenario = MockQueryFactory.GetFailureScenarios();

            Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
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
                        new OrderByItem(CosmosObject.CreateFromBuffer(Encoding.UTF8.GetBytes("{\"item\":\"2c4ce711-13c3-4c93-817c-49287b71b6c3\"}")))
                    };

                    OrderByContinuationToken orderByContinuationToken = new OrderByContinuationToken(
                        queryClient: mockQueryClient.Object,
                        compositeContinuationToken: compositeContinuation,
                        orderByItems: orderByItems,
                        rid: itemToRepresentPreviousQuery._rid,
                        skipCount: 0,
                        filter: null);

                    fullConitnuationToken = JsonConvert.SerializeObject(new OrderByContinuationToken[] { orderByContinuationToken });
                }


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
                if (allItems.Count == 0 && createInitialContinuationToken)
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

                Assert.IsTrue(!executionContext.IsDone);

                // Read all the pages from both splits
                List<ToDoItem> itemsRead = new List<ToDoItem>();
                QueryResponseCore? failure = null;
                while (!executionContext.IsDone)
                {
                    QueryResponseCore queryResponse = await executionContext.DrainAsync(
                        maxPageSize,
                        this.cancellationToken);
                    if (queryResponse.IsSuccess)
                    {
                        string responseContinuationToken = queryResponse.ContinuationToken;
                        foreach (CosmosElement element in queryResponse.CosmosElements)
                        {
                            string jsonValue = element.ToString();
                            ToDoItem item = JsonConvert.DeserializeObject<ToDoItem>(jsonValue);
                            itemsRead.Add(item);
                        }
                    }
                    else
                    {
                        Assert.IsNull(failure, "There should only be one error");
                        failure = queryResponse;
                    }
                }

                Assert.IsNotNull(failure);
                Assert.AreEqual((HttpStatusCode)429, failure.Value.StatusCode);
                Assert.IsNull(failure.Value.ErrorMessage);

                Assert.AreEqual(allItems.Count, itemsRead.Count);

                CollectionAssert.AreEqual(allItems.ToList(), itemsRead, new ToDoItemComparer());
            }
        }

        [TestMethod]
        public async Task TestQueryDrainsExactItemCountAsync()
        {
            int maxPageSize = 5;
            Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
            MockPartitionResponse[] mockResponse = MockQueryFactory.CreateDefaultResponse(
                new List<int[]>()
                {
                    new int[] { 0, 2, 4, 6, 8 },
                    MockQueryFactory.EmptyPage,
                    new int[] { 1, 3, 5, 7, 9 },
                    MockQueryFactory.EmptyPage,
                    new int[] { 10, 11 },
                    MockQueryFactory.EmptyPage,
                    new int[] { 12, 13, 14 },
                    MockQueryFactory.EmptyPage,
                    new int[] { 15 }
                });
            IList<ToDoItem> allItems = MockQueryFactory.GenerateAndMockResponse(
                mockQueryClient,
                isOrderByQuery: true,
                sqlQuerySpec: MockQueryFactory.DefaultQuerySpec,
                containerRid: MockQueryFactory.DefaultCollectionRid,
                initContinuationToken: null,
                maxPageSize: maxPageSize,
                mockResponseForSinglePartition: mockResponse,
                cancellationTokenForMocks: default(CancellationToken));

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

            CosmosOrderByItemQueryExecutionContext orderByExecutionContext = await CosmosOrderByItemQueryExecutionContext.CreateAsync(
                context,
                initParams,
                null,
                default(CancellationToken));

            initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                sqlQuerySpec: MockQueryFactory.DefaultQuerySpec,
                collectionRid: MockQueryFactory.DefaultCollectionRid,
                partitionedQueryExecutionInfo: new PartitionedQueryExecutionInfo()
                {
                    QueryInfo = new QueryInfo()
                    {
                    }
                },
                partitionKeyRanges: new List<PartitionKeyRange>() { MockQueryFactory.DefaultPartitionKeyRange },
                initialPageSize: maxPageSize,
                maxConcurrency: null,
                maxItemCount: maxPageSize,
                maxBufferedItemCount: null);

            CosmosParallelItemQueryExecutionContext parallelExecutionContext = await CosmosParallelItemQueryExecutionContext.CreateAsync(
                context,
                initParams,
                null,
                default(CancellationToken));

            foreach (CosmosCrossPartitionQueryExecutionContext crossPartitionContext in new CosmosCrossPartitionQueryExecutionContext[]
            {
                    orderByExecutionContext,
                    parallelExecutionContext
            })
            {
                Assert.IsTrue(!crossPartitionContext.IsDone);
                while (!crossPartitionContext.IsDone)
                {
                    QueryResponseCore queryResponse = await crossPartitionContext.DrainAsync(
                        maxPageSize,
                        default(CancellationToken));
                    Assert.IsTrue(queryResponse.IsSuccess);
                    if (!crossPartitionContext.IsDone)
                    {
                        // Query is not done so we expect an exact item count
                        Assert.AreEqual(maxPageSize, queryResponse.CosmosElements.Count);
                    }
                    else
                    {
                        // Query is done so the last page can be partially full
                        Assert.IsTrue(queryResponse.CosmosElements.Count <= maxPageSize);
                    }
                }
            }
        }
    }
}
