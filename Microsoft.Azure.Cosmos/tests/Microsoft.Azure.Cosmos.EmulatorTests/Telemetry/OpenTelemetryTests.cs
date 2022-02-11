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
    using System.Diagnostics;
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
    using global::Azure.Monitor.OpenTelemetry.Exporter;
    using OpenTelemetry;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;

    [TestClass]
    public class OpenTelemetryTests : BaseCosmosClientHelper
    {
        private CosmosClientBuilder cosmosClientBuilder;
        private static TracerProvider Provider;

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

            OpenTelemetryTests.Provider = Sdk.CreateTracerProviderBuilder()
                .AddSource("Azure.*") // Collect all traces from Azure SDKs
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(serviceName: "Cosmos SDK Emulator Test", serviceVersion: "1.0"))
                .AddAzureMonitorTraceExporter(options => options.ConnectionString =
                    "InstrumentationKey=2fabff39-6a32-42da-9e8f-9fcff7d99c6b;IngestionEndpoint=https://westus2-2.in.applicationinsights.azure.com/") // Export traces to Azure Monitor
                .Build();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            this.cosmosClientBuilder = TestCommon.GetDefaultConfiguration();
        }

        [ClassCleanup]
        public static void FinalCleanup()
        {
            OpenTelemetryTests.Provider.Dispose();
        }
        
        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task PointSuccessOperationsTest(ConnectionMode mode)
        {
            Container container = await this.CreateClientAndContainer(mode);
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
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        [DataRow(ConnectionMode.Gateway)]
        public async Task QueryOperationInvalidContinuationTokenTest(ConnectionMode mode)
        {
            Container container = await this.CreateClientAndContainer(mode);

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
        }

        [TestMethod]
        [DataRow(ConnectionMode.Direct)]
        public async Task CreateItemWithSubStatusCodeTest(ConnectionMode mode)
        {
            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper();
            HttpClient httpClient = new HttpClient(httpHandler);

            httpHandler.RequestCallBack = (request, cancellation) =>
            {
               if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "//addresses/")
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

            Container container = await this.CreateClientAndContainer(mode);
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

        }

        private static ItemBatchOperation CreateItem(string itemId)
        {
            var testItem = new { id = itemId, Status = itemId };
            return new ItemBatchOperation(Documents.OperationType.Create, 0, new Cosmos.PartitionKey(itemId), itemId, TestCommon.SerializerCore.ToStream(testItem));
        }

        private async Task<Container> CreateClientAndContainer(ConnectionMode mode,
            Microsoft.Azure.Cosmos.ConsistencyLevel? consistency = null,
            bool isLargeContainer = false)
        {
            if (consistency.HasValue)
            {
                this.cosmosClientBuilder = this.cosmosClientBuilder.WithConsistencyLevel(consistency.Value);
            }

            this.cosmosClient = mode == ConnectionMode.Gateway
                ? this.cosmosClientBuilder.WithConnectionModeGateway().Build()
                : this.cosmosClientBuilder.Build();

            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString());

            return await this.database.CreateContainerAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/id",
                throughput: isLargeContainer ? 15000 : 400);

        }

    }
}
