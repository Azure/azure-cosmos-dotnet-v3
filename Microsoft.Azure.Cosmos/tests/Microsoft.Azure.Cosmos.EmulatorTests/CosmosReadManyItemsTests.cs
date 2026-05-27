//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosReadManyItemsTests : BaseCosmosClientHelper
    {
        private Container Container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/pk";
            ContainerProperties containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                containerSettings,
                throughput: 20000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;

            // Create items with different pk values
            for (int i = 0; i < 500; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = "pk" + i.ToString();
                item.id = i.ToString();
                ItemResponse<ToDoActivity> itemResponse = await this.Container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        [DataRow(true, true, DisplayName = "Validates Read Many scenario with advanced replica selection enabled.")]
        [DataRow(true, false, DisplayName = "Validates Read Many scenario with advanced replica selection disabled.")]
        [DataRow(false, true, DisplayName = "Validates Read Many scenario with advanced replica selection enabled.")]
        [DataRow(false, false, DisplayName = "Validates Read Many scenario with advanced replica selection disabled.")]
        public async Task ReadManyTypedTestWithAdvancedReplicaSelection(
            bool binaryEncodingEnabled,
            bool advancedReplicaSelectionEnabled)
        {
            if (binaryEncodingEnabled)
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
            }
            CosmosClientOptions clientOptions = new ()
            {
                EnableAdvancedReplicaSelectionForTcp = advancedReplicaSelectionEnabled,
            };

            Database database = null;
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient(clientOptions);
            try
            {
                database = await cosmosClient.CreateDatabaseAsync("ReadManyTypedTestScenarioDb");
                Container container = await database.CreateContainerAsync("ReadManyTypedTestContainer", "/pk");

                // Create items with different pk values
                for (int i = 0; i < 500; i++)
                {
                    ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                    item.pk = "pk" + i.ToString();
                    item.id = i.ToString();
                    ItemResponse<ToDoActivity> itemResponse = await container.CreateItemAsync(item);
                    Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
                }

                List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
                for (int i = 0; i < 20; i++)
                {
                    itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
                }

                FeedResponse<ToDoActivity> feedResponse = await container.ReadManyItemsAsync<ToDoActivity>(itemList);
                Assert.IsNotNull(feedResponse);
                Assert.AreEqual(20, feedResponse.Count);
                Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
                Assert.IsNotNull(feedResponse.Diagnostics);

                int count = 0;
                foreach (ToDoActivity item in feedResponse)
                {
                    count++;
                    Assert.IsNotNull(item);
                }
                Assert.AreEqual(20, count);
            }
            finally
            {
                await database.DeleteAsync();
                cosmosClient.Dispose();
            }
        }

        [TestMethod]
        public async Task ReadManyStreamTest()
        {
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
            }

            using (ResponseMessage responseMessage = await this.Container.ReadManyItemsStreamAsync(itemList))
            {
                Assert.IsNotNull(responseMessage);
                Assert.IsTrue(responseMessage.Headers.RequestCharge > 0);
                Assert.IsNotNull(responseMessage.Diagnostics);

                ToDoActivity[] items = this.GetClient().ClientContext.SerializerCore.FromFeedStream<ToDoActivity>(
                                        CosmosFeedResponseSerializer.GetStreamWithoutServiceEnvelope(responseMessage.Content));
                Assert.AreEqual(items.Length, 5);
            }
        }

        [TestMethod]
        public async Task ReadManyDoesNotFetchQueryPlan()
        {
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
            }

            using (ResponseMessage responseMessage = await this.Container.ReadManyItemsStreamAsync(itemList))
            {
                Assert.IsNotNull(responseMessage);
                Assert.IsTrue(responseMessage.Headers.RequestCharge > 0);
                Assert.IsNotNull(responseMessage.Diagnostics);
                Assert.IsFalse(responseMessage.Diagnostics.ToString().Contains("Gateway QueryPlan"));
            }
        }

        [TestMethod]
        public async Task ReadManyWithIdasPk()
        {
            Container container = await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();

            // Create items with different pk values
            for (int i = 0; i < 5; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                ItemResponse<ToDoActivity> itemResponse = await container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
                
                itemList.Add((item.id, new PartitionKey(item.id)));

                ToDoActivity itemWithSingleQuotes = ToDoActivity.CreateRandomToDoActivity(id: item.id + "'singlequote");
                ItemResponse<ToDoActivity> itemResponseWithSingleQuotes = await container.CreateItemAsync(itemWithSingleQuotes);
                Assert.AreEqual(HttpStatusCode.Created, itemResponseWithSingleQuotes.StatusCode);

                itemList.Add((itemWithSingleQuotes.id, new PartitionKey(itemWithSingleQuotes.id)));
            }

            using (ResponseMessage responseMessage = await container.ReadManyItemsStreamAsync(itemList))
            {
                Assert.IsNotNull(responseMessage);
                Assert.IsTrue(responseMessage.Headers.RequestCharge > 0);
                Assert.IsNotNull(responseMessage.Diagnostics);

                ToDoActivity[] items = this.GetClient().ClientContext.SerializerCore.FromFeedStream<ToDoActivity>(
                                        CosmosFeedResponseSerializer.GetStreamWithoutServiceEnvelope(responseMessage.Content));
                Assert.AreEqual(items.Length, 10);
            }

            FeedResponse<ToDoActivity> feedResponse = await container.ReadManyItemsAsync<ToDoActivity>(itemList);
            Assert.IsNotNull(feedResponse);
            Assert.AreEqual(feedResponse.Count, 10);
            Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
            Assert.IsNotNull(feedResponse.Diagnostics);
        }

        [TestMethod]
        public async Task ReadManyWithNestedPk()
        {
            string PartitionKey = "/NestedObject/pk";
            ContainerProperties containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            Container container = await this.database.CreateContainerAsync(containerSettings);

            // Create items with different pk values
            for (int i = 0; i < 5; i++)
            {
                NestedToDoActivity item = NestedToDoActivity.CreateRandomNestedToDoActivity("pk" + i.ToString(), i.ToString());
                await container.CreateItemAsync(item);
            }

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i.ToString())));
            }

            FeedResponse<ToDoActivity> feedResponse = await container.ReadManyItemsAsync<ToDoActivity>(itemList);
            Assert.IsNotNull(feedResponse);
            Assert.AreEqual(feedResponse.Count, 5);
            Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
            Assert.IsNotNull(feedResponse.Diagnostics);
        }

        [TestMethod]
        public async Task ValidateContainerRecreateScenario()
        {
            CosmosClient cc1 = TestCommon.CreateCosmosClient();
            CosmosClient cc2 = TestCommon.CreateCosmosClient();

            Database database = null;
            try
            {
                database = await cc1.CreateDatabaseAsync("ContainerRecreateScenarioDb");
                Container containerCC1 = await database.CreateContainerAsync("ContainerRecreateContainer", "/pk");

                // Create items with different pk values
                for (int i = 0; i < 5; i++)
                {
                    ItemResponse<ToDoActivity> itemResponse = await containerCC1.CreateItemAsync(
                        ToDoActivity.CreateRandomToDoActivity("pk" + i, i.ToString()));
                }

                List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
                for (int i = 0; i < 5; i++)
                {
                    itemList.Add((i.ToString(), new PartitionKey("pk" + i)));
                }

                FeedResponse<ToDoActivity> feedResponse = await containerCC1.ReadManyItemsAsync<ToDoActivity>(itemList);
                Assert.AreEqual(feedResponse.Count, 5);

                Database databaseCC2 = cc2.GetDatabase("ContainerRecreateScenarioDb");
                Container containerCC2 = cc2.GetContainer("ContainerRecreateScenarioDb", "ContainerRecreateContainer");
                await containerCC2.DeleteContainerAsync();

                // Recreate container 
                containerCC2 = await databaseCC2.CreateContainerAsync("ContainerRecreateContainer", "/pk");

                // Check if recreate scenario works
                feedResponse = await containerCC1.ReadManyItemsAsync<ToDoActivity>(itemList);
                Assert.AreEqual(feedResponse.Count, 0);
                Assert.IsTrue(feedResponse.StatusCode == HttpStatusCode.OK);
            }
            finally
            {
                await database.DeleteAsync();
                cc1.Dispose();
                cc2.Dispose();
            }
        }

        [TestMethod]
        public async Task MultipleQueriesToSamePartitionTest()
        {
            for (int i = 0; i < 2500; i++)
            {
                await this.Container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity("pk", i.ToString()));
            }

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 2500; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk")));
            }

            FeedResponse<ToDoActivity> feedResponse = await this.Container.ReadManyItemsAsync<ToDoActivity>(itemList);
            Assert.AreEqual(feedResponse.Count, 2500);
        }

        [TestMethod]
        public async Task ReadManyTestWithIncorrectIntendedContainerRid()
        {
            for (int i = 0; i < 2; i++)
            {
                await this.Container.CreateItemAsync<ToDoActivity>(ToDoActivity.CreateRandomToDoActivity("pk", i.ToString()));
            }

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 2; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk")));
            }           

            // pass incorrect Rid.
            ReadManyRequestOptions readManyRequestOptions = new ReadManyRequestOptions
            {
                AddRequestHeaders = (headers) =>
                {
                    headers[Documents.HttpConstants.HttpHeaders.IsClientEncrypted] = bool.TrueString;
                    headers[Documents.WFConstants.BackendHeaders.IntendedCollectionRid] = "iCoRrecTrID=";
                }
            };

            FeedResponse<ToDoActivity> feedResponse;
            try
            {
                feedResponse = await this.Container.ReadManyItemsAsync<ToDoActivity>(itemList, readManyRequestOptions);
                Assert.Fail("ReadManyItemsAsync execution should have failed. ");
            }
            catch(CosmosException ex)
            {
                if (ex.StatusCode != HttpStatusCode.BadRequest || ex.SubStatusCode != 1024)
                {
                    Assert.Fail("ReadManyItemsAsync execution should have failed with the StatusCode: BadRequest and SubStatusCode: 1024. ");
                }
            }

            using (ResponseMessage responseMessage = await this.Container.ReadManyItemsStreamAsync(itemList , readManyRequestOptions))
            {
                if(responseMessage.StatusCode != HttpStatusCode.BadRequest ||
                    !string.Equals(responseMessage.Headers.Get("x-ms-substatus"), "1024"))
                {
                    Assert.Fail("ReadManyItemsStreamAsync execution should have failed with the StatusCode: BadRequest and SubStatusCode: 1024. ");
                }
            }

            // validate by passing correct Rid.
            ContainerInlineCore containerInternal = (ContainerInlineCore)this.Container;
            string rid = await containerInternal.GetCachedRIDAsync(forceRefresh: false, NoOpTrace.Singleton, cancellationToken: default);

            readManyRequestOptions = new ReadManyRequestOptions
            {
                AddRequestHeaders = (headers) =>
                {
                    headers[Documents.HttpConstants.HttpHeaders.IsClientEncrypted] = bool.TrueString;
                    headers[Documents.WFConstants.BackendHeaders.IntendedCollectionRid] = rid;
                }
            };
            
            feedResponse = await this.Container.ReadManyItemsAsync<ToDoActivity>(itemList, readManyRequestOptions);
            Assert.AreEqual(feedResponse.Count, 2);

            using (ResponseMessage responseMessage = await this.Container.ReadManyItemsStreamAsync(itemList, readManyRequestOptions))
            {
                Assert.AreEqual(responseMessage.StatusCode, HttpStatusCode.OK);

                Assert.IsNotNull(responseMessage);
                Assert.IsTrue(responseMessage.Headers.RequestCharge > 0);
                Assert.IsNotNull(responseMessage.Diagnostics);

                ToDoActivity[] items = this.GetClient().ClientContext.SerializerCore.FromFeedStream<ToDoActivity>(
                                        CosmosFeedResponseSerializer.GetStreamWithoutServiceEnvelope(responseMessage.Content));
                Assert.AreEqual(items.Length, 2);
            }
        }

        [TestMethod]
        public async Task ReadMany404ExceptionTest()
        {
            Database database = await this.GetClient().CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");
            for (int i = 0; i < 5; i++)
            {
                await container.CreateItemAsync(
                    ToDoActivity.CreateRandomToDoActivity("pk" + i, i.ToString()));
            }

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add((i.ToString(), new PartitionKey("pk" + i)));
            }

            // 429 test
            //List<Task> tasks = new List<Task>();
            //ConcurrentQueue<ResponseMessage> failedRequests = new ConcurrentQueue<ResponseMessage>();
            //for (int i = 0; i < 500; i++)
            //{
            //    tasks.Add(Task.Run(async () =>
            //    {
            //        ResponseMessage responseMessage = await container.ReadManyItemsStreamAsync(itemList);
            //        if (!responseMessage.IsSuccessStatusCode)
            //        {
            //            failedRequests.Enqueue(responseMessage);
            //            Assert.AreEqual(responseMessage.StatusCode, HttpStatusCode.TooManyRequests);
            //        }    
            //    }));
            //}

            //await Task.WhenAll(tasks);
            //Assert.IsTrue(failedRequests.Count > 0);

            await container.ReadManyItemsAsync<ToDoActivity>(itemList); // Warm up caches
            
            using (CosmosClient cosmosClient = TestCommon.CreateCosmosClient())
            {
                Container newContainer = cosmosClient.GetContainer(database.Id, container.Id);
                await newContainer.DeleteContainerAsync();
            }

            using (ResponseMessage responseMessage = await container.ReadManyItemsStreamAsync(itemList))
            {
                Assert.AreEqual(responseMessage.StatusCode, HttpStatusCode.NotFound);
            }

            try
            {
                await container.ReadManyItemsAsync<ToDoActivity>(itemList);
                Assert.Fail("Typed API should throw");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(ex.Error.Code, "NotFound");
            }

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task ReadManyWithNonePkValues()
        {
            for (int i = 0; i < 5; i++)
            {
                await this.Container.CreateItemAsync(new ActivityWithNoPk("id" + i.ToString()),
                                                     PartitionKey.None);
            }

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add(("id" + i.ToString(), PartitionKey.None));
            }

            FeedResponse<ActivityWithNoPk> feedResponse = await this.Container.ReadManyItemsAsync<ActivityWithNoPk>(itemList);
            Assert.AreEqual(feedResponse.Count, 5);
            int j = 0;
            foreach (ActivityWithNoPk item in feedResponse.Resource)
            {
                Assert.AreEqual(item.id, "id" + j);
                j++;
            }
        }

        [TestMethod]
        public async Task ReadManyItemsFromNonPartitionedContainers()
        {
            ContainerInternal container = await NonPartitionedContainerHelper.CreateNonPartitionedContainer(this.database,
                                                                                                             Guid.NewGuid().ToString());
            for (int i = 0; i < 5; i++)
            {
                await NonPartitionedContainerHelper.CreateItemInNonPartitionedContainer(container, "id" + i.ToString());
            }

            // read using PartitionKey.None pk value
            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add(("id" + i.ToString(), PartitionKey.None));
            }

            FeedResponse<ActivityWithNoPk> feedResponse = await container.ReadManyItemsAsync<ActivityWithNoPk>(itemList);
            Assert.AreEqual(feedResponse.Count, 5);

            // Start inserting documents with same id but new pk values
            for (int i = 0; i < 5; i++)
            {
                await container.CreateItemAsync(new ActivityWithSystemPk("id" + i.ToString(), "newPK"),
                                                new PartitionKey("newPK"));
            }

            feedResponse = await container.ReadManyItemsAsync<ActivityWithNoPk>(itemList);
            Assert.AreEqual(feedResponse.Count, 5);
            int j = 0;
            foreach (ActivityWithNoPk item in feedResponse.Resource)
            {
                Assert.AreEqual(item.id, "id" + j);
                j++;
            }

            for (int i = 0; i < 5; i++)
            {
                itemList.Add(("id" + i.ToString(), new PartitionKey("newPK")));
            }
            FeedResponse<ActivityWithSystemPk> feedResponseWithPK = await container.ReadManyItemsAsync<ActivityWithSystemPk>(itemList);
            Assert.AreEqual(feedResponseWithPK.Count, 10);
            j = 0;
            foreach (ActivityWithSystemPk item in feedResponseWithPK.Resource)
            {
                Assert.AreEqual(item.id, "id" + (j % 5));
                if (j > 4)
                {
                    Assert.AreEqual(item._partitionKey, "newPK");
                }
                else
                {
                    Assert.IsNull(item._partitionKey);
                }
                j++;
            }
        }

        [TestMethod]
        [DataRow(HttpStatusCode.NotFound)]
        public async Task ReadManyExceptionsTest(HttpStatusCode statusCode)
        {
            RequestHandler[] requestHandlers = new RequestHandler[1];
            requestHandlers[0] = new CustomHandler(statusCode);

            CosmosClientBuilder builder = TestCommon.GetDefaultConfiguration();
            builder.AddCustomHandlers(requestHandlers);
            using CosmosClient client = builder.Build();
            Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk");
            for (int i = 0; i < 5; i++)
            {
                await container.CreateItemAsync(
                    ToDoActivity.CreateRandomToDoActivity("pk" + i, i.ToString()));
            }

            List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
            for (int i = 0; i < 5; i++)
            {
                itemList.Add(("IncorrectId" + i, new PartitionKey("pk" + i))); // wrong ids
            }

            using (ResponseMessage responseMessage = await container.ReadManyItemsStreamAsync(itemList))
            {
                Assert.AreEqual(responseMessage.StatusCode, statusCode);
            }

            try
            {
                await container.ReadManyItemsAsync<ToDoActivity>(itemList);
                Assert.Fail("Typed API should throw");
            }
            catch (CosmosException ex)
            {
                Assert.AreEqual(ex.StatusCode, statusCode);
            }

            await database.DeleteAsync();
            client.Dispose();
        }     

        [TestMethod]
        public async Task ReadMany_AllSingleTuplePartitions_UsesPointReadsOnWire()
        {
            // Insert 200 items (one per candidate PK), then ReadMany ONE PK per physical
            // partition. Every physical partition therefore has exactly one tuple in the
            // request -> every dispatch must take the point-read fast path.
            //
            // Expected wire pattern: 0 queries, 1 point read per physical partition.
            CountingReadVsQueryHandler counter = new CountingReadVsQueryHandler();
            CosmosClientBuilder builder = TestCommon.GetDefaultConfiguration();
            builder.AddCustomHandlers(counter);
            using CosmosClient client = builder.Build();

            Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            try
            {
                // 20000 RU/s -> emulator provisions multiple physical partitions for the
                // container, giving us several distinct PKRs to land on.
                ContainerResponse containerResponse = await database.CreateContainerAsync(
                    new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                    throughput: 20000);
                Container container = containerResponse.Container;
                ContainerProperties containerProperties = containerResponse.Resource;

                const int totalItemCount = 200;
                for (int i = 0; i < totalItemCount; i++)
                {
                    ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                    item.pk = "pk-" + i.ToString();
                    item.id = "id-" + i.ToString();
                    await container.CreateItemAsync(item);
                }

                Dictionary<string, string> pkPerPhysicalPartition = await PickOnePkPerPhysicalPartitionAsync(
                    container,
                    containerProperties,
                    candidatePkFactory: i => "pk-" + i.ToString(),
                    candidatePkCount: totalItemCount);

                Assert.IsTrue(
                    pkPerPhysicalPartition.Count >= 2,
                    $"This test needs at least 2 distinct physical partitions; got {pkPerPhysicalPartition.Count}. Increase throughput or candidate PK count.");

                counter.Reset();

                // For each (pkrId, pk), recover the original id (since pk == "pk-i", id == "id-i")
                List<(string, PartitionKey)> itemList = pkPerPhysicalPartition.Values
                    .Select(pk => ("id-" + pk.Substring("pk-".Length), new PartitionKey(pk)))
                    .ToList();

                FeedResponse<ToDoActivity> feedResponse = await container.ReadManyItemsAsync<ToDoActivity>(itemList);

                Assert.AreEqual(itemList.Count, feedResponse.Count);
                Assert.IsTrue(feedResponse.Headers.RequestCharge > 0);
                Assert.AreEqual(
                    0,
                    counter.QueryCount,
                    $"Every physical partition has exactly one tuple, so no query should be issued. Observed = {counter.QueryCount}.");
                Assert.AreEqual(
                    itemList.Count,
                    counter.ReadCount,
                    $"Exactly one point read per single-tuple physical partition is expected. Observed = {counter.ReadCount}, expected = {itemList.Count}.");
            }
            finally
            {
                await database.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task ReadMany_MixedPartitionLayout_UsesBothPaths()
        {
            // Insert 200 items, then pick 5 tuples for the ReadMany call:
            //   * 4 tuples whose PKs all hash to ONE physical partition ("multi"),
            //   * 1 tuple whose PK hashes to a DIFFERENT physical partition ("single").
            //
            // The dispatcher batches per-physical-partition: the 4-tuple group goes
            // through the query path as a single batched query (SELECT * WHERE c.id IN
            // (4 ids); maxItemsPerQuery is 1000), and the 1-tuple group goes through the
            // new point-read fast path.
            //
            // Expected wire pattern: queryCount == 1, readCount == 1.
            CountingReadVsQueryHandler counter = new CountingReadVsQueryHandler();
            CosmosClientBuilder builder = TestCommon.GetDefaultConfiguration();
            builder.AddCustomHandlers(counter);
            using CosmosClient client = builder.Build();

            Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            try
            {
                ContainerResponse containerResponse = await database.CreateContainerAsync(
                    new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                    throughput: 20000);
                Container container = containerResponse.Container;
                ContainerProperties containerProperties = containerResponse.Resource;

                const int totalItemCount = 200;
                for (int i = 0; i < totalItemCount; i++)
                {
                    ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                    item.pk = "pk-" + i.ToString();
                    item.id = "id-" + i.ToString();
                    await container.CreateItemAsync(item);
                }

                // Group all 200 inserted PKs by their physical partition.
                Dictionary<string, List<string>> pksByPhysicalPartition = await GroupPksByPhysicalPartitionAsync(
                    container,
                    containerProperties,
                    pks: Enumerable.Range(0, totalItemCount).Select(i => "pk-" + i.ToString()));

                // Pick a physical partition with >= 4 inserted PKs to be the multi-tuple side,
                // and any DIFFERENT physical partition with >= 1 PK to be the single-tuple side.
                KeyValuePair<string, List<string>> multiPartition = pksByPhysicalPartition
                    .FirstOrDefault(kvp => kvp.Value.Count >= 4);
                Assert.IsNotNull(
                    multiPartition.Key,
                    "Could not find any physical partition with >= 4 candidate PKs out of 200. Increase totalItemCount.");

                KeyValuePair<string, List<string>> singlePartition = pksByPhysicalPartition
                    .FirstOrDefault(kvp => kvp.Key != multiPartition.Key && kvp.Value.Count >= 1);
                Assert.IsNotNull(
                    singlePartition.Key,
                    "Could not find a second physical partition with >= 1 candidate PK. Increase throughput or totalItemCount.");

                List<(string, PartitionKey)> itemList = new List<(string, PartitionKey)>();
                foreach (string pk in multiPartition.Value.Take(4))
                {
                    string id = "id-" + pk.Substring("pk-".Length);
                    itemList.Add((id, new PartitionKey(pk)));
                }
                string singlePk = singlePartition.Value.First();
                string singleId = "id-" + singlePk.Substring("pk-".Length);
                itemList.Add((singleId, new PartitionKey(singlePk)));

                counter.Reset();

                FeedResponse<ToDoActivity> feedResponse = await container.ReadManyItemsAsync<ToDoActivity>(itemList);

                Assert.AreEqual(5, feedResponse.Count);
                Assert.AreEqual(
                    1,
                    counter.QueryCount,
                    $"The 4 tuples sharing one physical partition must batch into exactly one query. Observed = {counter.QueryCount}.");
                Assert.AreEqual(
                    1,
                    counter.ReadCount,
                    $"The 1 tuple alone on its physical partition must take the point-read fast path. Observed = {counter.ReadCount}.");
            }
            finally
            {
                await database.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task ReadMany_SingleTuplePartitionMissingItem_SilentlyOmitted()
        {
            // Validates the 404-swallow behavior on the point-read branch (mirrors Java
            // pointReadsForReadMany and Python _execute_query_chunk_worker).
            //
            // Construction is deterministic via the routing map:
            //   - existing: 2 distinct physical partitions each holding one real item
            //   - missing : 2 distinct physical partitions each requested with an id
            //               that does NOT exist
            // Both groups satisfy count == 1 for their physical partition, so both go
            // through the point-read branch. Missing items must be silently omitted,
            // the RU charge from the 404 must still aggregate, and the call must not throw.
            CountingReadVsQueryHandler counter = new CountingReadVsQueryHandler();
            CosmosClientBuilder builder = TestCommon.GetDefaultConfiguration();
            builder.AddCustomHandlers(counter);
            using CosmosClient client = builder.Build();

            Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            try
            {
                ContainerResponse containerResponse = await database.CreateContainerAsync(
                    new ContainerProperties(Guid.NewGuid().ToString(), "/pk"),
                    throughput: 20000);
                Container container = containerResponse.Container;
                ContainerProperties containerProperties = containerResponse.Resource;

                Dictionary<string, string> pkPerPhysicalPartition = await PickOnePkPerPhysicalPartitionAsync(
                    container,
                    containerProperties,
                    candidatePkFactory: i => "pk-" + i.ToString(),
                    candidatePkCount: 200);

                Assert.IsTrue(
                    pkPerPhysicalPartition.Count >= 4,
                    $"This test needs at least 4 distinct physical partitions; got {pkPerPhysicalPartition.Count}.");

                // Seed real items only on the first 2 physical partitions.
                List<KeyValuePair<string, string>> existing = pkPerPhysicalPartition.Take(2).ToList();
                foreach (KeyValuePair<string, string> entry in existing)
                {
                    ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                    item.pk = entry.Value;
                    item.id = "real-" + entry.Key;
                    await container.CreateItemAsync(item);
                }

                // Build the ReadMany input: 2 existing reads + 2 missing reads, each landing
                // on a distinct physical partition.
                List<(string, PartitionKey)> itemList = existing
                    .Select(kvp => ("real-" + kvp.Key, new PartitionKey(kvp.Value)))
                    .ToList();
                List<KeyValuePair<string, string>> missing = pkPerPhysicalPartition.Skip(2).Take(2).ToList();
                foreach (KeyValuePair<string, string> entry in missing)
                {
                    itemList.Add(("missing-" + entry.Key, new PartitionKey(entry.Value)));
                }

                int expectedSingleTuplePartitions = existing.Count + missing.Count;

                counter.Reset();

                FeedResponse<ToDoActivity> feedResponse = await container.ReadManyItemsAsync<ToDoActivity>(itemList);

                Assert.AreEqual(
                    existing.Count,
                    feedResponse.Count,
                    "Only existing items should be returned; missing-item 404s must be silently omitted.");
                Assert.IsTrue(
                    feedResponse.Headers.RequestCharge > 0,
                    "Aggregate RU charge should include the 404 responses, so it must be > 0.");
                Assert.AreEqual(
                    0,
                    counter.QueryCount,
                    $"Every physical partition has exactly one tuple; no query should be issued. Observed = {counter.QueryCount}.");
                Assert.AreEqual(
                    expectedSingleTuplePartitions,
                    counter.ReadCount,
                    $"Each single-tuple physical partition (existing + missing) should produce exactly one point read. Observed = {counter.ReadCount}, expected = {expectedSingleTuplePartitions}.");
            }
            finally
            {
                await database.DeleteAsync();
            }
        }

        /// <summary>
        /// Picks one candidate PK value per physical partition by routing each candidate
        /// through the SDK's own hash path
        /// (<see cref="Documents.Routing.PartitionKeyInternal.GetEffectivePartitionKeyString"/>
        /// -> <see cref="Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap.GetRangeByEffectivePartitionKey"/>).
        /// Returns a dictionary keyed by PartitionKeyRange id.
        /// </summary>
        private static async Task<Dictionary<string, string>> PickOnePkPerPhysicalPartitionAsync(
            Container container,
            ContainerProperties containerProperties,
            Func<int, string> candidatePkFactory,
            int candidatePkCount)
        {
            ContainerInternal containerInternal = (ContainerInternal)container;
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap collectionRoutingMap =
                await containerInternal.GetRoutingMapAsync(CancellationToken.None);

            Dictionary<string, string> pkPerPhysicalPartition = new Dictionary<string, string>();
            for (int i = 0; i < candidatePkCount; i++)
            {
                string candidatePk = candidatePkFactory(i);
                string effectivePk = new PartitionKey(candidatePk)
                    .InternalKey
                    .GetEffectivePartitionKeyString(containerProperties.PartitionKey);
                string pkrId = collectionRoutingMap.GetRangeByEffectivePartitionKey(effectivePk).Id;

                if (!pkPerPhysicalPartition.ContainsKey(pkrId))
                {
                    pkPerPhysicalPartition.Add(pkrId, candidatePk);
                }
            }

            return pkPerPhysicalPartition;
        }

        /// <summary>
        /// Groups the given PK values by physical partition (PartitionKeyRange id) using
        /// the same routing path the SDK uses at dispatch time.
        /// </summary>
        private static async Task<Dictionary<string, List<string>>> GroupPksByPhysicalPartitionAsync(
            Container container,
            ContainerProperties containerProperties,
            IEnumerable<string> pks)
        {
            ContainerInternal containerInternal = (ContainerInternal)container;
            Microsoft.Azure.Cosmos.Routing.CollectionRoutingMap collectionRoutingMap =
                await containerInternal.GetRoutingMapAsync(CancellationToken.None);

            Dictionary<string, List<string>> pksByPhysicalPartition = new Dictionary<string, List<string>>();
            foreach (string pk in pks)
            {
                string effectivePk = new PartitionKey(pk)
                    .InternalKey
                    .GetEffectivePartitionKeyString(containerProperties.PartitionKey);
                string pkrId = collectionRoutingMap.GetRangeByEffectivePartitionKey(effectivePk).Id;

                if (!pksByPhysicalPartition.TryGetValue(pkrId, out List<string> bucket))
                {
                    bucket = new List<string>();
                    pksByPhysicalPartition[pkrId] = bucket;
                }
                bucket.Add(pk);
            }

            return pksByPhysicalPartition;
        }

        private class NestedToDoActivity
        {
            public ToDoActivity NestedObject { get; set; }
#pragma warning disable IDE1006 // Naming Styles
            public string id { get; set; }
#pragma warning restore IDE1006 // Naming Styles

            public static NestedToDoActivity CreateRandomNestedToDoActivity(string pk, string id)
            {
                return new NestedToDoActivity()
                {
                    id = id,
                    NestedObject = ToDoActivity.CreateRandomToDoActivity(pk: pk)
                };
            }
        }

        private class CustomHandler: RequestHandler
        {
            private readonly HttpStatusCode statusCode;

            public CustomHandler(HttpStatusCode statusCode)
            {
                this.statusCode = statusCode;
            }

            public override async Task<ResponseMessage> SendAsync(RequestMessage requestMessage,
                                                                CancellationToken cancellationToken)
            {
                if (requestMessage.OperationType == Documents.OperationType.Query)
                {
                    return new ResponseMessage(this.statusCode);
                }

                return await base.SendAsync(requestMessage, cancellationToken);
            }
        }

        private class CountingReadVsQueryHandler : RequestHandler
        {
            private int readCount;
            private int queryCount;

            public int ReadCount => this.readCount;

            public int QueryCount => this.queryCount;

            public void Reset()
            {
                Interlocked.Exchange(ref this.readCount, 0);
                Interlocked.Exchange(ref this.queryCount, 0);
            }

            public override Task<ResponseMessage> SendAsync(RequestMessage requestMessage,
                                                            CancellationToken cancellationToken)
            {
                if (requestMessage.ResourceType == Documents.ResourceType.Document)
                {
                    if (requestMessage.OperationType == Documents.OperationType.Read)
                    {
                        Interlocked.Increment(ref this.readCount);
                    }
                    else if (requestMessage.OperationType == Documents.OperationType.Query)
                    {
                        Interlocked.Increment(ref this.queryCount);
                    }
                }

                return base.SendAsync(requestMessage, cancellationToken);
            }
        }

        private class ActivityWithNoPk
        {
            public ActivityWithNoPk(string id)
            {
                this.id = id;
            }

#pragma warning disable IDE1006 // Naming Styles
            public string id { get; set; }
#pragma warning restore IDE1006 // Naming Styles
        }

        private class ActivityWithSystemPk
        {
            public ActivityWithSystemPk(string id, string _partitionKey)
            {
                this.id = id;
                this._partitionKey = _partitionKey;
            }

#pragma warning disable IDE1006 // Naming Styles
            public string id { get; set; }
#pragma warning restore IDE1006 // Naming Styles

#pragma warning disable IDE1006 // Naming Styles
            public string _partitionKey { get; set; }
#pragma warning restore IDE1006 // Naming Styles
        }
    }
}
