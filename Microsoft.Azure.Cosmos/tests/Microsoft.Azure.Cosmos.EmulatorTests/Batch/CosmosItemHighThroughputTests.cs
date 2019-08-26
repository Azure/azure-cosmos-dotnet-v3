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

    [TestClass]
    public class CosmosItemHighThroughputTests
    {
        private static CosmosSerializer cosmosDefaultJsonSerializer = new CosmosJsonDotNetSerializer();

        private Container container;
        private Database database;

        [TestInitialize]
        public async Task TestInitialize()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions();
            clientOptions.HighThroughputModeEnabled = true;
            CosmosClient client = TestCommon.CreateCosmosClient(clientOptions);

            DatabaseResponse response = await client.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
            this.database = response.Database;

            ContainerResponse containerResponse = await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/Status", 10000);
            this.container = containerResponse;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.database.DeleteAsync();
        }

        [TestMethod]
        public async Task CreateItemStream_WithHighThroughput()
        {
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(ExecuteAsync(this.container, CreateItem(i.ToString())));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = tasks[i];
                ResponseMessage result = await task;
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);

                MyDocument document = cosmosDefaultJsonSerializer.FromStream<MyDocument>(result.Content);
                Assert.AreEqual(i.ToString(), document.id);

                ItemResponse<MyDocument> storedDoc = await this.container.ReadItemAsync<MyDocument>(i.ToString(), new Cosmos.PartitionKey(i.ToString()));
                Assert.IsNotNull(storedDoc.Resource);
            }
        }

        private static Task<ResponseMessage> ExecuteAsync(Container container, MyDocument item)
        {
            return container.CreateItemStreamAsync(cosmosDefaultJsonSerializer.ToStream(item), new PartitionKey(item.Status));
        }

        private static MyDocument CreateItem(string id) => new MyDocument() { id = id, Status = id };

        private class MyDocument
        {
            public string id { get; set; }

            public string Status { get; set; }

            public bool Updated { get; set; }
        }
    }
}
