//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Handler;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json;
    using Documents.Rntbd;
    using System.Globalization;
    using System.Linq;
    using Cosmos.Util;
    using Microsoft.Azure.Cosmos.Telemetry.Models;

    [TestClass]
    public class ClientTelemetryTests : BaseCosmosClientHelper
    {
        private const int scheduledInSeconds = 1;
        private static readonly object jsonObject = JsonConvert.DeserializeObject("{\"compute\":{\"azEnvironment\":\"AzurePublicCloud\",\"customData\":\"\",\"isHostCompatibilityLayerVm\":\"false\",\"licenseType\":\"\",\"location\":\"eastus\",\"name\":\"sourabh-testing\",\"offer\":\"UbuntuServer\",\"osProfile\":{\"adminUsername\":\"azureuser\",\"computerName\":\"sourabh-testing\"},\"osType\":\"Linux\",\"placementGroupId\":\"\",\"plan\":{\"name\":\"\",\"product\":\"\",\"publisher\":\"\"},\"platformFaultDomain\":\"0\",\"platformUpdateDomain\":\"0\",\"provider\":\"Microsoft.Compute\",\"publicKeys\":[{\"keyData\":\"ssh-rsa AAAAB3NzaC1yc2EAAAADAQABAAABgQC5uCeOAm3ehmhI+2PbMoMl17Eo\r\nqfHKCycSaBJsv9qxlmBOuFheSJc1XknJleXUSsuTO016/d1PyWpevnqOZNRksWoa\r\nJvQ23sDTxcK+X2OP3QlCUeX4cMjPXqlL8z1UYzU4Bx3fFvf8fs67G3N72sxWBw5P\r\nZyuXyhBm0NCe/2NYMKgEDT4ma8XszO0ikbhoPKbMbgHAQk/ktWQHNcqYOPQKEWqp\r\nEK1R0rjS2nmtovfScP/ZGXcvOpJ1/NDBo4dh1K+OxOGM/4PSH/F448J5Zy4eAyEk\r\nscys+IpeIOTOlRUy/703SNIX0LEWlnYqbyL9c1ypcYLQqF76fKkDfzzFI/OWVlGw\r\nhj/S9uP8iMsR+fhGIbn6MAa7O4DWPWLuedSp7KDYyjY09gqNJsfuaAJN4LiC6bPy\r\nhknm0PVLK3ux7EUOt+cZrHCdIFWbdOtxiPNIl1tkv9kV5aE5Aj2gJm4MeB9uXYhS\r\nOuksboBc0wyUGrl9+XZJ1+NlZOf7IjVi86CieK8= generated-by-azure\r\n\",\"path\":\"/home/azureuser/.ssh/authorized_keys\"}],\"publisher\":\"Canonical\",\"resourceGroupName\":\"sourabh-telemetry-sdk\",\"resourceId\":\"/subscriptions/8fba6d4f-7c37-4d13-9063-fd58ad2b86e2/resourceGroups/sourabh-telemetry-sdk/providers/Microsoft.Compute/virtualMachines/sourabh-testing\",\"securityProfile\":{\"secureBootEnabled\":\"false\",\"virtualTpmEnabled\":\"false\"},\"sku\":\"18.04-LTS\",\"storageProfile\":{\"dataDisks\":[],\"imageReference\":{\"id\":\"\",\"offer\":\"UbuntuServer\",\"publisher\":\"Canonical\",\"sku\":\"18.04-LTS\",\"version\":\"latest\"},\"osDisk\":{\"caching\":\"ReadWrite\",\"createOption\":\"FromImage\",\"diffDiskSettings\":{\"option\":\"\"},\"diskSizeGB\":\"30\",\"encryptionSettings\":{\"enabled\":\"false\"},\"image\":{\"uri\":\"\"},\"managedDisk\":{\"id\":\"/subscriptions/8fba6d4f-7c37-4d13-9063-fd58ad2b86e2/resourceGroups/sourabh-telemetry-sdk/providers/Microsoft.Compute/disks/sourabh-testing_OsDisk_1_9a54abfc5ba149c6a106bd9e5b558c2a\",\"storageAccountType\":\"Premium_LRS\"},\"name\":\"sourabh-testing_OsDisk_1_9a54abfc5ba149c6a106bd9e5b558c2a\",\"osType\":\"Linux\",\"vhd\":{\"uri\":\"\"},\"writeAcceleratorEnabled\":\"false\"}},\"subscriptionId\":\"8fba6d4f-7c37-4d13-9063-fd58ad2b86e2\",\"tags\":\"azsecpack:nonprod;platformsettings.host_environment.service.platform_optedin_for_rootcerts:true\",\"tagsList\":[{\"name\":\"azsecpack\",\"value\":\"nonprod\"},{\"name\":\"platformsettings.host_environment.service.platform_optedin_for_rootcerts\",\"value\":\"true\"}],\"version\":\"18.04.202103250\",\"vmId\":\"d0cb93eb-214b-4c2b-bd3d-cc93e90d9efd\",\"vmScaleSetName\":\"\",\"vmSize\":\"Standard_D2s_v3\",\"zone\":\"1\"},\"network\":{\"interface\":[{\"ipv4\":{\"ipAddress\":[{\"privateIpAddress\":\"10.0.7.5\",\"publicIpAddress\":\"\"}],\"subnet\":[{\"address\":\"10.0.7.0\",\"prefix\":\"24\"}]},\"ipv6\":{\"ipAddress\":[]},\"macAddress\":\"000D3A8F8BA0\"}]}}");

        private CosmosClientBuilder cosmosClientBuilder;
        private static SystemUsageMonitor systemUsageMonitor;

        private List<ClientTelemetryProperties> actualInfo;
        private List<string> preferredRegionList;

        private IDictionary<string, string> expectedMetricNameUnitMap;

        private HttpClientHandlerHelper httpHandler;
        private HttpClientHandlerHelper httpHandlerForNonAzureInstance;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
        {
            SystemUsageMonitor oldSystemUsageMonitor = (SystemUsageMonitor)typeof(DiagnosticsHandlerHelper)
                .GetField("systemUsageMonitor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(DiagnosticsHandlerHelper.Instance);
            oldSystemUsageMonitor.Stop();

            ClientTelemetryTests.ResetSystemUsageMonitor(true);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            Util.EnableClientTelemetryEnvironmentVariables();
            
            this.actualInfo = new List<ClientTelemetryProperties>();

            this.httpHandler = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellation) =>
                {
                    if (request.RequestUri.AbsoluteUri.Equals(ClientTelemetryOptions.GetClientTelemetryEndpoint().AbsoluteUri))
                    {
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                        string jsonObject = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        lock (this.actualInfo)
                        {
                            this.actualInfo.Add(JsonConvert.DeserializeObject<ClientTelemetryProperties>(jsonObject));
                        }

                        return Task.FromResult(result);
                    }
                    else if (request.RequestUri.AbsoluteUri.Equals(VmMetadataApiHandler.vmMetadataEndpointUrl.AbsoluteUri))
                    {
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                        string payload = JsonConvert.SerializeObject(ClientTelemetryTests.jsonObject);
                        result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                        return Task.FromResult(result);
                    }
                    return null;
                }
            };

            this.httpHandlerForNonAzureInstance = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellation) =>
                {
                    if (request.RequestUri.AbsoluteUri.Equals(ClientTelemetryOptions.GetClientTelemetryEndpoint().AbsoluteUri))
                    {
                        HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                        string jsonObject = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        lock (this.actualInfo)
                        {
                            this.actualInfo.Add(JsonConvert.DeserializeObject<ClientTelemetryProperties>(jsonObject));
                        }

                        return Task.FromResult(result);
                    }
                    return null;
                }
            };

            this.preferredRegionList = new List<string>
            {
                "region1",
                "region2"
            };

            this.expectedMetricNameUnitMap = new Dictionary<string, string>()
            {
                { ClientTelemetryOptions.CpuName, ClientTelemetryOptions.CpuUnit },
                { ClientTelemetryOptions.MemoryName, ClientTelemetryOptions.MemoryUnit },
                { ClientTelemetryOptions.AvailableThreadsName, ClientTelemetryOptions.AvailableThreadsUnit },
                { ClientTelemetryOptions.IsThreadStarvingName, ClientTelemetryOptions.IsThreadStarvingUnit },
                { ClientTelemetryOptions.ThreadWaitIntervalInMsName, ClientTelemetryOptions.ThreadWaitIntervalInMsUnit }
            };

            this.cosmosClientBuilder = TestCommon.GetDefaultConfiguration()
                                        .WithApplicationPreferredRegions(this.preferredRegionList);
        }

        private static void ResetSystemUsageMonitor(bool isTelemetryEnabled)
        {
            ClientTelemetryTests.systemUsageMonitor?.Stop();

            FieldInfo diagnosticsHandlerHelperInstance = typeof(DiagnosticsHandlerHelper)
                .GetField("isTelemetryMonitoringEnabled", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
            diagnosticsHandlerHelperInstance.SetValue(null, isTelemetryEnabled);

            List<SystemUsageRecorder> recorders = new List<SystemUsageRecorder>()
            {
                (SystemUsageRecorder)typeof(DiagnosticsHandlerHelper)
                        .GetField("diagnosticSystemUsageRecorder", 
                                                BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(DiagnosticsHandlerHelper.Instance)
            };

            if (isTelemetryEnabled)
            {
                recorders.Add(
                    (SystemUsageRecorder)typeof(DiagnosticsHandlerHelper)
                                .GetField("telemetrySystemUsageRecorder", 
                                                            BindingFlags.Instance | BindingFlags.NonPublic)
                                .GetValue(DiagnosticsHandlerHelper.Instance));
            }

            ClientTelemetryTests.systemUsageMonitor = SystemUsageMonitor.CreateAndStart(recorders);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            FieldInfo isInitializedField = typeof(VmMetadataApiHandler).GetField("isInitialized",
               BindingFlags.Static |
               BindingFlags.NonPublic);
            isInitializedField.SetValue(null, false);

            FieldInfo azMetadataField = typeof(VmMetadataApiHandler).GetField("azMetadata",
               BindingFlags.Static |
               BindingFlags.NonPublic);
            azMetadataField.SetValue(null, null);

            await base.TestCleanup();

            Util.DisableClientTelemetryEnvironmentVariables();
        }

        [ClassCleanup]
        public static void FinalCleanup()
        {
            ClientTelemetryTests.ResetSystemUsageMonitor(false);
        }
            
        [TestMethod]
        [DataRow(ConnectionMode.Direct, true)]
        [DataRow(ConnectionMode.Gateway, true)]
        [DataRow(ConnectionMode.Direct, false)]
        [DataRow(ConnectionMode.Gateway, false)]
        public async Task PointSuccessOperationsTest(ConnectionMode mode, bool isAzureInstance)
        {
            Container container = await this.CreateClientAndContainer(
                mode: mode,
                isAzureInstance: isAzureInstance);

            // Create an item
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");
            ItemResponse<ToDoActivity> createResponse = await container.CreateItemAsync<ToDoActivity>(testItem);
            ToDoActivity testItemCreated = createResponse.Resource;

            // Read an Item
            await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));

            // Upsert an Item
            await container.UpsertItemAsync<ToDoActivity>(testItem);

            // Replace an Item
            await container.ReplaceItemAsync<ToDoActivity>(testItemCreated, testItemCreated.id.ToString());

            // Patch an Item
            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Add("/new", "patched")
            };
            await ((ContainerInternal)container).PatchItemAsync<ToDoActivity>(
                testItem.id,
                new Cosmos.PartitionKey(testItem.id),
                patch);

            // Delete an Item
            await container.DeleteItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Create.ToString(), 1},
                { Documents.OperationType.Upsert.ToString(), 1},
                { Documents.OperationType.Read.ToString(), 1},
                { Documents.OperationType.Replace.ToString(), 1},
                { Documents.OperationType.Patch.ToString(), 1},
                { Documents.OperationType.Delete.ToString(), 1}
            };

            await this.WaitAndAssert(expectedOperationCount: 12,
                expectedOperationRecordCountMap: expectedRecordCountInOperation,
                isAzureInstance: isAzureInstance);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task PointReadFailureOperationsTest(ConnectionMode mode)
        {
            // Fail Read
            try
            {
                Container container = await this.CreateClientAndContainer(mode, Microsoft.Azure.Cosmos.ConsistencyLevel.ConsistentPrefix);

                await container.ReadItemAsync<JObject>(
                    new Guid().ToString(),
                    new Cosmos.PartitionKey(new Guid().ToString()),
                     new ItemRequestOptions()
                     {
                         BaseConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.Eventual // overriding client level consistency
                     });
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound)
            {
                string message = ce.ToString();
                Assert.IsNotNull(message);
            }

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Read.ToString(), 1}
            };

            await this.WaitAndAssert(expectedOperationCount: 2,
                expectedConsistencyLevel: Microsoft.Azure.Cosmos.ConsistencyLevel.Eventual,
                expectedOperationRecordCountMap: expectedRecordCountInOperation, 
                expectedCacheSource: null,
                isExpectedNetworkTelemetry: false);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task StreamReadFailureOperationsTest(ConnectionMode mode)
        {
            Container container = await this.CreateClientAndContainer(mode);

            // Fail Read
            try
            {
                await container.ReadItemStreamAsync(
                    new Guid().ToString(),
                    new Cosmos.PartitionKey(new Guid().ToString()),
                    new ItemRequestOptions()
                    {
                        BaseConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.ConsistentPrefix // Request level consistency
                    });
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.NotFound)
            {
                string message = ce.ToString();
                Assert.IsNotNull(message);
            }

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Read.ToString(), 1}
            };

            await this.WaitAndAssert(expectedOperationCount: 2,
                expectedConsistencyLevel: Microsoft.Azure.Cosmos.ConsistencyLevel.ConsistentPrefix,
                expectedOperationRecordCountMap: expectedRecordCountInOperation,
                expectedCacheSource: null,
                isExpectedNetworkTelemetry: false);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task StreamOperationsTest(ConnectionMode mode)
        {
            Container container = await this.CreateClientAndContainer(mode);

            // Create an item
            var testItem = new { id = "MyTestItemId", partitionKeyPath = "MyTestPkValue", details = "it's working", status = "done" };
            await container
                .CreateItemStreamAsync(TestCommon.SerializerCore.ToStream(testItem),
                new Cosmos.PartitionKey(testItem.id));

            //Upsert an Item
            await container.UpsertItemStreamAsync(TestCommon.SerializerCore.ToStream(testItem), new Cosmos.PartitionKey(testItem.id));

            //Read an Item
            await container.ReadItemStreamAsync(testItem.id, new Cosmos.PartitionKey(testItem.id));

            //Replace an Item
            await container.ReplaceItemStreamAsync(TestCommon.SerializerCore.ToStream(testItem), testItem.id, new Cosmos.PartitionKey(testItem.id));

            // Patch an Item
            List<PatchOperation> patch = new List<PatchOperation>()
            {
                PatchOperation.Add("/new", "patched")
            };
            await ((ContainerInternal)container).PatchItemStreamAsync(
                partitionKey: new Cosmos.PartitionKey(testItem.id),
                id: testItem.id,
                patchOperations: patch);

            //Delete an Item
            await container.DeleteItemStreamAsync(testItem.id, new Cosmos.PartitionKey(testItem.id));

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Create.ToString(), 1},
                { Documents.OperationType.Upsert.ToString(), 1},
                { Documents.OperationType.Read.ToString(), 1},
                { Documents.OperationType.Replace.ToString(), 1},
                { Documents.OperationType.Patch.ToString(), 1},
                { Documents.OperationType.Delete.ToString(), 1}
            };

            await this.WaitAndAssert(expectedOperationCount: 12,
                expectedOperationRecordCountMap: expectedRecordCountInOperation,
                expectedCacheSource: null);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task BatchOperationsTest(ConnectionMode mode)
        {
            Container container = await this.CreateClientAndContainer(mode, Microsoft.Azure.Cosmos.ConsistencyLevel.Eventual); // Client level consistency
            using (BatchAsyncContainerExecutor executor =
                new BatchAsyncContainerExecutor(
                    (ContainerInlineCore)container,
                    ((ContainerInlineCore)container).ClientContext,
                    20,
                    Documents.Constants.MaxDirectModeBatchRequestBodySizeInBytes)
                )
            {
                List<Task<TransactionalBatchOperationResult>> tasks = new List<Task<TransactionalBatchOperationResult>>();
                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(executor.AddAsync(CreateItem(i.ToString()), NoOpTrace.Singleton, default));
                }

                await Task.WhenAll(tasks);
            }

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Batch.ToString(), 1}
            };

            await this.WaitAndAssert(expectedOperationCount: 2,
                expectedConsistencyLevel: Microsoft.Azure.Cosmos.ConsistencyLevel.Eventual,
                expectedOperationRecordCountMap: expectedRecordCountInOperation);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task SingleOperationMultipleTimesTest(ConnectionMode mode)
        {
            Container container = await this.CreateClientAndContainer(mode);

            // Create an item
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();

            await container.CreateItemAsync<ToDoActivity>(testItem, requestOptions: new ItemRequestOptions());

            for (int count = 0; count < 50; count++)
            {
                // Read an Item
                await container.ReadItemAsync<ToDoActivity>(testItem.id, new Cosmos.PartitionKey(testItem.id));
            }

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Read.ToString(), 50},
                { Documents.OperationType.Create.ToString(), 1}
            };

            await this.WaitAndAssert(
                expectedOperationCount: 4,// 2 (read, requetLatency + requestCharge) + 2 (create, requestLatency + requestCharge)
                expectedOperationRecordCountMap: expectedRecordCountInOperation); 
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task QueryOperationSinglePartitionTest(ConnectionMode mode)
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetrySchedulingInSeconds, "20");

            Container container = await this.CreateClientAndContainer(mode);

            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue", "MyTestItemId");
            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.ConsistentPrefix
            };

            ItemResponse<ToDoActivity> createResponse = await container.CreateItemAsync<ToDoActivity>(
                item: testItem,
                requestOptions: requestOptions);

            QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
            {
                ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.ConsistentPrefix,
            };

            List<object> families = new List<object>();
            if (createResponse.StatusCode == HttpStatusCode.Created)
            {
                string sqlQueryText = "SELECT * FROM c";

                QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
                using (FeedIterator<object> queryResultSetIterator = container.GetItemQueryIterator<object>(
                    queryDefinition: queryDefinition,
                    requestOptions: queryRequestOptions))
                {
                    while (queryResultSetIterator.HasMoreResults)
                    {
                        FeedResponse<object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                        foreach (object family in currentResultSet)
                        {
                            families.Add(family);
                        }
                    }
                }

                Assert.AreEqual(1, families.Count);

            }

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Query.ToString(), 1},
                { Documents.OperationType.Create.ToString(), 1}
            };

            await this.WaitAndAssert(expectedOperationCount: 4,
                expectedOperationRecordCountMap: expectedRecordCountInOperation,
                expectedConsistencyLevel: Microsoft.Azure.Cosmos.ConsistencyLevel.ConsistentPrefix);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task QueryMultiPageSinglePartitionOperationTest(ConnectionMode mode)
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetrySchedulingInSeconds, "20");
            Container container = await this.CreateClientAndContainer(mode: mode);

            ItemRequestOptions requestOptions = new ItemRequestOptions()
            {
                ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.ConsistentPrefix
            };

            ToDoActivity testItem1 = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue1", "MyTestItemId1");
            ItemResponse<ToDoActivity> createResponse1 = await container.CreateItemAsync<ToDoActivity>(
                item: testItem1,
                requestOptions: requestOptions);
            ToDoActivity testItem2 = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue2", "MyTestItemId2");
            ItemResponse<ToDoActivity> createResponse2 = await container.CreateItemAsync<ToDoActivity>(
                item: testItem2,
                requestOptions: requestOptions);

            if (createResponse1.StatusCode == HttpStatusCode.Created &&
                createResponse2.StatusCode == HttpStatusCode.Created)
            {
                string sqlQueryText = "SELECT * FROM c";

                List<object> families = new List<object>();
                QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
                using (FeedIterator<object> queryResultSetIterator = container.GetItemQueryIterator<object>(
                    queryDefinition: queryDefinition,
                    requestOptions: new QueryRequestOptions()
                    {
                        ConsistencyLevel = Microsoft.Azure.Cosmos.ConsistencyLevel.ConsistentPrefix,
                        MaxItemCount = 1
                    }))
                {
                    while (queryResultSetIterator.HasMoreResults)
                    {
                        FeedResponse<object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                        foreach (object family in currentResultSet)
                        {
                            families.Add(family);
                        }
                    }
                }

                Assert.AreEqual(2, families.Count);

            }

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Query.ToString(), 3},
                { Documents.OperationType.Create.ToString(), 2}
            };

            await this.WaitAndAssert(
                expectedOperationCount: 4,
                expectedOperationRecordCountMap: expectedRecordCountInOperation,
                expectedConsistencyLevel: Microsoft.Azure.Cosmos.ConsistencyLevel.ConsistentPrefix);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task QueryOperationCrossPartitionTest(ConnectionMode mode)
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetrySchedulingInSeconds, "20");

            ContainerInternal itemsCore = (ContainerInternal)await this.CreateClientAndContainer(
                mode: mode,
                isLargeContainer: true);

            // Verify container has multiple partitions
            int pkRangesCount = (await itemsCore.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(itemsCore.LinkUri)).Count;
            Assert.IsTrue(pkRangesCount > 1, "Should have created a multi partition container.");

            Container container = (Container)itemsCore;

            await ToDoActivity.CreateRandomItems(
                container: container,
                pkCount: 2,
                perPKItemCount: 5);

            string sqlQueryText = "SELECT * FROM c";

            List<object> families = new List<object>();

            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            using (FeedIterator<object> queryResultSetIterator = container.GetItemQueryIterator<object>(queryDefinition))
            {
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    foreach (object family in currentResultSet)
                    {
                        families.Add(family);
                    }
                }
            }

            Assert.AreEqual(10, families.Count);

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Query.ToString(), pkRangesCount},
                { Documents.OperationType.Create.ToString(), 10}
            };

            await this.WaitAndAssert(
                            expectedOperationCount: 4,
                            expectedOperationRecordCountMap: expectedRecordCountInOperation);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task QueryOperationMutiplePageCrossPartitionTest(ConnectionMode mode)
        {
            Environment.SetEnvironmentVariable(ClientTelemetryOptions.EnvPropsClientTelemetrySchedulingInSeconds, "20");

            ContainerInternal itemsCore = (ContainerInternal)await this.CreateClientAndContainer(
                mode: mode,
                isLargeContainer: true);

            // Verify container has multiple partitions
            int pkRangesCount = (await itemsCore.ClientContext.DocumentClient.ReadPartitionKeyRangeFeedAsync(itemsCore.LinkUri)).Count;
            Assert.IsTrue(pkRangesCount > 1, "Should have created a multi partition container.");

            Container container = (Container)itemsCore;

            await ToDoActivity.CreateRandomItems(
                container: container,
                pkCount: 2,
                perPKItemCount: 5);

            string sqlQueryText = "SELECT * FROM c";

            List<object> families = new List<object>();
            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            using (FeedIterator<object> queryResultSetIterator = container.GetItemQueryIterator<object>(
                 queryDefinition: queryDefinition,
                 requestOptions: new QueryRequestOptions()
                 {
                     MaxItemCount = 1
                 }))
            {
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<object> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    foreach (object family in currentResultSet)
                    {
                        families.Add(family);
                    }
                }
            }

            Assert.AreEqual(10, families.Count);

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Query.ToString(), pkRangesCount + 10}, // 10 is number of items
                { Documents.OperationType.Create.ToString(), 10}
            };

            await this.WaitAndAssert(
                expectedOperationCount: 4,
                expectedOperationRecordCountMap: expectedRecordCountInOperation);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task QueryOperationInvalidContinuationTokenTest(ConnectionMode mode)
        {
            Container container = await this.CreateClientAndContainer(mode);

            // Create an item : First successful request to load Cache
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");
            await container.CreateItemAsync<ToDoActivity>(testItem);

            List<ToDoActivity> results = new List<ToDoActivity>();
            using (FeedIterator<ToDoActivity> resultSetIterator = container.GetItemQueryIterator<ToDoActivity>(
                  "SELECT * FROM c",
                  continuationToken: "dummy token"))
            {
                try
                {
                    while (resultSetIterator.HasMoreResults)
                    {
                        FeedResponse<ToDoActivity> response = await resultSetIterator.ReadNextAsync();
                        results.AddRange(response);
                    }
                }
                catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.BadRequest)
                {
                    string message = ce.ToString();
                    Assert.IsNotNull(message);
                }
            }

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Create.ToString(), 1}
            };

            await this.WaitAndAssert(expectedOperationCount: 2,
                expectedOperationRecordCountMap: expectedRecordCountInOperation);
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        public async Task CreateItemWithSubStatusCodeTest(ConnectionMode mode)
        {
            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper();
            HttpClient httpClient = new HttpClient(httpHandler);

            httpHandler.RequestCallBack = (request, cancellation) =>
            {
                if (request.RequestUri.AbsoluteUri.Equals(ClientTelemetryOptions.GetClientTelemetryEndpoint().AbsoluteUri))
                {
                    HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                    string jsonObject = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    lock (this.actualInfo)
                    {
                        this.actualInfo.Add(JsonConvert.DeserializeObject<ClientTelemetryProperties>(jsonObject));
                    }

                    return Task.FromResult(result);
                }
                else if (request.RequestUri.AbsoluteUri.Equals(VmMetadataApiHandler.vmMetadataEndpointUrl.AbsoluteUri))
                {
                    HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.OK);

                    string payload = JsonConvert.SerializeObject(ClientTelemetryTests.jsonObject);
                    result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                    return Task.FromResult(result);
                }
                else if (request.Method == HttpMethod.Get && request.RequestUri.AbsolutePath == "//addresses/")
                {
                    HttpResponseMessage result = new HttpResponseMessage(HttpStatusCode.Forbidden);

                    // Add a substatus code that is not part of the enum.
                    // This ensures that if the backend adds a enum the status code is not lost.
                    result.Headers.Add(WFConstants.BackendHeaders.SubStatus, 999999.ToString(CultureInfo.InvariantCulture));

                    string payload = JsonConvert.SerializeObject(new Error() { Message = "test message" });
                    result.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                    return Task.FromResult(result);
                }

                return null;
            };

            // Replacing originally initialized cosmos Builder with this one with new handler
            this.cosmosClientBuilder = this.cosmosClientBuilder
                                        .WithHttpClientFactory(() => new HttpClient(httpHandler));

            Container container = await this.CreateClientAndContainer(
                                                mode: mode,
                                                customHttpHandler: httpHandler);
            try
            {
                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity("MyTestPkValue");
                ItemResponse<ToDoActivity> createResponse = await container.CreateItemAsync<ToDoActivity>(testItem);
                Assert.Fail("Request should throw exception.");
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.Forbidden)
            {
                Assert.AreEqual(999999, ce.SubStatusCode);
            }

            IDictionary<string, long> expectedRecordCountInOperation = new Dictionary<string, long>
            {
                { Documents.OperationType.Create.ToString(), 1}
            };
            
            await this.WaitAndAssert(expectedOperationCount: 2,
                expectedOperationRecordCountMap: expectedRecordCountInOperation,
                expectedSubstatuscode: 999999,
                isExpectedNetworkTelemetry: false);

        }

        /// <summary>
        /// This method wait for the expected operations to get recorded by telemetry and assert the values
        /// </summary>
        /// <param name="expectedOperationCount"> Expected number of unique OperationInfo irrespective of response size.  </param>
        /// <param name="expectedConsistencyLevel"> Expected Consistency level of the operation recorded by telemetry</param>
        /// <param name="expectedOperationRecordCountMap"> Expected number of requests recorded for each operation </param>
        /// <returns></returns>
        private async Task WaitAndAssert(
            int expectedOperationCount = 0,
            Microsoft.Azure.Cosmos.ConsistencyLevel? expectedConsistencyLevel = null,
            IDictionary<string, long> expectedOperationRecordCountMap = null,
            int expectedSubstatuscode = 0,
            bool? isAzureInstance = null,
            string expectedCacheSource = "ClientCollectionCache",
            bool isExpectedNetworkTelemetry = true)
        {
            Assert.IsNotNull(this.actualInfo, "Telemetry Information not available");

            // As this feature is thread based execution so wait for the results to avoid test flakiness
            List<ClientTelemetryProperties> localCopyOfActualInfo = null;
            ValueStopwatch stopwatch = ValueStopwatch.StartNew();

            HashSet<CacheRefreshInfo> cacheRefreshInfoSet = new HashSet<CacheRefreshInfo>();
            do
            {
                await Task.Delay(TimeSpan.FromMilliseconds(1500)); // wait at least for 1 round of telemetry

                HashSet<OperationInfo> actualOperationSet = new HashSet<OperationInfo>();
                HashSet<RequestInfo> requestInfoSet = new HashSet<RequestInfo>();
                
                lock (this.actualInfo)
                {
                    // Setting the number of unique OperationInfo irrespective of response size as response size is varying in case of queries.
                    this.actualInfo
                        .ForEach(x =>
                        {
                            if (x.CacheRefreshInfo != null && x.CacheRefreshInfo.Count > 0)
                            {
                                x.CacheRefreshInfo
                                  .ForEach(y =>
                                  {
                                      y.GreaterThan1Kb = false;
                                      cacheRefreshInfoSet.Add(y);
                                  });

                            }

                            x.OperationInfo
                                .ForEach(y =>
                                {
                                    y.GreaterThan1Kb = false;
                                    actualOperationSet.Add(y);
                                });
                        });

                    if (actualOperationSet.Count == expectedOperationCount / 2)
                    {
                        // Copy the list to avoid it being modified while validating
                        localCopyOfActualInfo = new List<ClientTelemetryProperties>(this.actualInfo);
                        break;
                    }

                    Assert.IsTrue(stopwatch.Elapsed.TotalMinutes < 1, $"The expected operation count({expectedOperationCount}) was never hit, Actual Operation Count is {actualOperationSet.Count}.  ActualInfo:{JsonConvert.SerializeObject(this.actualInfo)}");
                }
            }
            while (localCopyOfActualInfo == null);

            List<OperationInfo> actualOperationList = new List<OperationInfo>();
            HashSet<SystemInfo> actualSystemInformation = new HashSet<SystemInfo>();
            List<RequestInfo> actualRequestInformation = new List<RequestInfo>();
            
            if (localCopyOfActualInfo[0].ConnectionMode == ConnectionMode.Direct.ToString().ToUpperInvariant())
            {
                this.expectedMetricNameUnitMap.Add(ClientTelemetryOptions.NumberOfTcpConnectionName, ClientTelemetryOptions.NumberOfTcpConnectionUnit);
            }

            ClientTelemetryTests.AssertAccountLevelInformation(
                localCopyOfActualInfo: localCopyOfActualInfo,
                actualOperationList: actualOperationList,
                actualSystemInformation: actualSystemInformation,
                actualRequestInformation: actualRequestInformation,
                isAzureInstance: isAzureInstance);

            ClientTelemetryTests.AssertOperationLevelInformation(
                expectedConsistencyLevel: expectedConsistencyLevel,
                expectedOperationRecordCountMap: expectedOperationRecordCountMap,
                actualOperationList: actualOperationList,
                expectedSubstatuscode: expectedSubstatuscode);

            if(!string.IsNullOrEmpty(expectedCacheSource))
            {
                Assert.IsTrue(cacheRefreshInfoSet.Count > 0, "Cache Refresh Information is not there");

                ClientTelemetryTests.AssertCacheRefreshInfoInformation(
                  cacheRefreshInfoSet: cacheRefreshInfoSet,
                  expectedCacheSource: expectedCacheSource);
            }
           
            ClientTelemetryTests.AssertSystemLevelInformation(actualSystemInformation, this.expectedMetricNameUnitMap);
            if (localCopyOfActualInfo.First().ConnectionMode == ConnectionMode.Direct.ToString().ToUpperInvariant() 
                && isExpectedNetworkTelemetry)
            {
                ClientTelemetryTests.AssertNetworkLevelInformation(actualRequestInformation);
            }
            else
            {
                Assert.IsTrue(actualRequestInformation == null || actualRequestInformation.Count == 0, "Request Information is not expected in Gateway mode");
            }
        }
        
        private static void AssertNetworkLevelInformation(List<RequestInfo> actualRequestInformation)
        {
            Assert.IsNotNull(actualRequestInformation);
            Assert.IsTrue(actualRequestInformation.Count > 0);
            
            foreach(RequestInfo requestInfo in actualRequestInformation)
            {
                Assert.IsNotNull(requestInfo.Uri);
                Assert.IsNotNull(requestInfo.DatabaseName);
                Assert.IsNotNull(requestInfo.ContainerName);
                Assert.IsNotNull(requestInfo.Operation);
                Assert.IsNotNull(requestInfo.Resource);
                Assert.IsNotNull(requestInfo.StatusCode);
                Assert.AreNotEqual(0, requestInfo.StatusCode);
                Assert.IsNotNull(requestInfo.SubStatusCode);

                Assert.IsNotNull(requestInfo.Metrics, "MetricInfo is null");
            }
        }
            
        private static void AssertSystemLevelInformation(HashSet<SystemInfo> actualSystemInformation, IDictionary<string, string> expectedMetricNameUnitMap)
        {
            IDictionary<string, string> actualMetricNameUnitMap = new Dictionary<string, string>();

            // Asserting If system information list is as expected
            foreach (SystemInfo systemInfo in actualSystemInformation)
            {
                Assert.AreEqual("HostMachine", systemInfo.Resource);
                Assert.IsNotNull(systemInfo.MetricInfo, "MetricInfo is null");

                if(!actualMetricNameUnitMap.TryAdd(systemInfo.MetricInfo.MetricsName, systemInfo.MetricInfo.UnitName))
                {
                    Assert.AreEqual(systemInfo.MetricInfo.UnitName, actualMetricNameUnitMap[systemInfo.MetricInfo.MetricsName]);
                }

                if(!systemInfo.MetricInfo.MetricsName.Equals(ClientTelemetryOptions.IsThreadStarvingName) &&
                    !systemInfo.MetricInfo.MetricsName.Equals(ClientTelemetryOptions.ThreadWaitIntervalInMsName))
                {
                    Assert.IsTrue(systemInfo.MetricInfo.Count > 0, $"MetricInfo ({systemInfo.MetricInfo.MetricsName}) Count is not greater than 0");
                    Assert.IsNotNull(systemInfo.MetricInfo.Percentiles, $"Percentiles is null for metrics ({systemInfo.MetricInfo.MetricsName})");
                }
                Assert.IsTrue(systemInfo.MetricInfo.Mean >= 0, $"MetricInfo ({systemInfo.MetricInfo.MetricsName}) Mean is not greater than or equal to 0");
                Assert.IsTrue(systemInfo.MetricInfo.Max >= 0, $"MetricInfo ({systemInfo.MetricInfo.MetricsName}) Max is not greater than or equal to 0");
                Assert.IsTrue(systemInfo.MetricInfo.Min >= 0, $"MetricInfo ({systemInfo.MetricInfo.MetricsName}) Min is not greater than or equal to 0");
                if (systemInfo.MetricInfo.MetricsName.Equals(ClientTelemetryOptions.CpuName))
                {
                    Assert.IsTrue(systemInfo.MetricInfo.Mean <= 100, $"MetricInfo ({systemInfo.MetricInfo.MetricsName}) Mean is not greater than 100 for CPU Usage");
                    Assert.IsTrue(systemInfo.MetricInfo.Max <= 100, $"MetricInfo ({systemInfo.MetricInfo.MetricsName}) Max is not greater than 100 for CPU Usage");
                    Assert.IsTrue(systemInfo.MetricInfo.Min <= 100, $"MetricInfo ({systemInfo.MetricInfo.MetricsName}) Min is not greater than 100 for CPU Usage");
                };
            }

            Assert.IsTrue(expectedMetricNameUnitMap.EqualsTo<string, string>(actualMetricNameUnitMap), $"Actual System Information metric i.e {string.Join(", ", actualMetricNameUnitMap)} is not matching with expected System Information Metric i.e. {string.Join(", ", expectedMetricNameUnitMap)}");

        }

        private static void AssertOperationLevelInformation(
            Microsoft.Azure.Cosmos.ConsistencyLevel? expectedConsistencyLevel, 
            IDictionary<string, long> expectedOperationRecordCountMap, 
            List<OperationInfo> actualOperationList,
            int expectedSubstatuscode = 0)
        {
            IDictionary<string, long> actualOperationRecordCountMap = new Dictionary<string, long>();
            // Asserting If operation list is as expected
            foreach (OperationInfo operation in actualOperationList)
            {
                Assert.IsNotNull(operation.Operation, "Operation Type is null");
                Assert.IsNotNull(operation.Resource, "Resource Type is null");
                
                Assert.AreEqual(expectedSubstatuscode, operation.SubStatusCode);
                Assert.AreEqual(expectedConsistencyLevel?.ToString(), operation.Consistency, $"Consistency is not {expectedConsistencyLevel}");

                Assert.IsNotNull(operation.MetricInfo, "MetricInfo is null");
                Assert.IsNotNull(operation.MetricInfo.MetricsName, "MetricsName is null");
                Assert.IsNotNull(operation.MetricInfo.UnitName, "UnitName is null");
                Assert.IsNotNull(operation.MetricInfo.Percentiles, "Percentiles is null");
                Assert.IsTrue(operation.MetricInfo.Count > 0, "MetricInfo Count is not greater than 0");
                Assert.IsTrue(operation.MetricInfo.Mean >= 0, "MetricInfo Mean is not greater than or equal to 0");
                Assert.IsTrue(operation.MetricInfo.Max >= 0, "MetricInfo Max is not greater than or equal to 0");
                Assert.IsTrue(operation.MetricInfo.Min >= 0, "MetricInfo Min is not greater than or equal to 0");
                if (operation.MetricInfo.MetricsName.Equals(ClientTelemetryOptions.RequestLatencyName)) // putting this condition to avoid doubling of count as we have same information for each metrics
                {
                    if (!actualOperationRecordCountMap.TryGetValue(operation.Operation.ToString(), out long recordCount))
                    {
                        actualOperationRecordCountMap.Add(operation.Operation.ToString(), operation.MetricInfo.Count);
                    }
                    else
                    {
                        actualOperationRecordCountMap.Remove(operation.Operation.ToString());
                        actualOperationRecordCountMap.Add(operation.Operation.ToString(), recordCount + operation.MetricInfo.Count);
                    }
                }
            }

            if (expectedOperationRecordCountMap != null)
            {
                    Assert.IsTrue(expectedOperationRecordCountMap.EqualsTo<string,long>(actualOperationRecordCountMap), $"actual record i.e. ({actualOperationRecordCountMap}) for operation does not match with expected record i.e. ({expectedOperationRecordCountMap})");
            }
        }

        private static void AssertAccountLevelInformation(
            List<ClientTelemetryProperties> localCopyOfActualInfo, 
            List<OperationInfo> actualOperationList, 
            HashSet<SystemInfo> actualSystemInformation,
            List<RequestInfo> actualRequestInformation,
            bool? isAzureInstance)
        {
            ISet<string> machineId = new HashSet<string>();

            // Asserting If basic client telemetry object is as expected
            foreach (ClientTelemetryProperties telemetryInfo in localCopyOfActualInfo)
            {
                if (telemetryInfo.OperationInfo != null)
                {
                    actualOperationList.AddRange(telemetryInfo.OperationInfo);
                }

                if (telemetryInfo.SystemInfo != null)
                {
                    foreach (SystemInfo sysInfo in telemetryInfo.SystemInfo)
                    {
                        actualSystemInformation.Add(sysInfo);
                    }
                }
                
                if (telemetryInfo.RequestInfo != null)
                {
                    actualRequestInformation.AddRange(telemetryInfo.RequestInfo);
                }

                if (telemetryInfo.ConnectionMode == ConnectionMode.Direct.ToString().ToUpperInvariant())
                {
                    Assert.AreEqual(6, telemetryInfo.SystemInfo.Count, $"System Information Count doesn't Match; {JsonConvert.SerializeObject(telemetryInfo.SystemInfo)}");
                }
                else
                {
                    Assert.AreEqual(5, telemetryInfo.SystemInfo.Count, $"System Information Count doesn't Match; {JsonConvert.SerializeObject(telemetryInfo.SystemInfo)}");
                }

                Assert.IsNotNull(telemetryInfo.GlobalDatabaseAccountName, "GlobalDatabaseAccountName is null");
                Assert.IsNotNull(telemetryInfo.DateTimeUtc, "Timestamp is null");
                Assert.AreEqual(2, telemetryInfo.PreferredRegions.Count);
                Assert.AreEqual("region1", telemetryInfo.PreferredRegions[0]);
                Assert.AreEqual("region2", telemetryInfo.PreferredRegions[1]);
                Assert.AreEqual(1, telemetryInfo.AggregationIntervalInSec);
                Assert.IsNull(telemetryInfo.AcceleratedNetworking);
                Assert.IsNotNull(telemetryInfo.ClientId);
                Assert.IsNotNull(telemetryInfo.ProcessId);
                Assert.AreEqual(HashingExtension.ComputeHash(System.Diagnostics.Process.GetCurrentProcess().ProcessName), telemetryInfo.ProcessId);
                Assert.IsNotNull(telemetryInfo.UserAgent);
                Assert.IsFalse(telemetryInfo.UserAgent.Contains("userAgentSuffix"), "Useragent should not have suffix appended"); // Useragent should not contain useragentsuffix as it can have PII
                Assert.IsNotNull(telemetryInfo.ConnectionMode);

                if(!string.IsNullOrEmpty(telemetryInfo.MachineId))
                {
                    machineId.Add(telemetryInfo.MachineId);
                }
            }

            if(isAzureInstance.HasValue)
            {
                if (isAzureInstance.Value)
                {
                    Assert.AreEqual($"{VmMetadataApiHandler.VmIdPrefix}{"d0cb93eb-214b-4c2b-bd3d-cc93e90d9efd"}", machineId.First(), $"Generated Machine id is : {machineId.First()}");
                }
                else
                {
                    Assert.AreNotEqual($"{VmMetadataApiHandler.VmIdPrefix}{"d0cb93eb-214b-4c2b-bd3d-cc93e90d9efd"}", machineId.First(), $"Generated Machine id is : {machineId.First()}");
                    Assert.AreEqual(1, machineId.Count, $"Multiple Machine Id has been generated i.e {JsonConvert.SerializeObject(machineId)}");
                }
            }
        }


        private static void AssertCacheRefreshInfoInformation(
            HashSet<CacheRefreshInfo> cacheRefreshInfoSet,
            string expectedCacheSource)
        {
            foreach(CacheRefreshInfo cacheRefreshInfo in cacheRefreshInfoSet)
            {
                Assert.IsNotNull(cacheRefreshInfo.CacheRefreshSource);
                Assert.IsTrue(expectedCacheSource.Contains(cacheRefreshInfo.CacheRefreshSource));
                Assert.IsNotNull(cacheRefreshInfo.Operation, "Operation Type is null");
                Assert.IsNotNull(cacheRefreshInfo.Resource, "Resource Type is null");
                Assert.IsNotNull(cacheRefreshInfo.StatusCode, "StatusCode is null");
                Assert.IsNotNull(cacheRefreshInfo.SubStatusCode);
                Assert.IsNull(cacheRefreshInfo.Consistency);
                Assert.IsNotNull(cacheRefreshInfo.ContainerName, "ContainerName is null");
                Assert.IsNotNull(cacheRefreshInfo.MetricInfo, "MetricInfo is null");
                Assert.IsNotNull(cacheRefreshInfo.MetricInfo.MetricsName, "MetricsName is null");
                Assert.IsNotNull(cacheRefreshInfo.MetricInfo.UnitName, "UnitName is null");
                Assert.IsNotNull(cacheRefreshInfo.MetricInfo.Percentiles, "Percentiles is null");
                Assert.IsTrue(cacheRefreshInfo.MetricInfo.Count >= 0, "MetricInfo Count is not greater than 0");
                Assert.IsTrue(cacheRefreshInfo.MetricInfo.Mean >= 0, "MetricInfo Mean is not greater than or equal to 0");
                Assert.IsTrue(cacheRefreshInfo.MetricInfo.Max >= 0, "MetricInfo Max is not greater than or equal to 0");
                Assert.IsTrue(cacheRefreshInfo.MetricInfo.Min >= 0, "MetricInfo Min is not greater than or equal to 0");
            }
        }
        
        [TestMethod]
        public async Task CheckMisconfiguredTelemetryEndpoint_should_stop_the_job()
        {
            int retryCounter = 0;
            HttpClientHandlerHelper customHttpHandler = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellation) =>
                {
                    if (request.RequestUri.AbsoluteUri.Equals(ClientTelemetryOptions.GetClientTelemetryEndpoint().AbsoluteUri))
                    {
                        retryCounter++;
                        throw new Exception("Exception while sending telemetry");
                    }

                    return null;
                }
            };

            Container container = await this.CreateClientAndContainer(
                mode: ConnectionMode.Direct, 
                customHttpHandler: customHttpHandler);

            await Task.Delay(TimeSpan.FromMilliseconds(5000)); // wait for 5 sec, ideally telemetry would be sent 5 times but client telemetry endpoint is not functional (in this test), it should try 3 times maximum and after that client telemetry job should be stopped.
            
            Assert.AreEqual(3, retryCounter);
        }

        private static ItemBatchOperation CreateItem(string itemId)
        {
            var testItem = new { id = itemId, Status = itemId };
            return new ItemBatchOperation(Documents.OperationType.Create, 0, new Cosmos.PartitionKey(itemId), itemId, TestCommon.SerializerCore.ToStream(testItem));
        }

        private async Task<Container> CreateClientAndContainer(ConnectionMode mode,
            Microsoft.Azure.Cosmos.ConsistencyLevel? consistency = null,
            bool isLargeContainer = false,
            bool isAzureInstance = false,
            HttpClientHandlerHelper customHttpHandler = null)
        {
            if (consistency.HasValue)
            {
                this.cosmosClientBuilder = this.cosmosClientBuilder
                    .WithConsistencyLevel(consistency.Value);
            }

            HttpClientHandlerHelper handlerHelper;
            if (customHttpHandler == null)
            {
                handlerHelper = isAzureInstance ? this.httpHandler : this.httpHandlerForNonAzureInstance;
            } 
            else
            {
                handlerHelper = customHttpHandler;
            }

            this.cosmosClientBuilder = this.cosmosClientBuilder
                .WithHttpClientFactory(() => new HttpClient(handlerHelper))
                .WithApplicationName("userAgentSuffix");

            this.SetClient(mode == ConnectionMode.Gateway
                ? this.cosmosClientBuilder.WithConnectionModeGateway().Build()
                : this.cosmosClientBuilder.Build());

            this.database = await this.GetClient().CreateDatabaseAsync(Guid.NewGuid().ToString());
    
            return await this.database.CreateContainerAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/id",
                throughput: isLargeContainer? 15000 : 400);

        }

    }
}
