//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    [TestClass]
    public class CosmosDiagnosticTests : BaseCosmosClientHelper
    {
        private Container Container = null;
        private ContainerProperties containerSettings = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/status";
            this.containerSettings = new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey);
            ContainerResponse response = await this.database.CreateContainerAsync(
                this.containerSettings,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task PointOperationDiagnostic()
        {
            //Checking point operation diagnostics on typed operations
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> createResponse = await this.Container.CreateItemAsync<ToDoActivity>(item: testItem);
            Assert.IsNotNull(createResponse.cosmosDiagnostic.pointOperationStatistics);

            ItemResponse<ToDoActivity> readResponse = await this.Container.ReadItemAsync<ToDoActivity>(id: testItem.id, partitionKey: new PartitionKey(testItem.status));
            Assert.IsNotNull(readResponse.cosmosDiagnostic.pointOperationStatistics);

            testItem.description = "NewDescription";
            ItemResponse<ToDoActivity> replaceResponse = await this.Container.ReplaceItemAsync<ToDoActivity>(item: testItem, id: testItem.id, partitionKey: new PartitionKey(testItem.status));
            Assert.AreEqual(replaceResponse.Resource.description, "NewDescription");
            Assert.IsNotNull(replaceResponse.cosmosDiagnostic.pointOperationStatistics);

            ItemResponse<ToDoActivity> deleteResponse = await this.Container.DeleteItemAsync<ToDoActivity>(partitionKey: new Cosmos.PartitionKey(testItem.status), id: testItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.IsNotNull(deleteResponse.cosmosDiagnostic.pointOperationStatistics);

            //Checking point operation diagnostics on stream operations
            ResponseMessage createStreamResponse =  await this.Container.CreateItemStreamAsync(
                partitionKey: new PartitionKey(testItem.status),
                streamPayload: TestCommon.Serializer.ToStream<ToDoActivity>(testItem));
            Assert.IsNotNull(createStreamResponse.cosmosDiagnostic.pointOperationStatistics);

            ResponseMessage readStreamResponse = await this.Container.ReadItemStreamAsync(
                id: testItem.id,
                partitionKey: new PartitionKey(testItem.status));
            Assert.IsNotNull(readStreamResponse.cosmosDiagnostic.pointOperationStatistics);

            ResponseMessage replaceStreamResponse = await this.Container.ReplaceItemStreamAsync(
               streamPayload: TestCommon.Serializer.ToStream<ToDoActivity>(testItem),
               id: testItem.id,
               partitionKey: new PartitionKey(testItem.status));
            Assert.IsNotNull(replaceStreamResponse.cosmosDiagnostic.pointOperationStatistics);

            ResponseMessage deleteStreamResponse = await this.Container.DeleteItemStreamAsync(
               id: testItem.id,
               partitionKey: new PartitionKey(testItem.status));
            Assert.IsNotNull(deleteStreamResponse.cosmosDiagnostic.pointOperationStatistics);


        }

        [TestMethod]
        public async Task QueryOperationDiagnostic()
        {
            IList<ToDoActivity> itemList = await ToDoActivity.CreateRandomItems(this.Container, 3, randomPartitionKey: true);

            //Checking query metrics on typed query
            ToDoActivity find = itemList.First();
            QueryDefinition sql = new QueryDefinition("select * from ToDoActivity");

            QueryRequestOptions requestOptions = new QueryRequestOptions()
            {
                MaxItemCount = 1,
                MaxConcurrency = 1,
                PopulateQueryMetrics = true,
            };

            FeedIterator<ToDoActivity> feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                    sql,
                    requestOptions: requestOptions);

            while (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await feedIterator.ReadNextAsync();
                Assert.IsNotNull(iter.cosmosDiagnostic.queryOperationStatistics);
            }

            sql = new QueryDefinition("select * from ToDoActivity t ORDER BY t.cost");
            feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                   sql,
                   requestOptions: requestOptions);
            if (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await feedIterator.ReadNextAsync();
                Assert.IsNotNull(iter.cosmosDiagnostic.queryOperationStatistics);
            }

            sql = new QueryDefinition("select DISTINCT t.cost from ToDoActivity t");
            feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                   sql,
                   requestOptions: requestOptions);
            if (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await feedIterator.ReadNextAsync();
                Assert.IsNotNull(iter.cosmosDiagnostic.queryOperationStatistics);
                Assert.AreEqual(1, iter.cosmosDiagnostic.queryOperationStatistics.queryMetrics.Values.First().OutputDocumentCount);
            }

            sql = new QueryDefinition("select * from ToDoActivity OFFSET 1 LIMIT 1");
            feedIterator = this.Container.GetItemQueryIterator<ToDoActivity>(
                  sql,
                  requestOptions: requestOptions);
            if (feedIterator.HasMoreResults)
            {
                FeedResponse<ToDoActivity> iter = await feedIterator.ReadNextAsync();
                Assert.IsNotNull(iter.cosmosDiagnostic.queryOperationStatistics);
            }

            //Checking query metrics on stream query
            sql = new QueryDefinition("select * from ToDoActivity");

            FeedIterator iterator = this.Container.GetItemQueryStreamIterator(
                sql,
                requestOptions: requestOptions);
            if (iterator.HasMoreResults)
            {
                ResponseMessage responseMessage = await iterator.ReadNextAsync();
                Assert.IsNotNull(responseMessage.cosmosDiagnostic.queryOperationStatistics);
            }
        }
    }
}
