//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [TestClass]
    public class CosmosJsonSerializerTests : BaseCosmosClientHelper
    {
        private Container container = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            this.container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TestQueryWithCustomJsonSerializer()
        {
            int toStreamCount = 0;
            int fromStreamCount = 0;
            CosmosSerializer serializer = new CosmosSerializerHelper(new Newtonsoft.Json.JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            },
            (item) => fromStreamCount++,
            (item) => toStreamCount++);

            CosmosClient client = TestCommon.CreateCosmosClient(builder => builder.WithCustomSerializer(serializer));
            Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
            try
            {
                Container container = await database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");
                Assert.AreEqual(0, toStreamCount);
                Assert.AreEqual(0, fromStreamCount);

                double cost = 9001.42;
                for (int i = 0; i < 5; i++)
                {
                    ToDoActivity toDoActivity = new ToDoActivity()
                    {
                        id = "TestId" + i,
                        cost = cost
                    };

                    await container.CreateItemAsync<ToDoActivity>(toDoActivity, new PartitionKey(toDoActivity.id));
                }

                Assert.AreEqual(5, toStreamCount);
                Assert.AreEqual(5, fromStreamCount);

                toStreamCount = 0;
                fromStreamCount = 0;

                QueryDefinition query = new QueryDefinition("select * from T where T.id != @id").
                    WithParameter("@id", Guid.NewGuid());

                FeedIterator<DatabaseProperties> feedIterator = client.GetDatabaseQueryIterator<DatabaseProperties>(
                    query);
                List<DatabaseProperties> databases = new List<DatabaseProperties>();
                while (feedIterator.HasMoreResults)
                {
                    databases.AddRange(await feedIterator.ReadNextAsync());
                }

                Assert.AreEqual(1, toStreamCount, "parameter should use custom serializer");
                Assert.AreEqual(0, fromStreamCount);

                toStreamCount = 0;
                fromStreamCount = 0;

                FeedIterator<ToDoActivity> itemIterator = container.GetItemQueryIterator<ToDoActivity>(
                    query);
                List<ToDoActivity> items = new List<ToDoActivity>();
                while (itemIterator.HasMoreResults)
                {
                    items.AddRange(await itemIterator.ReadNextAsync());
                }

                Assert.AreEqual(1, toStreamCount);
                Assert.AreEqual(1, fromStreamCount);

                toStreamCount = 0;
                fromStreamCount = 0;

                // Verify that the custom serializer is actually being used via stream
                FeedIterator itemStreamIterator = container.GetItemQueryStreamIterator(
                    query);
                while (itemStreamIterator.HasMoreResults)
                {
                    ResponseMessage response = await itemStreamIterator.ReadNextAsync();
                    using (StreamReader reader = new StreamReader(response.Content))
                    {
                        string content = await reader.ReadToEndAsync();
                        Assert.IsTrue(content.Contains("9001.42"));
                        Assert.IsFalse(content.Contains("description"), "Description should be ignored and not in the JSON");
                    }
                }

                Assert.AreEqual(1, toStreamCount);
                Assert.AreEqual(0, fromStreamCount);

            }
            finally
            {
                if (database != null)
                {
                    using (await database.DeleteStreamAsync()) { }
                }
            }
        }

        [TestMethod]
        public async Task TestCustomJsonSerializer()
        {
            int toStreamCount = 0;
            int fromStreamCount = 0;

            Mock<CosmosSerializer> mockJsonSerializer = new Mock<CosmosSerializer>();

            //The item object will be serialized with the custom json serializer.
            ToDoActivity testItem = this.CreateRandomToDoActivity();
            mockJsonSerializer.Setup(x => x.ToStream<ToDoActivity>(It.IsAny<ToDoActivity>()))
                .Callback(() => toStreamCount++)
                .Returns(TestCommon.SerializerCore.ToStream<ToDoActivity>(testItem));

            mockJsonSerializer.Setup(x => x.FromStream<ToDoActivity>(It.IsAny<Stream>()))
                .Callback<Stream>(x => { x.Dispose(); fromStreamCount++; })
                .Returns(testItem);

            //Create a new cosmos client with the mocked cosmos json serializer
            CosmosClient mockClient = TestCommon.CreateCosmosClient(
                (cosmosClientBuilder) => cosmosClientBuilder.WithCustomSerializer(mockJsonSerializer.Object));
            Container mockContainer = mockClient.GetContainer(this.database.Id, this.container.Id);
            Assert.AreEqual(mockJsonSerializer.Object, mockClient.ClientOptions.Serializer);

            //Validate that the custom json serializer is used for creating the item
            ItemResponse<ToDoActivity> response = await mockContainer.CreateItemAsync<ToDoActivity>(item: testItem);
            Assert.IsNotNull(response);
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            Assert.AreEqual(1, toStreamCount);
            Assert.AreEqual(1, fromStreamCount);

            await mockContainer.DeleteItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.status));
        }

        /// <summary>
        /// Verify that null attributes get serialized by default.
        /// </summary>
        [TestMethod]
        public async Task DefaultNullValueHandling()
        {
            ToDoActivity document = new ToDoActivity()
            {
                id = Guid.NewGuid().ToString(),
                description = default(string),
                status = "TBD",
                taskNum = 42,
                cost = double.MaxValue
            };

            await this.container.UpsertItemAsync(document);

            ResponseMessage cosmosResponseMessage = await this.container.ReadItemStreamAsync(document.id, new PartitionKey(document.status));
            StreamReader reader = new StreamReader(cosmosResponseMessage.Content);
            string text = reader.ReadToEnd();

            Assert.IsTrue(text.IndexOf(nameof(document.description)) > -1, "Stored item doesn't contains null attributes");
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
            [JsonProperty(PropertyName = "id")]
            public string id { get; set; }
            public int taskNum { get; set; }
            public double cost { get; set; }
            public string description { get; set; }
            public string status { get; set; }
        }
    }
}
