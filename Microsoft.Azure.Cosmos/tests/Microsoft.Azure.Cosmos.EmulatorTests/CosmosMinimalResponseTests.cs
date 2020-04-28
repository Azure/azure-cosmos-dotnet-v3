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
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosMinimalResponseTests
    {
        protected CosmosClient cosmosClient = null;
        protected CancellationTokenSource cancellationTokenSource = null;
        protected CancellationToken cancellationToken;

        [TestInitialize]
        public void TestInit()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.cosmosClient = TestCommon.CreateCosmosClient();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            this.cancellationTokenSource?.Cancel();
            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task DatabaseNoResponseTest()
        {
            RequestOptions requestOptions = new RequestOptions()
            {
                ReturnMinimalResponse = true
            };

            string databaseName = Guid.NewGuid().ToString();

            DatabaseResponse databaseResponse = await this.cosmosClient.CreateDatabaseAsync(
                    id: databaseName,
                    requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.Created, databaseResponse.StatusCode);
            this.ValidateResponse(databaseResponse);

            databaseResponse = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(
                    id: databaseName,
                    requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, databaseResponse.StatusCode);
            this.ValidateResponse(databaseResponse);

            databaseResponse = await databaseResponse.Database.DeleteAsync(
                    requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.NoContent, databaseResponse.StatusCode);
            this.ValidateResponse(databaseResponse);
        }

        [TestMethod]
        public async Task ContainerNoResponseTest()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                ReturnMinimalResponse = true
            };

            Database database = await this.cosmosClient.CreateDatabaseAsync(
                    id: Guid.NewGuid().ToString(),
                    requestOptions: requestOptions);

            string containerId = Guid.NewGuid().ToString();
            ContainerResponse containerResponse = await database.CreateContainerAsync(
                     id: containerId,
                     partitionKeyPath: "/id",
                     requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.Created, containerResponse.StatusCode);
            this.ValidateResponse(containerResponse);

            containerResponse = await database.CreateContainerIfNotExistsAsync(
                    id: containerId,
                    partitionKeyPath: "/id",
                    requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, containerResponse.StatusCode);
            this.ValidateResponse(containerResponse);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task ItemNoResponseTest()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                ReturnMinimalResponse = true
            };

            Database database = await this.cosmosClient.CreateDatabaseAsync(
                    id: Guid.NewGuid().ToString(),
                    requestOptions: requestOptions);

            Container container = await database.CreateContainerAsync(
                     id: "ItemNoResponseTest",
                     partitionKeyPath: "/status",
                     requestOptions: requestOptions);

            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> itemResponse = await container.CreateItemAsync(item, requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            this.ValidateResponse(itemResponse);

            itemResponse = await container.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(item.status), requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            this.ValidateResponse(itemResponse);

            item.cost = 424242.42;
            itemResponse = await container.UpsertItemAsync<ToDoActivity>(item, requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            this.ValidateResponse(itemResponse);

            item.cost = 9000.42;
            itemResponse = await container.ReplaceItemAsync<ToDoActivity>(
                item,
                item.id,
                new PartitionKey(item.status),
                requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            this.ValidateResponse(itemResponse);

            itemResponse = await container.DeleteItemAsync<ToDoActivity>(
                item.id,
                new PartitionKey(item.status),
                requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.NoContent, itemResponse.StatusCode);
            this.ValidateResponse(itemResponse);

            await database.DeleteAsync();

            Assert.ThrowsException<NotSupportedException>(() =>
            {
                new QueryRequestOptions()
                {
                    ReturnMinimalResponse = true
                };
            });
        }

        [TestMethod]
        public async Task ItemBatchNoResponseTest()
        {
            TransactionalBatchItemRequestOptions requestOptions = new TransactionalBatchItemRequestOptions()
            {
                ReturnMinimalResponse = true
            };

            Database database = await this.cosmosClient.CreateDatabaseAsync(
                    id: Guid.NewGuid().ToString(),
                    requestOptions: requestOptions);

            Container container = await database.CreateContainerAsync(
                     id: "ItemBatchNoResponseTest",
                     partitionKeyPath: "/status",
                     requestOptions: requestOptions);

            string pkId = "TestBatchId";
            TransactionalBatch batch = container.CreateTransactionalBatch(new PartitionKey(pkId));

            int noResponseItemCount = 100;
            for (int i = 0; i < noResponseItemCount; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                batch.CreateItem<ToDoActivity>(item, requestOptions: requestOptions);
            }

            TransactionalBatchResponse response = await batch.ExecuteAsync();
            Assert.AreEqual(100, response.Count);
            this.ValidateResponse(response, noResponseItemCount);

            pkId = "TestBatchId2";
            batch = container.CreateTransactionalBatch(new PartitionKey(pkId));

            noResponseItemCount = 0;
            for (int i = 0; i < 1; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                batch.CreateItem<ToDoActivity>(item, requestOptions: requestOptions);
                noResponseItemCount++;
                batch.ReadItem(item.id, requestOptions: requestOptions);
                noResponseItemCount++;
                ToDoActivity item2 = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                item2.id = item.id;
                batch.ReplaceItem<ToDoActivity>(item2.id, item2, requestOptions);
                noResponseItemCount++;
            }

            // The last 5 have a body response
            int withBodyCount = 2;
            for (int i = 0; i < withBodyCount; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                batch.CreateItem<ToDoActivity>(item);
            }

            response = await batch.ExecuteAsync();
            Assert.AreEqual(noResponseItemCount + withBodyCount, response.Count);
            this.ValidateResponse(response, noResponseItemCount);

            await database.DeleteAsync();
        }

        [TestMethod]
        public async Task ItemBulkNoResponseTest()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                ReturnMinimalResponse = true
            };

            CosmosClient bulkClient = TestCommon.CreateCosmosClient((builder) => builder.WithBulkExecution(true));
            Database database = await bulkClient.CreateDatabaseAsync(
                    id: Guid.NewGuid().ToString(),
                    requestOptions: requestOptions);

            Container container = await database.CreateContainerAsync(
                     id: "ItemBulkNoResponseTest",
                     partitionKeyPath: "/status",
                     requestOptions: requestOptions);

            string pkId = "TestBulkId";

            List<Task<ItemResponse<ToDoActivity>>> bulkOperations = new List<Task<ItemResponse<ToDoActivity>>>();
            List<ToDoActivity> items = new List<ToDoActivity>();
            for (int i = 0; i < 100; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                items.Add(item);
                bulkOperations.Add(container.CreateItemAsync<ToDoActivity>(item, requestOptions: requestOptions));
            }

            foreach(Task<ItemResponse<ToDoActivity>> result in bulkOperations)
            {
                ItemResponse<ToDoActivity> itemResponse = await result;
                this.ValidateResponse(itemResponse);
            }

            bulkOperations = new List<Task<ItemResponse<ToDoActivity>>>();
            foreach (ToDoActivity item in items)
            {
                bulkOperations.Add(container.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(item.status), requestOptions: requestOptions));
            }

            foreach (Task<ItemResponse<ToDoActivity>> result in bulkOperations)
            {
                ItemResponse<ToDoActivity> itemResponse = await result;
                this.ValidateResponse(itemResponse);
            }

            await database.DeleteAsync();
        }


        [TestMethod]
        public async Task ItemstreamNoResponseTest()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                ReturnMinimalResponse = true
            };

            Database database = await this.cosmosClient.CreateDatabaseAsync(
                    id: Guid.NewGuid().ToString(),
                    requestOptions: requestOptions);

            Container container = await database.CreateContainerAsync(
                     id: "ItemNoResponseTest",
                     partitionKeyPath: "/id",
                     requestOptions: requestOptions);

            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            using (ResponseMessage itemResponse = await container.CreateItemStreamAsync(
                TestCommon.SerializerCore.ToStream(item),
                new PartitionKey(item.id),
                requestOptions: requestOptions))
            {
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
                this.ValidateResponse(itemResponse);
            }

            item.cost = 424242.42;
            using (ResponseMessage itemResponse = await container.UpsertItemStreamAsync(
                TestCommon.SerializerCore.ToStream(item),
                new PartitionKey(item.id),
                requestOptions: requestOptions))
            {
                Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
                this.ValidateResponse(itemResponse);
            }

            item.cost = 9000.42;
            using (ResponseMessage itemResponse = await container.ReplaceItemStreamAsync(
                TestCommon.SerializerCore.ToStream(item),
                item.id,
                new PartitionKey(item.id),
                requestOptions: requestOptions))
            {
                Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
                this.ValidateResponse(itemResponse);
            }

            using (ResponseMessage itemResponse = await container.DeleteItemStreamAsync(
                item.id,
                new PartitionKey(item.id),
                requestOptions: requestOptions))
            {
                Assert.AreEqual(HttpStatusCode.NoContent, itemResponse.StatusCode);
                this.ValidateResponse(itemResponse);
            }

            await database.DeleteAsync();
        }

        private void ValidateResponse(
           TransactionalBatchResponse response,
           int noResponseItemCount)
        {
            Assert.IsNotNull(response);
            Assert.IsTrue(response.IsSuccessStatusCode);
            Assert.IsTrue(response.RequestCharge > 0);
            Assert.IsNotNull(response.ActivityId);

            int count = 0;
            foreach (TransactionalBatchOperationResult itemResponse in response)
            {
                count++;
                if(count == noResponseItemCount)
                {
                    break;
                }

                Assert.IsTrue(itemResponse.IsSuccessStatusCode);
                Assert.IsTrue(itemResponse.StatusCode == HttpStatusCode.OK || itemResponse.StatusCode == HttpStatusCode.Created);
                Assert.IsNull(itemResponse.ResourceStream);
                Assert.IsTrue(itemResponse.RequestCharge > 0);
            }

            for (int i = 0; i < response.Count && i < noResponseItemCount; i++)
            {
                TransactionalBatchOperationResult<ToDoActivity> itemResponse = response.GetOperationResultAtIndex<ToDoActivity>(i);
                Assert.IsNull(itemResponse.Resource);
                Assert.IsNull(itemResponse.ResourceStream);
                Assert.IsTrue(response.RequestCharge > 0);
                Assert.IsNotNull(response.ActivityId);
            }

            for (int i = noResponseItemCount; i < response.Count; i++)
            {
                TransactionalBatchOperationResult<ToDoActivity> itemResponse = response.GetOperationResultAtIndex<ToDoActivity>(i);
                Assert.IsNotNull(itemResponse.Resource);
                Assert.IsNotNull(itemResponse.ResourceStream);
                Assert.IsTrue(response.RequestCharge > 0);
                Assert.IsNotNull(response.ActivityId);
            }
        }

        private void ValidateResponse(
            ResponseMessage response)
        {
            Assert.IsNotNull(response);
            this.ValidateNoContentResponse(
                response.Content,
                response.Headers);
        }

        private void ValidateResponse(
           ItemResponse<ToDoActivity> response)
        {
            Assert.IsNotNull(response);
            this.ValidateNoContentResponse(
                response.Resource,
                response.Headers);
        }

        private void ValidateResponse(
           ContainerResponse containerResponse)
        {
            Assert.IsNotNull(containerResponse);
            Assert.IsNotNull(containerResponse.Container);
            this.ValidateNoContentResponse(
                containerResponse.Resource,
                containerResponse.Headers);
        }

        private void ValidateResponse(
            DatabaseResponse databaseResponse)
        {
            Assert.IsNotNull(databaseResponse);
            Assert.IsNotNull(databaseResponse.Database);
            this.ValidateNoContentResponse(
                databaseResponse.Resource,
                databaseResponse.Headers);
        }

        private void ValidateNoContentResponse(
            dynamic resource,
            Headers headers)
        {
            Assert.IsNull(resource);
            Assert.IsNotNull(headers);
            Assert.IsTrue(headers.RequestCharge > 0);
            //Assert.IsNotNull(headers.ActivityId);
        }
    }
}
