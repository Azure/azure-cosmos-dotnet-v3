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
    using Microsoft.Azure.Cosmos.Core.Trace;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosClientContentResponseTests
    {
        private CosmosClient cosmosClientWithFlag;
        private Database databaseWithFlag;
        private Container containerWithFlag;

        private CosmosClient cosmosClientWithoutFlag;
        private Database databaseWithoutFlag;
        private Container containerWithoutFlag;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClientWithFlag = this.CreateCosmosClientWithContentResponse(true);
            await Util.DeleteAllDatabasesAsync(this.cosmosClientWithFlag);

            this.databaseWithFlag = await this.cosmosClientWithFlag.CreateDatabaseAsync(
                   id: Guid.NewGuid().ToString());

            this.containerWithFlag = await this.databaseWithFlag.CreateContainerAsync(
                     id: "ClientItemNoResponseTest",
                     partitionKeyPath: "/pk");

            this.cosmosClientWithoutFlag = this.CreateCosmosClientWithContentResponse(false);
            this.databaseWithoutFlag = await this.cosmosClientWithoutFlag.CreateDatabaseAsync(
                   id: Guid.NewGuid().ToString());

            this.containerWithoutFlag = await this.databaseWithoutFlag.CreateContainerAsync(
                     id: "ClientItemNoResponseTest",
                     partitionKeyPath: "/pk");
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.cosmosClientWithFlag != null)
            {
                using (await this.databaseWithFlag.DeleteStreamAsync()) { }
                this.cosmosClientWithFlag.Dispose();
            }

            if (this.cosmosClientWithoutFlag != null)
            {
                using (await this.databaseWithoutFlag.DeleteStreamAsync()) { }
                this.cosmosClientWithoutFlag.Dispose();
            }
        }

        [TestMethod]
        public async Task ClientContentResponseTest()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> itemResponse = await this.containerWithFlag.CreateItemAsync(item);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse);
            Assert.IsNotNull(itemResponse.Resource);

            itemResponse = await this.containerWithFlag.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk));
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            item.cost = 424242.42;
            
            itemResponse = await this.containerWithFlag.UpsertItemAsync<ToDoActivity>(item);
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse.Resource);

            item.cost = 9000.42;
            itemResponse = await this.containerWithFlag.ReplaceItemAsync<ToDoActivity>(
                item,
                item.id,
                new PartitionKey(item.pk));
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse.Resource);

            ContainerInternal containerInternal = (ContainerInternal)this.containerWithFlag;
            item.cost = 1000;
            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Replace("/cost", item.cost)
            };
            itemResponse = await containerInternal.PatchItemAsync<ToDoActivity>(
                item.id,
                new PartitionKey(item.pk),
                patchOperations: patch);
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse.Resource);

            ItemResponse<ToDoActivity> itemResponseWithoutFlag = await this.containerWithoutFlag.CreateItemAsync(item);
            Assert.AreEqual(HttpStatusCode.Created, itemResponseWithoutFlag.StatusCode);
            Assert.IsNotNull(itemResponseWithoutFlag);
            Assert.IsNull(itemResponseWithoutFlag.Resource);
        }

        [TestMethod]
        public async Task ClientContentResponseWithItemRequestOverrideTest()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();

            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                EnableContentResponseOnWrite = false
            };

            ItemResponse<ToDoActivity> itemResponse = await this.containerWithFlag.CreateItemAsync(item, requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse);
            Assert.IsNull(itemResponse.Resource);

            ItemResponse<ToDoActivity> itemResponseWithoutFlag = await this.containerWithoutFlag.CreateItemAsync(item, requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.Created, itemResponseWithoutFlag.StatusCode);
            Assert.IsNotNull(itemResponseWithoutFlag);
            Assert.IsNull(itemResponseWithoutFlag.Resource);
        }

        [TestMethod]
        public async Task ClientContentResponseWithItemRequestOverrideTrueTest()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();

            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                EnableContentResponseOnWrite = true
            };

            ItemResponse<ToDoActivity> itemResponse = await this.containerWithFlag.CreateItemAsync(item, requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            Assert.IsNotNull(itemResponse);
            Assert.IsNotNull(itemResponse.Resource);

            ItemResponse<ToDoActivity> itemResponseWithoutFlag = await this.containerWithoutFlag.CreateItemAsync(item, requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.Created, itemResponseWithoutFlag.StatusCode);
            Assert.IsNotNull(itemResponseWithoutFlag);
            Assert.IsNotNull(itemResponseWithoutFlag.Resource);
        }

        [TestMethod]
        public async Task ClientContentResponseReadTest()
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();

            ItemResponse<ToDoActivity> itemResponse = await this.containerWithFlag.CreateItemAsync(item);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);

            // Ensuring the Reads are returning the Resource
            ItemResponse<ToDoActivity> readResponse = await this.containerWithFlag.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk));
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(itemResponse);
            Assert.IsNotNull(itemResponse.Resource);
        }

        [TestMethod]
        public async Task NoContentResponseTransactionBatchTest()
        {
            string pkId = "TestBatchId";
            TransactionalBatch batch = this.containerWithoutFlag.CreateTransactionalBatch(new PartitionKey(pkId));

            int noResponseItemCount = 100;
            for (int i = 0; i < noResponseItemCount; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                batch.CreateItem<ToDoActivity>(item);
            }

            TransactionalBatchResponse response = await batch.ExecuteAsync();
            Assert.AreEqual(response.Count, 100);
            foreach (TransactionalBatchOperationResult itemResponse in response)
            {
                Assert.IsTrue(itemResponse.StatusCode == HttpStatusCode.Created);
                Assert.IsNull(itemResponse.ResourceStream);
            }
        }

        [TestMethod]
        public async Task NoContentResponseTransactionBatchOverrideTest()
        {
            string pkId = "TestBatchId";
            TransactionalBatch batch = this.containerWithFlag.CreateTransactionalBatch(new PartitionKey(pkId));
            TransactionalBatchItemRequestOptions requestOptions = new TransactionalBatchItemRequestOptions()
            {
                EnableContentResponseOnWrite = false
            };

            int noResponseItemCount = 100;
            for (int i = 0; i < noResponseItemCount; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                batch.CreateItem<ToDoActivity>(item, requestOptions: requestOptions);
            }

            TransactionalBatchResponse response = await batch.ExecuteAsync();
            Assert.AreEqual(response.Count, 100);
            foreach (TransactionalBatchOperationResult itemResponse in response)
            {
                Assert.IsTrue(itemResponse.StatusCode == HttpStatusCode.Created);
                Assert.IsNull(itemResponse.ResourceStream);
            }
        }

        [TestMethod]
        public async Task NoContentResponseBulkTest()
        {
            CosmosClientBuilder cosmosClientBuilder = TestCommon.GetDefaultConfiguration();
            cosmosClientBuilder = cosmosClientBuilder.WithBulkExecution(true).WithContentResponseOnWrite(false);
            CosmosClient bulkClient = cosmosClientBuilder.Build();
            Container bulkContainer = bulkClient.GetContainer(this.databaseWithoutFlag.Id, this.containerWithoutFlag.Id);

            string pkId = "TestBulkId";
            List<Task<ItemResponse<ToDoActivity>>> bulkOperations = new List<Task<ItemResponse<ToDoActivity>>>();
            List<ToDoActivity> items = new List<ToDoActivity>();
            for (int i = 0; i < 50; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                items.Add(item);
                bulkOperations.Add(bulkContainer.CreateItemAsync<ToDoActivity>(item));
            }

            foreach (Task<ItemResponse<ToDoActivity>> result in bulkOperations)
            {
                ItemResponse<ToDoActivity> itemResponse = await result;
                Assert.AreEqual(itemResponse.StatusCode, HttpStatusCode.Created);
                Assert.IsNull(itemResponse.Resource);
            }
        }

        private CosmosClient CreateCosmosClientWithContentResponse(bool flag = false)
        {
            CosmosClientBuilder cosmosClientBuilder = TestCommon.GetDefaultConfiguration();
            cosmosClientBuilder = cosmosClientBuilder.WithContentResponseOnWrite(flag);
            return cosmosClientBuilder.Build();
        }
    }
}
