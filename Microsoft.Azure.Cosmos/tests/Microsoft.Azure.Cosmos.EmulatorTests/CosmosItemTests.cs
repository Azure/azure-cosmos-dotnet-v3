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
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class CosmosItemTests : BaseCosmosClientHelper
    {
        private CosmosContainer Container = null;
        private CosmosDefaultJsonSerializer jsonSerializer = null;

        private static CosmosContainer fixedContainer = null;
        private static readonly string utc_date = DateTime.UtcNow.ToString("r");

        private static readonly string nonPartitionContainerId = "fixed-Container";
        private static readonly string nonPartitionItemId = "fixed-Container-Item";
        private static readonly JsonSerializer Serializer = new JsonSerializer();

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
            await CreateNonPartitionContainerItem();
            fixedContainer = database.Containers[nonPartitionContainerId];
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
                CosmosResultSetIterator setIterator =
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
                .CreateItemQueryAsStream(sql, find.status, maxItemCount);

            int iterationCount = 0;
            int totalReadItem = 0;
            int expectedIterationCount = perPKItemCount / maxItemCount;
            string lastContinuationToken = null;

            while (setIterator.HasMoreResults && expectedIterationCount > iterationCount)
            {
                iterationCount++;

                using (CosmosResponseMessage response = await setIterator.FetchNextSetAsync())
                {
                    lastContinuationToken = response.Headers.Continuation;
                    Trace.TraceInformation($"ContinuationToken: {lastContinuationToken}");

                    using (StreamReader sr = new StreamReader(response.Content))
                    using (JsonTextReader jtr = new JsonTextReader(sr))
                    {
                        JObject result = JObject.Load(jtr);

                        JArray documents = result["Documents"].ToObject<JArray>();
                        ToDoActivity[] readTodoActivities = documents
                            .ToObject<ToDoActivity[]>()
                            .OrderBy(e => e.id)
                            .ToArray();

                        ToDoActivity[] expectedTodoActivities = deleteList
                                .Where(e => e.status == find.status)
                                .Where(e => readTodoActivities.Any(e1 => e1.id == e.id))
                                .OrderBy(e => e.id)
                                .ToArray();

                        totalReadItem += expectedTodoActivities.Length;
                        string expectedSerialized = JsonConvert.SerializeObject(expectedTodoActivities);
                        string readSerialized = JsonConvert.SerializeObject(readTodoActivities);
                        Trace.TraceInformation($"Query Response: {Environment.NewLine} {result.ToString()}");
                        Trace.TraceInformation($"Expected: {Environment.NewLine} {expectedSerialized}");
                        Trace.TraceInformation($"Read: {Environment.NewLine} {readSerialized}");

                        int count = result["_count"].ToObject<int>();
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

                CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from toDoActivity t where t.taskNum = @task").UseParameter("@task", deleteList.First().taskNum);

                // Test max size at 1
                CosmosResultSetIterator<ToDoActivity> setIterator =
                    this.Container.Items.CreateItemQuery<ToDoActivity>(sql, "TBD", maxItemCount: 1);
                while (setIterator.HasMoreResults)
                {
                    CosmosQueryResponse<ToDoActivity> iter = await setIterator.FetchNextSetAsync();
                    Assert.AreEqual(1, iter.Count());
                }

                // Test max size at 2
                CosmosResultSetIterator<ToDoActivity> setIteratorMax2 =
                    this.Container.Items.CreateItemQuery<ToDoActivity>(sql, "TBD", maxItemCount: 2);
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

        // Read write non partition Container item.
        [TestMethod]
        public async Task ReadNonPartitionItemAsync()
        {
            CosmosContainerResponse containerResponse = await fixedContainer.ReadAsync();
            Assert.IsTrue(containerResponse.Resource.PartitionKey.Paths.Count > 0);
            Assert.AreEqual(containerResponse.Resource.PartitionKey.Paths[0], PartitionKey.SystemPartitionKeyName);

            CosmosItemResponse<ToDoActivity> response = await fixedContainer.Items.ReadItemAsync<ToDoActivity>(
                partitionKey: PartitionKey.Empty,
                id: nonPartitionItemId);

            Assert.IsNotNull(response.Resource);
            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK);
            Assert.IsNotNull(response.Resource.id == nonPartitionItemId);

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

        private async Task CreateNonPartitionContainerItem()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            //Creating non partition Container, rest api used instead of .NET SDK api as it is not supported anymore.
            var client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(endpoint);
            string verb = "POST";
            string resourceType = "colls";
            string resourceId = string.Format("dbs/{0}", this.database.Id);
            string resourceLink = string.Format("dbs/{0}/colls", this.database.Id);
            client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", "2018-09-17");

            string authHeader = GenerateMasterKeyAuthorizationSignature(verb, resourceId, resourceType, authKey, "master", "1.0");

            client.DefaultRequestHeaders.Add("authorization", authHeader);
            String containerDefinition = "{\n  \"id\": \"" + nonPartitionContainerId + "\"\n}";
            StringContent containerContent = new StringContent(containerDefinition);
            Uri requestUri = new Uri(baseUri, resourceLink);
            await client.PostAsync(requestUri.ToString(), containerContent);

            //Creating non partition Container item.
            verb = "POST";
            resourceType = "docs";
            resourceId = string.Format("dbs/{0}/colls/{1}", this.database.Id, nonPartitionContainerId);
            resourceLink = string.Format("dbs/{0}/colls/{1}/docs", this.database.Id, nonPartitionContainerId);
            authHeader = GenerateMasterKeyAuthorizationSignature(verb, resourceId, resourceType, authKey, "master", "1.0");

            client.DefaultRequestHeaders.Remove("authorization");
            client.DefaultRequestHeaders.Add("authorization", authHeader);

            String itemDefinition = JsonConvert.SerializeObject(CreateRandomToDoActivity(id : nonPartitionItemId));
            StringContent itemContent = new StringContent(itemDefinition);
            requestUri = new Uri(baseUri, resourceLink);
            await client.PostAsync(requestUri.ToString(), itemContent);
        }

        private string GenerateMasterKeyAuthorizationSignature(string verb, string resourceId, string resourceType, string key, string keyType, string tokenVersion)
        {
            var hmacSha256 = new System.Security.Cryptography.HMACSHA256 { Key = Convert.FromBase64String(key) };

            string payLoad = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}\n{1}\n{2}\n{3}\n{4}\n",
                    verb.ToLowerInvariant(),
                    resourceType.ToLowerInvariant(),
                    resourceId,
                    utc_date.ToLowerInvariant(),
                    ""
            );

            byte[] hashPayLoad = hmacSha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payLoad));
            string signature = Convert.ToBase64String(hashPayLoad);

            return System.Web.HttpUtility.UrlEncode(String.Format(System.Globalization.CultureInfo.InvariantCulture, "type={0}&ver={1}&sig={2}",
                keyType,
                tokenVersion,
                signature));
        }

        private ToDoActivity CreateRandomToDoActivity(string pk = null, string id = null)
        {
            if (string.IsNullOrEmpty(pk))
            {
                pk = "TBD" + Guid.NewGuid().ToString();
            }
            if(id == null)
            {
                id = Guid.NewGuid().ToString();
            }
            return new ToDoActivity()
            {
                id = id,
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
