//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure;
    using Azure.Cosmos;
    using Azure.Cosmos.Query;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    //using Newtonsoft.Json;
    //using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosItemTests : BaseCosmosClientHelper
    {
        private CosmosContainer Container = null;
        private ContainerProperties containerSettings = null;

        private static readonly string nonPartitionItemId = "fixed-Container-Item";
        private static readonly string undefinedPartitionItemId = "undefined-partition-Item";

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
            Assert.IsNotNull(response.Value);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CreateDropItemTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            Assert.IsNotNull(response);
            Response<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new PartitionKey(testItem.status), id: testItem.id);
            Assert.IsNotNull(deleteResponse);
        }

        [TestMethod]
        public async Task CustomSerilizerTest()
        {
            string id1 = "MyCustomSerilizerTestId1";
            string id2 = "MyCustomSerilizerTestId2";
            string pk = "MyTestPk";

            // Delete the item to prevent create conflicts if test is run multiple times
            using (await this.Container.DeleteItemStreamAsync(id1, new PartitionKey(pk)))
            { }
            using (await this.Container.DeleteItemStreamAsync(id2, new PartitionKey(pk)))
            { }

            // Both items have null description
            ToDoActivity testItem = new ToDoActivity { id = id1, status = pk };
            ToDoActivity testItem2 = new ToDoActivity { id = id2, status = pk };

            // Create a client that ignore null
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                Serializer = CosmosTextJsonSerializer.CreateUserDefaultSerializer(
                    new CosmosSerializationOptions()
                    {
                        IgnoreNullValues = true
                    })
            };

            CosmosClient ignoreNullClient = TestCommon.CreateCosmosClient(clientOptions);
            CosmosContainer ignoreContainer = ignoreNullClient.GetContainer(this.database.Id, this.Container.Id);
            await ignoreContainer.CreateItemAsync<ToDoActivity>(item: testItem);
            Response<Dictionary<string, object>> ignoreNullResponse = await ignoreContainer.ReadItemAsync<Dictionary<string, object>>(id1, new PartitionKey(pk));
            Assert.IsNotNull(ignoreNullResponse);
            Assert.IsNotNull(ignoreNullResponse.Value);
            Assert.IsFalse(ignoreNullResponse.Value.ContainsKey("description"));

            await this.Container.CreateItemAsync<ToDoActivity>(item: testItem2);
            Response<Dictionary<string, object>> keepNullResponse = await this.Container.ReadItemAsync<Dictionary<string, object>>(id2, new PartitionKey(pk));
            Assert.IsNotNull(keepNullResponse);
            Assert.IsNotNull(keepNullResponse.Value);
            Assert.IsTrue(keepNullResponse.Value.ContainsKey("description"));

            using (await this.Container.DeleteItemStreamAsync(id1, new PartitionKey(pk)))
            { }
            using (await this.Container.DeleteItemStreamAsync(id2, new PartitionKey(pk)))
            { }
        }

        [TestMethod]
        public async Task CreateDropItemUndefinedPartitionKeyTest()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString()
            };

            Response<dynamic> response = await this.Container.CreateItemAsync<dynamic>(item: testItem, partitionKey: new PartitionKey(Microsoft.Azure.Documents.Undefined.Value));
            Assert.IsNotNull(response);
            Assert.AreEqual((int)HttpStatusCode.Created, response.GetRawResponse().Status);
            //Assert.IsNotNull(response.MaxResourceQuota);
            //Assert.IsNotNull(response.CurrentResourceQuotaUsage);

            Response<dynamic> deleteResponse = await this.Container.DeleteItemAsync<dynamic>(id: testItem.id, partitionKey: new PartitionKey(Microsoft.Azure.Documents.Undefined.Value));
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task CreateDropItemPartitionKeyNotInTypeTest()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString()
            };

            Response<dynamic> response = await this.Container.CreateItemAsync<dynamic>(item: testItem);
            Assert.IsNotNull(response);
            Assert.AreEqual((int)HttpStatusCode.Created, response.GetRawResponse().Status);
            //Assert.IsNotNull(response.MaxResourceQuota);
            //Assert.IsNotNull(response.CurrentResourceQuotaUsage);

            Response<dynamic> readResponse = await this.Container.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: PartitionKey.None);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual((int)HttpStatusCode.OK, readResponse.GetRawResponse().Status);

            Response<dynamic> deleteResponse = await this.Container.DeleteItemAsync<dynamic>(id: testItem.id, partitionKey: PartitionKey.None);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);

            try
            {
                readResponse = await this.Container.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: PartitionKey.None);
                Assert.Fail("Should throw exception.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public async Task CreateDropItemMultiPartPartitionKeyTest()
        {
            CosmosContainer multiPartPkContainer = await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/a/b/c");

            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString(),
                a = new
                {
                    b = new
                    {
                        c = "pk1",
                    }
                }
            };

            Response<dynamic> response = await multiPartPkContainer.CreateItemAsync<dynamic>(item: testItem);
            Assert.IsNotNull(response);
            Assert.AreEqual((int)HttpStatusCode.Created, response.GetRawResponse().Status);

            Response<dynamic> readResponse = await multiPartPkContainer.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: new PartitionKey("pk1"));
            Assert.IsNotNull(readResponse);
            Assert.AreEqual((int)HttpStatusCode.OK, readResponse.GetRawResponse().Status);

            Response<dynamic> deleteResponse = await multiPartPkContainer.DeleteItemAsync<dynamic>(id: testItem.id, partitionKey: new PartitionKey("pk1"));
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);

            try
            {
                readResponse = await multiPartPkContainer.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: new PartitionKey("pk1"));
                Assert.Fail("Should throw exception.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReadCollectionNotExists()
        {
            string collectionName = Guid.NewGuid().ToString();
            CosmosContainer testContainer = this.database.GetContainer(collectionName);
            await CosmosItemTests.TestNonePKForNonExistingContainer(testContainer);

            // Item -> Container -> Database contract 
            string dbName = Guid.NewGuid().ToString();
            testContainer = this.cosmosClient.GetDatabase(dbName).GetContainer(collectionName);
            await CosmosItemTests.TestNonePKForNonExistingContainer(testContainer);
        }

        [TestMethod]
        public async Task NonPartitionKeyLookupCacheTest()
        {
            int count = 0;
            CosmosClient client = TestCommon.CreateCosmosClient(builder =>
            {
                builder.WithConnectionModeDirect();
                builder.WithSendingRequestEventArgs((sender, e) =>
                {
                    if (e.DocumentServiceRequest != null)
                    {
                        Trace.TraceInformation($"{e.DocumentServiceRequest.ToString()}");
                    }

                    if (e.HttpRequest != null)
                    {
                        Trace.TraceInformation($"{e.HttpRequest.ToString()}");
                    }

                    if (e.IsHttpRequest()
                        && e.HttpRequest.RequestUri.AbsolutePath.Contains("/colls/"))
                    {
                        count++;
                    }

                    if (e.IsHttpRequest()
                        && e.HttpRequest.RequestUri.AbsolutePath.Contains("/pkranges"))
                    {
                        Debugger.Break();
                    }
                });
            });

            string dbName = Guid.NewGuid().ToString();
            string containerName = Guid.NewGuid().ToString();
            ContainerCore testContainer = (ContainerCore)client.GetContainer(dbName, containerName);

            int loopCount = 2;
            for (int i = 0; i < loopCount; i++)
            {
                try
                {
                    await testContainer.GetNonePartitionKeyValueAsync();
                    Assert.Fail();
                }
                catch (CosmosException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
                {
                }
            }

            Assert.AreEqual(loopCount, count);

            // Create real container and address 
            Cosmos.CosmosDatabase db = await client.CreateDatabaseAsync(dbName);
            CosmosContainer container = await db.CreateContainerAsync(containerName, "/id");

            // reset counter
            count = 0;
            for (int i = 0; i < loopCount; i++)
            {
                await testContainer.GetNonePartitionKeyValueAsync();
            }

            // expected once post create 
            Assert.AreEqual(1, count);

            // reset counter
            count = 0;
            for (int i = 0; i < loopCount; i++)
            {
                await testContainer.GetRIDAsync(default(CancellationToken));
            }

            // Already cached by GetNonePartitionKeyValueAsync before
            Assert.AreEqual(0, count);

            // reset counter
            count = 0;
            int expected = 0;
            for (int i = 0; i < loopCount; i++)
            {
                await testContainer.GetRoutingMapAsync(default(CancellationToken));
                expected = count;
            }

            // OkRagnes should be fetched only once. 
            // Possible to make multiple calls for ranges
            Assert.AreEqual(expected, count);
        }

        [TestMethod]
        public async Task CreateDropItemStreamTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            using (Stream stream = TestCommon.Serializer.Value.ToStream<ToDoActivity>(testItem))
            {
                using (Response response = await this.Container.CreateItemStreamAsync(partitionKey: new PartitionKey(testItem.status), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual((int)HttpStatusCode.Created, response.Status);
                    if (response.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.RequestCharge, out string requestChargeString))
                    {
                        Assert.IsTrue(double.Parse(requestChargeString, CultureInfo.InvariantCulture) > 0);
                    }
                    Assert.IsNotNull(response.Headers.GetActivityId());
                    //Assert.IsNotNull(response.Headers.ETag);
                }
            }

            using (Response deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new PartitionKey(testItem.status), id: testItem.id))
            {
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.Status);
                if (deleteResponse.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.RequestCharge, out string requestChargeString))
                {
                    Assert.IsTrue(double.Parse(requestChargeString, CultureInfo.InvariantCulture) > 0);
                }

                Assert.IsNotNull(deleteResponse.Headers.GetActivityId());
            }
        }

        [TestMethod]
        public async Task UpsertItemStreamTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            using (Stream stream = TestCommon.Serializer.Value.ToStream<ToDoActivity>(testItem))
            {
                //Create the object
                using (Response response = await this.Container.UpsertItemStreamAsync(partitionKey: new PartitionKey(testItem.status), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual((int)HttpStatusCode.Created, response.Status);
                    using (StreamReader str = new StreamReader(response.ContentStream))
                    {
                        string responseContentAsString = await str.ReadToEndAsync();
                    }
                }
            }

            //Updated the taskNum field
            testItem.taskNum = 9001;
            using (Stream stream = TestCommon.Serializer.Value.ToStream<ToDoActivity>(testItem))
            {
                using (Response response = await this.Container.UpsertItemStreamAsync(partitionKey: new PartitionKey(testItem.status), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual((int)HttpStatusCode.OK, response.Status);
                }
            }
            using (Response deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new PartitionKey(testItem.status), id: testItem.id))
            {
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(deleteResponse.Status, (int)HttpStatusCode.NoContent);
            }
        }

        [TestMethod]
        public async Task ReplaceItemStreamTest()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            using (Stream stream = TestCommon.Serializer.Value.ToStream<ToDoActivity>(testItem))
            {
                //Replace a non-existing item. It should fail, and not throw an exception.
                using (Response response = await this.Container.ReplaceItemStreamAsync(
                    partitionKey: new PartitionKey(testItem.status),
                    id: testItem.id,
                    streamPayload: stream))
                {
                    Assert.IsFalse(response.IsSuccessStatusCode());
                    Assert.IsNotNull(response);
                    Assert.AreEqual((int)HttpStatusCode.NotFound, response.Status);
                }
            }

            using (Stream stream = TestCommon.Serializer.Value.ToStream<ToDoActivity>(testItem))
            {
                //Create the item
                using (Response response = await this.Container.CreateItemStreamAsync(partitionKey: new PartitionKey(testItem.status), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual((int)HttpStatusCode.Created, response.Status);
                }
            }

            //Updated the taskNum field
            testItem.taskNum = 9001;
            using (Stream stream = TestCommon.Serializer.Value.ToStream<ToDoActivity>(testItem))
            {
                using (Response response = await this.Container.ReplaceItemStreamAsync(partitionKey: new PartitionKey(testItem.status), id: testItem.id, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual((int)HttpStatusCode.OK, response.Status);
                }

                using (Response deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new PartitionKey(testItem.status), id: testItem.id))
                {
                    Assert.IsNotNull(deleteResponse);
                    Assert.AreEqual(deleteResponse.Status, (int)HttpStatusCode.NoContent);
                }
            }
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod]
        [Timeout(5000)]
        public async Task ItemStreamIterator(bool useStatelessIterator)
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 3, randomPartitionKey: true);
            HashSet<string> itemIds = deleteList.Select(x => x.id).ToHashSet<string>();

            string lastContinuationToken = null;
            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = 1
            };

            if (useStatelessIterator)
            {
                while(itemIds.Count > 0)
                {
                    await foreach (Response responseMessage in this.Container.GetItemQueryStreamIterator(
                        continuationToken: lastContinuationToken,
                        requestOptions: requestOptions))
                    {
                        responseMessage.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.Continuation, out lastContinuationToken);
                        Collection<ToDoActivity> response = TestCommon.Serializer.Value.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.ContentStream).Data;
                        foreach (ToDoActivity toDoActivity in response)
                        {
                            if (itemIds.Contains(toDoActivity.id))
                            {
                                itemIds.Remove(toDoActivity.id);
                            }
                        }
                        break;
                    }
                }
            }
            else
            {
                await foreach (Response responseMessage in this.Container.GetItemQueryStreamIterator(requestOptions: requestOptions))
                {
                    responseMessage.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.Continuation, out lastContinuationToken);
                    Collection<ToDoActivity> response = TestCommon.Serializer.Value.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.ContentStream).Data;
                    foreach (ToDoActivity toDoActivity in response)
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
        public async Task ItemCustomSerialzierTest()
        {
            DateTime createDateTime = DateTime.UtcNow;
            Dictionary<string, int> keyValuePairs = new Dictionary<string, int>()
            {
                {"test1", 42 },
                {"test42", 9001 }
            };

            dynamic testItem1 = new
            {
                id = "ItemCustomSerialzierTest1",
                cost = (double?)null,
                totalCost = 98.2789,
                status = "MyCustomStatus",
                taskNum = 4909,
                createdDateTime = createDateTime,
                statusCode = HttpStatusCode.Accepted,
                itemIds = new int[] { 1, 5, 10 },
                dictionary = keyValuePairs
            };

            dynamic testItem2 = new
            {
                id = "ItemCustomSerialzierTest2",
                cost = (double?)null,
                totalCost = 98.2789,
                status = "MyCustomStatus",
                taskNum = 4909,
                createdDateTime = createDateTime,
                statusCode = HttpStatusCode.Accepted,
                itemIds = new int[] { 1, 5, 10 },
                dictionary = keyValuePairs
            };

            JsonSerializerOptions jsonSerializerSettings = new JsonSerializerOptions();
            jsonSerializerSettings.Converters.Add(new CosmosSerializerHelper.FormatNumbersTextConverter());
            jsonSerializerSettings.Converters.Add(new CosmosSerializerHelper.FormatDoubleAsTextConverter());

            List<QueryDefinition> queryDefinitions = new List<QueryDefinition>()
            {
                new QueryDefinition("select * from t where t.status = @status" ).WithParameter("@status", testItem1.status),
                new QueryDefinition("select * from t where t.cost = @cost" ).WithParameter("@cost", testItem1.cost),
                new QueryDefinition("select * from t where t.taskNum = @taskNum" ).WithParameter("@taskNum", testItem1.taskNum),
                new QueryDefinition("select * from t where t.totalCost = @totalCost" ).WithParameter("@totalCost", testItem1.totalCost),
                new QueryDefinition("select * from t where t.createdDateTime = @createdDateTime" ).WithParameter("@createdDateTime", testItem1.createdDateTime),
                new QueryDefinition("select * from t where t.statusCode = @statusCode" ).WithParameter("@statusCode", testItem1.statusCode),
                new QueryDefinition("select * from t where t.itemIds = @itemIds" ).WithParameter("@itemIds", testItem1.itemIds),
                new QueryDefinition("select * from t where t.dictionary = @dictionary" ).WithParameter("@dictionary", testItem1.dictionary),
                new QueryDefinition("select * from t where t.status = @status and t.cost = @cost" )
                    .WithParameter("@status", testItem1.status)
                    .WithParameter("@cost", testItem1.cost),
            };

            int toStreamCount = 0;
            int fromStreamCount = 0;
            CosmosSerializerHelper cosmosSerializerHelper = new CosmosSerializerHelper(
                jsonSerializerSettings,
                toStreamCallBack: (itemValue) =>
                {
                    Type itemType = itemValue != null ? itemValue.GetType() : null;
                    if (itemValue == null
                        || itemType == typeof(int)
                        || itemType == typeof(double)
                        || itemType == typeof(string)
                        || itemType == typeof(DateTime)
                        || itemType == typeof(HttpStatusCode)
                        || itemType == typeof(int[])
                        || itemType == typeof(Dictionary<string, int>))
                    {
                        toStreamCount++;
                    }
                },
                fromStreamCallback: (item) => fromStreamCount++);

            CosmosClientOptions options = new CosmosClientOptions()
            {
                Serializer = cosmosSerializerHelper
            };

            CosmosClient clientSerializer = TestCommon.CreateCosmosClient(options);
            CosmosContainer containerSerializer = clientSerializer.GetContainer(this.database.Id, this.Container.Id);

            try
            {
                await containerSerializer.CreateItemAsync<dynamic>(testItem1);
                await containerSerializer.CreateItemAsync<dynamic>(testItem2);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Ignore conflicts since the object already exists
            }

            foreach (QueryDefinition queryDefinition in queryDefinitions)
            {
                toStreamCount = 0;
                fromStreamCount = 0;

                List<dynamic> allItems = new List<dynamic>();

                await foreach(Page<Dictionary<string, object>> response in containerSerializer.GetItemQueryIterator<Dictionary<string, object>>(
                    queryDefinition: queryDefinition).AsPages())
                {
                    allItems.AddRange(response.Values);
                }

                Assert.AreEqual(2, allItems.Count, $"missing query results. Only found: {allItems.Count} items for query:{queryDefinition.ToSqlQuerySpec().QueryText}");
                foreach (Dictionary<string, object> item in allItems)
                {
                    Assert.IsFalse(string.Equals(testItem1.id, item["id"]) || string.Equals(testItem2.id, item["id"]));
                    Assert.IsTrue(((JsonElement)item["totalCost"]).GetString() != null);
                    Assert.IsTrue(((JsonElement)item["taskNum"]).GetString() != null);
                }

                // Each parameter in query spec should be a call to the custom serializer
                int parameterCount = queryDefinition.ToSqlQuerySpec().Parameters.Count;
                Assert.AreEqual(parameterCount, toStreamCount, $"missing to stream call. Expected: {parameterCount}, Actual: {toStreamCount} for query:{queryDefinition.ToSqlQuerySpec().QueryText}");
                Assert.AreEqual(1, fromStreamCount);
            }
        }

        [TestMethod]
        public async Task ItemIterator()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 3, randomPartitionKey: true);
            HashSet<string> itemIds = deleteList.Select(x => x.id).ToHashSet<string>();
            await foreach (ToDoActivity toDoActivity in this.Container.GetItemQueryIterator<ToDoActivity>())
            {
                if (itemIds.Contains(toDoActivity.id))
                {
                    itemIds.Remove(toDoActivity.id);
                }
            }

            Assert.AreEqual(itemIds.Count, 0);
        }


        [TestMethod]
        public async Task ItemStreamContractVerifier()
        {
            int totalCount = 4;
            Dictionary<string, ToDoActivity> toDoActivities = new Dictionary<string, ToDoActivity>();
            // Create 3 constant items;
            for (int i = 0; i < totalCount; i++)
            {
                ToDoActivity toDoActivity = new ToDoActivity()
                {
                    id = "toDoActivity" + i,
                    status = "InProgress",
                    cost = 9000 + i,
                    description = "Constant to do activity",
                    taskNum = i
                };

                toDoActivities.Add(toDoActivity.id, toDoActivity);

                await this.Container.CreateItemAsync<ToDoActivity>(toDoActivity);
            }

            List<IAsyncEnumerable<Response>> FeedIterators = new List<IAsyncEnumerable<Response>>();

            // The stream contract should return the same contract as read feed.
            // {
            //    "_rid": "containerRid",
            //    "Documents": [{
            //        "id": "03230",
            //        "_rid": "qHVdAImeKAQBAAAAAAAAAA==",
            //        "_self": "dbs\/qHVdAA==\/colls\/qHVdAImeKAQ=\/docs\/qHVdAImeKAQBAAAAAAAAAA==\/",
            //        "_etag": "\"410000b0-0000-0000-0000-597916b00000\"",
            //        "_attachments": "attachments\/",
            //        "_ts": 1501107886
            //    }],
            //    "_count": 1
            // }

            IAsyncEnumerable<Response> setIterator =
                this.Container.GetItemQueryStreamIterator();
            FeedIterators.Add(setIterator);

            QueryRequestOptions options = new QueryRequestOptions()
            {
                MaxItemCount = 4,
                MaxConcurrency = 1,
            };

            IAsyncEnumerable<Response> queryIterator = this.Container.GetItemQueryStreamIterator(
                    queryText: @"select * from t where t.id != """" ",
                    requestOptions: options);

            FeedIterators.Add(queryIterator);
            string previousResult = null;

            foreach (IAsyncEnumerable<Response> iterator in FeedIterators)
            {
                int count = 0;
                await foreach(Response response in iterator)
                {
                    using (StreamReader sr = new StreamReader(response.ContentStream))
                    {
                        string jsonString = await sr.ReadToEndAsync();
                        Assert.IsNotNull(jsonString);
                        JsonDocument jObject = JsonDocument.Parse(jsonString);
                        Assert.IsNotNull(jObject.RootElement.GetProperty("Documents"));
                        Assert.IsNotNull(jObject.RootElement.GetProperty("_rid"));
                        Assert.IsNotNull(jObject.RootElement.GetProperty("_count"));
                        Assert.IsTrue(jObject.RootElement.GetProperty("_count").GetInt32() >= 0);
                        foreach (JsonElement item in jObject.RootElement.GetProperty("Documents").EnumerateArray())
                        {
                            count++;
                            Assert.IsNotNull(item.GetProperty("id"));
                            ToDoActivity createdItem = toDoActivities[item.GetProperty("id").GetString()];

                            Assert.AreEqual(createdItem.taskNum, item.GetProperty("taskNum").GetInt32());
                            Assert.AreEqual(createdItem.cost, item.GetProperty("cost").GetDouble());
                            Assert.AreEqual(createdItem.description, item.GetProperty("description").GetString());
                            Assert.AreEqual(createdItem.status, item.GetProperty("status").GetString());
                            Assert.IsNotNull(item.GetProperty("_rid"));
                            Assert.IsNotNull(item.GetProperty("_self"));
                            Assert.IsNotNull(item.GetProperty("_etag"));
                            Assert.IsNotNull(item.GetProperty("_attachments"));
                            Assert.IsNotNull(item.GetProperty("_ts"));
                        }

                        if (previousResult != null)
                        {
                            Assert.AreEqual(previousResult, jObject.ToString());
                        }
                        else
                        {
                            previousResult = jObject.ToString();
                        }
                    }
                }

                Assert.AreEqual(totalCount, count);
            }
        }


        [DataRow(1, 1)]
        [DataRow(5, 5)]
        [DataRow(6, 2)]
        [DataTestMethod]
        public async Task QuerySinglePartitionItemStreamTest(int perPKItemCount, int maxItemCount)
        {
            IList<ToDoActivity> deleteList = deleteList = await ToDoActivity.CreateRandomItems(this.Container, pkCount: 3, perPKItemCount: perPKItemCount, randomPartitionKey: true);
            ToDoActivity find = deleteList.First();

            QueryDefinition sql = new QueryDefinition("select * from r where r.status = @status").WithParameter("@status", find.status);

            int iterationCount = 0;
            int totalReadItem = 0;
            int expectedIterationCount = perPKItemCount / maxItemCount;
            string lastContinuationToken = null;

            do
            {
                iterationCount++;
                IAsyncEnumerable<Response> feedIterator = this.Container.GetItemQueryStreamIterator(
                    sql,
                    continuationToken: lastContinuationToken,
                    requestOptions: new QueryRequestOptions()
                    {
                        MaxItemCount = maxItemCount,
                        MaxConcurrency = 1,
                        PartitionKey = new Cosmos.PartitionKey(find.status),
                    });

                Stream content = null;
                await foreach(Response response in feedIterator)
                {
                    lastContinuationToken = response.Headers.GetContinuationToken();
                    content = response.ContentStream;
                    break;
                }

                Trace.TraceInformation($"ContinuationToken: {lastContinuationToken}");

                ToDoActivity[] results = (await JsonSerializer.DeserializeAsync<CosmosFeedResponseUtil<ToDoActivity>>(content)).Data.ToArray();
                ToDoActivity[] readTodoActivities = results.OrderBy(e => e.id)
                    .ToArray();

                ToDoActivity[] expectedTodoActivities = deleteList
                        .Where(e => e.status == find.status)
                        .Where(e => readTodoActivities.Any(e1 => e1.id == e.id))
                        .OrderBy(e => e.id)
                        .ToArray();

                totalReadItem += expectedTodoActivities.Length;
                string expectedSerialized = JsonSerializer.Serialize(expectedTodoActivities);
                string readSerialized = JsonSerializer.Serialize(readTodoActivities);
                Trace.TraceInformation($"Expected: {Environment.NewLine} {expectedSerialized}");
                Trace.TraceInformation($"Read: {Environment.NewLine} {readSerialized}");

                int count = results.Length;
                Assert.AreEqual(maxItemCount, count);

                Assert.AreEqual(expectedSerialized, readSerialized);

                Assert.AreEqual(maxItemCount, expectedTodoActivities.Length);
            }
            while (lastContinuationToken != null);

            Assert.AreEqual(expectedIterationCount, iterationCount);
            Assert.AreEqual(perPKItemCount, totalReadItem);
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionQuery()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 3, randomPartitionKey: true);

            ToDoActivity find = deleteList.First();
            QueryDefinition sql = new QueryDefinition("select * from toDoActivity t where t.id = '" + find.id + "'");

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxBufferedItemCount = 10,
                ResponseContinuationTokenLimitInKb = 500,
                MaxItemCount = 1,
                MaxConcurrency = 1,
            };

            await foreach(Page<ToDoActivity> iter in this.Container.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: requestOptions).AsPages())
            {
                Assert.AreEqual(1, iter.Values.Count);
                ToDoActivity response = iter.Values.First();
                Assert.AreEqual(find.id, response.id);
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionOrderByQueryStream()
        {
            CultureInfo defaultCultureInfo = System.Threading.Thread.CurrentThread.CurrentCulture;

            CultureInfo[] cultureInfoList = new CultureInfo[]
            {
                defaultCultureInfo,
                System.Globalization.CultureInfo.GetCultureInfo("fr-FR")
            };

            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 300, randomPartitionKey: true);

            try
            {
                foreach (CultureInfo cultureInfo in cultureInfoList)
                {
                    System.Threading.Thread.CurrentThread.CurrentCulture = cultureInfo;

                    QueryDefinition sql = new QueryDefinition("SELECT * FROM toDoActivity t ORDER BY t.taskNum ");

                    QueryRequestOptions requestOptions = new QueryRequestOptions()
                    {
                        MaxBufferedItemCount = 10,
                        ResponseContinuationTokenLimitInKb = 500,
                        MaxConcurrency = 5,
                        MaxItemCount = 1,
                    };

                    List<ToDoActivity> resultList = new List<ToDoActivity>();
                    double totalRequstCharge = 0;

                    await foreach(Response iter in this.Container.GetItemQueryStreamIterator(
                        sql,
                        requestOptions: requestOptions))
                    {
                        Assert.IsTrue(iter.IsSuccessStatusCode());

                        totalRequstCharge += iter.Headers.GetRequestCharge();

                        ToDoActivity[] activities = TestCommon.Serializer.Value.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(iter.ContentStream).Data.ToArray();
                        Assert.AreEqual(1, activities.Length);
                        ToDoActivity response = activities.First();
                        resultList.Add(response);
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
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = defaultCultureInfo;
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionQueryStream()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 101, randomPartitionKey: true);
            QueryDefinition sql = new QueryDefinition("SELECT * FROM toDoActivity t");

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxConcurrency = 5,
                MaxItemCount = 5,
            };

            List<ToDoActivity> resultList = new List<ToDoActivity>();
            double totalRequstCharge = 0;
            await foreach(Response iter in this.Container.GetItemQueryStreamIterator(sql, requestOptions: requestOptions))
            {
                Assert.IsTrue(iter.IsSuccessStatusCode());
                Assert.IsNull(iter.ReasonPhrase);
                totalRequstCharge += iter.Headers.GetRequestCharge();

                ToDoActivity[] response = TestCommon.Serializer.Value.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(iter.ContentStream).Data.ToArray();
                Assert.IsTrue(response.Length <= 5);
                resultList.AddRange(response);
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

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemSinglePartitionQueryStream()
        {
            //Create a 101 random items with random guid PK values
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, pkCount: 101, perPKItemCount: 1, randomPartitionKey: true);

            // Create 10 items with same pk value
            IList<ToDoActivity> findItems = await ToDoActivity.CreateRandomItems(this.Container, pkCount: 1, perPKItemCount: 10, randomPartitionKey: false);

            string findPkValue = findItems.First().status;
            QueryDefinition sql = new QueryDefinition("SELECT * FROM toDoActivity t where t.status = @pkValue").WithParameter("@pkValue", findPkValue);


            double totalRequstCharge = 0;
            List<ToDoActivity> foundItems = new List<ToDoActivity>();
            await foreach (Response iter in this.Container.GetItemQueryStreamIterator(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    MaxConcurrency = 1,
                    PartitionKey = new Cosmos.PartitionKey(findPkValue),
                }))
            {
                Assert.IsTrue(iter.IsSuccessStatusCode());
                Assert.IsNull(iter.ReasonPhrase);
                if (iter.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.RequestCharge, out string requestChargeString))
                {
                    totalRequstCharge += double.Parse(requestChargeString, CultureInfo.InvariantCulture);
                }

                Collection<ToDoActivity> response = TestCommon.Serializer.Value.FromStream<CosmosFeedResponseUtil<ToDoActivity>>(iter.ContentStream).Data;
                foundItems.AddRange(response);
            }

            Assert.AreEqual(findItems.Count, foundItems.Count);
            Assert.IsFalse(foundItems.Any(x => !string.Equals(x.status, findPkValue)), "All the found items should have the same PK value");
            Assert.IsTrue(totalRequstCharge > 0);
        }

        [TestMethod]
        public async Task EpkPointReadTest()
        {
            string pk = Guid.NewGuid().ToString();
            string epk = new PartitionKey(pk)
                            .InternalKey
                            .GetEffectivePartitionKeyString(this.containerSettings.PartitionKey);

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions
            {
                IsEffectivePartitionKeyRouting = true,
                Properties = new Dictionary<string, object>()
            };
            itemRequestOptions.Properties.Add(Microsoft.Azure.Documents.WFConstants.BackendHeaders.EffectivePartitionKeyString, epk);

            Response response = await this.Container.ReadItemStreamAsync(
                Guid.NewGuid().ToString(),
                PartitionKey.Null,
                itemRequestOptions);

            // Ideally it should be NotFound
            // BadReqeust bcoz collection is regular and not binary 
            Assert.AreEqual((int)HttpStatusCode.BadRequest, response.Status);

            await this.Container.CreateItemAsync<dynamic>(new { id = Guid.NewGuid().ToString(), status = "test" });
            epk = new PartitionKey("test")
                           .InternalKey
                           .GetEffectivePartitionKeyString(this.containerSettings.PartitionKey);

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions
            {
                IsEffectivePartitionKeyRouting = true,
                Properties = new Dictionary<string, object>()
            };
            queryRequestOptions.Properties.Add(Microsoft.Azure.Documents.WFConstants.BackendHeaders.EffectivePartitionKeyString, epk);

            await foreach(Page<dynamic> page in this.Container.GetItemQueryIterator<dynamic>(
                    queryText: "SELECT * FROM root",
                    requestOptions: queryRequestOptions).AsPages())
            {
                Assert.AreEqual(1, page.Values.Count);
            }
        }

        /// <summary>
        /// Validate that if the EPK is set in the options that only a single range is selected.
        /// </summary>
        [TestMethod]
        public async Task ItemEpkQuerySingleKeyRangeValidation()
        {
            IList<ToDoActivity> deleteList = new List<ToDoActivity>();
            ContainerCore container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk",
                    throughput: 15000);
                container = (ContainerCore)containerResponse;

                // Get all the partition key ranges to verify there is more than one partition
                IRoutingMapProvider routingMapProvider = await this.cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync();
                IReadOnlyList<Microsoft.Azure.Documents.PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(
                    containerResponse.Value.ResourceId,
                    new Microsoft.Azure.Documents.Routing.Range<string>("00", "FF", isMaxInclusive: true, isMinInclusive: true));

                // If this fails the RUs of the container needs to be increased to ensure at least 2 partitions.
                Assert.IsTrue(ranges.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                ContainerQueryProperties containerQueryProperties = new ContainerQueryProperties(
                    containerResponse.Value.ResourceId,
                    null,
                    containerResponse.Value.PartitionKey);

                // There should only be one range since the EPK option is set.
                List<Microsoft.Azure.Documents.PartitionKeyRange> partitionKeyRanges = await CosmosQueryExecutionContextFactory.GetTargetPartitionKeyRangesAsync(
                    queryClient: new CosmosQueryClientCore(container.ClientContext, container),
                    resourceLink: container.LinkUri.OriginalString,
                    partitionedQueryExecutionInfo: null,
                    containerQueryProperties: containerQueryProperties,
                    properties: new Dictionary<string, object>()
                    {
                        {"x-ms-effective-partition-key-string", "AA" }
                    });

                Assert.IsTrue(partitionKeyRanges.Count == 1, "Only 1 partition key range should be selected since the EPK option is set.");
            }
            finally
            {
                if (container != null)
                {
                    await container.DeleteContainerAsync();
                }
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemQueryStreamSerializationSetting()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(this.Container, 101, randomPartitionKey: true);

            QueryDefinition sql = new QueryDefinition("SELECT * FROM toDoActivity t ORDER BY t.taskNum");

            CosmosSerializationFormatOptions options = new CosmosSerializationFormatOptions(
                Microsoft.Azure.Documents.ContentSerializationFormat.CosmosBinary.ToString(),
                (content) => JsonNavigator.Create(content),
                () => Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Binary));

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                CosmosSerializationFormatOptions = options,
                MaxConcurrency = 5,
                MaxItemCount = 5,
            };

            List<ToDoActivity> resultList = new List<ToDoActivity>();
            double totalRequstCharge = 0;
            await foreach(Response response in this.Container.GetItemQueryStreamIterator(
                sql,
                requestOptions: requestOptions))
            {
                Assert.IsTrue(response.IsSuccessStatusCode());
                Assert.IsNull(response.ReasonPhrase);
                totalRequstCharge += response.Headers.GetRequestCharge();

                //Copy the stream and check that the first byte is the correct value
                MemoryStream memoryStream = new MemoryStream();
                response.ContentStream.CopyTo(memoryStream);
                byte[] content = memoryStream.ToArray();

                // Examine the first buffer byte to determine the serialization format
                byte firstByte = content[0];
                Assert.AreEqual(128, firstByte);
                Assert.AreEqual(JsonSerializationFormat.Binary, (JsonSerializationFormat)firstByte);

                IJsonReader reader = Microsoft.Azure.Cosmos.Json.JsonReader.Create(content);
                IJsonWriter textWriter = Microsoft.Azure.Cosmos.Json.JsonWriter.Create(JsonSerializationFormat.Text);
                textWriter.WriteAll(reader);
                string json = Encoding.UTF8.GetString(textWriter.GetResult().ToArray());
                Assert.IsNotNull(json);
                ToDoActivity[] responseActivities = JsonSerializer.Deserialize<CosmosFeedResponseUtil<ToDoActivity>>(json).Data.ToArray();
                Assert.IsTrue(responseActivities.Length <= 5);
                resultList.AddRange(responseActivities);
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

        /// <summary>
        /// Validate that the max item count works correctly.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ValidateMaxItemCountOnItemQuery()
        {
            IList<ToDoActivity> deleteList = await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 1, perPKItemCount: 6, randomPartitionKey: false);

            ToDoActivity toDoActivity = deleteList.First();
            QueryDefinition sql = new QueryDefinition(
                "select * from toDoActivity t where t.status = @status")
                .WithParameter("@status", toDoActivity.status);

            // Test max size at 1
            await foreach(Page<ToDoActivity> iter in this.Container.GetItemQueryIterator<ToDoActivity>(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 1,
                    PartitionKey = new Cosmos.PartitionKey(toDoActivity.status),
                }).AsPages())
            {
                Assert.AreEqual(1, iter.Values.Count);
            }

            // Test max size at 2
            await foreach(Page<ToDoActivity> iter in this.Container.GetItemQueryIterator<ToDoActivity>(
                sql,
                requestOptions: new QueryRequestOptions()
                {
                    MaxItemCount = 2,
                    PartitionKey = new Cosmos.PartitionKey(toDoActivity.status),
                }).AsPages())
            {
                Assert.AreEqual(2, iter.Values.Count);
            }
        }

        /// <summary>
        /// Validate that the max item count works correctly.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task NegativeQueryTest()
        {
            IList<ToDoActivity> items = await ToDoActivity.CreateRandomItems(container: this.Container, pkCount: 10, perPKItemCount: 20, randomPartitionKey: true);

            try
            {
                await foreach(dynamic item in this.Container.GetItemQueryIterator<dynamic>(
                    queryText: "SELECT r.id FROM root r WHERE r._ts > 0",
                    requestOptions: new QueryRequestOptions() { ResponseContinuationTokenLimitInKb = 0, MaxItemCount = 10, MaxConcurrency = 1 }))
                {

                }
                Assert.Fail("Expected query to fail");
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.IsTrue(exception.Message.Contains("continuation token limit specified is not large enough"), exception.Message);
            }

            try
            {
                await foreach(dynamic item in this.Container.GetItemQueryIterator<dynamic>(
                    queryText: "SELECT r.id FROM root r WHERE r._ts >!= 0",
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 1 }))
                {

                }
                Assert.Fail("Expected query to fail");
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.IsTrue(exception.Message.Contains("Syntax error, incorrect syntax near"), exception.Message);
            }
        }

        [TestMethod]
        public async Task ItemRequestOptionAccessConditionTest()
        {
            // Create an item
            ToDoActivity testItem = (await ToDoActivity.CreateRandomItems(this.Container, 1, randomPartitionKey: true)).First();

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions()
            {
                IfMatch = new ETag(Guid.NewGuid().ToString()),
            };

            try
            {
                Response<ToDoActivity> response = await this.Container.ReplaceItemAsync<ToDoActivity>(
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
                Response<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new PartitionKey(testItem.status), id: testItem.id);
                Assert.IsNotNull(deleteResponse);
            }
        }

        // Read write non partition Container item.
        [TestMethod]
        public async Task ReadNonPartitionItemAsync()
        {
            ContainerCore fixedContainer = null;
            try
            {
                fixedContainer = await NonPartitionedContainerHelper.CreateNonPartitionedContainer(
                    this.database,
                    "ReadNonPartition" + Guid.NewGuid());

                await NonPartitionedContainerHelper.CreateItemInNonPartitionedContainer(fixedContainer, nonPartitionItemId);
                await NonPartitionedContainerHelper.CreateUndefinedPartitionItem((ContainerCore)this.Container, undefinedPartitionItemId);

                ContainerResponse containerResponse = await fixedContainer.ReadContainerAsync();
                Assert.IsTrue(containerResponse.Value.PartitionKey.Paths.Count > 0);
                Assert.AreEqual(PartitionKey.SystemKeyPath, containerResponse.Value.PartitionKey.Paths[0]);

                //Reading item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                Response<ToDoActivity> response = await fixedContainer.ReadItemAsync<ToDoActivity>(
                    partitionKey: PartitionKey.None,
                    id: nonPartitionItemId);

                Assert.IsNotNull(response.Value);
                Assert.AreEqual((int)HttpStatusCode.OK, response.GetRawResponse().Status);
                Assert.AreEqual(nonPartitionItemId, response.Value.id);

                //Adding item to fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                ToDoActivity itemWithoutPK = ToDoActivity.CreateRandomToDoActivity();
                Response<ToDoActivity> createResponseWithoutPk = await fixedContainer.CreateItemAsync<ToDoActivity>(
                 item: itemWithoutPK,
                 partitionKey: PartitionKey.None);

                Assert.IsNotNull(createResponseWithoutPk.Value);
                Assert.AreEqual((int)HttpStatusCode.Created, createResponseWithoutPk.GetRawResponse().Status);
                Assert.AreEqual(itemWithoutPK.id, createResponseWithoutPk.Value.id);

                //Updating item on fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                itemWithoutPK.status = "updatedStatus";
                Response<ToDoActivity> updateResponseWithoutPk = await fixedContainer.ReplaceItemAsync<ToDoActivity>(
                 id: itemWithoutPK.id,
                 item: itemWithoutPK,
                 partitionKey: PartitionKey.None);

                Assert.IsNotNull(updateResponseWithoutPk.Value);
                Assert.AreEqual((int)HttpStatusCode.OK, updateResponseWithoutPk.GetRawResponse().Status);
                Assert.AreEqual(itemWithoutPK.id, updateResponseWithoutPk.Value.id);

                //Adding item to fixed container with non-none PK.
                ToDoActivityAfterMigration itemWithPK = this.CreateRandomToDoActivityAfterMigration("TestPk");
                Response<ToDoActivityAfterMigration> createResponseWithPk = await fixedContainer.CreateItemAsync<ToDoActivityAfterMigration>(
                 item: itemWithPK);

                Assert.IsNotNull(createResponseWithPk.Value);
                Assert.AreEqual((int)HttpStatusCode.Created, createResponseWithPk.GetRawResponse().Status);
                Assert.AreEqual(itemWithPK.id, createResponseWithPk.Value.id);

                //Quering items on fixed container with cross partition enabled.
                QueryDefinition sql = new QueryDefinition("select * from r");
                await foreach(Page<dynamic> queryResponse in fixedContainer.GetItemQueryIterator<dynamic>(
                    sql,
                    requestOptions: new QueryRequestOptions() { MaxConcurrency = 1, MaxItemCount = 10 }).AsPages())
                {
                    Assert.AreEqual(3, queryResponse.Values.Count);
                }

                //Reading all items on fixed container.
                await foreach(Page<dynamic> queryResponse in fixedContainer.GetItemQueryIterator<dynamic>(requestOptions: new QueryRequestOptions() { MaxItemCount = 10 }).AsPages())
                {
                    Assert.AreEqual(3, queryResponse.Values.Count);
                }

                //Quering items on fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                await foreach(Page<dynamic> queryResponse in fixedContainer.GetItemQueryIterator<dynamic>(
                    new QueryDefinition("select * from r"),
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 10, PartitionKey = Cosmos.PartitionKey.None, }).AsPages())
                {
                    Assert.AreEqual(2, queryResponse.Values.Count);
                }

                //Quering items on fixed container with non-none PK.
                await foreach(Page<dynamic> queryResponse in fixedContainer.GetItemQueryIterator<dynamic>(
                    sql,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 10, PartitionKey = new Cosmos.PartitionKey(itemWithPK.status) }).AsPages())
                {
                    Assert.AreEqual(1, queryResponse.Values.Count);
                }

                //Deleting item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                Response<ToDoActivity> deleteResponseWithoutPk = await fixedContainer.DeleteItemAsync<ToDoActivity>(
                 partitionKey: PartitionKey.None,
                 id: itemWithoutPK.id);

                Assert.IsNull(deleteResponseWithoutPk.Value);
                Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponseWithoutPk.GetRawResponse().Status);

                //Deleting item from fixed container with non-none PK.
                Response<ToDoActivityAfterMigration> deleteResponseWithPk = await fixedContainer.DeleteItemAsync<ToDoActivityAfterMigration>(
                 partitionKey: new PartitionKey(itemWithPK.status),
                 id: itemWithPK.id);

                Assert.IsNull(deleteResponseWithPk.Value);
                Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponseWithPk.GetRawResponse().Status);

                //Reading item from partitioned container with CosmosContainerSettings.NonePartitionKeyValue.
                Response<ToDoActivity> undefinedItemResponse = await this.Container.ReadItemAsync<ToDoActivity>(
                    partitionKey: PartitionKey.None,
                    id: undefinedPartitionItemId);

                Assert.IsNotNull(undefinedItemResponse.Value);
                Assert.AreEqual((int)HttpStatusCode.OK, undefinedItemResponse.GetRawResponse().Status);
                Assert.AreEqual(undefinedPartitionItemId, undefinedItemResponse.Value.id);
            }
            finally
            {
                if (fixedContainer != null)
                {
                    await fixedContainer.DeleteContainerAsync();
                }
            }
        }

        // Move the data from None Partition to other logical partitions
        [TestMethod]
        public async Task MigrateDataInNonPartitionContainer()
        {
            ContainerCore fixedContainer = null;
            try
            {
                fixedContainer = await NonPartitionedContainerHelper.CreateNonPartitionedContainer(
                    this.database,
                    "ItemTestMigrateData" + Guid.NewGuid().ToString());

                const int ItemsToCreate = 4;
                // Insert a few items with no Partition Key
                for (int i = 0; i < ItemsToCreate; i++)
                {
                    await NonPartitionedContainerHelper.CreateItemInNonPartitionedContainer(fixedContainer, Guid.NewGuid().ToString());
                }

                // Read the container metadata
                ContainerResponse containerResponse = await fixedContainer.ReadContainerAsync();

                // Query items on the container that have no partition key value
                int resultsFetched = 0;
                QueryDefinition sql = new QueryDefinition("select * from r ");
                await foreach(Page<ToDoActivity> queryResponse in fixedContainer.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = 2, PartitionKey = Cosmos.PartitionKey.None, }).AsPages())
                {
                    resultsFetched += queryResponse.Values.Count;

                    // For the items returned with NonePartitionKeyValue
                    foreach(ToDoActivity activity in queryResponse.Values)
                    {
                        // Re-Insert into container with a partition key
                        ToDoActivityAfterMigration itemWithPK = new ToDoActivityAfterMigration
                        { id = activity.id, cost = activity.cost, description = activity.description, status = "TestPK", taskNum = activity.taskNum };
                        ItemResponse<ToDoActivityAfterMigration> createResponseWithPk = await fixedContainer.CreateItemAsync<ToDoActivityAfterMigration>(
                         item: itemWithPK);
                        Assert.AreEqual((int)HttpStatusCode.Created, createResponseWithPk.GetRawResponse().Status);

                        // Deleting item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                        ItemResponse<ToDoActivity> deleteResponseWithoutPk = await fixedContainer.DeleteItemAsync<ToDoActivity>(
                         partitionKey: Cosmos.PartitionKey.None,
                         id: activity.id);
                        Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponseWithoutPk.GetRawResponse().Status);
                    }
                }

                // Validate all items with no partition key value are returned
                Assert.AreEqual(ItemsToCreate, resultsFetched);

                // Re-Query the items on the container with NonePartitionKeyValue
                await foreach(Page<ToDoActivity> queryResponse in fixedContainer.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = ItemsToCreate, PartitionKey = Cosmos.PartitionKey.None, }).AsPages())
                {
                    Assert.AreEqual(0, queryResponse.Values.Count);
                }

                // Query the items with newly inserted PartitionKey
                await foreach(Page<ToDoActivity> queryResponse in fixedContainer.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: new QueryRequestOptions() { MaxItemCount = ItemsToCreate + 1, PartitionKey = new Cosmos.PartitionKey("TestPK"), }).AsPages())
                {
                    Assert.AreEqual(ItemsToCreate, queryResponse.Values.Count);
                }
            }
            finally
            {
                if (fixedContainer != null)
                {
                    await fixedContainer.DeleteContainerAsync();
                }
            }
        }


        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [TestCategory("Quarantine") /* Gated runs emulator as rate limiting disabled */]
        public async Task VerifyToManyRequestTest(bool isQuery)
        {
            CosmosClient client = TestCommon.CreateCosmosClient();
            CosmosDatabase db = await client.CreateDatabaseIfNotExistsAsync("LoadTest");
            CosmosContainer container = await db.CreateContainerIfNotExistsAsync("LoadContainer", "/status");

            try
            {
                Task[] createItems = new Task[300];
                for (int i = 0; i < createItems.Length; i++)
                {
                    ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity();
                    createItems[i] = container.CreateItemStreamAsync(
                        partitionKey: new PartitionKey(temp.status),
                        streamPayload: TestCommon.Serializer.Value.ToStream<ToDoActivity>(temp));
                }

                Task.WaitAll(createItems);

                List<Task> createQuery = new List<Task>(500);
                List<Response> failedToManyRequests = new List<Response>();
                for (int i = 0; i < 500 && failedToManyRequests.Count == 0; i++)
                {
                    createQuery.Add(VerifyQueryToManyExceptionAsync(
                        container,
                        isQuery,
                        failedToManyRequests));
                }

                Task[] tasks = createQuery.ToArray();
                Task.WaitAll(tasks);

                Assert.IsTrue(failedToManyRequests.Count > 0, "Rate limiting appears to be disabled");
                Response failedResponseMessage = failedToManyRequests.First();
                Assert.AreEqual(failedResponseMessage.Status, 429);
                Assert.IsNull(failedResponseMessage.ReasonPhrase);
            }
            finally
            {
                await db.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task VerifySessionTokenPassThrough()
        {
            ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity("TBD");

            ItemResponse<ToDoActivity> responseAstype = await this.Container.CreateItemAsync<ToDoActivity>(partitionKey: new PartitionKey(temp.status), item: temp);

            string sessionToken = responseAstype.Session;
            Assert.IsNotNull(sessionToken);

            Response readResponse = await this.Container.ReadItemStreamAsync(temp.id, new PartitionKey(temp.status), new ItemRequestOptions() { SessionToken = sessionToken });

            Assert.AreEqual((int)HttpStatusCode.OK, readResponse.Status);
            Assert.IsTrue(readResponse.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.SessionToken, out string readSessionToken));
            Assert.AreEqual(sessionToken, readSessionToken);
        }

        /// <summary>
        /// Stateless container re-create test. 
        /// Create two client instances and do meta data operations through a single client
        /// but do all validation using both clients.
        /// </summary>
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(false, false)]
        [DataTestMethod]
        public async Task ContainterReCreateStatelessTest(bool operationBetweenRecreate, bool isQuery)
        {
            Func<CosmosContainer, int, Task> operation = null;
            if (isQuery)
            {
                operation = ExecuteQueryAsync;
            }
            else
            {
                operation = ExecuteReadFeedAsync;
            }

            CosmosClient cc1 = TestCommon.CreateCosmosClient();
            CosmosClient cc2 = TestCommon.CreateCosmosClient();
            CosmosDatabase db1 = null;
            try
            {
                string dbName = Guid.NewGuid().ToString();
                string containerName = Guid.NewGuid().ToString();

                db1 = await cc1.CreateDatabaseAsync(dbName);
                ContainerCore container1 = (ContainerCore)await db1.CreateContainerAsync(containerName, "/id");

                await operation(container1, (int)HttpStatusCode.OK);

                // Read through client2 -> return 404
                CosmosContainer container2 = cc2.GetDatabase(dbName).GetContainer(containerName);
                await operation(container2, (int)HttpStatusCode.OK);

                // Delete container 
                await container1.DeleteContainerAsync();

                if (operationBetweenRecreate)
                {
                    // Read on deleted container through client1
                    await operation(container1, (int)HttpStatusCode.NotFound);

                    // Read on deleted container through client2
                    await operation(container2, (int)HttpStatusCode.NotFound);
                }

                // Re-create again 
                container1 = (ContainerCore)await db1.CreateContainerAsync(containerName, "/id");

                // Read through client1
                await operation(container1, (int)HttpStatusCode.OK);

                // Read through client2
                await operation(container2, (int)HttpStatusCode.OK);
            }
            finally
            {
                await db1.DeleteAsync();
                cc1.Dispose();
                cc2.Dispose();
            }
        }

        [TestMethod]
        public async Task NoAutoGenerateIdTest()
        {
            try
            {
                ToDoActivity t = new ToDoActivity();
                t.status = "AutoID";
                Response<ToDoActivity> responseAstype = await this.Container.CreateItemAsync<ToDoActivity>(
                    partitionKey: new PartitionKey(t.status), item: t);

                Assert.Fail("Unexpected ID auto-generation");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }

        [TestMethod]
        public async Task AutoGenerateIdPatternTest()
        {
            ToDoActivity itemWithoutId = new ToDoActivity();
            itemWithoutId.status = "AutoID";

            ToDoActivity createdItem = await this.AutoGenerateIdPatternTest<ToDoActivity>(
                new PartitionKey(itemWithoutId.status), itemWithoutId);

            Assert.IsNotNull(createdItem.id);
            Assert.AreEqual(itemWithoutId.status, createdItem.status);
        }

        private async Task<T> AutoGenerateIdPatternTest<T>(PartitionKey pk, T itemWithoutId)
        {
            string autoId = Guid.NewGuid().ToString();

            Dictionary<string, object> tmpJObject = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(itemWithoutId));

            if (!tmpJObject.ContainsKey("id") || tmpJObject["id"] == null)
            {
                tmpJObject["id"] = autoId;
            }

            Response<Dictionary<string, object>> response = await this.Container.CreateItemAsync<Dictionary<string, object>>(
                partitionKey: pk, item: tmpJObject);

            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(response.Value));
        }

        private static async Task VerifyQueryToManyExceptionAsync(
            CosmosContainer container,
            bool isQuery,
            List<Response> failedToManyMessages)
        {
            string queryText = null;
            if (isQuery)
            {
                queryText = "select * from r";
            }

            await foreach(Response response in container.GetItemQueryStreamIterator(queryText))
            {
                if (response.Status == 429)
                {
                    failedToManyMessages.Add(response);
                    return;
                }
            }
        }

        private static async Task ExecuteQueryAsync(CosmosContainer container, int expected)
        {
            await foreach(Response response in container.GetItemQueryStreamIterator("select * from r"))
            {
                Assert.AreEqual(expected, response.Status, $"ExecuteQueryAsync substatuscode: {response.Headers.GetSubStatusCode()} ");
            }
        }

        private static async Task ExecuteReadFeedAsync(CosmosContainer container, int expected)
        {
            await foreach(Response response in container.GetItemQueryStreamIterator())
            {
                Assert.AreEqual(expected, response.Status, $"ExecuteReadFeedAsync substatuscode: {response.Headers.GetSubStatusCode()} ");
            }
        }

        public class ToDoActivityAfterMigration
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            [JsonPropertyName("_partitionKey")]
            public string status { get; set; }
        }

        private ToDoActivityAfterMigration CreateRandomToDoActivityAfterMigration(string pk = null, string id = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }
            if (id == null)
            {
                id = Guid.NewGuid().ToString();
            }
            return new ToDoActivityAfterMigration()
            {
                id = id,
                description = "CreateRandomToDoActivity",
                status = pk,
                taskNum = 42,
                cost = double.MaxValue
            };
        }

        private static async Task TestNonePKForNonExistingContainer(CosmosContainer container)
        {
            // Stream implementation should not throw
            Response response = await container.ReadItemStreamAsync("id1", PartitionKey.None);
            Assert.AreEqual((int)HttpStatusCode.NotFound, response.Status);
            Assert.IsTrue(response.Headers.TryGetValue(Microsoft.Azure.Documents.HttpConstants.HttpHeaders.ActivityId, out string activityId));
            Assert.IsNotNull(activityId);
            //Assert.IsNotNull(response.ErrorMessage);
            // For typed, it will throw 
            try
            {
                Response<string> typedResponse = await container.ReadItemAsync<string>("id1", PartitionKey.None);
                Assert.Fail("Should throw exception.");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode);
            }
        }
    }
}
