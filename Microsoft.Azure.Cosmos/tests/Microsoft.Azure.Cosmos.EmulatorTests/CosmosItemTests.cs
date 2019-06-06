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
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using JsonReader = Json.JsonReader;
    using JsonWriter = Json.JsonWriter;
    using Microsoft.Azure.Cosmos.Linq;

    [TestClass]
    public class CosmosItemTests : BaseCosmosClientHelper
    {
        private CosmosContainer Container = null;
        private CosmosJsonSerializerCore jsonSerializer = null;
        private CosmosContainerSettings containerSettings = null;

        private static CosmosContainer fixedContainer = null;
        private static readonly string utc_date = DateTime.UtcNow.ToString("r");

        private static readonly string PreNonPartitionedMigrationApiVersion = "2018-09-17";
        private static readonly string nonPartitionContainerId = "fixed-Container";
        private static readonly string nonPartitionItemId = "fixed-Container-Item";

        private static readonly string undefinedPartitionItemId = "undefined-partition-Item";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            this.containerSettings = new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
            this.jsonSerializer = new CosmosJsonSerializerCore();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CreateDropItemTest()
        {
            ToDoActivity testItem = this.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.MaxResourceQuota);
            Assert.IsNotNull(response.CurrentResourceQuotaUsage);
            ItemResponse<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id);
            Assert.IsNotNull(deleteResponse);
        }

        [TestMethod]
        public async Task CreateDropItemUndefinedPartitionKeyTest()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString()
            };

            ItemResponse<dynamic> response = await this.Container.CreateItemAsync<dynamic>(item: testItem, partitionKey: new Cosmos.PartitionKey(Undefined.Value));
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.IsNotNull(response.MaxResourceQuota);
            Assert.IsNotNull(response.CurrentResourceQuotaUsage);

            ItemResponse<dynamic> deleteResponse = await this.Container.DeleteItemAsync<dynamic>(id: testItem.id, partitionKey: new Cosmos.PartitionKey(Undefined.Value));
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateDropItemPartitionKeyNotInTypeTest()
        {
            dynamic testItem = new
            {
                id = Guid.NewGuid().ToString()
            };

            ItemResponse<dynamic> response = await this.Container.CreateItemAsync<dynamic>(item: testItem);
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            Assert.IsNotNull(response.MaxResourceQuota);
            Assert.IsNotNull(response.CurrentResourceQuotaUsage);

            ItemResponse<dynamic> readResponse = await this.Container.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);

            ItemResponse<dynamic> deleteResponse = await this.Container.DeleteItemAsync<dynamic>(id: testItem.id, partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            readResponse = await this.Container.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, readResponse.StatusCode);
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

            ItemResponse<dynamic> response = await multiPartPkContainer.CreateItemAsync<dynamic>(item: testItem);
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            ItemResponse<dynamic> readResponse = await multiPartPkContainer.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: new Cosmos.PartitionKey("pk1"));
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);

            ItemResponse<dynamic> deleteResponse = await multiPartPkContainer.DeleteItemAsync<dynamic>(id: testItem.id, partitionKey: new Cosmos.PartitionKey("pk1"));
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            readResponse = await multiPartPkContainer.ReadItemAsync<dynamic>(id: testItem.id, partitionKey: new Cosmos.PartitionKey("pk1"));
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.NotFound, readResponse.StatusCode);
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
            CosmosContainerCore testContainer = (CosmosContainerCore)client.GetContainer(dbName, containerName);

            int loopCount = 2;
            for (int i = 0; i < loopCount; i++)
            {
                try
                {
                    await testContainer.GetNonePartitionKeyValueAsync();
                    Assert.Fail();
                }
                catch (DocumentClientException dce) when (dce.StatusCode == HttpStatusCode.NotFound)
                {
                }
            }

            Assert.AreEqual(loopCount, count);

            // Create real container and address 
            CosmosDatabase db = await client.CreateDatabaseAsync(dbName);
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
            ToDoActivity testItem = this.CreateRandomToDoActivity();
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                using (CosmosResponseMessage response = await this.Container.CreateItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.status), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    Assert.IsTrue(response.Headers.RequestCharge > 0);
                    Assert.IsNotNull(response.Headers.ActivityId);
                    Assert.IsNotNull(response.Headers.ETag);
                }
            }

            using (CosmosResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id))
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
            ToDoActivity testItem = this.CreateRandomToDoActivity();
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                //Create the object
                using (CosmosResponseMessage response = await this.Container.UpsertItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.status), streamPayload: stream))
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
                using (CosmosResponseMessage response = await this.Container.UpsertItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.status), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }
            }
            using (CosmosResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id))
            {
                Assert.IsNotNull(deleteResponse);
                Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
            }
        }

        [TestMethod]
        public async Task ReplaceItemStreamTest()
        {
            ToDoActivity testItem = this.CreateRandomToDoActivity();
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                //Replace a non-existing item. It should fail, and not throw an exception.
                using (CosmosResponseMessage response = await this.Container.ReplaceItemStreamAsync(
                    partitionKey: new Cosmos.PartitionKey(testItem.status),
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
                using (CosmosResponseMessage response = await this.Container.CreateItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.status), streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                }
            }

            //Updated the taskNum field
            testItem.taskNum = 9001;
            using (Stream stream = this.jsonSerializer.ToStream<ToDoActivity>(testItem))
            {
                using (CosmosResponseMessage response = await this.Container.ReplaceItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id, streamPayload: stream))
                {
                    Assert.IsNotNull(response);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }

                using (CosmosResponseMessage deleteResponse = await this.Container.DeleteItemStreamAsync(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id))
                {
                    Assert.IsNotNull(deleteResponse);
                    Assert.AreEqual(deleteResponse.StatusCode, HttpStatusCode.NoContent);
                }
            }
        }

        [DataRow(false)]
        [DataRow(true)]
        [DataTestMethod]
        public async Task ItemStreamIterator(bool useStatelessIterator)
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(3, randomPartitionKey: true);
            HashSet<string> itemIds = deleteList.Select(x => x.id).ToHashSet<string>();

            string lastContinuationToken = null;
            int pageSize = 1;
            ItemRequestOptions requestOptions = new ItemRequestOptions();
            FeedIterator feedIterator =
                this.Container.GetItemsStreamIterator(maxItemCount: pageSize, continuationToken: lastContinuationToken, requestOptions: requestOptions);

            while (feedIterator.HasMoreResults)
            {
                if (useStatelessIterator)
                {
                    feedIterator = this.Container.GetItemsStreamIterator(maxItemCount: pageSize, continuationToken: lastContinuationToken, requestOptions: requestOptions);
                }

                using (CosmosResponseMessage responseMessage =
                    await feedIterator.FetchNextSetAsync(this.cancellationToken))
                {
                    lastContinuationToken = responseMessage.Headers.Continuation;

                    Collection<ToDoActivity> response = new CosmosJsonSerializerCore().FromStream<CosmosFeedResponseUtil<ToDoActivity>>(responseMessage.Content).Data;
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
        public async Task ItemIterator()
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(3, randomPartitionKey: true);
            HashSet<string> itemIds = deleteList.Select(x => x.id).ToHashSet<string>();
            FeedIterator<ToDoActivity> feedIterator =
                this.Container.GetItemsIterator<ToDoActivity>();
            while (feedIterator.HasMoreResults)
            {
                foreach (ToDoActivity toDoActivity in await feedIterator.FetchNextSetAsync(this.cancellationToken))
                {
                    if (itemIds.Contains(toDoActivity.id))
                    {
                        itemIds.Remove(toDoActivity.id);
                    }
                }
            }

            Assert.AreEqual(itemIds.Count, 0);
        }

        [DataRow(1, 1)]
        [DataRow(5, 5)]
        [DataRow(6, 2)]
        [DataTestMethod]
        public async Task QuerySinglePartitionItemStreamTest(int perPKItemCount, int maxItemCount)
        {
            IList<ToDoActivity> deleteList = deleteList = await this.CreateRandomItems(pkCount: 3, perPKItemCount: perPKItemCount, randomPartitionKey: true);
            ToDoActivity find = deleteList.First();

            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from r");

            int iterationCount = 0;
            int totalReadItem = 0;
            int expectedIterationCount = perPKItemCount / maxItemCount;
            string lastContinuationToken = null;

            do
            {
                iterationCount++;
                FeedIterator feedIterator = this.Container
                    .CreateItemQueryStream(sql, 1, new Cosmos.PartitionKey(find.status),
                        maxItemCount: maxItemCount,
                        continuationToken: lastContinuationToken,
                        requestOptions: new QueryRequestOptions());

                CosmosResponseMessage response = await feedIterator.FetchNextSetAsync();
                lastContinuationToken = response.Headers.Continuation;
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
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(3, randomPartitionKey: true);

            ToDoActivity find = deleteList.First();
            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from toDoActivity t where t.id = '" + find.id + "'");

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxBufferedItemCount = 10,
                ResponseContinuationTokenLimitInKb = 500
            };

            FeedIterator<ToDoActivity> feedIterator =
                this.Container.CreateItemQuery<ToDoActivity>(sql, maxConcurrency: 1, maxItemCount: 1, requestOptions: requestOptions);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await feedIterator.FetchNextSetAsync();
                Assert.AreEqual(1, iter.Count());
                ToDoActivity response = iter.First();
                Assert.AreEqual(find.id, response.id);
            }
        }

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionOrderByQueryStream()
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(300, randomPartitionKey: true);

            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("SELECT * FROM toDoActivity t ORDER BY t.taskNum ");

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxBufferedItemCount = 10,
                ResponseContinuationTokenLimitInKb = 500
            };

            List<ToDoActivity> resultList = new List<ToDoActivity>();
            double totalRequstCharge = 0;
            FeedIterator feedIterator =
                this.Container.CreateItemQueryStream(sql, maxConcurrency: 5, maxItemCount: 1, requestOptions: requestOptions);
            while (feedIterator.HasMoreResults)
            {
                CosmosResponseMessage iter = await feedIterator.FetchNextSetAsync();
                Assert.IsTrue(iter.IsSuccessStatusCode);
                Assert.IsNull(iter.ErrorMessage);
                totalRequstCharge += iter.Headers.RequestCharge;

                ToDoActivity[] activities = this.jsonSerializer.FromStream<ToDoActivity[]>(iter.Content);
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

        /// <summary>
        /// Validate multiple partition query
        /// </summary>
        [TestMethod]
        public async Task ItemMultiplePartitionQueryStream()
        {
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(101, randomPartitionKey: true);
            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("SELECT * FROM toDoActivity t");

            List<ToDoActivity> resultList = new List<ToDoActivity>();
            double totalRequstCharge = 0;
            FeedIterator feedIterator =
                this.Container.CreateItemQueryStream(sql, maxConcurrency: 5, maxItemCount: 5);
            while (feedIterator.HasMoreResults)
            {
                CosmosResponseMessage iter = await feedIterator.FetchNextSetAsync();
                Assert.IsTrue(iter.IsSuccessStatusCode);
                Assert.IsNull(iter.ErrorMessage);
                totalRequstCharge += iter.Headers.RequestCharge;
                ToDoActivity[] response = this.jsonSerializer.FromStream<ToDoActivity[]>(iter.Content);
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
            IList<ToDoActivity> deleteList = await this.CreateRandomItems( pkCount: 101, perPKItemCount: 1, randomPartitionKey: true);

            // Create 10 items with same pk value
            IList<ToDoActivity> findItems = await this.CreateRandomItems(pkCount: 1, perPKItemCount: 10, randomPartitionKey: false);

            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("SELECT * FROM toDoActivity t");

            string findPkValue = findItems.First().status;
            double totalRequstCharge = 0;
            FeedIterator setIterator =
                this.Container.CreateItemQueryStream(sql, maxConcurrency: 1, partitionKey: new Cosmos.PartitionKey(findPkValue));

            List<ToDoActivity> foundItems = new List<ToDoActivity>();
            while (setIterator.HasMoreResults)
            {
                CosmosResponseMessage iter = await setIterator.FetchNextSetAsync();
                Assert.IsTrue(iter.IsSuccessStatusCode);
                Assert.IsNull(iter.ErrorMessage);
                totalRequstCharge += iter.Headers.RequestCharge;
                ToDoActivity[] response = this.jsonSerializer.FromStream<ToDoActivity[]>(iter.Content);
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

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions();
            itemRequestOptions.Properties = new Dictionary<string, object>();
            itemRequestOptions.Properties.Add(WFConstants.BackendHeaders.EffectivePartitionKeyString, epk);

            CosmosResponseMessage response = await this.Container.ReadItemStreamAsync(
                null,
                Guid.NewGuid().ToString(),
                itemRequestOptions);

            // Ideally it should be NotFound
            // BadReqeust bcoz collection is regular and not binary 
            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        /// <summary>
        /// Validate that if the EPK is set in the options that only a single range is selected.
        /// </summary>
        [TestMethod]
        public async Task ItemEpkQuerySingleKeyRangeValidation()
        {
            IList<ToDoActivity> deleteList = new List<ToDoActivity>();
            CosmosContainerCore container = null;
            try
            {
                // Create a container large enough to have at least 2 partitions
                ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                    id: Guid.NewGuid().ToString(),
                    partitionKeyPath: "/pk",
                    requestUnits: 15000);
                container = (CosmosContainerCore)containerResponse;

                // Get all the partition key ranges to verify there is more than one partition
                IRoutingMapProvider routingMapProvider = await this.cosmosClient.DocumentClient.GetPartitionKeyRangeCacheAsync();
                IReadOnlyList<PartitionKeyRange> ranges = await routingMapProvider.TryGetOverlappingRangesAsync(
                    containerResponse.Resource.ResourceId,
                    new Documents.Routing.Range<string>("00", "FF", isMaxInclusive: true, isMinInclusive: true));

                // If this fails the RUs of the container needs to be increased to ensure at least 2 partitions.
                Assert.IsTrue(ranges.Count > 1, " RUs of the container needs to be increased to ensure at least 2 partitions.");

                QueryRequestOptions options = new QueryRequestOptions()
                {
                    Properties = new Dictionary<string, object>()
                    {
                        {"x-ms-effective-partition-key-string", "AA" }
                    }
                };

                // There should only be one range since the EPK option is set.
                List<PartitionKeyRange> partitionKeyRanges = await CosmosQueryExecutionContextFactory.GetTargetPartitionKeyRangesAsync(
                    queryClient: new CosmosQueryClientCore(container.ClientContext, container),
                    resourceLink: container.LinkUri.OriginalString,
                    partitionedQueryExecutionInfo: null,
                    collection: containerResponse,
                    queryRequestOptions: options);

                Assert.IsTrue(partitionKeyRanges.Count == 1, "Only 1 partition key range should be selected since the EPK option is set.");
            }
            finally
            {
                if (container != null)
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
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(101, randomPartitionKey: true);

            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("SELECT * FROM toDoActivity t ORDER BY t.taskNum");
            CosmosSerializationOptions options = new CosmosSerializationOptions(
                ContentSerializationFormat.CosmosBinary.ToString(),
                (content) => JsonNavigator.Create(content),
                () => JsonWriter.Create(JsonSerializationFormat.Binary));

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                CosmosSerializationOptions = options
            };

            List<ToDoActivity> resultList = new List<ToDoActivity>();
            double totalRequstCharge = 0;
            FeedIterator feedIterator =
                this.Container.CreateItemQueryStream(sql, maxConcurrency: 5, maxItemCount: 5, requestOptions: requestOptions);
            while (feedIterator.HasMoreResults)
            {
                CosmosResponseMessage response = await feedIterator.FetchNextSetAsync();
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsNull(response.ErrorMessage);
                totalRequstCharge += response.Headers.RequestCharge;

                //Copy the stream and check that the first byte is the correct value
                MemoryStream memoryStream = new MemoryStream();
                response.Content.CopyTo(memoryStream);
                byte[] content = memoryStream.ToArray();

                // Examine the first buffer byte to determine the serialization format
                byte firstByte = content[0];
                Assert.AreEqual(128, firstByte);
                Assert.AreEqual(JsonSerializationFormat.Binary, (JsonSerializationFormat)firstByte);

                IJsonReader reader = JsonReader.Create(response.Content);
                IJsonWriter textWriter = JsonWriter.Create(JsonSerializationFormat.Text);
                textWriter.WriteAll(reader);
                string json = Encoding.UTF8.GetString(textWriter.GetResult());
                Assert.IsNotNull(json);
                ToDoActivity[] responseActivities = JsonConvert.DeserializeObject<ToDoActivity[]>(json);
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
            IList<ToDoActivity> deleteList = await this.CreateRandomItems(pkCount: 1, perPKItemCount: 6, randomPartitionKey: false);

            ToDoActivity toDoActivity = deleteList.First();
            CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition(
                "select * from toDoActivity t where t.status = @status")
                .UseParameter("@status", toDoActivity.status);

            // Test max size at 1
            FeedIterator<ToDoActivity> feedIterator =
                this.Container.CreateItemQuery<ToDoActivity>(sql, new Cosmos.PartitionKey(toDoActivity.status), maxItemCount: 1);
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await feedIterator.FetchNextSetAsync();
                Assert.AreEqual(1, iter.Count());
            }

            // Test max size at 2
            FeedIterator<ToDoActivity> setIteratorMax2 =
                this.Container.CreateItemQuery<ToDoActivity>(sql, new Cosmos.PartitionKey(toDoActivity.status), maxItemCount: 2);
            while (setIteratorMax2.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await setIteratorMax2.FetchNextSetAsync();
                Assert.AreEqual(2, iter.Count());
            }
        }

        /// <summary>
        /// Validate that the max item count works correctly.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task NegativeQueryTest()
        {
            IList<ToDoActivity> items = await this.CreateRandomItems(pkCount: 10, perPKItemCount: 20, randomPartitionKey: true);

            try
            {
                FeedIterator<dynamic> resultSet = this.Container.CreateItemQuery<dynamic>(
                    sqlQueryText: "SELECT r.id FROM root r WHERE r._ts > 0",
                    maxConcurrency: 1,
                    maxItemCount: 10,
                    requestOptions: new QueryRequestOptions() { ResponseContinuationTokenLimitInKb = 0 });

                await resultSet.FetchNextSetAsync();
                Assert.Fail("Expected query to fail");
            }
            catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.BadRequest)
            {
                Assert.IsTrue(exception.Message.Contains("continuation token limit specified is not large enough"), exception.Message);
            }

            try
            {
                FeedIterator<dynamic> resultSet = this.Container.CreateItemQuery<dynamic>(
                    sqlQueryText: "SELECT r.id FROM root r WHERE r._ts >!= 0",
                    maxConcurrency: 1);

                await resultSet.FetchNextSetAsync();
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
            ToDoActivity testItem = (await this.CreateRandomItems(1, randomPartitionKey: true)).First();

            ItemRequestOptions itemRequestOptions = new ItemRequestOptions()
            {
                IfMatchEtag = Guid.NewGuid().ToString(),
            };

            try
            {
                ItemResponse<ToDoActivity> response = await this.Container.ReplaceItemAsync<ToDoActivity>(
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
                ItemResponse<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id);
                Assert.IsNotNull(deleteResponse);
            }
        }

        // Read write non partition Container item.
        [TestMethod]
        public async Task ReadNonPartitionItemAsync()
        {
            try
            {
                await this.CreateNonPartitionedContainer();
                await this.CreateItemInNonPartitionedContainer(nonPartitionItemId);
                await this.CreateUndefinedPartitionItem();
                fixedContainer = this.database.GetContainer(nonPartitionContainerId);

                ContainerResponse containerResponse = await fixedContainer.ReadAsync();
                Assert.IsTrue(containerResponse.Resource.PartitionKey.Paths.Count > 0);
                Assert.AreEqual(PartitionKey.SystemKeyPath, containerResponse.Resource.PartitionKey.Paths[0]);

                //Reading item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                ItemResponse<ToDoActivity> response = await fixedContainer.ReadItemAsync<ToDoActivity>(
                    partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue,
                    id: nonPartitionItemId);

                Assert.IsNotNull(response.Resource);
                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(nonPartitionItemId, response.Resource.id);

                //Adding item to fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                ToDoActivity itemWithoutPK = this.CreateRandomToDoActivity();
                ItemResponse<ToDoActivity> createResponseWithoutPk = await fixedContainer.CreateItemAsync<ToDoActivity>(
                 item: itemWithoutPK,
                 partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue);

                Assert.IsNotNull(createResponseWithoutPk.Resource);
                Assert.AreEqual(HttpStatusCode.Created, createResponseWithoutPk.StatusCode);
                Assert.AreEqual(itemWithoutPK.id, createResponseWithoutPk.Resource.id);

                //Updating item on fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                itemWithoutPK.status = "updatedStatus";
                ItemResponse<ToDoActivity> updateResponseWithoutPk = await fixedContainer.ReplaceItemAsync<ToDoActivity>(
                 id: itemWithoutPK.id,
                 item: itemWithoutPK,
                 partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue);

                Assert.IsNotNull(updateResponseWithoutPk.Resource);
                Assert.AreEqual(HttpStatusCode.OK, updateResponseWithoutPk.StatusCode);
                Assert.AreEqual(itemWithoutPK.id, updateResponseWithoutPk.Resource.id);

                //Adding item to fixed container with non-none PK.
                ToDoActivityAfterMigration itemWithPK = this.CreateRandomToDoActivityAfterMigration("TestPk");
                ItemResponse<ToDoActivityAfterMigration> createResponseWithPk = await fixedContainer.CreateItemAsync<ToDoActivityAfterMigration>(
                 item: itemWithPK);

                Assert.IsNotNull(createResponseWithPk.Resource);
                Assert.AreEqual(HttpStatusCode.Created, createResponseWithPk.StatusCode);
                Assert.AreEqual(itemWithPK.id, createResponseWithPk.Resource.id);

                //Quering items on fixed container with cross partition enabled.
                CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from r");
                FeedIterator<dynamic> feedIterator = fixedContainer
                    .CreateItemQuery<dynamic>(sql, maxConcurrency: 1, maxItemCount: 10);
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> queryResponse = await feedIterator.FetchNextSetAsync();
                    Assert.AreEqual(3, queryResponse.Count());
                }

                //Reading all items on fixed container.
                feedIterator = fixedContainer
                    .GetItemsIterator<dynamic>(maxItemCount: 10);
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> queryResponse = await feedIterator.FetchNextSetAsync();
                    Assert.AreEqual(3, queryResponse.Count());
                }

                //Quering items on fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                feedIterator = fixedContainer
                    .CreateItemQuery<dynamic>(sql, partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue, maxItemCount: 10);
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> queryResponse = await feedIterator.FetchNextSetAsync();
                    Assert.AreEqual(2, queryResponse.Count());
                }

                //Quering items on fixed container with non-none PK.
                feedIterator = fixedContainer
                    .CreateItemQuery<dynamic>(sql, partitionKey: new Cosmos.PartitionKey(itemWithPK.status), maxItemCount: 10);
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> queryResponse = await feedIterator.FetchNextSetAsync();
                    Assert.AreEqual(1, queryResponse.Count());
                }

                //Deleting item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                ItemResponse<ToDoActivity> deleteResponseWithoutPk = await fixedContainer.DeleteItemAsync<ToDoActivity>(
                 partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue,
                 id: itemWithoutPK.id);

                Assert.IsNull(deleteResponseWithoutPk.Resource);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponseWithoutPk.StatusCode);

                //Deleting item from fixed container with non-none PK.
                ItemResponse<ToDoActivityAfterMigration> deleteResponseWithPk = await fixedContainer.DeleteItemAsync<ToDoActivityAfterMigration>(
                 partitionKey: new Cosmos.PartitionKey(itemWithPK.status),
                 id: itemWithPK.id);

                Assert.IsNull(deleteResponseWithPk.Resource);
                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponseWithPk.StatusCode);

                //Reading item from partitioned container with CosmosContainerSettings.NonePartitionKeyValue.
                ItemResponse<ToDoActivity> undefinedItemResponse = await this.Container.ReadItemAsync<ToDoActivity>(
                    partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue,
                    id: undefinedPartitionItemId);

                Assert.IsNotNull(undefinedItemResponse.Resource);
                Assert.AreEqual(HttpStatusCode.OK, undefinedItemResponse.StatusCode);
                Assert.AreEqual(undefinedPartitionItemId, undefinedItemResponse.Resource.id);
            }
            finally
            {
                if (fixedContainer != null)
                {
                    await fixedContainer.DeleteAsync();
                }
            }
        }

        [TestMethod]
        public async Task ItemLINQQueryTest()
        {
            //Creating items for query.
            IList<ToDoActivity> itemList = await CreateRandomItems(pkCount: 2, perPKItemCount: 1, randomPartitionKey: true);

            IOrderedQueryable<ToDoActivity> linqQueryable = this.Container.CreateItemQuery<ToDoActivity>();
            IQueryable<ToDoActivity> queriable = linqQueryable.Where(item => (item.taskNum < 100));
            //V3 Asynchronous query execution with LINQ query generation sql text.
            FeedIterator<ToDoActivity> setIterator = this.Container
                   .CreateItemQuery<ToDoActivity>(queriable.ToSqlQueryText(), maxConcurrency: 2);
            int resultsFetched = 0;
            while (setIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> queryResponse = await setIterator.FetchNextSetAsync();
                resultsFetched += queryResponse.Count();

                // For the items returned with NonePartitionKeyValue
                var iter = queryResponse.GetEnumerator();
                while (iter.MoveNext())
                {
                    ToDoActivity activity = iter.Current;
                    Assert.AreEqual(42, activity.taskNum);
                }
                Assert.AreEqual(2, resultsFetched);
            }

            //LINQ query execution without partition key.
            linqQueryable = this.Container.CreateItemQuery<ToDoActivity>(allowSynchronousQueryExecution: true);
            queriable = linqQueryable.Where(item => (item.taskNum < 100));

            Assert.AreEqual(2, queriable.Count());
            Assert.AreEqual(itemList[0].id, queriable.ToList()[0].id);
            Assert.AreEqual(itemList[1].id, queriable.ToList()[1].id);

            //LINQ query execution with wrong partition key.
            linqQueryable = this.Container.CreateItemQuery<ToDoActivity>(partitionKey: "test", allowSynchronousQueryExecution: true);
            queriable = linqQueryable.Where(item => (item.taskNum < 100));
            Assert.AreEqual(0, queriable.Count());

            //LINQ query execution with correct partition key.
            linqQueryable = this.Container.CreateItemQuery<ToDoActivity>(partitionKey: itemList[1].status, allowSynchronousQueryExecution: true, requestOptions: new QueryRequestOptions { ConsistencyLevel = Cosmos.ConsistencyLevel.Eventual });
            queriable = linqQueryable.Where(item => (item.taskNum < 100));
            Assert.AreEqual(1, queriable.Count());
            Assert.AreEqual(itemList[1].id, queriable.ToList()[0].id);

            //Creating LINQ query without setting allowSynchronousQueryExecution true.
            try
            {
                linqQueryable = this.Container.CreateItemQuery<ToDoActivity>(partitionKey: itemList[0].status);
                queriable = linqQueryable.Where(item => (item.taskNum < 100));
                queriable.ToList();
                Assert.Fail("Should throw NotSupportedException");
            }
            catch (NotSupportedException exception)
            {
                Assert.IsTrue(exception.Message.Contains("To execute LINQ query please set allowSynchronousQueryExecution true"));
            }
        }
        // Move the data from None Partition to other logical partitions
        [TestMethod]
        public async Task MigrateDataInNonPartitionContainer()
        {
            try
            {
                await this.CreateNonPartitionedContainer();

                const int ItemsToCreate = 4;
                // Insert a few items with no Partition Key
                for (int i = 0; i < ItemsToCreate; i++)
                {
                    await this.CreateItemInNonPartitionedContainer(Guid.NewGuid().ToString());
                }

                fixedContainer = this.database.GetContainer(nonPartitionContainerId);

                // Read the container metadata
                ContainerResponse containerResponse = await fixedContainer.ReadAsync();

                // Query items on the container that have no partition key value
                int resultsFetched = 0;
                CosmosSqlQueryDefinition sql = new CosmosSqlQueryDefinition("select * from r");
                FeedIterator<ToDoActivity> setIterator = fixedContainer
                    .CreateItemQuery<ToDoActivity>(sql, partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue, maxItemCount: 2);
                while (setIterator.HasMoreResults)
                {
                    FeedResponse<ToDoActivity> queryResponse = await setIterator.FetchNextSetAsync();
                    resultsFetched += queryResponse.Count();

                    // For the items returned with NonePartitionKeyValue
                    var iter = queryResponse.GetEnumerator();
                    while (iter.MoveNext())
                    {
                        ToDoActivity activity = iter.Current;

                        // Re-Insert into container with a partition key
                        ToDoActivityAfterMigration itemWithPK = new ToDoActivityAfterMigration
                        { id = activity.id, cost = activity.cost, description = activity.description, status = "TestPK", taskNum = activity.taskNum };
                        ItemResponse<ToDoActivityAfterMigration> createResponseWithPk = await fixedContainer.CreateItemAsync<ToDoActivityAfterMigration>(
                         item: itemWithPK);
                        Assert.AreEqual(HttpStatusCode.Created, createResponseWithPk.StatusCode);

                        // Deleting item from fixed container with CosmosContainerSettings.NonePartitionKeyValue.
                        ItemResponse<ToDoActivity> deleteResponseWithoutPk = await fixedContainer.DeleteItemAsync<ToDoActivity>(
                         partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue,
                         id: activity.id);
                        Assert.AreEqual(HttpStatusCode.NoContent, deleteResponseWithoutPk.StatusCode);
                    }
                }

                // Validate all items with no partition key value are returned
                Assert.AreEqual(ItemsToCreate, resultsFetched);

                // Re-Query the items on the container with NonePartitionKeyValue
                setIterator = fixedContainer
                    .CreateItemQuery<ToDoActivity>(sql, partitionKey: Cosmos.PartitionKey.NonePartitionKeyValue, maxItemCount: ItemsToCreate);
                Assert.IsTrue(setIterator.HasMoreResults);
                {
                    FeedResponse<ToDoActivity> queryResponse = await setIterator.FetchNextSetAsync();
                    Assert.AreEqual(0, queryResponse.Count());
                }

                // Query the items with newly inserted PartitionKey
                setIterator = fixedContainer
                    .CreateItemQuery<ToDoActivity>(sql, partitionKey: new Cosmos.PartitionKey("TestPK"), maxItemCount: ItemsToCreate + 1);
                Assert.IsTrue(setIterator.HasMoreResults);
                {
                    FeedResponse<ToDoActivity> queryResponse = await setIterator.FetchNextSetAsync();
                    Assert.AreEqual(ItemsToCreate, queryResponse.Count());
                }
            }
            finally
            {
                if (fixedContainer != null)
                {
                    await fixedContainer.DeleteAsync();
                }
            }
        }

        [TestMethod]
        public async Task VerifySessionTokenPassThrough()
        {
            ToDoActivity temp = this.CreateRandomToDoActivity("TBD");

            ItemResponse<ToDoActivity> responseAstype = await this.Container.CreateItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(temp.status), item: temp);

            string sessionToken = responseAstype.Headers.Session;
            Assert.IsNotNull(sessionToken);

            CosmosResponseMessage readResponse = await this.Container.ReadItemStreamAsync(new Cosmos.PartitionKey(temp.status), temp.id, new ItemRequestOptions() { SessionToken = sessionToken });

            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(readResponse.Headers.Session);
            Assert.AreEqual(sessionToken, readResponse.Headers.Session);
        }

        private async Task<IList<ToDoActivity>> CreateRandomItems(int pkCount, int perPKItemCount = 1, bool randomPartitionKey = true)
        {
            Assert.IsFalse(!randomPartitionKey && pkCount > 1);

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
                    ToDoActivity temp = this.CreateRandomToDoActivity(pk);

                    createdList.Add(temp);

                    await this.Container.CreateItemAsync<ToDoActivity>(item: temp);
                }
            }

            return createdList;
        }

        private async Task CreateNonPartitionedContainer()
        {
            await CosmosItemTests.CreateNonPartitionedContainer(this.database.Id, 
                CosmosItemTests.nonPartitionContainerId);
        }

        internal static async Task CreateNonPartitionedContainer(
            string dbName,
            string containerName,
            string indexingPolicyString = null)
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            //Creating non partition Container, rest api used instead of .NET SDK api as it is not supported anymore.
            HttpClient client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(endpoint);
            string verb = "POST";
            string resourceType = "colls";
            string resourceId = string.Format("dbs/{0}", dbName);
            string resourceLink = string.Format("dbs/{0}/colls", dbName);
            client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", CosmosItemTests.PreNonPartitionedMigrationApiVersion);

            string authHeader = CosmosItemTests.GenerateMasterKeyAuthorizationSignature(verb, resourceId, resourceType, authKey, "master", "1.0");

            client.DefaultRequestHeaders.Add("authorization", authHeader);
            DocumentCollection documentCollection = new DocumentCollection()
            {
                Id = containerName
            };
            if (indexingPolicyString != null)
            {
                documentCollection.IndexingPolicy = JsonConvert.DeserializeObject<IndexingPolicy>(indexingPolicyString);
            }
            string containerDefinition = documentCollection.ToString();
            StringContent containerContent = new StringContent(containerDefinition);
            Uri requestUri = new Uri(baseUri, resourceLink);
            HttpResponseMessage response = await client.PostAsync(requestUri.ToString(), containerContent);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, response.ToString());
        }

        private async Task CreateItemInNonPartitionedContainer(string itemId)
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            //Creating non partition Container item.
            HttpClient client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(endpoint);
            string verb = "POST";
            string resourceType = "docs";
            string resourceId = string.Format("dbs/{0}/colls/{1}", this.database.Id, nonPartitionContainerId);
            string resourceLink = string.Format("dbs/{0}/colls/{1}/docs", this.database.Id, nonPartitionContainerId);
            string authHeader = CosmosItemTests.GenerateMasterKeyAuthorizationSignature(verb, resourceId, resourceType, authKey, "master", "1.0");

            client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", CosmosItemTests.PreNonPartitionedMigrationApiVersion);
            client.DefaultRequestHeaders.Add("authorization", authHeader);

            string itemDefinition = JsonConvert.SerializeObject(this.CreateRandomToDoActivity(id: itemId));
            {
                StringContent itemContent = new StringContent(itemDefinition);
                Uri requestUri = new Uri(baseUri, resourceLink);
                HttpResponseMessage response = await client.PostAsync(requestUri.ToString(), itemContent);
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, response.ToString());
            }
        }

        private async Task CreateUndefinedPartitionItem()
        {
            string authKey = ConfigurationManager.AppSettings["MasterKey"];
            string endpoint = ConfigurationManager.AppSettings["GatewayEndpoint"];
            //Creating undefined partition key  item, rest api used instead of .NET SDK api as it is not supported anymore.
            HttpClient client = new System.Net.Http.HttpClient();
            Uri baseUri = new Uri(endpoint);
            string verb = "POST";
            string resourceType = "colls";
            string resourceId = string.Format("dbs/{0}", this.database.Id);
            string resourceLink = string.Format("dbs/{0}/colls", this.database.Id);
            client.DefaultRequestHeaders.Add("x-ms-date", utc_date);
            client.DefaultRequestHeaders.Add("x-ms-version", CosmosItemTests.PreNonPartitionedMigrationApiVersion);
            client.DefaultRequestHeaders.Add("x-ms-documentdb-partitionkey", "[{}]");

            //Creating undefined partition Container item.
            verb = "POST";
            resourceType = "docs";
            resourceId = string.Format("dbs/{0}/colls/{1}", this.database.Id, this.Container.Id);
            resourceLink = string.Format("dbs/{0}/colls/{1}/docs", this.database.Id, this.Container.Id);
            string authHeader = CosmosItemTests.GenerateMasterKeyAuthorizationSignature(verb, resourceId, resourceType, authKey, "master", "1.0");

            client.DefaultRequestHeaders.Remove("authorization");
            client.DefaultRequestHeaders.Add("authorization", authHeader);

            var payload = new { id = undefinedPartitionItemId, user = undefinedPartitionItemId };
            string itemDefinition = JsonConvert.SerializeObject(payload);
            StringContent itemContent = new StringContent(itemDefinition);
            Uri requestUri = new Uri(baseUri, resourceLink);
            await client.PostAsync(requestUri.ToString(), itemContent);
        }

        private static string GenerateMasterKeyAuthorizationSignature(string verb, string resourceId, string resourceType, string key, string keyType, string tokenVersion)
        {
            System.Security.Cryptography.HMACSHA256 hmacSha256 = new System.Security.Cryptography.HMACSHA256 { Key = Convert.FromBase64String(key) };

            string payLoad = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}\n{1}\n{2}\n{3}\n{4}\n",
                    verb.ToLowerInvariant(),
                    resourceType.ToLowerInvariant(),
                    resourceId,
                    utc_date.ToLowerInvariant(),
                    ""
            );

            byte[] hashPayLoad = hmacSha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(payLoad));
            string signature = Convert.ToBase64String(hashPayLoad);

            return System.Web.HttpUtility.UrlEncode(string.Format(System.Globalization.CultureInfo.InvariantCulture, "type={0}&ver={1}&sig={2}",
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
            if (id == null)
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

        public class ToDoActivityAfterMigration
        {
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            [JsonProperty(PropertyName = "_partitionKey")]
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

        private static async Task TestNonePKForNonExistingContainer(CosmosContainer cosmosContainer)
        {
            // Stream implementation should not throw
            CosmosResponseMessage response = await cosmosContainer.ReadItemStreamAsync(Cosmos.PartitionKey.NonePartitionKeyValue, "id1");
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            Assert.IsNotNull(response.Headers.ActivityId);
            Assert.IsNotNull(response.ErrorMessage);

            // FOr typed also its not error
            var typedResponse = await cosmosContainer.ReadItemAsync<string>(Cosmos.PartitionKey.NonePartitionKeyValue, "id1");
            Assert.AreEqual(HttpStatusCode.NotFound, typedResponse.StatusCode);
            Assert.IsNotNull(typedResponse.Headers.ActivityId);
        }
    }
}
