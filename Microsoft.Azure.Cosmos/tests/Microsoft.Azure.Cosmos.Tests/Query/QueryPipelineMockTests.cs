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
        [DataRow(null)]
        public async Task TestCosmosCrossPartitionQueryExecutionContextWithEmptyPagesAndSplitAsync(string initialContinuationToken)
        {
            int maxPageSize = 5;

            List<MockPartitionResponse[]> mockResponsesScenario = MockQueryFactory.GetSplitScenarios();
            foreach (MockPartitionResponse[] mockResponse in mockResponsesScenario)
            {
                Mock<CosmosQueryClient> mockQueryClient = new Mock<CosmosQueryClient>();
                IList<ToDoItem> allItems = MockQueryFactory.GenerateAndMockResponse(
                    mockQueryClient,
                    sqlQuerySpec: MockQueryFactory.DefaultQuerySpec,
                    containerRid: MockQueryFactory.DefaultCollectionRid,
                    initContinuationToken: initialContinuationToken,
                    maxPageSize: maxPageSize,
                    mockResponseForSinglePartition: mockResponse,
                    cancellationTokenForMocks: cancellationToken);

                CosmosQueryContext context = MockQueryFactory.CreateContext(
                    mockQueryClient.Object);

                CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams initParams = new CosmosCrossPartitionQueryExecutionContext.CrossPartitionInitParams(
                    MockQueryFactory.DefaultCollectionRid,
                    new PartitionedQueryExecutionInfo() { QueryInfo = new QueryInfo() },
                    new List<PartitionKeyRange>() { MockQueryFactory.DefaultPartitionKeyRange },
                    maxPageSize,
                    initialContinuationToken);

                CosmosParallelItemQueryExecutionContext executionContext = await CosmosParallelItemQueryExecutionContext.CreateAsync(
                    context,
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

                Assert.AreEqual(allItems.Count, itemsRead.Count);
                List<ToDoItem> exepected = allItems.OrderBy(x => x.id).ToList();
                List<ToDoItem> actual = itemsRead.OrderBy(x => x.id).ToList();

                CollectionAssert.AreEqual(exepected, actual, new ToDoItemComparer());
            }
        }
    }
}
