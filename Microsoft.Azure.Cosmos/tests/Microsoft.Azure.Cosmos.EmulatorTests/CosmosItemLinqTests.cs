//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Linq.Dynamic;
    using System.Linq.Expressions;
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
            string PartitionKey = "/pk";
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

                    Collection<ToDoActivity> items = TestCommon.SerializerCore.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
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
                requestOptions: new QueryRequestOptions { ConsistencyLevel = Cosmos.ConsistencyLevel.Eventual, PartitionKey = new Cosmos.PartitionKey(itemList[1].pk) });
            queriable = linqQueryable.Where(item => item.taskNum < 100);
            Assert.AreEqual(1, queriable.Count());
            Assert.AreEqual(itemList[1].id, queriable.ToList()[0].id);
        }

        [TestMethod]
        public async Task ItemLINQQueryWithContinuationTokenTest()
        {
            // Creating items for query.
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(
                container: this.Container,
                pkCount: 10,
                perPKItemCount: 1,
                randomPartitionKey: true);

            IList<ToDoActivity> filteredList = itemList.Where(item => item.taskNum < 100).ToList();
            int filteredDocumentCount = filteredList.Count();

            Console.WriteLine($"Filtered List: {JsonConvert.SerializeObject(filteredList)}.");

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
                Console.WriteLine($"First page: {JsonConvert.SerializeObject(feedResponse.Resource)}.");
                if (firstItemSet > 0)
                {
                    break;
                }
            }

            linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(
                continuationToken: continuationToken,
                requestOptions: queryRequestOptions);
            queriable = linqQueryable.Where(item => item.taskNum < 100);
            feedIterator = queriable.ToFeedIterator();

            // Test continuationToken with LINQ query generation and asynchronous feedIterator execution.
            int secondItemSet = 0;
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> feedResponse = await feedIterator.ReadNextAsync();
                secondItemSet += feedResponse.Count();
                Console.WriteLine($"Second Async page: {JsonConvert.SerializeObject(feedResponse.Resource)}.");
            }

            Assert.AreEqual(
                filteredDocumentCount - firstItemSet,
                secondItemSet,
                "Failed to resume execution for async iterator.");

            // Test continuationToken with blocking LINQ execution
            linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(
                allowSynchronousQueryExecution: true,
                continuationToken: continuationToken,
                requestOptions: queryRequestOptions);
            List<ToDoActivity> secondSyncPage = linqQueryable.Where(item => item.taskNum < 100).ToList();
            Console.WriteLine($"Second Sync page: {JsonConvert.SerializeObject(secondSyncPage)}.");
            int linqExecutionItemCount = secondSyncPage.Count();
            Assert.AreEqual(
                filteredDocumentCount - firstItemSet,
                linqExecutionItemCount,
                "Failed to resume execution for sync iterator");
        }

        [TestMethod]
        public async Task QueryableExtentionFunctionsTest()
        {
            //Creating items for query.
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 10, perPKItemCount: 1, randomPartitionKey: true);

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions();
            IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(
                requestOptions: queryRequestOptions);

            int count = await linqQueryable.CountAsync();
            Assert.AreEqual(10, count);

            Response<int> intSum = await linqQueryable.Select(item => item.taskNum).SumAsync();
            this.VerifyResponse(intSum, 420, queryRequestOptions);

            Response<int?> intNullableSum = await linqQueryable.Select(item => (int?)item.taskNum).SumAsync();
            this.VerifyResponse(intNullableSum, 420, queryRequestOptions);

            Response<float> floatSum = await linqQueryable.Select(item => (float)item.taskNum).SumAsync();
            this.VerifyResponse(floatSum, 420, queryRequestOptions);

            Response<float?> floatNullableSum = await linqQueryable.Select(item => (float?)item.taskNum).SumAsync();
            this.VerifyResponse(floatNullableSum, 420, queryRequestOptions);

            Response<double> doubleSum = await linqQueryable.Select(item => (double)item.taskNum).SumAsync();
            this.VerifyResponse(doubleSum, 420, queryRequestOptions);

            Response<double?> doubleNullableSum = await linqQueryable.Select(item => (double?)item.taskNum).SumAsync();
            this.VerifyResponse(doubleNullableSum, 420, queryRequestOptions);

            Response<long> longSum = await linqQueryable.Select(item => (long)item.taskNum).SumAsync();
            this.VerifyResponse(longSum, 420, queryRequestOptions);

            Response<long?> longNullableSum = await linqQueryable.Select(item => (long?)item.taskNum).SumAsync();
            this.VerifyResponse(longNullableSum, 420, queryRequestOptions);

            Response<decimal> decimalSum = await linqQueryable.Select(item => (decimal)item.taskNum).SumAsync();
            this.VerifyResponse(decimalSum, 420, queryRequestOptions);

            Response<decimal?> decimalNullableSum = await linqQueryable.Select(item => (decimal?)item.taskNum).SumAsync();
            this.VerifyResponse(decimalNullableSum, 420, queryRequestOptions);

            Response<double> intToDoubleAvg = await linqQueryable.Select(item => item.taskNum).AverageAsync();
            this.VerifyResponse(intToDoubleAvg, 42, queryRequestOptions);

            Response<double?> intToDoubleNulableAvg = await linqQueryable.Select(item => (double?)item.taskNum).AverageAsync();
            this.VerifyResponse(intToDoubleNulableAvg, 42, queryRequestOptions);

            Response<float> floatAvg = await linqQueryable.Select(item => (float)item.taskNum).AverageAsync();
            this.VerifyResponse(floatAvg, 42, queryRequestOptions);

            Response<float?> floatNullableAvg = await linqQueryable.Select(item => (float?)item.taskNum).AverageAsync();
            this.VerifyResponse(floatNullableAvg, 42, queryRequestOptions);

            Response<double> doubleAvg = await linqQueryable.Select(item => (double)item.taskNum).AverageAsync();
            this.VerifyResponse(doubleAvg, 42, queryRequestOptions);

            Response<double?> doubleNullableAvg = await linqQueryable.Select(item => (double?)item.taskNum).AverageAsync();
            this.VerifyResponse(doubleNullableAvg, 42, queryRequestOptions);

            Response<double> longToDoubleAvg = await linqQueryable.Select(item => (long)item.taskNum).AverageAsync();
            this.VerifyResponse(longToDoubleAvg, 42, queryRequestOptions);

            Response<double?> longToNullableDoubleAvg = await linqQueryable.Select(item => (long?)item.taskNum).AverageAsync();
            this.VerifyResponse(longToNullableDoubleAvg, 42, queryRequestOptions);

            Response<decimal> decimalAvg = await linqQueryable.Select(item => (decimal)item.taskNum).AverageAsync();
            this.VerifyResponse(decimalAvg, 42, queryRequestOptions);

            Response<decimal?> decimalNullableAvg = await linqQueryable.Select(item => (decimal?)item.taskNum).AverageAsync();
            this.VerifyResponse(decimalNullableAvg, 42, queryRequestOptions);

            //Adding more items to test min and max function
            ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
            toDoActivity.taskNum = 20;
            toDoActivity.id = "minTaskNum";
            await this.Container.CreateItemAsync(toDoActivity, new PartitionKey(toDoActivity.pk));
            toDoActivity.taskNum = 100;
            toDoActivity.id = "maxTaskNum";
            await this.Container.CreateItemAsync(toDoActivity, new PartitionKey(toDoActivity.pk));

            Response<int> minTaskNum = await linqQueryable.Select(item => item.taskNum).MinAsync();
            Assert.AreEqual(20, minTaskNum);

            Response<int> maxTaskNum = await linqQueryable.Select(item => item.taskNum).MaxAsync();
            Assert.AreEqual(100, maxTaskNum);
        }

        [DataRow(false)]
        [DataRow(true)]
        [TestMethod]
        public async Task ItemLINQWithCamelCaseSerializerOptions(bool isGatewayMode)
        {
            Action<CosmosClientBuilder> builder = action =>
            {
                action.WithSerializerOptions(new CosmosSerializationOptions()
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                });
                if (isGatewayMode)
                {
                    action.WithConnectionModeGateway();
                }
            };
            CosmosClient camelCaseCosmosClient = TestCommon.CreateCosmosClient(builder, false);
            Assert.IsNotNull(camelCaseCosmosClient.ClientOptions.Serializer);
            Assert.IsTrue(camelCaseCosmosClient.ClientOptions.Serializer is CosmosJsonSerializerWrapper);

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

        [TestMethod]
        public async Task LinqParameterisedTest1()
        {
            //Creating items for query.
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 10, perPKItemCount: 1, randomPartitionKey: true);

            string queryText = "SELECT VALUE item0 FROM root JOIN item0 IN root[\"children\"] WHERE (((((root[\"CamelCase\"] = @param1)" +
                " AND (root[\"description\"] = @param2)) AND (root[\"taskNum\"] < @param3))" +
                " AND (root[\"valid\"] = @param4)) AND (item0 = @param5))";
            ToDoActivity child1 = new ToDoActivity { id = "child1", taskNum = 30 };
            string description = "CreateRandomToDoActivity";
            string camelCase = "camelCase";
            int taskNum = 100;
            bool valid = true;


            // Passing incorrect boolean value, generating queryDefinition, updating parameter and verifying new result
            valid = false;
            IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(true);
            IQueryable<ToDoActivity> queriable = linqQueryable.Where(item => item.CamelCase == camelCase)
               .Where(item => item.description == description)
               .Where(item => item.taskNum < taskNum)
               .Where(item => item.valid == valid)
               .SelectMany(item => item.children)
               .Where(child => child == child1);
            Dictionary<object, string> parameters = new Dictionary<object, string>();
            parameters.Add(camelCase, "@param1");
            parameters.Add(description, "@param2");
            parameters.Add(taskNum, "@param3");
            parameters.Add(valid, "@param4");
            parameters.Add(child1, "@param5");
            QueryDefinition queryDefinition = queriable.ToQueryDefinition(parameters);
            Assert.AreEqual(5, queryDefinition.ToSqlQuerySpec().Parameters.Count);
            Assert.AreEqual(queryText, queryDefinition.ToSqlQuerySpec().QueryText);
            Assert.AreEqual(0, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            string paramNameForUpdate = parameters[valid];
            valid = true;
            queryDefinition.WithParameter(paramNameForUpdate, valid);
            Assert.AreEqual(10, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            // Passing incorrect string value, generating queryDefinition, updating parameter and verifying new result
            description = "wrongDescription";
            linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(true);
            queriable = linqQueryable.Where(item => item.CamelCase == "camelCase")
               .Where(item => item.description == description)
               .Where(item => item.taskNum < 100)
               .Where(item => item.valid == true)
               .SelectMany(item => item.children)
               .Where(child => child == child1);
            parameters = new Dictionary<object, string>();
            parameters.Add(camelCase, "@param1");
            parameters.Add(description, "@param2");
            parameters.Add(taskNum, "@param3");
            parameters.Add(valid, "@param4");
            parameters.Add(child1, "@param5");
            queryDefinition = queriable.ToQueryDefinition(parameters);
            Assert.AreEqual(5, queryDefinition.ToSqlQuerySpec().Parameters.Count);
            Assert.AreEqual(queryText, queryDefinition.ToSqlQuerySpec().QueryText);
            Assert.AreEqual(0, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            paramNameForUpdate = parameters[description];
            description = "CreateRandomToDoActivity";
            queryDefinition.WithParameter(paramNameForUpdate, description);
            Assert.AreEqual(10, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            // Passing incorrect number value, generating queryDefinition, updating parameter and verifying new result
            taskNum = 10;
            linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(true);
            queriable = linqQueryable.Where(item => item.CamelCase == "camelCase")
               .Where(item => item.description == "CreateRandomToDoActivity")
               .Where(item => item.taskNum < taskNum)
               .Where(item => item.valid == true)
               .SelectMany(item => item.children)
               .Where(child => child == child1);
            parameters = new Dictionary<object, string>();
            parameters.Add(camelCase, "@param1");
            parameters.Add(description, "@param2");
            parameters.Add(taskNum, "@param3");
            parameters.Add(valid, "@param4");
            parameters.Add(child1, "@param5");
            queryDefinition = queriable.ToQueryDefinition(parameters);
            Assert.AreEqual(5, queryDefinition.ToSqlQuerySpec().Parameters.Count);
            Assert.AreEqual(queryText, queryDefinition.ToSqlQuerySpec().QueryText);
            Assert.AreEqual(0, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            paramNameForUpdate = parameters[taskNum];
            taskNum = 100;
            queryDefinition.WithParameter(paramNameForUpdate, taskNum);
            Assert.AreEqual(10, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            // Passing incorrect object value, generating queryDefinition, updating parameter and verifying new result
            child1.taskNum = 40;
            queriable = linqQueryable.Where(item => item.CamelCase == "camelCase")
               .Where(item => item.description == "CreateRandomToDoActivity")
               .Where(item => item.taskNum < taskNum)
               .Where(item => item.valid == true)
               .SelectMany(item => item.children)
               .Where(child => child == child1);
            parameters = new Dictionary<object, string>();
            parameters.Add(camelCase, "@param1");
            parameters.Add(description, "@param2");
            parameters.Add(taskNum, "@param3");
            parameters.Add(valid, "@param4");
            parameters.Add(child1, "@param5");
            queryDefinition = queriable.ToQueryDefinition(parameters);
            Assert.AreEqual(5, queryDefinition.ToSqlQuerySpec().Parameters.Count);
            Assert.AreEqual(queryText, queryDefinition.ToSqlQuerySpec().QueryText);
            Assert.AreEqual(0, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            paramNameForUpdate = parameters[child1];
            child1.taskNum = 30;
            queryDefinition.WithParameter(paramNameForUpdate, child1);
            Assert.AreEqual(10, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);
        }

        [TestMethod]
        public async Task LinqParameterisedTest2()
        {
            //Creating items for query.
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 10, perPKItemCount: 1, randomPartitionKey: true);

            IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>(true);

            //Test same values in two where clause
            string camelCase = "wrongValue";
            IQueryable<ToDoActivity> queriable = linqQueryable
               .Where(item => item.CamelCase == camelCase)
               .Where(item => item.description != camelCase);
            Dictionary<object, string> parameters = new Dictionary<object, string>();
            parameters.Add(camelCase, "@param1");
            QueryDefinition queryDefinition = queriable.ToQueryDefinition(parameters);
            Assert.AreEqual(1, queryDefinition.ToSqlQuerySpec().Parameters.Count);
            Assert.AreEqual(0, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            camelCase = "camelCase";
            queryDefinition.WithParameter("@param1", camelCase);
            Assert.AreEqual(10, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            string queryText = "SELECT VALUE root FROM root WHERE (root[\"children\"] = [@param1, @param2])";
            //Test array in query, array items will be parametrized
            ToDoActivity child1 = new ToDoActivity { id = "child1", taskNum = 30 };
            ToDoActivity child2 = new ToDoActivity { id = "child2", taskNum = 40 };
            ToDoActivity[] children = new ToDoActivity[]
                { child1,
                  child2
                };
            queriable = linqQueryable
               .Where(item => item.children == children);
            parameters = new Dictionary<object, string>();
            parameters.Add(child1, "@param1");
            parameters.Add(child2, "@param2");
            queryDefinition = queriable.ToQueryDefinition(parameters);
            Assert.AreEqual(queryText, queryDefinition.ToSqlQuerySpec().QueryText);
            Assert.AreEqual(10, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            //updating child to wrong value, result in query returning 0 results
            child1.taskNum = 50;
            queryDefinition.WithParameter("@param1", child1);
            Assert.AreEqual(queryText, queryDefinition.ToSqlQuerySpec().QueryText);
            Assert.AreEqual(0, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            //Test orderby, skip, take, distinct, these will not get parameterized.
            queryText = "SELECT VALUE root " +
                "FROM root " +
                "WHERE (root[\"CamelCase\"] = @param1) " +
                "ORDER BY root[\"taskNum\"] ASC " +
                "OFFSET @param2 LIMIT @param3";
            queriable = linqQueryable
                .Where(item => item.CamelCase == camelCase)
                .OrderBy(item => item.taskNum)
                .Skip(5)
                .Take(4);
            parameters = new Dictionary<object, string>();
            parameters.Add(camelCase, "@param1");
            parameters.Add(5, "@param2");
            parameters.Add(4, "@param3");
            queryDefinition = queriable.ToQueryDefinition(parameters);
            Assert.AreEqual(queryText, queryDefinition.ToSqlQuerySpec().QueryText);
            Assert.AreEqual(4, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            queryDefinition.WithParameter("@param2", 10);
            queryDefinition.WithParameter("@param3", 0);
            Assert.AreEqual(0, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);


            queryText = "SELECT VALUE root FROM root WHERE (root[\"CamelCase\"] != @param1)";
            camelCase = "\b\n";
            queriable = linqQueryable
                .Where(item => item.CamelCase != camelCase);
            parameters = new Dictionary<object, string>();
            parameters.Add(camelCase, "@param1");
            queryDefinition = queriable.ToQueryDefinition(parameters);
            Assert.AreEqual("\b\n", queryDefinition.ToSqlQuerySpec().Parameters[0].Value);
            Assert.AreEqual(queryText, queryDefinition.ToSqlQuerySpec().QueryText);
            Assert.AreEqual(10, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);
        }

        [TestMethod]
        public async Task LinqParameterisedTest3()
        {
            string queryText = "SELECT VALUE item0 FROM root JOIN item0 IN root[\"children\"]" +
                " WHERE ((((((((((((((root[\"id\"] = @param1) AND (root[\"stringValue\"] = @Param2))" +
                " AND (root[\"sbyteValue\"] = @param3))" +
                " AND (root[\"byteValue\"] = @param4))" +
                " AND (root[\"shortValue\"] = @param5))" +
                " AND (root[\"uintValue\"] = @param6))" +
                " AND (root[\"longValue\"] = @param7))" +
                " AND (root[\"ulongValue\"] = @Param8))" +
                " AND (root[\"floatValue\"] = @param9))" +
                " AND (root[\"doubleValue\"] = @param10))" +
                " AND (root[\"decimaleValue\"] = @param11))" +
                " AND (root[\"ushortValue\"] = @param15))" +
                " AND (root[\"booleanValue\"] = @param12))" +
                " AND (item0 = @param13))";

            string id = "testId";
            string pk = "testPk";
            string stringValue = "testStringValue";
            sbyte sbyteValue = 5;
            byte byteValue = 6;
            short shortValue = 7;
            int intValue = 8;
            uint uintValue = 9;
            long longValue = 10;
            ulong ulongValue = 11;
            float floatValue = 12;
            double doubleValue = 13;
            decimal decimaleValue = 14;
            ushort ushortValue = 15;
            bool booleanValue = true;
            NumberLinqItem child = new NumberLinqItem { id = "childId" };
            NumberLinqItem[] children = new NumberLinqItem[]
            {
                child
            };


            NumberLinqItem parametrizedLinqItem = new NumberLinqItem
            {
                id = id,
                pk = pk,
                stringValue = stringValue,
                sbyteValue = sbyteValue,
                byteValue = byteValue,
                shortValue = shortValue,
                intValue = intValue,
                uintValue = uintValue,
                longValue = longValue,
                ulongValue = ulongValue,
                floatValue = floatValue,
                doubleValue = doubleValue,
                decimaleValue = decimaleValue,
                ushortValue = ushortValue,
                booleanValue = booleanValue,
                children = children
            };

            await this.Container.CreateItemAsync(parametrizedLinqItem, new PartitionKey(pk));

            IOrderedQueryable<NumberLinqItem> linqQueryable = this.Container.GetItemLinqQueryable<NumberLinqItem>(true);
            IQueryable<NumberLinqItem> queriable = linqQueryable
               .Where(item => item.id == id)
               .Where(item => item.stringValue == stringValue)
               .Where(item => item.sbyteValue == sbyteValue)
               .Where(item => item.byteValue == byteValue)
               .Where(item => item.shortValue == shortValue)
               .Where(item => item.uintValue == uintValue)
               .Where(item => item.longValue == longValue)
               .Where(item => item.ulongValue == ulongValue)
               .Where(item => item.floatValue == floatValue)
               .Where(item => item.doubleValue == doubleValue)
               .Where(item => item.decimaleValue == decimaleValue)
               .Where(item => item.ushortValue == ushortValue)
               .Where(item => item.booleanValue == booleanValue)
               .SelectMany(item => item.children)
               .Where(ch => ch == child);
            Dictionary<object, string> parameters = new Dictionary<object, string>();
            parameters.Add(id, "@param1");
            parameters.Add(stringValue, "@Param2");
            parameters.Add((int)sbyteValue, "@param3"); // Linq converts sbyte to int32, therefore adding int in cast
            parameters.Add((int)byteValue, "@param4");  // Linq converts byte to int32, therefore adding int in cast
            parameters.Add((int)shortValue, "@param5"); // Linq converts short to int32, therefore adding int in cast
            parameters.Add(uintValue, "@param6");
            parameters.Add(longValue, "@param7");
            parameters.Add(ulongValue, "@Param8");
            parameters.Add(floatValue, "@param9");
            parameters.Add(doubleValue, "@param10");
            parameters.Add(decimaleValue, "@param11");
            parameters.Add(booleanValue, "@param12");
            parameters.Add(child, "@param13");
            parameters.Add((int)ushortValue, "@param15"); // Linq converts ushort to int32, therefore adding int in cast

            QueryDefinition queryDefinition = queriable.ToQueryDefinition(parameters);
            Assert.AreEqual(queryText, queryDefinition.ToSqlQuerySpec().QueryText);
            Assert.AreEqual(1, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

            queryDefinition.WithParameter("@param3", 6);
            Assert.AreEqual(0, (await this.FetchResults<ToDoActivity>(queryDefinition)).Count);

        }

        [TestMethod]
        public async Task LinqCaseInsensitiveStringTest()
        {
            //Creating items for query.
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 2, perPKItemCount: 100, randomPartitionKey: true);

            IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.GetItemLinqQueryable<ToDoActivity>();

            async Task TestSearch(Expression<Func<ToDoActivity, bool>> expression, string expectedMethod, bool caseInsensitive, int expectedResults)
            {
                string expectedQueryText = $"SELECT VALUE root FROM root WHERE {expectedMethod}(root[\"description\"], @param1{(caseInsensitive ? ", true" : "")})";

                IArgumentProvider arguments = (IArgumentProvider)expression.Body;

                string searchString = (arguments.GetArgument(0) as ConstantExpression).Value as string;

                IQueryable<ToDoActivity> queryable = linqQueryable.Where(expression);

                Dictionary<object, string> parameters = new Dictionary<object, string>();
                parameters.Add(searchString, "@param1");

                QueryDefinition queryDefinition = queryable.ToQueryDefinition(parameters);

                string queryText = queryDefinition.ToSqlQuerySpec().QueryText;

                Assert.AreEqual(expectedQueryText, queryText);

                Assert.AreEqual(expectedResults, await queryable.CountAsync());
            }

            await TestSearch(x => x.description.StartsWith("create"), "STARTSWITH", false, 0);
            await TestSearch(x => x.description.StartsWith("cReAtE", StringComparison.OrdinalIgnoreCase), "STARTSWITH", true, 200);

            await TestSearch(x => x.description.EndsWith("activity"), "ENDSWITH", false, 0);
            await TestSearch(x => x.description.EndsWith("AcTiViTy", StringComparison.OrdinalIgnoreCase), "ENDSWITH", true, 200);

            await TestSearch(x => x.description.Equals("createrandomtodoactivity", StringComparison.OrdinalIgnoreCase), "StringEquals", true, 200);

            await TestSearch(x => x.description.Contains("todo"), "CONTAINS", false, 0);
            await TestSearch(x => x.description.Contains("tOdO", StringComparison.OrdinalIgnoreCase), "CONTAINS", true, 200);

        }

        private class NumberLinqItem
        {
            public string id;
            public string pk;
            public string stringValue;
            public sbyte sbyteValue;
            public byte byteValue;
            public short shortValue;
            public ushort ushortValue;
            public int intValue;
            public uint uintValue;
            public long longValue;
            public ulong ulongValue;
            public float floatValue;
            public double doubleValue;
            public decimal decimaleValue;
            public bool booleanValue;
            public NumberLinqItem[] children;

        }

        private async Task<List<T>> FetchResults<T>(QueryDefinition queryDefinition)
        {
            List<T> itemList = new List<T>();
            FeedIterator<T> feedIterator = this.Container.GetItemQueryIterator<T>(queryDefinition);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<T> queryResponse = await feedIterator.ReadNextAsync();
                IEnumerator<T> iter = queryResponse.GetEnumerator();
                while (iter.MoveNext())
                {
                    itemList.Add(iter.Current);
                }
            }
            return itemList;
        }

        private void VerifyResponse<T>(
            Response<T> response,
            T expectedValue,
            QueryRequestOptions queryRequestOptions)
        {
            Assert.AreEqual<T>(expectedValue, response.Resource);
            Assert.IsTrue(response.RequestCharge > 0);
        }
    }
}
