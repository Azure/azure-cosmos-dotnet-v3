namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Container = Container;

    [TestClass]
    public class SessionRetryOptionsTest
    {
        private string connectionString;
        private IDictionary<string, System.Uri> writeRegionMap;

        // to run code before running each test
        [TestInitialize]
        public async Task TestInitAsync()
        {
            this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", null);
            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }

            CosmosClient client = new CosmosClient(this.connectionString);
            await MultiRegionSetupHelpers.GetOrCreateMultiRegionDatabaseAndContainers(client);
            this.writeRegionMap = client.DocumentClient.GlobalEndpointManager.GetAvailableWriteEndpointsByLocation();
            Assert.IsTrue(this.writeRegionMap.Count() >= 2);

        }

        [TestMethod]
        [DataRow(FaultInjectionOperationType.ReadItem, DisplayName = "No retries for ReadItem when MaxInRegionRetryCount is 0 and EnableRemoteRegionPreferredForSessionRetry is true")]
        [DataRow(FaultInjectionOperationType.QueryItem, DisplayName = "No retries for QueryItem when MaxInRegionRetryCount is 0 and and EnableRemoteRegionPreferredForSessionRetry is true")]
        [TestCategory("MultiMaster")]
        public async Task ReadOrQueryOperationWithMaxInRegionRetryCountZero(FaultInjectionOperationType faultInjectionOperationType)
        {
            string[] preferredRegions = this.writeRegionMap.Keys.ToArray();

            FaultInjectionRule badSessionTokenRule = new FaultInjectionRuleBuilder(
                    id: "badSessionTokenRule",
                    condition:
                        new FaultInjectionConditionBuilder()
                            .WithOperationType(faultInjectionOperationType)
                            .WithRegion(preferredRegions[0])
                            .Build(),
                    result:
                        FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ReadSessionNotAvailable)
                            .Build())
                    .WithDuration(TimeSpan.FromMinutes(10))
                    .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { badSessionTokenRule };
            FaultInjector faultInjector = new FaultInjector(rules);
            Assert.IsNotNull(faultInjector);

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                EnableRemoteRegionPreferredForSessionRetry = true,
                ConsistencyLevel = ConsistencyLevel.Session,
                ApplicationPreferredRegions = preferredRegions,
                ConnectionMode = ConnectionMode.Direct,
            };

            // Explicitly set MaxInRegionRetryCount to 0 in SessionRetryOptions
            clientOptions.SessionRetryOptions.MaxInRegionRetryCount = 0;
            using (CosmosClient faultInjectionClient = new CosmosClient(
                        connectionString: this.connectionString,
                        clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
            {
                Database database = faultInjectionClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = await database.CreateContainerIfNotExistsAsync("sessionRetryPolicy", "/id");
                string GUID = Guid.NewGuid().ToString();
                dynamic testObject = new
                {
                    id = GUID,
                    name = "customer one",
                    address = new
                    {
                        line1 = "45 new street",
                        city = "mckinney",
                        postalCode = "98989",
                    }
                };

                ItemResponse<dynamic> response = await container.CreateItemAsync<dynamic>(testObject);
                Assert.IsNotNull(response);

                OperationExecutionResult executionResult = await this.PerformDocumentOperation(faultInjectionOperationType, container, testObject);
                this.ValidateOperationExecutionResult(executionResult, true);

                // Assert that only the original attempt happened (no retries)
                long hitCount = badSessionTokenRule.GetHitCount();
                Assert.AreEqual(4, hitCount, $"There should be only one attempt (no retries) for {faultInjectionOperationType} when MaxInRegionRetryCount is 0 and RemotePreferredRegion is set to true.");
            }

        }

        [TestMethod]
        [DataRow(FaultInjectionOperationType.ReadItem, 2, true, DisplayName = "Validate Read Item operation with remote region preferred.")]
        [DataRow(FaultInjectionOperationType.QueryItem, 1, true, DisplayName = "Validate Query Item operation with remote region preferred.")]
        [DataRow(FaultInjectionOperationType.ReadItem, 2, false, DisplayName = "Validate Read Item operation with local region preferred.")]
        [DataRow(FaultInjectionOperationType.QueryItem, 2, false, DisplayName = "Validate Query Item operation with local region preferred.")]
        [TestCategory("MultiMaster")]
        public async Task ReadOperationWithReadSessionUnavailableTest(FaultInjectionOperationType faultInjectionOperationType,
            int sessionTokenMismatchRetryAttempts, Boolean remoteRegionPreferred)
        {
            string[] preferredRegions = this.writeRegionMap.Keys.ToArray();
            Environment.SetEnvironmentVariable(ConfigurationManager.MinInRegionRetryTimeForWritesInMs, "100");
            Environment.SetEnvironmentVariable(ConfigurationManager.MaxRetriesInLocalRegionWhenRemoteRegionPreferred, Convert.ToString(sessionTokenMismatchRetryAttempts));
            try
            {
                // if I go to first region for reading an item, I should get a 404/2002 response for 10 minutes
                FaultInjectionRule badSessionTokenRule = new FaultInjectionRuleBuilder(
                id: "badSessionTokenRule",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(faultInjectionOperationType)
                        .WithRegion(preferredRegions[0])
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ReadSessionNotAvailable)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(10))
                .Build();

                List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { badSessionTokenRule };
                FaultInjector faultInjector = new FaultInjector(rules);
                Assert.IsNotNull(faultInjector);
                CosmosClientOptions clientOptions = new CosmosClientOptions()
                {
                    EnableRemoteRegionPreferredForSessionRetry = remoteRegionPreferred,
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ApplicationPreferredRegions = preferredRegions,
                    ConnectionMode = ConnectionMode.Direct,
                };

                using (CosmosClient faultInjectionClient = new CosmosClient(
                    connectionString: this.connectionString,
                    clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
                {
                    Database database = faultInjectionClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                    Container container = await database.CreateContainerIfNotExistsAsync("sessionRetryPolicy", "/id");
                    string GUID = Guid.NewGuid().ToString();
                    dynamic testObject = new
                    {
                        id = GUID,
                        name = "customer one",
                        address = new
                        {
                            line1 = "45 new street",
                            city = "mckinney",
                            postalCode = "98989",
                        }

                    };

                    ItemResponse<dynamic> response = await container.CreateItemAsync<dynamic>(testObject);
                    Assert.IsNotNull(response);

                    OperationExecutionResult executionResult = await this.PerformDocumentOperation(faultInjectionOperationType, container, testObject);
                    this.ValidateOperationExecutionResult(executionResult, remoteRegionPreferred);

                    // For a non-write operation, the request can go to multiple replicas (upto 4 replicas)
                    // Check if the SessionTokenMismatchRetryPolicy retries on the bad / lagging region
                    // for sessionTokenMismatchRetryAttempts by tracking the badSessionTokenRule hit count
                    long hitCount = badSessionTokenRule.GetHitCount();

                    if (remoteRegionPreferred)
                    {
                        Assert.IsTrue(hitCount >= sessionTokenMismatchRetryAttempts && hitCount <= (1 + sessionTokenMismatchRetryAttempts) * 4);
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.MinInRegionRetryTimeForWritesInMs, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.MaxRetriesInLocalRegionWhenRemoteRegionPreferred, null);
            }
        }

        [TestMethod]
        [DataRow(FaultInjectionOperationType.CreateItem, 2, true, DisplayName = "Validate Write Item operation with remote region preferred.")]
        [DataRow(FaultInjectionOperationType.ReplaceItem, 1, true, DisplayName = "Validate Replace Item operation with remote region preferred.")]
        [DataRow(FaultInjectionOperationType.DeleteItem, 2, true, DisplayName = "Validate Delete Item operation with remote region preferred.")]
        [DataRow(FaultInjectionOperationType.UpsertItem, 3, true, DisplayName = "Validate Upsert Item operation with remote region preferred.")]
        [DataRow(FaultInjectionOperationType.PatchItem, 1, true, DisplayName = "Validate Patch Item operation with remote region preferred.")]
        [DataRow(FaultInjectionOperationType.CreateItem, 3, false, DisplayName = "Validate Write Item operation with local region preferred.")]
        [DataRow(FaultInjectionOperationType.ReplaceItem, 1, false, DisplayName = "Validate Replace Item operation with local region preferred.")]
        [DataRow(FaultInjectionOperationType.DeleteItem, 2, false, DisplayName = "Validate Delete Item operation with local region preferred.")]
        [DataRow(FaultInjectionOperationType.UpsertItem, 1, false, DisplayName = "Validate Upsert Item operation with local region preferred.")]
        [DataRow(FaultInjectionOperationType.PatchItem, 1, false, DisplayName = "Validate Patch Item operation with remote region preferred.")]
        [TestCategory("MultiMaster")]
        public async Task WriteOperationWithReadSessionUnavailableTest(FaultInjectionOperationType faultInjectionOperationType,
           int sessionTokenMismatchRetryAttempts, Boolean remoteRegionPreferred)
        {

            string[] preferredRegions = this.writeRegionMap.Keys.ToArray();
            Environment.SetEnvironmentVariable(ConfigurationManager.MinInRegionRetryTimeForWritesInMs, "100");
            Environment.SetEnvironmentVariable(ConfigurationManager.MaxRetriesInLocalRegionWhenRemoteRegionPreferred, Convert.ToString(sessionTokenMismatchRetryAttempts));

            try
            {
                FaultInjectionRule badSessionTokenRule = new FaultInjectionRuleBuilder(
                    id: "badSessionTokenRule",
                    condition:
                        new FaultInjectionConditionBuilder()
                            .WithOperationType(faultInjectionOperationType)
                            .WithRegion(preferredRegions[0])
                            .Build(),
                    result:
                        FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ReadSessionNotAvailable)
                            .Build())
                    .WithDuration(TimeSpan.FromMinutes(10))
                    .Build();

                List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { badSessionTokenRule };
                FaultInjector faultInjector = new FaultInjector(rules);

                CosmosClientOptions clientOptions = new CosmosClientOptions()
                {
                    EnableRemoteRegionPreferredForSessionRetry = remoteRegionPreferred,
                    ConsistencyLevel = ConsistencyLevel.Session,
                    ApplicationPreferredRegions = preferredRegions,
                    ConnectionMode = ConnectionMode.Direct,
                };

                using (CosmosClient faultInjectionClient = new CosmosClient(
                    connectionString: this.connectionString,
                    clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
                {
                    Database database = faultInjectionClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                    Container container = await database.CreateContainerIfNotExistsAsync("sessionRetryPolicy", "/id");
                    string GUID = Guid.NewGuid().ToString();
                    dynamic testObject = new
                    {
                        id = GUID,
                        name = "customer one",
                        address = new
                        {
                            line1 = "45 new street",
                            city = "mckinney",
                            postalCode = "98989",
                        }

                    };

                    OperationExecutionResult executionResult = await this.PerformDocumentOperation(faultInjectionOperationType, container, testObject);
                    this.ValidateOperationExecutionResult(executionResult, remoteRegionPreferred);

                    // For a write operation, the request can just go to the primary replica
                    // Check if the SessionTokenMismatchRetryPolicy retries on the bad / lagging region
                    // for sessionTokenMismatchRetryAttempts by tracking the badSessionTokenRule hit count
                    long hitCount = badSessionTokenRule.GetHitCount();
                    if (remoteRegionPreferred)
                    {
                        // higher hit count is possible while in MinRetryWaitTimeWithinRegion
                        Assert.IsTrue(hitCount >= sessionTokenMismatchRetryAttempts);
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.MinInRegionRetryTimeForWritesInMs, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.MaxRetriesInLocalRegionWhenRemoteRegionPreferred, null);
            }
        }

        private void ValidateOperationExecutionResult(OperationExecutionResult operationExecutionResult, Boolean remoteRegionPreferred)
        {
            int sessionTokenMismatchDefaultWaitTime = 5000;

            FaultInjectionOperationType executionOpType = operationExecutionResult.OperationType;
            HttpStatusCode statusCode = operationExecutionResult.StatusCode;

            int executionDuration = operationExecutionResult.Duration;
            Trace.TraceInformation($" status code is {statusCode}");
            Trace.TraceInformation($" execution duration is {executionDuration}");

            if (executionOpType == FaultInjectionOperationType.CreateItem)
            {
                Assert.IsTrue(statusCode == HttpStatusCode.Created);
            }
            else if (executionOpType == FaultInjectionOperationType.DeleteItem)
            {
                Assert.IsTrue(statusCode == HttpStatusCode.NoContent);

            }
            else if (executionOpType == FaultInjectionOperationType.UpsertItem)
            {
                Assert.IsTrue(statusCode == HttpStatusCode.OK || statusCode == HttpStatusCode.Created);

            }
            else
            {
                Assert.IsTrue(statusCode == HttpStatusCode.OK);
            }

            if (remoteRegionPreferred)
            {
                Assert.IsTrue(executionDuration < sessionTokenMismatchDefaultWaitTime);
            }
            else
            {
                Assert.IsTrue(executionDuration > sessionTokenMismatchDefaultWaitTime);
            }
        }


        private async Task<OperationExecutionResult> PerformDocumentOperation(FaultInjectionOperationType operationType, Container container,
                                                                                    dynamic testObject)
        {

            Stopwatch durationTimer = new Stopwatch();
            if (operationType == FaultInjectionOperationType.ReadItem)
            {
                durationTimer.Start();
                ItemResponse<dynamic> itemResponse = await container.ReadItemAsync<dynamic>(testObject.id,
                                                                                  new PartitionKey(testObject.id));
                durationTimer.Stop();
                int timeElapsed = Convert.ToInt32(durationTimer.Elapsed.TotalMilliseconds);

                return new OperationExecutionResult(
                    itemResponse.Diagnostics,
                    timeElapsed,
                    itemResponse.StatusCode,
                    operationType);

            }

            if (operationType == FaultInjectionOperationType.CreateItem)
            {
                durationTimer.Start();
                ItemResponse<dynamic> itemResponse = await container.CreateItemAsync<dynamic>(testObject);

                durationTimer.Stop();
                int timeElapsed = Convert.ToInt32(durationTimer.Elapsed.TotalMilliseconds);

                return new OperationExecutionResult(
                    itemResponse.Diagnostics,
                    timeElapsed,
                    itemResponse.StatusCode,
                    operationType);

            }

            if (operationType == FaultInjectionOperationType.ReplaceItem)
            {

                await container.CreateItemAsync<dynamic>(testObject);
                durationTimer.Start();

                ItemResponse<dynamic> itemResponse = await container.ReplaceItemAsync<dynamic>(testObject, testObject.id, new PartitionKey(testObject.id));

                durationTimer.Stop();
                int timeElapsed = Convert.ToInt32(durationTimer.Elapsed.TotalMilliseconds);

                return new OperationExecutionResult(
                    itemResponse.Diagnostics,
                    timeElapsed,
                    itemResponse.StatusCode,
                    operationType);

            }


            if (operationType == FaultInjectionOperationType.UpsertItem)
            {

                durationTimer.Start();
                ItemResponse<dynamic> itemResponse = await container.UpsertItemAsync<dynamic>(testObject, new PartitionKey(testObject.id));

                durationTimer.Stop();
                int timeElapsed = Convert.ToInt32(durationTimer.Elapsed.TotalMilliseconds);

                return new OperationExecutionResult(
                    itemResponse.Diagnostics,
                    timeElapsed,
                    itemResponse.StatusCode,
                    operationType);

            }

            if (operationType == FaultInjectionOperationType.DeleteItem)
            {

                await container.CreateItemAsync<dynamic>(testObject);

                durationTimer.Start();
                ItemResponse<dynamic> itemResponse = await container.DeleteItemAsync<dynamic>(testObject.id, new PartitionKey(testObject.id));

                durationTimer.Stop();
                int timeElapsed = Convert.ToInt32(durationTimer.Elapsed.TotalMilliseconds);

                return new OperationExecutionResult(
                    itemResponse.Diagnostics,
                    timeElapsed,
                    itemResponse.StatusCode,
                    operationType);

            }

            if (operationType == FaultInjectionOperationType.QueryItem)
            {
                durationTimer.Start();
                String query = $"SELECT * from c where c.id = \"{testObject.id}\"";
                FeedIterator<dynamic> feed = container.GetItemQueryIterator<dynamic>(query);
                Assert.IsTrue(feed.HasMoreResults);
                FeedResponse<dynamic> feedResponse = null;
                while (feed.HasMoreResults)
                {
                    feedResponse = await feed.ReadNextAsync();
                    Assert.IsNotNull(feedResponse);
                    Trace.TraceInformation($" feed response count is {feedResponse.Count}");
                    Assert.IsTrue(feedResponse.Count == 1);
                }

                durationTimer.Stop();
                int timeElapsed = Convert.ToInt32(durationTimer.Elapsed.TotalMilliseconds);

                return new OperationExecutionResult(
                    feedResponse.Diagnostics,
                    timeElapsed,
                    feedResponse.StatusCode,
                    operationType);

            }

            if (operationType == FaultInjectionOperationType.PatchItem)
            {
                await container.CreateItemAsync<dynamic>(testObject);
                durationTimer.Start();

                ItemResponse<dynamic> itemResponse = await container.PatchItemAsync<dynamic>(testObject.id, new PartitionKey(testObject.id),
                    patchOperations: new[]
                    {
                        PatchOperation.Replace("/name", "Customer Two")
                    });

                durationTimer.Stop();
                int timeElapsed = Convert.ToInt32(durationTimer.Elapsed.TotalMilliseconds);



                return new OperationExecutionResult(
                    itemResponse.Diagnostics,
                    timeElapsed,
                    itemResponse.StatusCode,
                    operationType);

            }



            return null;
        }

    }

    internal class OperationExecutionResult
    {
        public CosmosDiagnostics Diagnostics { get; set; }
        public int Duration { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public FaultInjectionOperationType OperationType { get; set; }

        public OperationExecutionResult(CosmosDiagnostics diagnostics, int duration, HttpStatusCode statusCode, FaultInjectionOperationType operationType)
        {
            this.Diagnostics = diagnostics;
            this.Duration = duration;
            this.StatusCode = statusCode;
            this.OperationType = operationType;
        }
    }



}