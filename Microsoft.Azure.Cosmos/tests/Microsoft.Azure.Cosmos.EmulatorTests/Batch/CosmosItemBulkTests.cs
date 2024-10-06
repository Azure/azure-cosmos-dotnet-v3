//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("Batch")]
    public class CosmosItemBulkTests
    {
        private Container container;
        private Database database;
        private CosmosClient client;

        [TestInitialize]
        public async Task TestInitialize()
        {
            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                AllowBulkExecution = true
            };
            this.client = TestCommon.CreateCosmosClient(clientOptions);
            await Util.DeleteAllDatabasesAsync(this.client);

            DatabaseResponse response = await this.client.CreateDatabaseIfNotExistsAsync(Guid.NewGuid().ToString());
            this.database = response.Database;

            ContainerResponse containerResponse = await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk", 10000);
            this.container = containerResponse;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.database.DeleteAsync();
            this.client?.Dispose();
        }

        [TestMethod]
        public async Task ValidateRequestOptions()
        {
            async Task ExecuteAndValidateCreateItemAsync(int i)
            {
                try
                {
                    await CosmosItemBulkTests.ExecuteCreateStreamAsync(
                        this.container,
                        CosmosItemBulkTests.CreateItem(i.ToString()),
                           new ItemRequestOptions()
                           {
                               Properties = new Dictionary<string, object>() { { "test", "test" } },
                               DedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions { MaxIntegratedCacheStaleness = TimeSpan.FromMinutes(3) },
                               SessionToken = Guid.NewGuid().ToString(),
                               PreTriggers = new List<string>() { "preTrigger" },
                               PostTriggers = new List<string>() { "postTrigger" }
                           });
                    Assert.Fail("Request should have failed");
                }
                catch (InvalidOperationException)
                {
                }
            }

            List<Task> tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(ExecuteAndValidateCreateItemAsync(i));
            }

            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public async Task CreateItemStream_WithBulk()
        {
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(CosmosItemBulkTests.ExecuteCreateStreamAsync(this.container, CosmosItemBulkTests.CreateItem(i.ToString())));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = tasks[i];
                ResponseMessage result = await task;
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                ToDoActivity document = TestCommon.SerializerCore.FromStream<ToDoActivity>(result.Content);
                Assert.AreEqual(i.ToString(), document.id);
            }
        }

        [TestMethod]
        public async Task CreateItemAsync_WithBulk()
        {
            List<Task<ItemResponse<ToDoActivity>>> tasks = new List<Task<ItemResponse<ToDoActivity>>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(CosmosItemBulkTests.ExecuteCreateAsync(this.container, CosmosItemBulkTests.CreateItem(i.ToString())));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<ToDoActivity>> task = tasks[i];
                ItemResponse<ToDoActivity> result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task CreateItemAsyncValidateIntendedCollRid_WithBulk()
        {
            Container container = await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/pk", 10000);

            List<Task<ItemResponse<ToDoActivity>>> tasks = new List<Task<ItemResponse<ToDoActivity>>>();

            ContainerInlineCore containerInternal = (ContainerInlineCore)container;

            string rid = await containerInternal.GetCachedRIDAsync(forceRefresh: false, NoOpTrace.Singleton, cancellationToken: default);

            // case 1. use wrong rid by using a stale rid.
            ItemRequestOptions itemRequestOptions = new ItemRequestOptions()
            {
                AddRequestHeaders = (headers) =>
                {
                    headers[Documents.HttpConstants.HttpHeaders.IsClientEncrypted] = bool.TrueString;
                    headers[Documents.WFConstants.BackendHeaders.IntendedCollectionRid] = rid;
                }
            };

            // delete the container.
            using (await this.database.GetContainer(container.Id).DeleteContainerStreamAsync())
            { }
            
            // recreate with same id.
            await this.database.CreateContainerAsync(container.Id, "/pk", 10000);


            for (int i = 0; i < 2; i++)
            {
                tasks.Add(ExecuteCreateAsync(container, CreateItem(i.ToString()), itemRequestOptions));
            }

            try
            {
                await Task.WhenAll(tasks);
                Assert.Fail("Bulk execution should have failed. ");
            }
            catch(CosmosException ex)
            {
                if(ex.StatusCode == HttpStatusCode.Created || ex.SubStatusCode != 1024)
                {
                    Assert.Fail("Bulk execution should have failed with these specific status codes. ");
                }
            }

            // case 2.
            tasks.Clear();

            // should ignore if the item is not encrypted.
            itemRequestOptions = new ItemRequestOptions()
            {
                AddRequestHeaders = (headers) =>
                {
                    headers[Documents.HttpConstants.HttpHeaders.IsClientEncrypted] = bool.FalseString;
                    headers[Documents.WFConstants.BackendHeaders.IntendedCollectionRid] = rid;
                }
            };

            for (int i = 0; i < 2; i++)
            {
                tasks.Add(ExecuteCreateAsync(container, CreateItem(i.ToString()), itemRequestOptions));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 2; i++)
            {
                Task<ItemResponse<ToDoActivity>> task = tasks[i];
                ItemResponse<ToDoActivity> result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
            }

            // case 3.
            tasks.Clear();

            // use the correct rid.
            rid = await containerInternal.GetCachedRIDAsync(forceRefresh: false, NoOpTrace.Singleton, cancellationToken: default);

            itemRequestOptions = new ItemRequestOptions()
            {
                AddRequestHeaders = (headers) =>
                {
                    headers[Documents.HttpConstants.HttpHeaders.IsClientEncrypted] = bool.TrueString;
                    headers[Documents.WFConstants.BackendHeaders.IntendedCollectionRid] = rid;
                }
            };

            for (int i = 3; i < 8; i++)
            {
                tasks.Add(ExecuteCreateAsync(container, CreateItem(i.ToString()), itemRequestOptions));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 5; i++)
            {
                Task<ItemResponse<ToDoActivity>> task = tasks[i];
                ItemResponse<ToDoActivity> result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task CreateItemJObjectWithoutPK_WithBulk()
        {
            List<Task<ItemResponse<JObject>>> tasks = new List<Task<ItemResponse<JObject>>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(this.container.CreateItemAsync(CosmosItemBulkTests.CreateJObjectWithoutPK(i.ToString())));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<JObject>> task = tasks[i];
                ItemResponse<JObject> result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task UpsertItemStream_WithBulk()
        {
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(CosmosItemBulkTests.ExecuteUpsertStreamAsync(this.container, CosmosItemBulkTests.CreateItem(i.ToString())));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = tasks[i];
                ResponseMessage result = await task;
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                ToDoActivity document = TestCommon.SerializerCore.FromStream<ToDoActivity>(result.Content);
                Assert.AreEqual(i.ToString(), document.id);
            }
        }

        [TestMethod]
        public async Task UpsertItem_WithBulk()
        {
            List<Task<ItemResponse<ToDoActivity>>> tasks = new List<Task<ItemResponse<ToDoActivity>>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(CosmosItemBulkTests.ExecuteUpsertAsync(this.container, CosmosItemBulkTests.CreateItem(i.ToString())));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<ToDoActivity>> task = tasks[i];
                ItemResponse<ToDoActivity> result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task DeleteItemStream_WithBulk()
        {
            List<ToDoActivity> createdDocuments = new List<ToDoActivity>();
            // Create the items
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                ToDoActivity createdDocument = CosmosItemBulkTests.CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(CosmosItemBulkTests.ExecuteCreateStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ResponseMessage>> deleteTasks = new List<Task<ResponseMessage>>();
            // Delete the items
            foreach (ToDoActivity createdDocument in createdDocuments)
            {
                deleteTasks.Add(CosmosItemBulkTests.ExecuteDeleteStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(deleteTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = deleteTasks[i];
                ResponseMessage result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.NoContent, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task DeleteItem_WithBulk()
        {
            List<ToDoActivity> createdDocuments = new List<ToDoActivity>();
            // Create the items
            List<Task<ItemResponse<ToDoActivity>>> tasks = new List<Task<ItemResponse<ToDoActivity>>>();
            for (int i = 0; i < 100; i++)
            {
                ToDoActivity createdDocument = CosmosItemBulkTests.CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(CosmosItemBulkTests.ExecuteCreateAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ItemResponse<ToDoActivity>>> deleteTasks = new List<Task<ItemResponse<ToDoActivity>>>();
            // Delete the items
            foreach (ToDoActivity createdDocument in createdDocuments)
            {
                deleteTasks.Add(CosmosItemBulkTests.ExecuteDeleteAsync(this.container, createdDocument));
            }

            await Task.WhenAll(deleteTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<ToDoActivity>> task = deleteTasks[i];
                ItemResponse<ToDoActivity> result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.NoContent, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReadItemStream_WithBulk()
        {
            List<ToDoActivity> createdDocuments = new List<ToDoActivity>();
            // Create the items
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                ToDoActivity createdDocument = CosmosItemBulkTests.CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(CosmosItemBulkTests.ExecuteCreateStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ResponseMessage>> readTasks = new List<Task<ResponseMessage>>();
            // Read the items
            foreach (ToDoActivity createdDocument in createdDocuments)
            {
                readTasks.Add(CosmosItemBulkTests.ExecuteReadStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(readTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = readTasks[i];
                ResponseMessage result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReadItem_WithBulk()
        {
            List<ToDoActivity> createdDocuments = new List<ToDoActivity>();
            // Create the items
            List<Task<ItemResponse<ToDoActivity>>> tasks = new List<Task<ItemResponse<ToDoActivity>>>();
            for (int i = 0; i < 100; i++)
            {
                ToDoActivity createdDocument = CosmosItemBulkTests.CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(CosmosItemBulkTests.ExecuteCreateAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ItemResponse<ToDoActivity>>> readTasks = new List<Task<ItemResponse<ToDoActivity>>>();
            // Read the items
            foreach (ToDoActivity createdDocument in createdDocuments)
            {
                readTasks.Add(CosmosItemBulkTests.ExecuteReadAsync(this.container, createdDocument));
            }

            await Task.WhenAll(readTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<ToDoActivity>> task = readTasks[i];
                ItemResponse<ToDoActivity> result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReplaceItemStream_WithBulk()
        {
            List<ToDoActivity> createdDocuments = new List<ToDoActivity>();
            // Create the items
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                ToDoActivity createdDocument = CosmosItemBulkTests.CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(CosmosItemBulkTests.ExecuteCreateStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ResponseMessage>> replaceTasks = new List<Task<ResponseMessage>>();
            // Replace the items
            foreach (ToDoActivity createdDocument in createdDocuments)
            {
                replaceTasks.Add(CosmosItemBulkTests.ExecuteReplaceStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(replaceTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = replaceTasks[i];
                ResponseMessage result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task ReplaceItem_WithBulk()
        {
            List<ToDoActivity> createdDocuments = new List<ToDoActivity>();
            // Create the items
            List<Task<ItemResponse<ToDoActivity>>> tasks = new List<Task<ItemResponse<ToDoActivity>>>();
            for (int i = 0; i < 100; i++)
            {
                ToDoActivity createdDocument = CosmosItemBulkTests.CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(CosmosItemBulkTests.ExecuteCreateAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<Task<ItemResponse<ToDoActivity>>> replaceTasks = new List<Task<ItemResponse<ToDoActivity>>>();
            // Replace the items
            foreach (ToDoActivity createdDocument in createdDocuments)
            {
                replaceTasks.Add(CosmosItemBulkTests.ExecuteReplaceAsync(this.container, createdDocument));
            }

            await Task.WhenAll(replaceTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<ToDoActivity>> task = replaceTasks[i];
                ItemResponse<ToDoActivity> result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task PatchItemStream_WithBulk()
        {
            List<ToDoActivity> createdDocuments = new List<ToDoActivity>();
            // Create the items
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 100; i++)
            {
                ToDoActivity createdDocument = CosmosItemBulkTests.CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(CosmosItemBulkTests.ExecuteCreateStreamAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Add("/description", "patched")
            };
            List<Task<ResponseMessage>> PatchTasks = new List<Task<ResponseMessage>>();
            // Patch the items
            foreach (ToDoActivity createdDocument in createdDocuments)
            {
                PatchTasks.Add(CosmosItemBulkTests.ExecutePatchStreamAsync((ContainerInternal)this.container, createdDocument, patch));
            }

            await Task.WhenAll(PatchTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ResponseMessage> task = PatchTasks[i];
                ResponseMessage result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
            }
        }

        [TestMethod]
        public async Task PatchItem_WithBulk()
        {
            List<ToDoActivity> createdDocuments = new List<ToDoActivity>();
            // Create the items
            List<Task<ItemResponse<ToDoActivity>>> tasks = new List<Task<ItemResponse<ToDoActivity>>>();
            for (int i = 0; i < 100; i++)
            {
                ToDoActivity createdDocument = CosmosItemBulkTests.CreateItem(i.ToString());
                createdDocuments.Add(createdDocument);
                tasks.Add(CosmosItemBulkTests.ExecuteCreateAsync(this.container, createdDocument));
            }

            await Task.WhenAll(tasks);

            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Add("/description", "patched")
            };
            List<Task<ItemResponse<ToDoActivity>>> patchTasks = new List<Task<ItemResponse<ToDoActivity>>>();
            // Patch the items
            foreach (ToDoActivity createdDocument in createdDocuments)
            {
                patchTasks.Add(CosmosItemBulkTests.ExecutePatchAsync((ContainerInternal)this.container, createdDocument, patch));
            }

            await Task.WhenAll(patchTasks);
            for (int i = 0; i < 100; i++)
            {
                Task<ItemResponse<ToDoActivity>> task = patchTasks[i];
                ItemResponse<ToDoActivity> result = await task;
                Assert.IsTrue(result.Headers.RequestCharge > 0);
                Assert.IsNotNull(result.Headers.Session);
                Assert.IsNotNull(result.Headers.ActivityId);
                Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
                Assert.AreEqual("patched", result.Resource.description);
            }
        }

        [TestMethod]
        public async Task CreateItemStreamSlightlyTooLarge_WithBulk()
        {
            // The item is such that it is just over the limit for an item
            // but the batch request created with it and other operations should still
            // be small enough to be valid.
            await this.CreateLargeItemStreamWithBulk(Microsoft.Azure.Documents.Constants.MaxResourceSizeInBytes + 1);
        }

        [TestMethod]
        public async Task CreateItemStreamExtremelyLarge_WithBulk()
        {
            await this.CreateLargeItemStreamWithBulk(Microsoft.Azure.Documents.Constants.MaxResourceSizeInBytes * 2);
        }

        private async Task CreateLargeItemStreamWithBulk(int appxItemSize)
        {
            List<Task<ResponseMessage>> tasks = new List<Task<ResponseMessage>>();
            for (int i = 0; i < 3; i++)
            {
                ToDoActivity item = CosmosItemBulkTests.CreateItem(i.ToString());

                if (i == 1) { item.description = new string('x', appxItemSize); }
                tasks.Add(CosmosItemBulkTests.ExecuteCreateStreamAsync(this.container, item));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 3; i++)
            {
                Task<ResponseMessage> task = tasks[i];
                ResponseMessage result = await task;
                if (i == 0 || i == 2)
                {
                    Assert.IsTrue(result.Headers.RequestCharge > 0);
                    Assert.IsNotNull(result.Headers.Session);
                    Assert.IsNotNull(result.Headers.ActivityId);
                    Assert.IsNotNull(result.Headers.PartitionKeyRangeId);
                    Assert.IsFalse(string.IsNullOrEmpty(result.Diagnostics.ToString()));
                    Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);
                }
                else
                {
                    Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, result.StatusCode);
                }
            }
        }

        private static Task<ItemResponse<ToDoActivity>> ExecuteCreateAsync(Container container, ToDoActivity item, ItemRequestOptions itemRequestOptions = null)
        {
            return container.CreateItemAsync<ToDoActivity>(item, new PartitionKey(item.pk), itemRequestOptions);
        }

        private static Task<ItemResponse<JObject>> ExecuteCreateAsync(Container container, JObject item)
        {
            return container.CreateItemAsync<JObject>(item);
        }

        private static Task<ItemResponse<ToDoActivity>> ExecuteUpsertAsync(Container container, ToDoActivity item)
        {
            return container.UpsertItemAsync<ToDoActivity>(item, new PartitionKey(item.pk));
        }

        private static Task<ItemResponse<ToDoActivity>> ExecuteReplaceAsync(Container container, ToDoActivity item)
        {
            return container.ReplaceItemAsync<ToDoActivity>(item, item.id, new PartitionKey(item.pk));
        }

        private static Task<ItemResponse<ToDoActivity>> ExecutePatchAsync(ContainerInternal container, ToDoActivity item, List<PatchOperation> patch)
        {
            return container.PatchItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk), patch);
        }

        private static Task<ItemResponse<ToDoActivity>> ExecuteDeleteAsync(Container container, ToDoActivity item)
        {
            return container.DeleteItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk));
        }

        private static Task<ItemResponse<ToDoActivity>> ExecuteReadAsync(Container container, ToDoActivity item)
        {
            return container.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk));
        }

        private static Task<ResponseMessage> ExecuteCreateStreamAsync(Container container, ToDoActivity item, ItemRequestOptions itemRequestOptions = null)
        {
            return container.CreateItemStreamAsync(TestCommon.SerializerCore.ToStream(item), new PartitionKey(item.pk), itemRequestOptions);
        }

        private static Task<ResponseMessage> ExecuteUpsertStreamAsync(Container container, ToDoActivity item)
        {
            return container.UpsertItemStreamAsync(TestCommon.SerializerCore.ToStream(item), new PartitionKey(item.pk));
        }

        private static Task<ResponseMessage> ExecuteReplaceStreamAsync(Container container, ToDoActivity item)
        {
            return container.ReplaceItemStreamAsync(TestCommon.SerializerCore.ToStream(item), item.id, new PartitionKey(item.pk));
        }

        private static Task<ResponseMessage> ExecutePatchStreamAsync(ContainerInternal container, ToDoActivity item, List<PatchOperation> patch)
        {
            return container.PatchItemStreamAsync(item.id, new PartitionKey(item.pk), patch);
        }

        private static Task<ResponseMessage> ExecuteDeleteStreamAsync(Container container, ToDoActivity item)
        {
            return container.DeleteItemStreamAsync(item.id, new PartitionKey(item.pk));
        }

        private static Task<ResponseMessage> ExecuteReadStreamAsync(Container container, ToDoActivity item)
        {
            return container.ReadItemStreamAsync(item.id, new PartitionKey(item.pk));
        }

        private static ToDoActivity CreateItem(string id)
        {
            return new ToDoActivity() { id = id, pk = id };
        }

        private static JObject CreateJObjectWithoutPK(string id)
        {
            JObject document = new JObject
            {
                ["id"] = id
            };
            return document;
        }
    }
}
