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

    [TestClass]
    public class ClientTelemetryTests : BaseCosmosClientHelper
    {
        private Container container;
        private List<string> allowedMetrics;
        private List<string> allowedUnitnames;

        [TestInitialize]
        public async Task TestInitialize()
        {
            CosmosClientBuilder cosmosClientBuilder = TestCommon.GetDefaultConfiguration();
            cosmosClientBuilder.WithTelemetryEnabled();

            this.cosmosClient = cosmosClientBuilder.Build();

            string databaseId = Guid.NewGuid().ToString();
            string containerId = Guid.NewGuid().ToString();
     
            Database database = await this.cosmosClient.CreateDatabaseAsync(databaseId);
            Container container = await database.CreateContainerAsync(
                containerId,
                "/id");
            this.container = container;

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
        public async Task DBOperationsTest()
        {
            using (CosmosClient cosmosClient = this.cosmosClient)
            {
                //Patch is not there
                // Create an item
                var testItem = new { id = "MyTestItemId", partitionKeyPath = "MyTestPkValue", details = "it's working", status = "done" };
                ItemResponse<dynamic> createResponse = await this.container.CreateItemAsync<dynamic>(testItem);
                dynamic testItemCreated = createResponse.Resource;

                ClientTelemetry telemetry = cosmosClient.DocumentClient.clientTelemetry;
                ClientTelemetryInfo telemetryInfo = telemetry.clientTelemetryInfo;

                Assert.IsNull(telemetryInfo.AcceleratedNetworking);
                Assert.IsNotNull(telemetryInfo.ClientId);
                Assert.IsNotNull(telemetryInfo.GlobalDatabaseAccountName);
                Assert.IsNotNull(telemetryInfo.UserAgent);
                Assert.AreEqual(2, telemetryInfo.OperationInfoMap.Count);

                foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
                {
                    Assert.AreEqual(Documents.OperationType.Create, entry.Key.Operation);
                    Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                    Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                    Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
                }

                //Read an Item
                await this.container.ReadItemAsync<dynamic>(testItem.id, new PartitionKey(testItem.id));
                Assert.AreEqual(4, telemetryInfo.OperationInfoMap.Count);

                List<Documents.OperationType> allowedOperations
                   = new List<Documents.OperationType>(new Documents.OperationType[] {
                        Documents.OperationType.Create,
                        Documents.OperationType.Read
                   });
                foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
                {
                    Assert.IsTrue(allowedOperations.Contains(entry.Key.Operation));
                    Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                    Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                    Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
                }

                //Upsert an Item
                await this.container.UpsertItemAsync<dynamic>(testItem);
                Assert.AreEqual(6, telemetryInfo.OperationInfoMap.Count);

                allowedOperations.Add(Documents.OperationType.Upsert);
                foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
                {
                    Assert.IsTrue(allowedOperations.Contains(entry.Key.Operation));
                    Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                    Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                    Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
                }

                // Replace an Item
                await this.container.ReplaceItemAsync<dynamic>(testItemCreated, testItemCreated["id"].ToString());
                Assert.AreEqual(8, telemetryInfo.OperationInfoMap.Count);

                allowedOperations.Add(Documents.OperationType.Replace);
                foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
                {
                    Assert.IsTrue(allowedOperations.Contains(entry.Key.Operation));
                    Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                    Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                    Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
                }

                // Delete an Item
                await this.container.DeleteItemAsync<dynamic>(testItem.id, new PartitionKey(testItem.id));
                Assert.AreEqual(10, telemetryInfo.OperationInfoMap.Count);

                allowedOperations.Add(Documents.OperationType.Delete);
                foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
                {
                    Assert.IsTrue(allowedOperations.Contains(entry.Key.Operation));
                    Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                    Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                    Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
                }
            }
        }

        [TestMethod]
        public async Task DBStreamOperationsTest()
        {
            using (CosmosClient cosmosClient = this.cosmosClient)
            {
                //Patch is not there
                // Create an item
                var testItem = new { id = "MyTestItemId", partitionKeyPath = "MyTestPkValue", details = "it's working", status = "done" };
                ResponseMessage responseMessage = await this.container
                    .CreateItemStreamAsync(TestCommon.SerializerCore.ToStream(testItem), 
                    new PartitionKey(testItem.id));
                
                ClientTelemetryInfo telemetryInfo = cosmosClient.DocumentClient.clientTelemetry.clientTelemetryInfo;

                Assert.IsNull(telemetryInfo.AcceleratedNetworking);
                Assert.IsNotNull(telemetryInfo.ClientId);
                Assert.IsNotNull(telemetryInfo.GlobalDatabaseAccountName);
                Assert.IsNotNull(telemetryInfo.UserAgent);
                Assert.AreEqual(2, telemetryInfo.OperationInfoMap.Count);

                foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
                {
                    Assert.AreEqual(Documents.OperationType.Create, entry.Key.Operation);
                    Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                    Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                    Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
                }

                //Upsert an Item
                await this.container.UpsertItemStreamAsync(TestCommon.SerializerCore.ToStream(testItem), new PartitionKey(testItem.id));
                Assert.AreEqual(4, telemetryInfo.OperationInfoMap.Count);

                List<Documents.OperationType> allowedOperations
                  = new List<Documents.OperationType>(new Documents.OperationType[] {
                        Documents.OperationType.Create,
                        Documents.OperationType.Upsert
                  });
                foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
                {
                    Assert.IsTrue(allowedOperations.Contains(entry.Key.Operation));
                    Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                    Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                    Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
                }

                //Read an Item
                await this.container.ReadItemStreamAsync(testItem.id, new PartitionKey(testItem.id));
                Assert.AreEqual(6, telemetryInfo.OperationInfoMap.Count);

                allowedOperations.Add(Documents.OperationType.Read);
                foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
                {
                    Assert.IsTrue(allowedOperations.Contains(entry.Key.Operation));
                    Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                    Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                    Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
                }

                //Replace an Item
                await this.container.ReplaceItemStreamAsync(TestCommon.SerializerCore.ToStream(testItem), testItem.id, new PartitionKey(testItem.id));
                Assert.AreEqual(8, telemetryInfo.OperationInfoMap.Count);

                allowedOperations.Add(Documents.OperationType.Replace);
                foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
                {
                    Assert.IsTrue(allowedOperations.Contains(entry.Key.Operation));
                    Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                    Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                    Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
                }

                //Delete an Item
                await this.container.DeleteItemStreamAsync(testItem.id, new PartitionKey(testItem.id));
                Assert.AreEqual(10, telemetryInfo.OperationInfoMap.Count);

                allowedOperations.Add(Documents.OperationType.Delete);
                foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
                {
                    Assert.IsTrue(allowedOperations.Contains(entry.Key.Operation));
                    Assert.AreEqual(Documents.ResourceType.Document, entry.Key.Resource);
                    Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                    Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
                }
            }
        }

        [TestMethod]
        public async Task DBBatchOperationsTest()
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

            ClientTelemetryInfo telemetryInfo = this.cosmosClient.DocumentClient.clientTelemetry.clientTelemetryInfo;

            Assert.AreEqual(2, telemetryInfo.OperationInfoMap.Count);
            foreach (KeyValuePair<ReportPayload, LongConcurrentHistogram> entry in telemetryInfo.OperationInfoMap)
            {
                Assert.AreEqual(entry.Key.Operation, Documents.OperationType.Batch);
                Assert.AreEqual(entry.Key.Resource, Documents.ResourceType.Document);
                Assert.IsTrue(this.allowedMetrics.Contains(entry.Key.MetricInfo.MetricsName));
                Assert.IsTrue(this.allowedUnitnames.Contains(entry.Key.MetricInfo.UnitName));
            }
            executor.Dispose();
        }

        private static ItemBatchOperation CreateItem(string itemId)
        {
            var testItem = new { id = itemId, Status = itemId };
            return new ItemBatchOperation(Documents.OperationType.Create, 0, new Cosmos.PartitionKey(itemId), itemId, TestCommon.SerializerCore.ToStream(testItem));
        }
    }
}
