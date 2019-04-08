//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using JsonReader = Json.JsonReader;
    using JsonWriter = Json.JsonWriter;

    [TestClass]
    public class CosmosItemTests : BaseCosmosClientHelper
    {
        private CosmosContainer Container = null;
        private CosmosDefaultJsonSerializer jsonSerializer = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            CosmosContainerResponse response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
            this.jsonSerializer = new CosmosDefaultJsonSerializer();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CreateDropItemTest()
        {
            ToDoActivity testItem = CreateRandomToDoActivity();
            CosmosItemResponse<ToDoActivity> response = await this.Container.Items.CreateItemAsync<ToDoActivity>(partitionKey: testItem.status, item: testItem);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.MaxResourceQuota);
            Assert.IsNotNull(response.CurrentResourceQuotaUsage);
            CosmosItemResponse<ToDoActivity> deleteResponse = await this.Container.Items.DeleteItemAsync<ToDoActivity>(partitionKey: testItem.status, id: testItem.id);
            Assert.IsNotNull(deleteResponse);
        }

        [TestMethod]
        public async Task CreateDropItemUndefinedPartitionKeyTest()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString()
            };

            CosmosItemResponse<dynamic> response = await this.Container.Items.CreateItemAsync<dynamic>(partitionKey: Undefined.Value, item: testItem);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.MaxResourceQuota);
            Assert.IsNotNull(response.CurrentResourceQuotaUsage);

            CosmosItemResponse<dynamic> deleteResponse = await this.Container.Items.DeleteItemAsync<dynamic>(partitionKey: "[{}]", id: testItem.id);
            Assert.IsNotNull(deleteResponse);
        }

        [TestMethod]
        public async Task CreateDropItemStreamTest()
        {
            ToDoActivity testItem = CreateRandomToDoActivity();
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                using (CosmosResponseMessage response = await this.Container.Items.CreateItemStreamAsync(partitionKey: testItem.status, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    Assert.IsTrue(response.Headers.RequestCharge > 0);
                    Assert.IsNotNull(response.Headers.ActivityId);
                    Assert.IsNotNull(response.Headers.ETag);
                }
            }

            using (CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(partitionKey: testItem.status, id: testItem.id))
            {
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
                Assert.IsTrue(deleteResponse.Headers.RequestCharge > 0);
                Assert.IsNotNull(deleteResponse.Headers.ActivityId);
            }
        }

        [TestMethod]
        public async Task UpsertItemStreamTest()
        {
            ToDoActivity testItem = CreateRandomToDoActivity();
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                //Create the object
                using (CosmosResponseMessage response = await this.Container.Items.UpsertItemStreamAsync(partitionKey: testItem.status, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    using (StreamReader str = new StreamReader(response.Content))
                    {
                        string responseContentAsString = await str.ReadToEndAsync();
                    }
                }
            }

            //Updated the taskNum field
            testItem.taskNum = 9001;
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                using (CosmosResponseMessage response = await this.Container.Items.UpsertItemStreamAsync(partitionKey: testItem.status, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }
            }
            using (CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(partitionKey: testItem.status, id: testItem.id))
            {
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
            }
        }

        [TestMethod]
        public async Task ReplaceItemStreamTest()
        {
            ToDoActivity testItem = CreateRandomToDoActivity();
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                //Replace a non-existing item. It should fail, and not throw an exception.
                using (CosmosResponseMessage response = await this.Container.Items.ReplaceItemStreamAsync(
                    partitionKey: testItem.status,
                    id: testItem.id,
                    streamPayload: stream))
                {
                    Assert.IsFalse(response.IsSuccessStatusCode);
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, response.ErrorMessage);
                }
            }

            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                //Create the item
                using (CosmosResponseMessage response = await this.Container.Items.CreateItemStreamAsync(partitionKey: testItem.status, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                }
            }

            //Updated the taskNum field
            testItem.taskNum = 9001;
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                using (CosmosResponseMessage response = await this.Container.Items.ReplaceItemStreamAsync(partitionKey: testItem.status, id: testItem.id, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }

                using (CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(partitionKey: testItem.status, id: testItem.id))
                {
                    Assert.IsNotNull(deleteResponse);
                    Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
                }
            }
        }

        [TestMethod]
        public async Task ItemStreamIterator()
        {
            IList<ToDoActivity> deleteList = null;
            HashSet<string> itemIds = null;
            try
            {
                deleteList = await CreateRandomItems(3, randomPartitionKey: true);
                itemIds = deleteList.Select(x => x.id).ToHashSet<string>();
                CosmosFeedResultSetIterator setIterator =
                    this.Container.Items.GetItemStreamIterator();
                while (setIterator.HasMoreResults)
                {
                    using (CosmosResponseMessage iterator =
                        await setIterator.FetchNextSetAsync(this.cancellationToken))
                    {
                        Collection<ToDoActivity> response = new CosmosDefaultJsonSerializer().FromStream<CosmosFeedResponse<ToDoActivity>>(iterator.Content).Data;
                        foreach (ToDoActivity toDoActivity in response)
                        {
                            if (itemIds.Contains(toDoActivity.id))
                            {
                                itemIds.Remove(toDoActivity.id);
                            }
                        }

                    }

                }
            }
            finally
            {
                foreach (ToDoActivity delete in deleteList)
                {
                    CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(delete.status, delete.id);
                    deleteResponse.Dispose();
                }
            }

            Assert.AreEqual(itemIds.Count, 0);
        }

        [TestMethod]
        public async Task ItemIterator()
        {
            IList<ToDoActivity> deleteList = null;
            HashSet<string> itemIds = null;
            try
            {
                deleteList = await CreateRandomItems(3, randomPartitionKey: true);
                itemIds = deleteList.Select(x => x.id).ToHashSet<string>();
                CosmosResultSetIterator<ToDoActivity> setIterator =
                    this.Container.Items.GetItemIterator<ToDoActivity>();
                while (setIterator.HasMoreResults)
                {
                    foreach (ToDoActivity toDoActivity in await setIterator.FetchNextSetAsync(this.cancellationToken))
                    {
                        if (itemIds.Contains(toDoActivity.id))
                        {
                            itemIds.Remove(toDoActivity.id);
                        }
                    }
                }
            }
            finally
            {
                foreach (ToDoActivity delete in deleteList)
                {
                    CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(delete.status, delete.id);
                    deleteResponse.Dispose();
                }
            }

            Assert.AreEqual(itemIds.Count, 0);
        }

        [TestMethod]
        public async Task QueryStreamSingleItem()
        {
            await ItemSinglePartitionQueryStream(1, 1);
        }

        [TestMethod]
        public async Task QueryStreamMultipleItem()
        {
            await ItemSinglePartitionQueryStream(5, 5);
        }

        [TestMethod]
        public async Task QueryStreamMultipleItemWithMaxItemCount()
        {
            await ItemSinglePartitionQueryStream(6, 2);
        }

        internal async Task ItemSinglePartitionQueryStream(int perPKItemCount, int maxItemCount)
        {
            IList<ToDoActivity> deleteList = deleteList = await CreateRandomItems(pkCount: 3, perPKItemCount: perPKItemCount, randomPartitionKey: true);
            ToDoActivity find = deleteList.First();

            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from r");
            CosmosResultSetIterator setIterator = this.Container.Items
                .CreateItemQueryAsStream(sql, 1, find.status, maxItemCount);

            int iterationCount = 0;
            int totalReadItem = 0;
            int expectedIterationCount = perPKItemCount / maxItemCount;
            string lastContinuationToken = null;

            while (setIterator.HasMoreResults && expectedIterationCount > iterationCount)
            {
                iterationCount++;

                using (CosmosQueryResponse response = await setIterator.FetchNextSetAsync())
                {
                    lastContinuationToken = response.ContinuationToken;
                    Trace.TraceInformation($"ContinuationToken: {lastContinuationToken}");
                    JsonSerializer serializer = new JsonSerializer();

                    using (StreamReader sr = new StreamReader(response.Content))
                    using (JsonTextReader jtr = new JsonTextReader(sr))
                    {
                        ToDoActivity[] results = serializer.Deserialize<ToDoActivity[]>(jtr);
                        ToDoActivity[] readTodoActivities = results.OrderBy(e => e.id)
                            .ToArray();

                        ToDoActivity[] expectedTodoActivities = deleteList
                                .Where(e => e.status == find.status)
                                .Where(e => readTodoActivities.Any(e1 => e1.id == e.id))
                                .OrderBy(e => e.id)
                                .ToArray();

                        totalReadItem += expectedTodoActivities.Length;
                        string expectedSerialized = JsonConvert.SerializeObject(expectedTodoActivities);
                        string readSerialized = JsonConvert.SerializeObject(readTodoActivities);
                        Trace.TraceInformation($"Expected: {Environment.NewLine} {expectedSerialized}");
                        Trace.TraceInformation($"Read: {Environment.NewLine} {readSerialized}");

                        int count = results.Length;
                        Assert.AreEqual(maxItemCount, count);

                        Assert.AreEqual(expectedSerialized, readSerialized);

                        Assert.AreEqual(maxItemCount, expectedTodoActivities.Length);
                    }
                }
            }

            Assert.AreEqual(expectedIterationCount, iterationCount);
            Assert.AreEqual(perPKItemCount, totalReadItem);

            //Assert.IsNull(lastContinuationToken);
            //Assert.IsFalse(setIterator.HasMoreResults);
        }


        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionQuery()
        {
            IList<ToDoActivity> deleteList = new List<ToDoActivity>();
            try
            {
                deleteList = await CreateRandomItems(3, randomPartitionKey: true);

                ToDoActivity find = deleteList.First();
                CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from toDoActivity t where t.id = '" + find.id + "'");

                CosmosQueryRequestOptions requestOptions = new CosmosQueryRequestOptions()
                {
                    MaxBufferedItemCount = 10,
                    ResponseContinuationTokenLimitInKb = 500
                };

                CosmosResultSetIterator<ToDoActivity> setIterator =
                    this.Container.Items.CreateItemQuery<ToDoActivity>(sql, maxConcurrency: 1, maxItemCount: 1, requestOptions: requestOptions);
                while (setIterator.HasMoreResults)
                {
                    CosmosQueryResponse<ToDoActivity> iter = await setIterator.FetchNextSetAsync();
                    Assert.AreEqual(1, iter.Count());
                    ToDoActivity response = iter.First();
                    Assert.AreEqual(find.id, response.id);
                }
            }
            finally
            {
                foreach (ToDoActivity delete in deleteList)
                {
                    CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(delete.status, delete.id);
                    deleteResponse.Dispose();
                }
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionOrderByQueryStream()
        {
            IList<ToDoActivity> deleteList = new List<ToDoActivity>();
            try
            {
                deleteList = await CreateRandomItems(300, randomPartitionKey: true);
                
                CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("SELECT * FROM toDoActivity t ORDER BY t.taskNum ");

                CosmosQueryRequestOptions requestOptions = new CosmosQueryRequestOptions()
                {
                    MaxBufferedItemCount = 10,
                    ResponseContinuationTokenLimitInKb = 500
                };

                List<ToDoActivity> resultList = new List<ToDoActivity>();
                double totalRequstCharge = 0;
                CosmosResultSetIterator setIterator =
                    this.Container.Items.CreateItemQueryAsStream(sql, maxConcurrency: 5, maxItemCount: 1, requestOptions: requestOptions);
                while (setIterator.HasMoreResults)
                {
                    using (CosmosQueryResponse iter = await setIterator.FetchNextSetAsync())
                    {
                        Assert.IsTrue(iter.IsSuccess);
                        Assert.IsNull(iter.ErrorMessage);
                        Assert.IsTrue(iter.Count <= 5);
                        totalRequstCharge += iter.RequestCharge;

                        ToDoActivity[] activities = this.jsonSerializer.FromStream<ToDoActivity[]>(iter.Content);
                        Assert.AreEqual(1, activities.Length);
                        ToDoActivity response = activities.First();
                        resultList.Add(response);
                    }
                }

                Assert.AreEqual(deleteList.Count, resultList.Count);
                Assert.IsTrue(totalRequstCharge > 0);

                List<ToDoActivity> verifiedOrderBy = deleteList.OrderBy(x => x.taskNum).ToList();
                for(int i = 0; i < verifiedOrderBy.Count(); i++)
                {
                    Assert.AreEqual(verifiedOrderBy[i].taskNum, resultList[i].taskNum);
                    Assert.AreEqual(verifiedOrderBy[i].id, resultList[i].id);
                }
            }
            finally
            {
                foreach (ToDoActivity delete in deleteList)
                {
                    CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(delete.status, delete.id);
                    deleteResponse.Dispose();
                }
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionQueryStream()
        {
            IList<ToDoActivity> deleteList = new List<ToDoActivity>();
            try
            {
                deleteList = await CreateRandomItems(101, randomPartitionKey: true);

                CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("SELECT * FROM toDoActivity t");

                List<ToDoActivity> resultList = new List<ToDoActivity>();
                double totalRequstCharge = 0;
                CosmosResultSetIterator setIterator =
                    this.Container.Items.CreateItemQueryAsStream(sql, maxConcurrency: 5, maxItemCount: 5);
                while (setIterator.HasMoreResults)
                {
                    using (CosmosQueryResponse iter = await setIterator.FetchNextSetAsync())
                    {
                        Assert.IsTrue(iter.IsSuccess);
                        Assert.IsNull(iter.ErrorMessage);
                        Assert.IsTrue(iter.Count <= 5);
                        totalRequstCharge += iter.RequestCharge;
                        ToDoActivity[] response = this.jsonSerializer.FromStream<ToDoActivity[]>(iter.Content);
                        resultList.AddRange(response);
                    }
                }

                Assert.AreEqual(deleteList.Count, resultList.Count);
                Assert.IsTrue(totalRequstCharge > 0);

                List<ToDoActivity> verifiedOrderBy = deleteList.OrderBy(x => x.taskNum).ToList();
                resultList = resultList.OrderBy(x => x.taskNum).ToList();
                for (int i = 0; i < verifiedOrderBy.Count(); i++)
                {
                    Assert.AreEqual(verifiedOrderBy[i].taskNum, resultList[i].taskNum);
                    Assert.AreEqual(verifiedOrderBy[i].id, resultList[i].id);
                }
            }
            finally
            {
                foreach (ToDoActivity delete in deleteList)
                {
                    CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(delete.status, delete.id);
                    deleteResponse.Dispose();
                }
            }
        }

        /// <summary>
        /// Validate that if the EPK is set in the options that only a single range is selected.
        /// </summary>
        [TestMethod]
        public async Task ItemEpkQueryValidation()
        {
            IList<ToDoActivity> deleteList = new List<ToDoActivity>();
            CosmosContainer container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                var containerResponse = await this.database.Containers.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk",
                    throughput: 15000);
                container = containerResponse;

                // Get all the partition key ranges to verify there is more than one partition
                IRoutingMapProvider routingMapProvider = await this.cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync();
                IReadOnlyList<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(
                    containerResponse.Resource.ResourceId,
                    new Documents.Routing.Range<string>("00", "FF", isMaxInclusive: true, isMinInclusive: true));
                
                // If this fails the RUs of the container needs to be increased to ensure at least 2 partitions.
                Assert.IsTrue(ranges.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                FeedOptions options = new FeedOptions()
                {
                    Properties = new Dictionary<string, object>()
                    {
                        {"x-ms-effective-partition-key-string", "AA" }
                    }
                };

                // Create a bad expression. It will not be called. Expression is not allowed to be null.
                IQueryable<int> queryable = new List<int>().AsQueryable();
                Expression expression = queryable.Expression;

                DocumentQueryExecutionContextBase.InitParams inputParams = new DocumentQueryExecutionContextBase.InitParams(
                    new DocumentQueryClient(this.cosmosClient.DocumentClient),
                    ResourceType.Document,
                    typeof(object),
                    expression,
                    options,
                    container.LinkUri.OriginalString,
                    false,
                    Guid.NewGuid());

                DefaultDocumentQueryExecutionContext defaultDocumentQueryExecutionContext = new DefaultDocumentQueryExecutionContext(inputParams, true);

                // There should only be one range since the EPK option is set.
                List<PartitionKeyRange> partitionKeyRanges = await DocumentQueryExecutionContextFactory.GetTargetPartitionKeyRanges(
                    queryExecutionContext: defaultDocumentQueryExecutionContext,
                    partitionedQueryExecutionInfo: null,
                    collection: containerResponse,
                    feedOptions: options);

                Assert.IsTrue(partitionKeyRanges.Count == 1, "Only 1 partition key range should be selected since the EPK option is set.");
            }
            finally
            {
                if(container != null)
                {
                    await container.DeleteAsync();
                }
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemQueryStreamSerializationSetting()
        {
            IList<ToDoActivity> deleteList = new List<ToDoActivity>();
            try
            {
                deleteList = await CreateRandomItems(101, randomPartitionKey: true);

                CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("SELECT * FROM toDoActivity t ORDER BY t.taskNum");
                CosmosSerializationOptions options = new CosmosSerializationOptions(
                    ContentSerializationFormat.CosmosBinary.ToString(),
                    (content) => JsonNavigator.Create(content),
                    () => JsonWriter.Create(JsonSerializationFormat.Binary));

                CosmosQueryRequestOptions requestOptions = new CosmosQueryRequestOptions()
                {
                    CosmosSerializationOptions = options
                };

                List<ToDoActivity> resultList = new List<ToDoActivity>();
                double totalRequstCharge = 0;
                CosmosResultSetIterator setIterator =
                    this.Container.Items.CreateItemQueryAsStream(sql, maxConcurrency: 5, maxItemCount: 5, requestOptions: requestOptions);
                while (setIterator.HasMoreResults)
                {
                    using (CosmosQueryResponse iter = await setIterator.FetchNextSetAsync())
                    {
                        Assert.IsTrue(iter.IsSuccess);
                        Assert.IsNull(iter.ErrorMessage);
                        Assert.IsTrue(iter.Count <= 5);
                        totalRequstCharge += iter.RequestCharge;
                        IJsonReader reader = JsonReader.Create(iter.Content);
                        IJsonWriter textWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                        textWriter.WriteAll(reader);
                        string json = Encoding.UTF8.GetString(textWriter.GetResult());
                        Assert.IsNotNull(json);
                        ToDoActivity[] responseActivities = JsonConvert.DeserializeObject<ToDoActivity[]>(json);
                        resultList.AddRange(responseActivities);
                    }
                }

                Assert.AreEqual(deleteList.Count, resultList.Count);
                Assert.IsTrue(totalRequstCharge > 0);

                List<ToDoActivity> verifiedOrderBy = deleteList.OrderBy(x => x.taskNum).ToList();
                for (int i = 0; i < verifiedOrderBy.Count(); i++)
                {
                    Assert.AreEqual(verifiedOrderBy[i].taskNum, resultList[i].taskNum);
                    Assert.AreEqual(verifiedOrderBy[i].id, resultList[i].id);
                }
            }
            finally
            {
                foreach (ToDoActivity delete in deleteList)
                {
                    CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(delete.status, delete.id);
                    deleteResponse.Dispose();
                }
            }
        }

        /// <summary>
        /// Validate that the max item count works correctly.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ValidateMaxItemCountOnItemQuery()
        {
            IList<ToDoActivity> deleteList = new List<ToDoActivity>();
            HashSet<string> itemIds = new HashSet<string>();
            try
            {
                deleteList = await CreateRandomItems(6, randomPartitionKey: false);

                ToDoActivity toDoActivity = deleteList.First();
                CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition(
                    "select * from toDoActivity t where t.status = @status")
                    .UseParameter("@status", toDoActivity.status);

                // Test max size at 1
                CosmosResultSetIterator<ToDoActivity> setIterator =
                    this.Container.Items.CreateItemQuery<ToDoActivity>(sql, toDoActivity.status, maxItemCount: 1);
                while (setIterator.HasMoreResults)
                {
                    CosmosQueryResponse<ToDoActivity> iter = await setIterator.FetchNextSetAsync();
                    Assert.AreEqual(1, iter.Count());
                }

                // Test max size at 2
                CosmosResultSetIterator<ToDoActivity> setIteratorMax2 =
                    this.Container.Items.CreateItemQuery<ToDoActivity>(sql, toDoActivity.status, maxItemCount: 2);
                while (setIteratorMax2.HasMoreResults)
                {
                    CosmosQueryResponse<ToDoActivity> iter = await setIteratorMax2.FetchNextSetAsync();
                    Assert.AreEqual(2, iter.Count());
                }
            }
            finally
            {
                foreach (ToDoActivity delete in deleteList)
                {
                    CosmosResponseMessage deleteResponse = await this.Container.Items.DeleteItemStreamAsync(delete.status, delete.id);
                    deleteResponse.Dispose();
                }
            }
        }

        /// <summary>
        /// Validate that the max item count works correctly.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task NegativeQueryTest()
        {
            IList<ToDoActivity> items = await CreateRandomItems(pkCount: 10, perPKItemCount: 20, randomPartitionKey: true);

            try
            {
                CosmosResultSetIterator<dynamic> resultSet = this.Container.Items.CreateItemQuery<dynamic>(
                    sqlQueryText: "SELECT r.id FROM root r WHERE r._ts > 0",
                    maxConcurrency: 1,
                    maxItemCount: 10,
                    requestOptions: new CosmosQueryRequestOptions() { ResponseContinuationTokenLimitInKb = 0 });

                await resultSet.FetchNextSetAsync();
                Assert.Fail("Expected query to fail");
            }
            catch (AggregateException e)
            {
                CosmosException exception = e.InnerException as CosmosException;

                if (exception == null)
                {
                    throw e;
                }

                if (exception.StatusCode != HttpStatusCode.BadRequest)
                {
                    throw e;
                }

                Assert.IsTrue(exception.Message.Contains("continuation token limit specified is not large enough"));
            }

            try
            {
                CosmosResultSetIterator<dynamic> resultSet = this.Container.Items.CreateItemQuery<dynamic>(
                    sqlQueryText: "SELECT r.id FROM root r WHERE r._ts >!= 0",
                    maxConcurrency: 1);

                await resultSet.FetchNextSetAsync();
                Assert.Fail("Expected query to fail");
            }
            catch (AggregateException e)
            {
                CosmosException exception = e.InnerException as CosmosException;

                if (exception == null)
                {
                    throw e;
                }

                if (exception.StatusCode != HttpStatusCode.BadRequest)
                {
                    throw e;
                }

                Assert.IsTrue(exception.Message.Contains("Syntax error, incorrect syntax near"));
            }
        }

        [TestMethod]
        public async Task ItemRequestOptionAccessConditionTest()
        {
            // Create an item
            ToDoActivity testItem = (await CreateRandomItems(1, randomPartitionKey: true)).First();

            // Create an access condition that will fail because the etag will be different
            AccessCondition accessCondition = new AccessCondition
            {
                // Random etag
                Condition = Guid.NewGuid().ToString(),
                Type = AccessConditionType.IfMatch
            };

            CosmosItemRequestOptions itemRequestOptions = new CosmosItemRequestOptions()
            {
                AccessCondition = accessCondition
            };

            try
            {
                CosmosItemResponse<ToDoActivity> response = await this.Container.Items.ReplaceItemAsync<ToDoActivity>(
                    partitionKey: testItem.status,
                    id: testItem.id,
                    item: testItem,
                    requestOptions: itemRequestOptions);
                Assert.Fail("Access condition should have failed");
            }
            catch (CosmosException e)
            {
                Assert.IsNotNull(e);
                Assert.AreEqual(HttpStatusCode.PreconditionFailed, e.StatusCode, e.Message);
                Assert.IsNotNull(e.ActivityId);
                Assert.IsTrue(e.RequestCharge > 0);
            }
            finally
            {
                CosmosItemResponse<ToDoActivity> deleteResponse = await this.Container.Items.DeleteItemAsync<ToDoActivity>(partitionKey: testItem.status, id: testItem.id);
                Assert.IsNotNull(deleteResponse);
            }
        }

        private async Task<IList<ToDoActivity>> CreateRandomItems(int pkCount, int perPKItemCount = 1, bool randomPartitionKey = true)
        {
            Assert.IsFalse(!randomPartitionKey && perPKItemCount > 1);

            List<ToDoActivity> createdList = new List<ToDoActivity>();
            for (int i = 0; i < pkCount; i++)
            {
                string pk = "TBD";
                if (randomPartitionKey)
                {
                    pk += Guid.NewGuid().ToString();
                }

                for (int j = 0; j < perPKItemCount; j++)
                {
                    ToDoActivity temp = CreateRandomToDoActivity(pk);

                    createdList.Add(temp);

                    await this.Container.Items.CreateItemAsync<ToDoActivity>(partitionKey: temp.status, item: temp);
                }
            }

            return createdList;
        }

        private ToDoActivity CreateRandomToDoActivity(string pk = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }

            return new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                description = "CreateRandomToDoActivity",
                status = pk,
                taskNum = 42,
                cost = double.MaxValue
            };
        }

        public class ToDoActivity
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            public string status { get; set; }
        }
    }
}
