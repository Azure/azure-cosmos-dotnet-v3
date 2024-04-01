﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
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
            string PartitionKey = "/pk";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(
                    id: Guid.NewGuid().ToString(), 
                    partitionKeyPath: PartitionKey),
                throughput: 15000,
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

                foreach (bool enableODE in new bool[] { false, true })
                {
                    toStreamCount = 0;
                    fromStreamCount = 0;

                    FeedIterator<ToDoActivity> itemIterator = container.GetItemQueryIterator<ToDoActivity>(
                        query,
                        requestOptions: new QueryRequestOptions() { EnableOptimisticDirectExecution = enableODE }
                        );
                    List<ToDoActivity> items = new List<ToDoActivity>();
                    while (itemIterator.HasMoreResults)
                    {
                        items.AddRange(await itemIterator.ReadNextAsync());
                    }

                    // The toStreamCount variable will differ between ODE and non-ODE pipelines due to the non-ODE pipelines needing to get the query plan which makes an additional serialization call during its initialization.
                    if (enableODE)
                    {
                        Assert.AreEqual(1, toStreamCount);
                    }
                    else
                    {
                        Assert.AreEqual(2, toStreamCount);
                    }
                    
                    Assert.AreEqual(1, fromStreamCount);

                    toStreamCount = 0;
                    fromStreamCount = 0;

                    // Verify that the custom serializer is actually being used via stream
                    FeedIterator itemStreamIterator = container.GetItemQueryStreamIterator(
                        query,
                        requestOptions: new QueryRequestOptions() { EnableOptimisticDirectExecution = enableODE }
                        );
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

                    // The toStreamCount variable will differ between ODE and non-ODE pipelines due to the non-ODE pipelines needing to get the query plan which makes an additional serialization call during its initialization.
                    if (enableODE)
                    {
                        Assert.AreEqual(1, toStreamCount);
                    }
                    else
                    {
                        Assert.AreEqual(2, toStreamCount);
                    }

                    Assert.AreEqual(0, fromStreamCount);
                }
            }
            finally
            {
                if(database != null)
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
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
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

            await mockContainer.DeleteItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.pk));
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
                description = default,
                pk = "TBD",
                taskNum = 42,
                cost = double.MaxValue
            };

            await this.container.UpsertItemAsync(document);

            ResponseMessage cosmosResponseMessage = await this.container.ReadItemStreamAsync(document.id, new PartitionKey(document.pk));
            StreamReader reader = new StreamReader(cosmosResponseMessage.Content);
            string text = reader.ReadToEnd();

            Assert.IsTrue(text.IndexOf(nameof(document.description)) > -1, "Stored item doesn't contains null attributes");
        }
    }
}
