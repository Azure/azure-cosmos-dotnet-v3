//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using HdrHistogram;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Telemetry;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Threading;
    using System.Net;
    using System.Linq;

    [TestClass]
    public class ClientTelemetryTests : BaseCosmosClientHelper
    {
        private Container container;
        private List<string> allowedMetrics;
        private List<string> allowedUnitnames;
        private ClientTelemetry telemetry;
        private ClientTelemetryInfo telemetryInfo;

        [TestInitialize]
        public async Task TestInitialize()
        {
            Environment
                .SetEnvironmentVariable(ClientTelemetry.EnvPropsClientTelemetryEnabled, "true");
            Environment
                .SetEnvironmentVariable(ClientTelemetry.EnvPropsClientTelemetrySchedulingInSeconds, "1");

            CosmosClientBuilder cosmosClientBuilder = TestCommon.GetDefaultConfiguration();
            cosmosClientBuilder.WithTelemetryEnabled();

            this.cosmosClient = cosmosClientBuilder.Build();

            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());
            this.container = await this.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id");

            this.telemetry = this.cosmosClient.DocumentClient.clientTelemetry;
            this.telemetryInfo = this.telemetry.ClientTelemetryInfo;

            this.allowedMetrics = new List<string>(new string[] {
                ClientTelemetry.RequestChargeName,
                ClientTelemetry.RequestLatencyName
            });

            this.allowedUnitnames = new List<string>(new string[] {
                ClientTelemetry.RequestChargeUnit,
                ClientTelemetry.RequestLatencyUnit
            });
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task PointOperationsTest()
        {
            //Patch is not there
            // Create an item
            var testItem = new { id = "MyTestItemId", partitionKeyPath = "MyTestPkValue", details = "it's working", status = "done" };
            ItemResponse<dynamic> createResponse = await this.container.CreateItemAsync<dynamic>(testItem);
            dynamic testItemCreated = createResponse.Resource;
            List<Documents.OperationType> allowedOperations
               = new List<Documents.OperationType>(new Documents.OperationType[] {
                                Documents.OperationType.Create
               });
            this.AssertClientTelemetryInfo(allowedOperations, 2);

            //Read an Item
            await this.container.ReadItemAsync<dynamic>(testItem.id, new PartitionKey(testItem.id));
            allowedOperations.Add(Documents.OperationType.Read);
            this.AssertClientTelemetryInfo(allowedOperations, 4);

            //Upsert an Item
            await this.container.UpsertItemAsync<dynamic>(testItem);

            allowedOperations.Add(Documents.OperationType.Upsert);
            this.AssertClientTelemetryInfo(allowedOperations, 6);

            // Replace an Item
            await this.container.ReplaceItemAsync<dynamic>(testItemCreated, testItemCreated["id"].ToString());
            allowedOperations.Add(Documents.OperationType.Replace);
            this.AssertClientTelemetryInfo(allowedOperations, 8);
            
            // Patch an Item
            await ((ContainerInternal)this.container)
                .PatchItemAsync<ToDoActivity>(
                    id: testItem.id,
                    partitionKey: new Cosmos.PartitionKey(testItem.id),
                    patchOperations: new List<PatchOperation>() {
                        PatchOperation.Add("/children/1/id", "patched")
                    },
                    new PatchItemRequestOptions());

            allowedOperations.Add(Documents.OperationType.Patch);
            this.AssertClientTelemetryInfo(allowedOperations, 10);

            // Delete an Item
            await this.container.DeleteItemAsync<dynamic>(testItem.id, new PartitionKey(testItem.id));
            allowedOperations.Add(Documents.OperationType.Delete);
            this.AssertClientTelemetryInfo(allowedOperations, 12);
        }

        [TestMethod]
        public async Task StreamOperationsTest()
        {
            //Patch is not there
            // Create an item
            var testItem = new { id = "MyTestItemId", partitionKeyPath = "MyTestPkValue", details = "it's working", status = "done" };
            await this.container
                .CreateItemStreamAsync(TestCommon.SerializerCore.ToStream(testItem), 
                new PartitionKey(testItem.id));

            List<Documents.OperationType> allowedOperations
                = new List<Documents.OperationType>(new Documents.OperationType[] {
                                            Documents.OperationType.Create
                });
            this.AssertClientTelemetryInfo(allowedOperations, 2);

            //Upsert an Item
            await this.container.UpsertItemStreamAsync(TestCommon.SerializerCore.ToStream(testItem), new PartitionKey(testItem.id));
            allowedOperations.Add(Documents.OperationType.Upsert);
            this.AssertClientTelemetryInfo(allowedOperations, 4);

            //Read an Item
            await this.container.ReadItemStreamAsync(testItem.id, new PartitionKey(testItem.id));
            allowedOperations.Add(Documents.OperationType.Read);
            this.AssertClientTelemetryInfo(allowedOperations, 6);

            //Replace an Item
            await this.container.ReplaceItemStreamAsync(TestCommon.SerializerCore.ToStream(testItem), testItem.id, new PartitionKey(testItem.id));
            allowedOperations.Add(Documents.OperationType.Replace);
            this.AssertClientTelemetryInfo(allowedOperations, 8);

            //Delete an Item
            await this.container.DeleteItemStreamAsync(testItem.id, new PartitionKey(testItem.id));
            allowedOperations.Add(Documents.OperationType.Delete);
            this.AssertClientTelemetryInfo(allowedOperations, 10);
        }

        [TestMethod]
        public async Task BatchOperationsTest()
        {
            BatchAsyncContainerExecutor executor = new BatchAsyncContainerExecutor(
                (ContainerInlineCore)this.container, 
                ((ContainerInlineCore)this.container).ClientContext, 
                20,
                Documents.Constants.MaxDirectModeBatchRequestBodySizeInBytes);

            List<Task<TransactionalBatchOperationResult>> tasks = new List<Task<TransactionalBatchOperationResult>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(executor.AddAsync(CreateItem(i.ToString()), null, default));
            }

            await Task.WhenAll(tasks);

            List<Documents.OperationType> allowedOperations
               = new List<Documents.OperationType>(new Documents.OperationType[] {
                                            Documents.OperationType.Batch
               });
            this.AssertClientTelemetryInfo(allowedOperations, 2);

            executor.Dispose();
        }

        [TestMethod]
        public async Task QueryOperationTest()
        {
            var testItem = new { id = "MyTestItemId", partitionKeyPath = "MyTestPkValue", details = "it's working", status = "done" };
            ItemResponse<object> createResponse = await this.container.CreateItemAsync<dynamic>(testItem);

            if (createResponse.StatusCode == HttpStatusCode.Created)
            {
                this.telemetry.Reset();

                string sqlQueryText = "SELECT * FROM c";
                Console.WriteLine("Running query: {0}\n", sqlQueryText);

                QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
                FeedIterator<object> queryResultSetIterator = this.container.GetItemQueryIterator<object>(queryDefinition);

                List<object> families = new List<object>();
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    foreach (object family in currentResultSet)
                    {
                        families.Add(family);
                        Console.WriteLine("\tRead {0}\n", family);
                    }
                }
            }

            List<Documents.OperationType> allowedOperations
              = new List<Documents.OperationType>(new Documents.OperationType[] {
                                            Documents.OperationType.Query
              });
            this.AssertClientTelemetryInfo(allowedOperations, 2);
        }

        private void AssertClientTelemetryInfo(List<Documents.OperationType> allowedOperations, int OperationInfoMapCount)
        {
            Assert.AreEqual(OperationInfoMapCount, this.telemetryInfo.OperationInfoMap.Count);
            foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in this.telemetryInfo.OperationInfoMap)
            {
                Assert.IsTrue(allowedOperations.Contains(entry.Key.Operation));
                Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
            }
        }

        private static ItemBatchOperation CreateItem(string itemId)
        {
            var testItem = new { id = itemId, Status = itemId };
            return new ItemBatchOperation(Documents.OperationType.Create, 0, new Cosmos.PartitionKey(itemId), itemId, TestCommon.SerializerCore.ToStream(testItem));
        }


    }
}
