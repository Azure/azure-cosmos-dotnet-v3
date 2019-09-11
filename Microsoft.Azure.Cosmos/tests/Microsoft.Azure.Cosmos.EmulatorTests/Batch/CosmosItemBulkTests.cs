//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosItemBulkTests
    {
        private static CosmosSerializer cosmosDefaultJsonSerializer = new CosmosJsonDotNetSerializer();

        private Container container;
        private Database database;

        [TestInitialize]
        public async Task TestInitialize()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions();
            clientOptions.AllowBulkExecution = true;
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
        public async Task CreateItemStream_WithBulk()
        {
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(ExecuteCreateStreamAsync(this.container, CreateItem(i.ToString())));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = tasks[i];
                ResponseMessage result = await task;
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);

                MyDocument document = cosmosDefaultJsonSerializer.FromStream<MyDocument>(result.Content);
                Assert.AreEqual(i.ToString(), document.id);
            }
        }

        [TestMethod]
        public async Task CreateItemAsync_WithBulk()
        {
            List<Task<ItemResponse<MyDocument>>> tasks = new List<Task<ItemResponse<MyDocument>>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(ExecuteCreateAsync(this.container, CreateItem(i.ToString())));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<MyDocument>> task = tasks[i];
                ItemResponse<MyDocument> result = await task;
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task UpsertItemStream_WithBulk()
        {
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(ExecuteUpsertStreamAsync(this.container, CreateItem(i.ToString())));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = tasks[i];
                ResponseMessage result = await task;
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);

                MyDocument document = cosmosDefaultJsonSerializer.FromStream<MyDocument>(result.Content);
                Assert.AreEqual(i.ToString(), document.id);
            }
        }

        [TestMethod]
        public async Task UpsertItem_WithBulk()
        {
            List<Task<ItemResponse<MyDocument>>> tasks = new List<Task<ItemResponse<MyDocument>>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(ExecuteUpsertAsync(this.container, CreateItem(i.ToString())));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<MyDocument>> task = tasks[i];
                ItemResponse<MyDocument> result = await task;
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task DeleteItemStream_WithBulk()
        {
            List<MyDocument> createdDocuments = new List<MyDocument>();
            // Create the items
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                MyDocument createdDocument = CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(ExecuteCreateStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ResponseMessage>> deleteTasks = new List<Task<ResponseMessage>>();
            // Delete the items
            foreach (MyDocument createdDocument in createdDocuments)
            {
                deleteTasks.Add(ExecuteDeleteStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(deleteTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = deleteTasks[i];
                ResponseMessage result = await task;
                Assert.AreEqual(HttpStatusCode.NoContent, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task DeleteItem_WithBulk()
        {
            List<MyDocument> createdDocuments = new List<MyDocument>();
            // Create the items
            List<Task<ItemResponse<MyDocument>>> tasks = new List<Task<ItemResponse<MyDocument>>>();
            for (int i = 0; i < 100; i++)
            {
                MyDocument createdDocument = CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(ExecuteCreateAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ItemResponse<MyDocument>>> deleteTasks = new List<Task<ItemResponse<MyDocument>>>();
            // Delete the items
            foreach (MyDocument createdDocument in createdDocuments)
            {
                deleteTasks.Add(ExecuteDeleteAsync(this.container, createdDocument));
            }

            await Task.WhenAll(deleteTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<MyDocument>> task = deleteTasks[i];
                ItemResponse<MyDocument> result = await task;
                Assert.AreEqual(HttpStatusCode.NoContent, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReadItemStream_WithBulk()
        {
            List<MyDocument> createdDocuments = new List<MyDocument>();
            // Create the items
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                MyDocument createdDocument = CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(ExecuteCreateStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ResponseMessage>> readTasks = new List<Task<ResponseMessage>>();
            // Delete the items
            foreach (MyDocument createdDocument in createdDocuments)
            {
                readTasks.Add(ExecuteReadStreamAsync(this.container, createdDocument));
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
        public async Task ReadItem_WithBulk()
        {
            List<MyDocument> createdDocuments = new List<MyDocument>();
            // Create the items
            List<Task<ItemResponse<MyDocument>>> tasks = new List<Task<ItemResponse<MyDocument>>>();
            for (int i = 0; i < 100; i++)
            {
                MyDocument createdDocument = CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(ExecuteCreateAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ItemResponse<MyDocument>>> readTasks = new List<Task<ItemResponse<MyDocument>>>();
            // Delete the items
            foreach (MyDocument createdDocument in createdDocuments)
            {
                readTasks.Add(ExecuteReadAsync(this.container, createdDocument));
            }

            await Task.WhenAll(readTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<MyDocument>> task = readTasks[i];
                ItemResponse<MyDocument> result = await task;
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReplaceItemStream_WithBulk()
        {
            List<MyDocument> createdDocuments = new List<MyDocument>();
            // Create the items
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                MyDocument createdDocument = CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(ExecuteCreateStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ResponseMessage>> replaceTasks = new List<Task<ResponseMessage>>();
            // Replace the items
            foreach (MyDocument createdDocument in createdDocuments)
            {
                replaceTasks.Add(ExecuteReplaceStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(replaceTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = replaceTasks[i];
                ResponseMessage result = await task;
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReplaceItem_WithBulk()
        {
            List<MyDocument> createdDocuments = new List<MyDocument>();
            // Create the items
            List<Task<ItemResponse<MyDocument>>> tasks = new List<Task<ItemResponse<MyDocument>>>();
            for (int i = 0; i < 100; i++)
            {
                MyDocument createdDocument = CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(ExecuteCreateAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ItemResponse<MyDocument>>> replaceTasks = new List<Task<ItemResponse<MyDocument>>>();
            // Replace the items
            foreach (MyDocument createdDocument in createdDocuments)
            {
                replaceTasks.Add(ExecuteReplaceAsync(this.container, createdDocument));
            }

            await Task.WhenAll(replaceTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<MyDocument>> task = replaceTasks[i];
                ItemResponse<MyDocument> result = await task;
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        private static Task<ItemResponse<MyDocument>> ExecuteCreateAsync(Container container, MyDocument item)
        {
            return container.CreateItemAsync<MyDocument>(item, new PartitionKey(item.Status));
        }

        private static Task<ItemResponse<MyDocument>> ExecuteUpsertAsync(Container container, MyDocument item)
        {
            return container.UpsertItemAsync<MyDocument>(item, new PartitionKey(item.Status));
        }

        private static Task<ItemResponse<MyDocument>> ExecuteReplaceAsync(Container container, MyDocument item)
        {
            return container.ReplaceItemAsync<MyDocument>(item, item.id, new PartitionKey(item.Status));
        }

        private static Task<ItemResponse<MyDocument>> ExecuteDeleteAsync(Container container, MyDocument item)
        {
            return container.DeleteItemAsync<MyDocument>(item.id, new PartitionKey(item.Status));
        }

        private static Task<ItemResponse<MyDocument>> ExecuteReadAsync(Container container, MyDocument item)
        {
            return container.ReadItemAsync<MyDocument>(item.id, new PartitionKey(item.Status));
        }

        private static Task<ResponseMessage> ExecuteCreateStreamAsync(Container container, MyDocument item)
        {
            return container.CreateItemStreamAsync(cosmosDefaultJsonSerializer.ToStream(item), new PartitionKey(item.Status));
        }

        private static Task<ResponseMessage> ExecuteUpsertStreamAsync(Container container, MyDocument item)
        {
            return container.UpsertItemStreamAsync(cosmosDefaultJsonSerializer.ToStream(item), new PartitionKey(item.Status));
        }

        private static Task<ResponseMessage> ExecuteReplaceStreamAsync(Container container, MyDocument item)
        {
            return container.ReplaceItemStreamAsync(cosmosDefaultJsonSerializer.ToStream(item), item.id, new PartitionKey(item.Status));
        }

        private static Task<ResponseMessage> ExecuteDeleteStreamAsync(Container container, MyDocument item)
        {
            return container.DeleteItemStreamAsync(item.id, new PartitionKey(item.Status));
        }

        private static Task<ResponseMessage> ExecuteReadStreamAsync(Container container, MyDocument item)
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
