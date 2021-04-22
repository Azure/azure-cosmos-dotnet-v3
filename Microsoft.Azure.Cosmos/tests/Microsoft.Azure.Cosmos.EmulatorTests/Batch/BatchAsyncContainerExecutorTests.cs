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
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BatchAsyncContainerExecutorTests
    {
        private static CosmosSerializer cosmosDefaultJsonSerializer = new CosmosJsonDotNetSerializer();
        private CosmosClient cosmosClient;
        private ContainerInternal cosmosContainer;

        [TestInitialize]
        public async Task InitializeAsync()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient(useGateway: true);
            DatabaseResponse db = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/Status");
            this.cosmosContainer = (ContainerInlineCore)await db.Database.CreateContainerAsync(new ContainerProperties() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition }, 10000);
        }

        [TestCleanup]
        public async Task CleanupAsync()
        {
            if (this.cosmosContainer != null)
            {
                await this.cosmosContainer.Database.DeleteAsync();
            }
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task DoOperationsAsync()
        {
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(this.cosmosContainer, this.cosmosContainer.ClientContext, 20, Constants.MaxDirectModeBatchRequestBodySizeInBytes);

            List<Task<TransactionalBatchOperationResult>> tasks = new List<Task<TransactionalBatchOperationResult>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(executor.AddAsync(CreateItem(i.ToString()), null, default(CancellationToken)));
            }

            await Task.WhenAll(tasks);

            for (int i = 0; i < 100; i++)
            {
                Task<TransactionalBatchOperationResult> task = tasks[i];
                TransactionalBatchOperationResult result = await task;
                Assert.AreEqual(HttpStatusCode.Created, result.StatusCode);

                MyDocument document = cosmosDefaultJsonSerializer.FromStream<MyDocument>(result.ResourceStream);
                Assert.AreEqual(i.ToString(), document.id);

                ItemResponse<MyDocument> storedDoc = await this.cosmosContainer.ReadItemAsync<MyDocument>(i.ToString(), new Cosmos.PartitionKey(i.ToString()));
                Assert.IsNotNull(storedDoc.Resource);
            }

            executor.Dispose();
        }

        [TestMethod]
        [Owner("maquaran")]
        public async Task ValidateInvalidRequestOptionsAsync()
        {
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(this.cosmosContainer, this.cosmosContainer.ClientContext, 20, Constants.MaxDirectModeBatchRequestBodySizeInBytes);

            string id = Guid.NewGuid().ToString();
            MyDocument myDocument = new MyDocument() { id = id, Status = id };

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => executor.ValidateOperationAsync(new ItemBatchOperation(OperationType.Replace, 0, new Cosmos.PartitionKey(id), id, cosmosDefaultJsonSerializer.ToStream(myDocument)), new ItemRequestOptions() { SessionToken = "something" }));
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => executor.ValidateOperationAsync(
                new ItemBatchOperation(OperationType.Replace, 0, new Cosmos.PartitionKey(id), id, cosmosDefaultJsonSerializer.ToStream(myDocument)), 
                new ItemRequestOptions() { DedicatedGatewayRequestOptions = new DedicatedGatewayRequestOptions { MaxIntegratedCacheStaleness = TimeSpan.FromMinutes(3) }  }));
        }

        private static ItemBatchOperation CreateItem(string id)
        {
            MyDocument myDocument = new MyDocument() { id = id, Status = id };
            return new ItemBatchOperation(OperationType.Create, 0, new Cosmos.PartitionKey(id), id, cosmosDefaultJsonSerializer.ToStream(myDocument));
        }

        private class MyDocument
        {
            public string id { get; set; }

            public string Status { get; set; }

            public bool Updated { get; set; }
        }
    }
}