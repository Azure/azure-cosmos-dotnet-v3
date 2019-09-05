//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Dynamic;
    using System.Threading.Tasks;

    [TestClass]
    public class CosmosItemLinqTests : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod]
        public async Task ItemLinqReadFeedTest(bool useStatelessIterator)
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, pkCount: 3, randomPartitionKey: true);
            HashSet<string> itemIds = deleteList.Select(x => x.id).ToHashSet<string>();

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = 1
            };

            List<ToDoActivity> itemsViaReadFeed = this.Container.GetItemLinqQueryable<ToDoActivity>(
                allowSynchronousQueryExecution: true,
                requestOptions: requestOptions).ToList();

            Assert.IsTrue(itemsViaReadFeed.Count >= 3);
            CollectionAssert.AreEqual(deleteList.ToList(), itemsViaReadFeed);

            string lastContinuationToken = null;
            FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemLinqQueryable<ToDoActivity>(
                requestOptions: requestOptions).ToFeedIterator();

            while (feedIterator.HasMoreResults)
            {
                if (useStatelessIterator)
                {
                    feedIterator = this.Container.GetItemLinqQueryable<ToDoActivity>(
                        continuationToken: lastContinuationToken,
                        requestOptions: requestOptions).ToFeedIterator();
                }

                FeedResponse<ToDoActivity> responseMessage = await feedIterator.ReadNextAsync(this.cancellationToken);
                lastContinuationToken = responseMessage.ContinuationToken;

                foreach (ToDoActivity toDoActivity in responseMessage)
                {
                    if (itemIds.Contains(toDoActivity.id))
                    {
                        itemIds.Remove(toDoActivity.id);
                    }
                }
            }

            Assert.IsNull(lastContinuationToken);
            Assert.AreEqual(itemIds.Count, 0);

            itemIds = deleteList.Select(x => x.id).ToHashSet<string>();
            FeedIterator streamIterator = this.Container.GetItemLinqQueryable<ToDoActivity>(
                requestOptions: requestOptions).ToStreamIterator();

            while (streamIterator.HasMoreResults)
            {
                if (useStatelessIterator)
                {
                    streamIterator = this.Container.GetItemLinqQueryable<ToDoActivity>(
                        continuationToken: lastContinuationToken,
                        requestOptions: requestOptions).ToStreamIterator();
                }

                using (ResponseMessage responseMessage = await streamIterator.ReadNextAsync(this.cancellationToken))
                {
                    lastContinuationToken = responseMessage.Headers.ContinuationToken;

                    Collection<ToDoActivity> items = TestCommon.Serializer.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
                    foreach (ToDoActivity toDoActivity in items)
                    {
                        if (itemIds.Contains(toDoActivity.id))
                        {
                            itemIds.Remove(toDoActivity.id);
                        }
                    }
                }
            }

            Assert.IsNull(lastContinuationToken);
            Assert.AreEqual(itemIds.Count, 0);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void LinqQueryToIteratorBlockTest(bool isStreamIterator)
        {
            //Checking for exception in case of ToFeedIterator() use on non cosmos linq IQueryable.
            IQueryable<ToDoActivity> nonLinqQueryable = new List<ToDoActivity> { ToDoActivity.CreateRandomToDoActivity() }.AsQueryable();
            if (isStreamIterator)
            {
                nonLinqQueryable.ToStreamIterator();
            }
            else
            {
                nonLinqQueryable.ToFeedIterator();
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [ExpectedException(typeof(NotSupportedException))]
        public void LinqQuerySyncBlockTest(bool isReadFeed)
        {
            //Checking for exception in case of ToFeedIterator() use on non cosmos linq IQueryable.
            if (isReadFeed)
            {
                this.Container.GetItemLinqQueryable<ToDoActivity>().ToList();
            }
            else
            {
                this.Container.GetItemLinqQueryable<ToDoActivity>().Where(item => item.cost > 0).ToList();
            }
        }

        [TestMethod]
        public async Task ItemLINQQueryTest()
        {
            //Creating items for query.
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 2, perPKItemCount: 1, randomPartitionKey: true);

            IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>();
            IQueryable<ToDoActivity> queriable = linqQueryable.Where(item => item.taskNum < 100);
            //V3 Asynchronous query execution with LINQ query generation sql text.
            FeedIterator<ToDoActivity> setIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                queriable.ToQueryDefinition(),
                requestOptions: new QueryRequestOptions() { MaxConcurrency = 2 });

            int resultsFetched = 0;
            while (setIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> queryResponse = await setIterator.ReadNextAsync();
                resultsFetched += queryResponse.Count();

                // For the items returned with NonePartitionKeyValue
                IEnumerator<ToDoActivity> iter = queryResponse.GetEnumerator();
                while (iter.MoveNext())
                {
                    ToDoActivity activity = iter.Current;
                    Assert.AreEqual(42, activity.taskNum);
                }
                Assert.AreEqual(2, resultsFetched);
            }

            //LINQ query execution without partition key.
            linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(allowSynchronousQueryExecution: true);
            queriable = linqQueryable.Where(item => item.taskNum < 100);

            Assert.AreEqual(2, queriable.Count());
            Assert.AreEqual(itemList[0].id, queriable.ToList()[0].id);
            Assert.AreEqual(itemList[1].id, queriable.ToList()[1].id);

            //LINQ query execution with wrong partition key.
            linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(
                allowSynchronousQueryExecution: true,
                requestOptions: new QueryRequestOptions() { PartitionKey = new Cosmos.PartitionKey("test") });
            queriable = linqQueryable.Where(item => item.taskNum < 100);
            Assert.AreEqual(0, queriable.Count());

            //LINQ query execution with correct partition key.
            linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(
                allowSynchronousQueryExecution: true,
                requestOptions: new QueryRequestOptions { ConsistencyLevel = Cosmos.ConsistencyLevel.Eventual, PartitionKey = new Cosmos.PartitionKey(itemList[1].status) });
            queriable = linqQueryable.Where(item => item.taskNum < 100);
            Assert.AreEqual(1, queriable.Count());
            Assert.AreEqual(itemList[1].id, queriable.ToList()[0].id);
        }

        [TestMethod]
        public async Task ItemLINQQueryWithContinuationTokenTest()
        {
            //Creating items for query.
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 10, perPKItemCount: 1, randomPartitionKey: true);

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions();
            queryRequestOptions.MaxConcurrency = 1;
            queryRequestOptions.MaxItemCount = 5;
            IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(requestOptions: queryRequestOptions);
            IQueryable<ToDoActivity> queriable = linqQueryable.Where(item => item.taskNum < 100);
            FeedIterator<ToDoActivity> feedIterator = queriable.ToFeedIterator();

            int firstItemSet = 0;
            string continuationToken = null;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync();
                firstItemSet = feedResponse.Count();
                continuationToken = feedResponse.ContinuationToken;
                if (firstItemSet > 0)
                {
                    break;
                }
            }

            linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(continuationToken: continuationToken, requestOptions: queryRequestOptions);
            queriable = linqQueryable.Where(item => item.taskNum < 100);
            feedIterator = queriable.ToFeedIterator();

            //Test continuationToken with LINQ query generation and asynchronous feedIterator execution.
            int secondItemSet = 0;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync();
                secondItemSet += feedResponse.Count();
            }

            Assert.AreEqual(10 - firstItemSet, secondItemSet);

            //Test continuationToken with blocking LINQ execution
            linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(allowSynchronousQueryExecution: true, continuationToken: continuationToken, requestOptions: queryRequestOptions);
            int linqExecutionItemCount = linqQueryable.Where(item => item.taskNum < 100).Count();
            Assert.AreEqual(10 - firstItemSet, linqExecutionItemCount);
        }

        [TestMethod]
        public async Task QueryableExtentionFunctionsTest()
        {
            //Creating items for query.
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 10, perPKItemCount: 1, randomPartitionKey: true);

            IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>();

            int count = await linqQueryable.CountAsync();
            Assert.AreEqual(10, count);

            int intSum = await linqQueryable.Select(item => item.taskNum).SumAsync();
            Assert.AreEqual(420, intSum);

            int? intNullableSum = await linqQueryable.Select(item => item.taskNum).SumAsync();
            Assert.AreEqual(420, intNullableSum);

            float floatSum = await linqQueryable.Select(item => (float)item.taskNum).SumAsync();
            Assert.AreEqual(420, intSum);

            float? floatNullableSum = await linqQueryable.Select(item => (float?)item.taskNum).SumAsync();
            Assert.AreEqual(420, intNullableSum);

            double doubleSum = await linqQueryable.Select(item => (double)item.taskNum).SumAsync();
            Assert.AreEqual(420, doubleSum);

            double? doubleNullableSum = await linqQueryable.Select(item => (double?)item.taskNum).SumAsync();
            Assert.AreEqual(420, doubleNullableSum);

            long longSum = await linqQueryable.Select(item => (long)item.taskNum).SumAsync();
            Assert.AreEqual(420, longSum);

            long? longNullableSum = await linqQueryable.Select(item => (long?)item.taskNum).SumAsync();
            Assert.AreEqual(420, longNullableSum);

            decimal decimalSum = await linqQueryable.Select(item => (decimal)item.taskNum).SumAsync();
            Assert.AreEqual(420, decimalSum);

            decimal? decimalNullableSum = await linqQueryable.Select(item => (decimal?)item.taskNum).SumAsync();
            Assert.AreEqual(420, decimalNullableSum);

            double intToDoubleAvg = await linqQueryable.Select(item => item.taskNum).AverageAsync();
            Assert.AreEqual(42, intToDoubleAvg);

            double? intToDoubleNulableAvg = await linqQueryable.Select(item => item.taskNum).AverageAsync();
            Assert.AreEqual(42, intToDoubleNulableAvg);

            float floatAvg = await linqQueryable.Select(item => (float)item.taskNum).AverageAsync();
            Assert.AreEqual(42, floatAvg);

            float? floatNullableAvg = await linqQueryable.Select(item => (float?)item.taskNum).AverageAsync();
            Assert.AreEqual(42, floatNullableAvg);

            double doubleAvg = await linqQueryable.Select(item => (double)item.taskNum).AverageAsync();
            Assert.AreEqual(42, doubleAvg);

            double? doubleNullableAvg = await linqQueryable.Select(item => (double?)item.taskNum).AverageAsync();
            Assert.AreEqual(42, doubleNullableAvg);

            double longToDoubleAvg = await linqQueryable.Select(item => (long)item.taskNum).AverageAsync();
            Assert.AreEqual(42, longToDoubleAvg);

            double? longToNullableDoubleAvg = await linqQueryable.Select(item => (long?)item.taskNum).AverageAsync();
            Assert.AreEqual(42, longToNullableDoubleAvg);

            decimal decimalAvg = await linqQueryable.Select(item => (decimal)item.taskNum).AverageAsync();
            Assert.AreEqual(42, decimalAvg);

            decimal? decimalNullableAvg = await linqQueryable.Select(item => (decimal?)item.taskNum).AverageAsync();
            Assert.AreEqual(42, decimalNullableAvg);

            //Adding more items to test min and max function
            ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
            toDoActivity.taskNum = 20;
            toDoActivity.id = "minTaskNum";
            await this.Container.CreateItemAsync(toDoActivity, new PartitionKey(toDoActivity.status));
            toDoActivity.taskNum = 100;
            toDoActivity.id = "maxTaskNum";
            await this.Container.CreateItemAsync(toDoActivity, new PartitionKey(toDoActivity.status));

            int minTaskNum = await linqQueryable.Select(item => item.taskNum).MinAsync();
            Assert.AreEqual(20, minTaskNum);

            int maxTaskNum = await linqQueryable.Select(item => item.taskNum).MaxAsync();
            Assert.AreEqual(100, maxTaskNum);
        }

        [DataRow(false)]
        [DataRow(true)]
        public async Task ItemLINQWithCamelCaseSerializerOptions(bool isGatewayMode)
        {
            Action<CosmosClientBuilder> builder = action =>
            {
                action.WithSerializerOptions(new CosmosSerializationOptions()
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
                    );
                if (isGatewayMode)
                {
                    action.WithConnectionModeGateway();
                }
            };
            CosmosClient camelCaseCosmosClient = TestCommon.CreateCosmosClient(builder);
            Cosmos.Database database = camelCaseCosmosClient.GetDatabase(this.database.Id);
            Container containerFromCamelCaseClient = database.GetContainer(this.Container.Id);
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(container: containerFromCamelCaseClient, pkCount: 2, perPKItemCount: 1, randomPartitionKey: true);

            //Testing query without camelCase CosmosSerializationOptions using this.Container, should not return any result
            IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(true);
            IQueryable<ToDoActivity> queriable = linqQueryable.Where(item => item.CamelCase == "camelCase");
            string queryText = queriable.ToQueryDefinition().QueryText;
            Assert.AreEqual(queriable.Count(), 0);

            //Testing query with camelCase CosmosSerializationOptions using containerFromCamelCaseClient, should return all the items
            linqQueryable = containerFromCamelCaseClient.GetItemLinqQueryable<ToDoActivity>(true);
            queriable = linqQueryable.Where(item => item.CamelCase == "camelCase");
            queryText = queriable.ToQueryDefinition().QueryText;
            Assert.AreEqual(queriable.Count(), 2);
        }
    }
}
