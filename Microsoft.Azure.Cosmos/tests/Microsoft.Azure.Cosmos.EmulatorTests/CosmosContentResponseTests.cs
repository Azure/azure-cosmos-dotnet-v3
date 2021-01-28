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
    public class CosmosContentResponseTests
    {
        private CosmosClient cosmosClient;
        private Database database;
        private Container container;
        private ContainerInternal containerInternal;

        [TestInitialize]
        public async Task TestInit()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient();
            this.database = await this.cosmosClient.CreateDatabaseAsync(
                   id: Guid.NewGuid().ToString());

            this.container = await this.database.CreateContainerAsync(
                     id: "ItemNoResponseTest",
                     partitionKeyPath: "/pk");
            this.containerInternal = (ContainerInternal)this.container;
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            using (await this.database.DeleteStreamAsync()) { }
            this.cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task ItemCreateNoResponseTest()
        {
            PatchRequestOptions requestOptions = new PatchRequestOptions()
            {
                EnableContentResponseOnWrite = false
            };

            await this.Validate(
                requestOptions,
                this.ValidateItemNoContentResponse,
                this.ValidateItemResponse);
        }

        [TestMethod]
        public async Task ItemReadNoResponseTest()
        {
            PatchRequestOptions requestOptions = new PatchRequestOptions()
            {
                EnableContentResponseOnRead = false
            };

            await this.Validate(
                requestOptions,
                this.ValidateItemResponse,
                this.ValidateItemNoContentResponse);
        }

        private async Task Validate(
            PatchRequestOptions requestOptions,
            Action<ItemResponse<ToDoActivity>> ValidateWrite,
            Action<ItemResponse<ToDoActivity>> ValidateRead)
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> itemResponse = await this.container.CreateItemAsync(item, requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            ValidateWrite(itemResponse);

            itemResponse = await this.container.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk), requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            ValidateRead(itemResponse);

            item.cost = 424242.42;
            itemResponse = await this.container.UpsertItemAsync<ToDoActivity>(item, requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            ValidateWrite(itemResponse);

            item.cost = 9000.42;
            itemResponse = await this.container.ReplaceItemAsync<ToDoActivity>(
                item,
                item.id,
                new PartitionKey(item.pk),
                requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            ValidateWrite(itemResponse);

            item.cost = 1000;
            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Replace("/cost", item.cost)
            };
            itemResponse = await this.containerInternal.PatchItemAsync<ToDoActivity>(
                item.id,
                new PartitionKey(item.pk),
                patchOperations: patch,
                requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, itemResponse.StatusCode);
            ValidateWrite(itemResponse);

            itemResponse = await this.container.DeleteItemAsync<ToDoActivity>(
                item.id,
                new PartitionKey(item.pk),
                requestOptions: requestOptions);
            Assert.AreEqual(HttpStatusCode.NoContent, itemResponse.StatusCode);
            this.ValidateItemNoContentResponse(itemResponse);
        }

        [TestMethod]
        public async Task ItemStreamCreateNoResponseTest()
        {
            PatchRequestOptions requestOptions = new PatchRequestOptions()
            {
                EnableContentResponseOnWrite = false
            };

            await this.ValidateItemStream(
                requestOptions,
                this.ValidateItemStreamNoContentResponse,
                this.ValidateItemStreamResponse);
        }

        [TestMethod]
        public async Task ItemStreamReadNoResponseTest()
        {
            PatchRequestOptions requestOptions = new PatchRequestOptions()
            {
                EnableContentResponseOnRead = false
            };

            await this.ValidateItemStream(
                requestOptions,
                this.ValidateItemStreamResponse,
                this.ValidateItemStreamNoContentResponse);
        }

        private async Task ValidateItemStream(
            PatchRequestOptions requestOptions,
            Action<ResponseMessage> ValidateWrite,
            Action<ResponseMessage> ValidateRead)
        {
            ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
            using (ResponseMessage responseMessage = await this.container.CreateItemStreamAsync(
                TestCommon.SerializerCore.ToStream(item),
                new PartitionKey(item.pk),
                requestOptions: requestOptions))
            {
                Assert.AreEqual(HttpStatusCode.Created, responseMessage.StatusCode);
                ValidateWrite(responseMessage);
            }

            using (ResponseMessage responseMessage = await this.container.ReadItemStreamAsync(
                item.id,
                new PartitionKey(item.pk),
                requestOptions: requestOptions))
            {
                Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
                ValidateRead(responseMessage);
            }

            item.cost = 424242.42;
            using (ResponseMessage responseMessage = await this.container.UpsertItemStreamAsync(
                TestCommon.SerializerCore.ToStream(item),
                new PartitionKey(item.pk),
                requestOptions: requestOptions))
            {
                Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
                ValidateWrite(responseMessage);
            }

            item.cost = 9000.42;
            using (ResponseMessage responseMessage = await this.container.ReplaceItemStreamAsync(
                TestCommon.SerializerCore.ToStream(item),
                item.id,
                new PartitionKey(item.pk),
                requestOptions: requestOptions))
            {
                Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
                ValidateWrite(responseMessage);
            }

            item.cost = 1000;
            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Replace("/cost", item.cost)
            };
            using (ResponseMessage responseMessage = await this.containerInternal.PatchItemStreamAsync(
                item.id,
                new PartitionKey(item.pk),
                patchOperations: patch,
                requestOptions: requestOptions))
            {
                Assert.AreEqual(HttpStatusCode.OK, responseMessage.StatusCode);
                ValidateWrite(responseMessage);
            }

            using (ResponseMessage responseMessage = await this.container.DeleteItemStreamAsync(
                item.id,
                new PartitionKey(item.pk),
                requestOptions: requestOptions))
            {
                Assert.AreEqual(HttpStatusCode.NoContent, responseMessage.StatusCode);
                this.ValidateItemStreamNoContentResponse(responseMessage);
            }
        }

        [TestMethod]
        public async Task ItemBatchNoResponseTest()
        {
            TransactionalBatchItemRequestOptions requestOptions = new TransactionalBatchItemRequestOptions()
            {
                EnableContentResponseOnWrite = false
            };

            string pkId = "TestBatchId";
            TransactionalBatch batch = this.container.CreateTransactionalBatch(new PartitionKey(pkId));

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
            batch = this.container.CreateTransactionalBatch(new PartitionKey(pkId));
            BatchCore batchCore = (BatchCore)batch;
            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Remove("/cost")
            };

            noResponseItemCount = 0;
            for (int i = 0; i < 10; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                batch.CreateItem<ToDoActivity>(item, requestOptions: requestOptions);
                noResponseItemCount++;
                ToDoActivity item2 = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                item2.id = item.id;
                batch.ReplaceItem<ToDoActivity>(item2.id, item2, requestOptions);
                noResponseItemCount++;
                batchCore.PatchItem(item2.id, patch, requestOptions);
                noResponseItemCount++;
            }

            int withBodyCount = 0;
            for (int i = 0; i < 5; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                batch.CreateItem<ToDoActivity>(item);
                withBodyCount++;
                batch.ReadItem(item.id);
                withBodyCount++;
                batchCore.PatchItem(item.id, patch);
                withBodyCount++;
            }

            response = await batch.ExecuteAsync();
            Assert.AreEqual(noResponseItemCount + withBodyCount, response.Count);
            this.ValidateResponse(response, noResponseItemCount);
        }

        [TestMethod]
        public async Task ItemBulkNoResponseTest()
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                EnableContentResponseOnWrite = false
            };

            CosmosClient bulkClient = TestCommon.CreateCosmosClient((builder) => builder.WithBulkExecution(true));
            Container bulkContainer = bulkClient.GetContainer(this.database.Id, this.container.Id);
            ContainerInternal bulkContainerInternal = (ContainerInternal)bulkContainer;
            string pkId = "TestBulkId";
            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Remove("/cost")
            };

            List<Task<ItemResponse<ToDoActivity>>> bulkOperations = new List<Task<ItemResponse<ToDoActivity>>>();
            List<ToDoActivity> items = new List<ToDoActivity>();
            for (int i = 0; i < 50; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity(pk: pkId);
                items.Add(item);
                bulkOperations.Add(bulkContainer.CreateItemAsync<ToDoActivity>(item, requestOptions: requestOptions));
            }

            foreach (Task<ItemResponse<ToDoActivity>> result in bulkOperations)
            {
                ItemResponse<ToDoActivity> itemResponse = await result;
                this.ValidateItemNoContentResponse(itemResponse);
            }

            PatchRequestOptions patchRequestOptions = new PatchRequestOptions()
            {
                EnableContentResponseOnWrite = false
            };

            foreach (ToDoActivity item in items)
            {
                bulkOperations.Add(bulkContainerInternal.PatchItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk), patch, requestOptions: patchRequestOptions));
            }

            foreach (Task<ItemResponse<ToDoActivity>> result in bulkOperations)
            {
                ItemResponse<ToDoActivity> itemResponse = await result;
                this.ValidateItemNoContentResponse(itemResponse);
            }

            bulkOperations = new List<Task<ItemResponse<ToDoActivity>>>();
            foreach (ToDoActivity item in items)
            {
                bulkOperations.Add(bulkContainer.ReadItemAsync<ToDoActivity>(item.id, new PartitionKey(item.pk), requestOptions: requestOptions));
            }

            foreach (Task<ItemResponse<ToDoActivity>> result in bulkOperations)
            {
                ItemResponse<ToDoActivity> itemResponse = await result;
                this.ValidateItemResponse(itemResponse);
            }
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

        private void ValidateItemStreamNoContentResponse(
            ResponseMessage response)
        {
            Assert.IsNotNull(response);
            this.ValidateNoContentResponse(
                response.StatusCode,
                response.Content,
                response.Headers);
        }

        private void ValidateItemStreamResponse(
           ResponseMessage response)
        {
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Content);
            Assert.IsNotNull(response.Headers);
            Assert.IsTrue(response.Headers.RequestCharge > 0);
        }

        private void ValidateItemResponse(
           ItemResponse<ToDoActivity> response)
        {
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Resource);
            Assert.IsNotNull(response.Headers);
            Assert.IsTrue(response.Headers.RequestCharge > 0);
        }

        private void ValidateItemNoContentResponse(
           ItemResponse<ToDoActivity> response)
        {
            Assert.IsNotNull(response);
            this.ValidateNoContentResponse(
                response.StatusCode,
                response.Resource,
                response.Headers);
        }

        private void ValidateResponse(
           ContainerResponse containerResponse)
        {
            Assert.IsNotNull(containerResponse);
            Assert.IsNotNull(containerResponse.Container);
            this.ValidateNoContentResponse(
                containerResponse.StatusCode,
                containerResponse.Resource,
                containerResponse.Headers);
        }

        private void ValidateResponse(
            DatabaseResponse databaseResponse)
        {
            Assert.IsNotNull(databaseResponse);
            Assert.IsNotNull(databaseResponse.Database);
            this.ValidateNoContentResponse(
                databaseResponse.StatusCode,
                databaseResponse.Resource,
                databaseResponse.Headers);
        }

        private void ValidateNoContentResponse(
            HttpStatusCode statusCode,
            dynamic resource,
            Headers headers)
        {
            Assert.IsNull(resource);
            Assert.IsNotNull(headers);
            Assert.IsTrue(headers.RequestCharge > 0);

            // Delete response does not contain etag
            if (statusCode != HttpStatusCode.NoContent)
            {
                Assert.IsNotNull(headers.ETag);
            }
            
            //Assert.IsNotNull(headers.ActivityId);
        }
    }
}
