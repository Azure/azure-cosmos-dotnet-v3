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
                tasks.Add(ExecuteCreateAsync(this.container, CreateItem(i.ToString())));
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

        [TestMethod]
        public async Task UpsertItemStream_WithHighThroughput()
        {
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(ExecuteUpsertAsync(this.container, CreateItem(i.ToString())));
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

        [TestMethod]
        public async Task DeleteItemStream_WithHighThroughput()
        {
            List<MyDocument> createdDocuments = new List<MyDocument>();
            // Create the items
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                MyDocument createdDocument = CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(ExecuteCreateAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ResponseMessage>> deleteTasks = new List<Task<ResponseMessage>>();
            // Delete the items
            foreach (MyDocument createdDocument in createdDocuments)
            {
                deleteTasks.Add(ExecuteDeleteAsync(this.container, createdDocument));
            }

            await Task.WhenAll(deleteTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = deleteTasks[i];
                ResponseMessage result = await task;
                Assert.AreEqual(HttpStatusCode.NoContent, result.StatusCode);

                await Assert.ThrowsExceptionAsync<CosmosException>(() => this.container.ReadItemAsync<MyDocument>(i.ToString(), new Cosmos.PartitionKey(i.ToString())));
            }
        }

        [TestMethod]
        public async Task ReadItemStream_WithHighThroughput()
        {
            List<MyDocument> createdDocuments = new List<MyDocument>();
            // Create the items
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                MyDocument createdDocument = CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(ExecuteCreateAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ResponseMessage>> readTasks = new List<Task<ResponseMessage>>();
            // Delete the items
            foreach (MyDocument createdDocument in createdDocuments)
            {
                readTasks.Add(ExecuteReadAsync(this.container, createdDocument));
            }

            await Task.WhenAll(readTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = readTasks[i];
                ResponseMessage result = await task;
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReplaceItemStream_WithHighThroughput()
        {
            List<MyDocument> createdDocuments = new List<MyDocument>();
            // Create the items
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                MyDocument createdDocument = CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(ExecuteCreateAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ResponseMessage>> replaceTasks = new List<Task<ResponseMessage>>();
            // Replace the items
            foreach (MyDocument createdDocument in createdDocuments)
            {
                replaceTasks.Add(ExecuteReplaceAsync(this.container, createdDocument));
            }

            await Task.WhenAll(replaceTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = replaceTasks[i];
                ResponseMessage result = await task;
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);

                ItemResponse<MyDocument> storedDoc = await this.container.ReadItemAsync<MyDocument>(i.ToString(), new Cosmos.PartitionKey(i.ToString()));
                Assert.IsNotNull(storedDoc.Resource);
            }
        }

        private static Task<ResponseMessage> ExecuteCreateAsync(Container container, MyDocument item)
        {
            return container.CreateItemStreamAsync(cosmosDefaultJsonSerializer.ToStream(item), new PartitionKey(item.Status));
        }

        private static Task<ResponseMessage> ExecuteUpsertAsync(Container container, MyDocument item)
        {
            return container.UpsertItemStreamAsync(cosmosDefaultJsonSerializer.ToStream(item), new PartitionKey(item.Status));
        }

        private static Task<ResponseMessage> ExecuteReplaceAsync(Container container, MyDocument item)
        {
            return container.ReplaceItemStreamAsync(cosmosDefaultJsonSerializer.ToStream(item), item.id, new PartitionKey(item.Status));
        }

        private static Task<ResponseMessage> ExecuteDeleteAsync(Container container, MyDocument item)
        {
            return container.DeleteItemStreamAsync(item.id, new PartitionKey(item.Status));
        }

        private static Task<ResponseMessage> ExecuteReadAsync(Container container, MyDocument item)
        {
            return container.ReadItemStreamAsync(item.id, new PartitionKey(item.Status));
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
