//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
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
        [DataRow(true, DisplayName = "Validates Read Many scenario with advanced replica selection enabled.")]
        [DataRow(false, DisplayName = "Validates Read Many scenario with advanced replica selection disabled.")]
        public async Task ReadManyTypedTestWithAdvancedReplicaSelection(
            bool advancedReplicaSelectionEnabled)
        {
            CosmosClient cosmosClient = advancedReplicaSelectionEnabled
                ? TestCommon.CreateCosmosClient(
                    customizeClientBuilder: (CosmosClientBuilder builder) => builder.WithAdvancedReplicaSelectionEnabledForTcp())
                : TestCommon.CreateCosmosClient();

            Database database = null;
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
