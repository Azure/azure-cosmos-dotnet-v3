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
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Aggregate;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionComponent.Distinct;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.OrderBy;
    using Microsoft.Azure.Cosmos.Query.Core.ExecutionContext.Parallel;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Query.Core.QueryPlan;
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
                string fullContinuationToken = null;
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

                    fullContinuationToken = CosmosArray.Create(
                        new List<CosmosElement>()
                        {
                            CompositeContinuationToken.ToCosmosElement(compositeContinuation)
                        }).ToString();
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
                    maxBufferedItemCount: null,
                    returnResultsInDeterministicOrder: true,
                    testSettings: new TestInjections(simulate429s: false, simulateEmptyPages: false));

                IDocumentQueryExecutionComponent executionContext = (await CosmosParallelItemQueryExecutionContext.TryCreateAsync(
                    context,
                    initParams,
                    fullContinuationToken != null ? CosmosElement.Parse(fullContinuationToken) : null,
                    this.cancellationToken)).Result;

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
                string fullContinuationToken = null;
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

                    fullContinuationToken = CosmosArray.Create(
                        new List<CosmosElement>()
                        {
                            CompositeContinuationToken.ToCosmosElement(compositeContinuation)
                        }).ToString();
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
                    maxBufferedItemCount: null,
                    returnResultsInDeterministicOrder: true,
                    testSettings: new TestInjections(simulate429s: false, simulateEmptyPages: false));

                IDocumentQueryExecutionComponent executionContext = (await CosmosParallelItemQueryExecutionContext.TryCreateAsync(
                    context,
                    initParams,
                    fullContinuationToken != null ? CosmosElement.Parse(fullContinuationToken) : null,
                    this.cancellationToken)).Result;

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
                Assert.IsNotNull(failure.Value.CosmosException.ToString());

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
                string fullContinuationToken = null;
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
                        compositeContinuationToken: compositeContinuation,
                        orderByItems: orderByItems,
                        rid: itemToRepresentPreviousQuery._rid,
                        skipCount: 0,
                        filter: null);

                    fullContinuationToken = CosmosArray.Create(
                        new List<CosmosElement>()
                        {
                            OrderByContinuationToken.ToCosmosElement(orderByContinuationToken)
                        }).ToString();
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
                    maxBufferedItemCount: null,
                    returnResultsInDeterministicOrder: true,
                    testSettings: new TestInjections(simulate429s: false, simulateEmptyPages: false));

                IDocumentQueryExecutionComponent executionContext = (await CosmosOrderByItemQueryExecutionContext.TryCreateAsync(
                    context,
                    initParams,
                    fullContinuationToken != null ? CosmosElement.Parse(fullContinuationToken) : null,
                    this.cancellationToken)).Result;

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
                string fullContinuationToken = null;
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
                        compositeContinuationToken: compositeContinuation,
                        orderByItems: orderByItems,
                        rid: itemToRepresentPreviousQuery._rid,
                        skipCount: 0,
                        filter: null);

                    fullContinuationToken = CosmosArray.Create(
                        new List<CosmosElement>()
                        {
                            OrderByContinuationToken.ToCosmosElement(orderByContinuationToken)
                        }).ToString();
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
                    maxBufferedItemCount: null,
                    returnResultsInDeterministicOrder: true,
                    testSettings: new TestInjections(simulate429s: false, simulateEmptyPages: false));

                TryCatch<IDocumentQueryExecutionComponent> tryCreate = await CosmosOrderByItemQueryExecutionContext.TryCreateAsync(
                    context,
                    initParams,
                    fullContinuationToken != null ? CosmosElement.Parse(fullContinuationToken) : null,
                    this.cancellationToken);

                if (tryCreate.Succeeded)
                {
                    IDocumentQueryExecutionComponent executionContext = tryCreate.Result;

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
                    Assert.IsNotNull(failure.Value.CosmosException.ToString());

                    Assert.AreEqual(0 /*We don't get any items, since we don't buffer the failure anymore*/, itemsRead.Count);

                    //CollectionAssert.AreEqual(allItems.ToList(), itemsRead, new ToDoItemComparer());
                }
                else
                {
                    QueryResponseCore queryResponseCore = QueryResponseFactory.CreateFromException(tryCreate.Exception);
                    Assert.AreEqual((HttpStatusCode)429, queryResponseCore.StatusCode);
                }
            }
        }

        [TestMethod]
        public async Task TestNegativeAggreateComponentCreation()
        {
            TryCatch<IDocumentQueryExecutionComponent> tryCreateWhenSourceFails = await AggregateDocumentQueryExecutionComponent.TryCreateAsync(
                ExecutionEnvironment.Client,
                new AggregateOperator[] { },
                new Dictionary<string, AggregateOperator?>(),
                new string[] { },
                false,
                null,
                FailToCreateSource);

            Assert.IsFalse(tryCreateWhenSourceFails.Succeeded);

            TryCatch<IDocumentQueryExecutionComponent> tryCreateWhenInvalidContinuationToken = await AggregateDocumentQueryExecutionComponent.TryCreateAsync(
                ExecutionEnvironment.Client,
                new AggregateOperator[] { },
                new Dictionary<string, AggregateOperator?>(),
                new string[] { },
                false,
                null,
                FailToCreateSource);

            Assert.IsFalse(tryCreateWhenInvalidContinuationToken.Succeeded);
        }

        [TestMethod]
        public async Task TestNegativeDistinctComponentCreation()
        {
            TryCatch<IDocumentQueryExecutionComponent> tryCreateWhenSourceFails = await DistinctDocumentQueryExecutionComponent.TryCreateAsync(
                ExecutionEnvironment.Client,
                null,
                FailToCreateSource,
                DistinctQueryType.Ordered);

            Assert.IsFalse(tryCreateWhenSourceFails.Succeeded);

            TryCatch<IDocumentQueryExecutionComponent> tryCreateWhenInvalidContinuationToken = await DistinctDocumentQueryExecutionComponent.TryCreateAsync(
                ExecutionEnvironment.Client,
                CosmosElement.Parse("This is not a valid continuation token"),
                CreateSource,
                DistinctQueryType.Unordered);

            Assert.IsFalse(tryCreateWhenInvalidContinuationToken.Succeeded);
        }

        // ADD MORE TESTS HERE

        private static Task<TryCatch<IDocumentQueryExecutionComponent>> FailToCreateSource(CosmosElement continuationToken)
        {
            return Task.FromResult(TryCatch<IDocumentQueryExecutionComponent>.FromException(new Exception()));
        }

        private static Task<TryCatch<IDocumentQueryExecutionComponent>> CreateSource(CosmosElement continuationToken)
        {
            return Task.FromResult(TryCatch<IDocumentQueryExecutionComponent>.FromResult(new Mock<IDocumentQueryExecutionComponent>().Object));
        }
    }
}
