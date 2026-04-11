namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Routing.GlobalPartitionEndpointManagerCore;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.MultiRegionSetupHelpers;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.TransportClientHelper;

    /// <summary>
    /// Integration tests for Cosmos DB multi-region scenarios.
    /// </summary>
    [TestClass]
    public class CosmosItemIntegrationTests
    {
        private string connectionString;
        private CosmosClient client;
        private Database database;
        private Container container;
        private Container changeFeedContainer;

        private static string region1;
        private static string region2;
        private static string region3;
        private IDictionary<string, Uri> readRegionsMapping;
        private IList<Uri> thinClientreadRegionalEndpoints;
        private CosmosSystemTextJsonSerializer cosmosSystemTextJsonSerializer;
        private const string HubRegionHeader = "x-ms-cosmos-hub-region-processing-only";

        [TestInitialize]
        public async Task TestInitAsync()
        {
            this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", null);

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            this.cosmosSystemTextJsonSerializer = new MultiRegionSetupHelpers.CosmosSystemTextJsonSerializer(jsonSerializerOptions);

            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }
            this.client = new CosmosClient(
                this.connectionString,
                new CosmosClientOptions()
                {
                    Serializer = this.cosmosSystemTextJsonSerializer,
                });

            (this.database, this.container, this.changeFeedContainer) = await MultiRegionSetupHelpers.GetOrCreateMultiRegionDatabaseAndContainers(this.client);

            this.readRegionsMapping = this.client.DocumentClient.GlobalEndpointManager.GetAvailableReadEndpointsByLocation();
            Assert.IsTrue(this.readRegionsMapping.Count() >= 3);

            region1 = this.readRegionsMapping.Keys.ElementAt(0);
            region2 = this.readRegionsMapping.Keys.ElementAt(1);
            region3 = this.readRegionsMapping.Keys.ElementAt(2);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            try
            {
                this.container?.DeleteItemAsync<CosmosIntegrationTestObject>("deleteMe", new PartitionKey("MMWrite"));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignore
            }
            finally
            {
                //Do not delete the resources (except MM Write test object), georeplication is slow and we want to reuse the resources
                this.client?.Dispose();
                Environment.SetEnvironmentVariable(ConfigurationManager.StalePartitionUnavailabilityRefreshIntervalInSeconds, null);
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        [Timeout(70000)]
        public async Task ReadMany2UnreachablePartitionsTest()
        {
            List<FeedRange> feedRanges = (List<FeedRange>)await this.container.GetFeedRangesAsync();
            Assert.IsTrue(feedRanges.Count > 0);

            FaultInjectionCondition condition = new FaultInjectionConditionBuilder()
                .WithConnectionType(FaultInjectionConnectionType.Direct)
                .WithOperationType(FaultInjectionOperationType.QueryItem)
                .WithEndpoint(new FaultInjectionEndpointBuilder(
                    MultiRegionSetupHelpers.dbName,
                    MultiRegionSetupHelpers.containerName,
                    feedRanges[0])
                    .WithReplicaCount(2)
                    .WithIncludePrimary(false)
                    .Build())
                .Build();

            FaultInjectionServerErrorResult result = new FaultInjectionServerErrorResultBuilder(FaultInjectionServerErrorType.Gone)
                .WithTimes(int.MaxValue - 1)
                .Build();

            FaultInjectionRule rule = new FaultInjectionRuleBuilder("connectionDelay", condition, result)
                .WithDuration(TimeSpan.FromDays(1))
                .Build();

            FaultInjector injector = new FaultInjector(new List<FaultInjectionRule> { rule });

            rule.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = ConsistencyLevel.Strong,
                Serializer = this.cosmosSystemTextJsonSerializer,
                FaultInjector = injector,
            };

            CosmosClient fiClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: clientOptions);

            Database fidb = fiClient.GetDatabase(MultiRegionSetupHelpers.dbName);
            Container fic = fidb.GetContainer(MultiRegionSetupHelpers.containerName);

            IReadOnlyList<(string, PartitionKey)> items = new List<(string, PartitionKey)>()
            {
                ("testId", new PartitionKey("pk")),
                ("testId2", new PartitionKey("pk2")),
                ("testId3", new PartitionKey("pk3")),
                ("testId4", new PartitionKey("pk4")),
            };

            try
            {
                rule.Enable();
                FeedResponse<CosmosIntegrationTestObject> feedResponse = await fic.ReadManyItemsAsync<CosmosIntegrationTestObject>(items);
            }
            catch (Exception ex)
            {
                Assert.Fail(ex.ToString());
            }
            finally
            {
                rule.Disable();
                fiClient.Dispose();
            }
        }

        [TestMethod]
        [Timeout(70000)]
        [TestCategory("MultiRegion")]
        public async Task DateTimeArrayRoundtrip_BinaryEncoding_CompareExtraDates_IntegrationTest()
        {
            string binaryEncodingEnabled = "binaryEncodingEnabled" + Guid.NewGuid().ToString("N");
            string binaryEncodingDisabled = "binaryEncodingDisabled" + Guid.NewGuid().ToString("N");
            string pk = "pk";
            string testId = Guid.NewGuid().ToString();

            string[] dateStrings =
            {
                "12/25/2023","2023-12-25","12-25-2023","25.12.2023","25/12/2023",
                "Dec 25, 2023","Dec 25 2023","2023-12-25T10:00:00","2023-12-25T10:00:00.123",
                "12/25/2023 10:00 AM","12/25/2023 10:00:00 AM","12/25/2023 10:00:00.123 AM","9999-12-31T23:59:59",
                "2023-12-25T10:00:00.1","2023-12-25T10:00:00.12",
                "2023-12-25T10:00:00.1234","2023-12-25T10:00:00.1234567"
            };
            string[] formats =
            {
                "MM/dd/yyyy","yyyy-MM-dd","MM-dd-yyyy","dd.MM.yyyy","dd/MM/yyyy",
                "MMM dd, yyyy","MMM dd yyyy","yyyy-MM-ddTHH:mm:ss","yyyy-MM-ddTHH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss.f","yyyy-MM-ddTHH:mm:ss.ff","yyyy-MM-ddTHH:mm:ss.ffff",
                "yyyy-MM-ddTHH:mm:ss.fffffff","MM/dd/yyyy hh:mm tt","MM/dd/yyyy hh:mm:ss tt",
                "MM/dd/yyyy hh:mm:ss.fff tt"
            };
            DateTime[] parsedDates = dateStrings
                .Select(s => DateTime.ParseExact(s, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None))
                .ToArray();

            TestCosmosItem testItem = new TestCosmosItem(
                id: testId,
                pk: pk,
                title: "title",
                email: "test@example.com",
                body: "Binary encoding test document.",
                createdUtc: DateTime.UtcNow,
                modifiedUtc: DateTime.Parse("2025-03-26T20:22:20Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal),
                extraDates: parsedDates);

            Database db = this.database;
            ContainerResponse containerBEEnabledResponse = await db.CreateContainerAsync(binaryEncodingEnabled, "/pk");
            ContainerResponse containerBEDisabledResponse = await db.CreateContainerAsync(binaryEncodingDisabled, "/pk");

            try
            {
                // BinaryEncodingEnabled = True
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "True");
                string rawJsonBEEnabled;
                string rawJsonBEDisabled;
                using (CosmosClient clientBinaryEncodingEnabled = new CosmosClient(this.connectionString))
                {
                    Container containerBinaryEncodingEnabled = clientBinaryEncodingEnabled.GetDatabase(db.Id).GetContainer(binaryEncodingEnabled);
                    await containerBinaryEncodingEnabled.CreateItemAsync(testItem, new Microsoft.Azure.Cosmos.PartitionKey(pk));
                    using ResponseMessage response = await containerBinaryEncodingEnabled.ReadItemStreamAsync(testId, new Microsoft.Azure.Cosmos.PartitionKey(pk));
                    using StreamReader reader = new StreamReader(response.Content, Encoding.UTF8);
                    rawJsonBEEnabled = await reader.ReadToEndAsync();

                }

                // BinaryEncodingEnabled = False
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, "False");
                using (CosmosClient clientBinaryEncodingDisabled = new CosmosClient(this.connectionString))
                {
                    Container containerBinaryEncodingDisabled = clientBinaryEncodingDisabled.GetDatabase(db.Id).GetContainer(binaryEncodingDisabled);
                    await containerBinaryEncodingDisabled.CreateItemAsync(testItem, new Microsoft.Azure.Cosmos.PartitionKey(pk));
                    using ResponseMessage response = await containerBinaryEncodingDisabled.ReadItemStreamAsync(testId, new Microsoft.Azure.Cosmos.PartitionKey(pk));
                    using StreamReader reader = new StreamReader(response.Content, Encoding.UTF8);
                    rawJsonBEDisabled = await reader.ReadToEndAsync();
                }

                using JsonDocument docTrue = JsonDocument.Parse(rawJsonBEEnabled);
                using JsonDocument docFalse = JsonDocument.Parse(rawJsonBEDisabled);

                string extraDatesTrue = docTrue.RootElement.GetProperty("ExtraDates").GetRawText();
                string extraDatesFalse = docFalse.RootElement.GetProperty("ExtraDates").GetRawText();

                Assert.AreEqual(extraDatesTrue, extraDatesFalse, $"ExtraDates JSON mismatch:\nTrue: {extraDatesTrue}\nFalse: {extraDatesFalse}");
            }
            finally
            {
                await containerBEEnabledResponse.Container.DeleteContainerAsync();
                await containerBEDisabledResponse.Container.DeleteContainerAsync();
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        [DataRow(FaultInjectionServerErrorType.ServiceUnavailable)]
        [DataRow(FaultInjectionServerErrorType.InternalServerError)]
        [DataRow(FaultInjectionServerErrorType.DatabaseAccountNotFound)]
        [DataRow(FaultInjectionServerErrorType.LeaseNotFound)]
        public async Task MetadataEndpointUnavailableCrossRegionalRetryTest(FaultInjectionServerErrorType serverErrorType)
        {
            FaultInjectionRule collReadBad = new FaultInjectionRuleBuilder(
                id: "collread",
                condition: new FaultInjectionConditionBuilder()
                    .WithOperationType(FaultInjectionOperationType.MetadataContainer)
                    .WithRegion(region1)
                    .Build(),
                result: new FaultInjectionServerErrorResultBuilder(serverErrorType)
                    .Build())
                .Build();

            FaultInjectionRule pkRangeBad = new FaultInjectionRuleBuilder(
                id: "pkrange",
                condition: new FaultInjectionConditionBuilder()
                    .WithOperationType(FaultInjectionOperationType.MetadataPartitionKeyRange)
                    .WithRegion(region1)
                    .Build(),
                result: new FaultInjectionServerErrorResultBuilder(serverErrorType)
                    .Build())
                .Build();

            collReadBad.Disable();
            pkRangeBad.Disable();

            FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { pkRangeBad, collReadBad });

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                ConnectionMode = ConnectionMode.Direct,
                Serializer = this.cosmosSystemTextJsonSerializer,
                FaultInjector = faultInjector,
                ApplicationPreferredRegions = new List<string> { region1, region2,  region3 }
            };

            using (CosmosClient fiClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: cosmosClientOptions))
            {
                Database fidb = fiClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container fic = fidb.GetContainer(MultiRegionSetupHelpers.containerName);

                pkRangeBad.Enable();
                collReadBad.Enable();

                try
                {
                    FeedIterator<CosmosIntegrationTestObject> frTest = fic.GetItemQueryIterator<CosmosIntegrationTestObject>("SELECT * FROM c");
                    while (frTest.HasMoreResults)
                    {
                        FeedResponse<CosmosIntegrationTestObject> feedres = await frTest.ReadNextAsync();

                        Assert.AreEqual(HttpStatusCode.OK, feedres.StatusCode);
                    }
                }
                catch (CosmosException ex)
                {
                    Assert.Fail(ex.Message);
                }
                finally
                {
                    //Cross regional retry needs to ocur (could trigger for other metadata call to try on secondary region so rule would not trigger)
                    Assert.IsTrue(pkRangeBad.GetHitCount() + collReadBad.GetHitCount() >= 1);

                    pkRangeBad.Disable();
                    collReadBad.Disable();

                    fiClient.Dispose();
                }
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        public async Task AddressRefreshTimeoutTest()
        {
            FaultInjectionRule gatewayRule = new FaultInjectionRuleBuilder(
                id: "gatewayRule",
                condition: new FaultInjectionConditionBuilder()
                    .WithOperationType(FaultInjectionOperationType.MetadataRefreshAddresses)
                    .WithRegion(region1)
                    .Build(),
                result: new FaultInjectionServerErrorResultBuilder(FaultInjectionServerErrorType.SendDelay)
                    .WithDelay(TimeSpan.FromSeconds(65))
                    .Build())
                .Build();

            gatewayRule.Disable();

            FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { gatewayRule });

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                ConnectionMode = ConnectionMode.Direct,
                Serializer = this.cosmosSystemTextJsonSerializer,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(1),

            };

            using (CosmosClient fiClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: cosmosClientOptions))
            {
                Database fidb = fiClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container fic = fidb.GetContainer(MultiRegionSetupHelpers.containerName);

                gatewayRule.Enable();

                try
                {
                    ItemResponse<CosmosIntegrationTestObject> o = await fic.ReadItemAsync<CosmosIntegrationTestObject>(
                        "testId", 
                        new PartitionKey("pk"));
                    Assert.IsTrue(o.StatusCode == HttpStatusCode.OK);
                }
                catch (Exception ex)
                {
                    Assert.Fail(ex.ToString());
                }
                finally
                {
                    gatewayRule.Disable();
                    Assert.IsTrue(gatewayRule.GetHitCount() >= 3);

                    fiClient.Dispose();
                }
            }
        }
          
        [Owner("dkunda")]
        [TestCategory("MultiRegion")]
        [DataRow(true, DisplayName = "Test scenario when binary encoding is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when binary encoding is disabled at client level.")]
        public async Task ExecuteTransactionalBatch_WhenBinaryEncodingEnabled_ShouldCompleteSuccessfully(
            bool isBinaryEncodingEnabled)
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, isBinaryEncodingEnabled.ToString());

            Random random = new();
            CosmosIntegrationTestObject testItem = new()
            {
                Id = $"smTestId{random.Next()}",
                Pk = $"smpk{random.Next()}",
            };

            try
            {
                CosmosClientOptions cosmosClientOptions = new()
                {
                    ConsistencyLevel = ConsistencyLevel.Session,
                    RequestTimeout = TimeSpan.FromSeconds(10),
                    Serializer = new CosmosJsonDotNetSerializer(
                        cosmosSerializerOptions: new CosmosSerializationOptions()
                        {
                            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                        },
                        binaryEncodingEnabled: isBinaryEncodingEnabled)
                };

                using CosmosClient cosmosClient = new(
                    connectionString: this.connectionString,
                    clientOptions: cosmosClientOptions);

                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Create a transactional batch
                TransactionalBatch transactionalBatch = container.CreateTransactionalBatch(new PartitionKey(testItem.Pk));

                transactionalBatch.CreateItem(
                    testItem,
                    new TransactionalBatchItemRequestOptions
                    {
                        EnableContentResponseOnWrite = true,
                    });

                transactionalBatch.ReadItem(
                    testItem.Id,
                    new TransactionalBatchItemRequestOptions
                    {
                        EnableContentResponseOnWrite = true,
                    });

                // Execute the transactional batch
                TransactionalBatchResponse transactionResponse = await transactionalBatch.ExecuteAsync(
                    new TransactionalBatchRequestOptions
                    {
                    });

                Assert.AreEqual(HttpStatusCode.OK, transactionResponse.StatusCode);
                Assert.AreEqual(2, transactionResponse.Count);

                TransactionalBatchOperationResult<CosmosIntegrationTestObject> createOperationResult = transactionResponse.GetOperationResultAtIndex<CosmosIntegrationTestObject>(0);

                Assert.IsNotNull(createOperationResult);
                Assert.IsNotNull(createOperationResult.Resource);
                Assert.AreEqual(testItem.Id, createOperationResult.Resource.Id);
                Assert.AreEqual(testItem.Pk, createOperationResult.Resource.Pk);

                TransactionalBatchOperationResult<CosmosIntegrationTestObject> readOperationResult = transactionResponse.GetOperationResultAtIndex<CosmosIntegrationTestObject>(1);

                Assert.IsNotNull(readOperationResult);
                Assert.IsNotNull(readOperationResult.Resource);
                Assert.AreEqual(testItem.Id, readOperationResult.Resource.Id);
                Assert.AreEqual(testItem.Pk, readOperationResult.Resource.Pk);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.BinaryEncodingEnabled, null);

                await this.container.DeleteItemAsync<CosmosIntegrationTestObject>(
                    testItem.Id,
                    new PartitionKey(testItem.Pk));
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        [DataRow(ConnectionMode.Direct, "15", "10", DisplayName = "Direct Mode - Scenario when the total iteration count is 15 and circuit breaker consecutive failure threshold is set to 10.")]
        [DataRow(ConnectionMode.Direct, "25", "20", DisplayName = "Direct Mode - Scenario when the total iteration count is 25 and circuit breaker consecutive failure threshold is set to 20.")]
        [DataRow(ConnectionMode.Direct, "35", "30", DisplayName = "Direct Mode - Scenario when the total iteration count is 35 and circuit breaker consecutive failure threshold is set to 30.")]
        [DataRow(ConnectionMode.Gateway, "15", "10", DisplayName = "Gateway Mode - Scenario when the total iteration count is 15 and circuit breaker consecutive failure threshold is set to 10.")]
        [DataRow(ConnectionMode.Gateway, "25", "20", DisplayName = "Gateway Mode - Scenario when the total iteration count is 25 and circuit breaker consecutive failure threshold is set to 20.")]
        [DataRow(ConnectionMode.Gateway, "35", "30", DisplayName = "Gateway Mode - Scenario when the total iteration count is 35 and circuit breaker consecutive failure threshold is set to 30.")]
        [Owner("dkunda")]
        [Timeout(70000)]
        public async Task ReadItemAsync_WithCircuitBreakerEnabledAndSingleMasterAccountAndServiceUnavailableReceived_ShouldApplyPartitionLevelOverride(
            ConnectionMode connectionMode,
            string iterationCount,
            string circuitBreakerConsecutiveFailureCount)
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, circuitBreakerConsecutiveFailureCount);

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = connectionMode,
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
            };

            List<CosmosIntegrationTestObject> itemsList = new ()
            {
                new() { Id = Guid.NewGuid().ToString(), Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(3000);

                int consecutiveFailureCount = int.Parse(circuitBreakerConsecutiveFailureCount);
                int totalIterations = int.Parse(iterationCount);

                for (int attemptCount = 1; attemptCount <= totalIterations; attemptCount++)
                {
                    try
                    {
                        ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                            id: itemsList[0].Id,
                            partitionKey: new PartitionKey(itemsList[0].Pk));

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                        Assert.AreEqual(
                            expected: HttpStatusCode.OK,
                            actual: readResponse.StatusCode);

                        Assert.IsNotNull(contactedRegions);

                        PartitionKeyRangeFailoverInfo failoverInfo = TestCommon.GetFailoverInfoForFirstPartitionUsingReflection(
                            globalPartitionEndpointManager: cosmosClient.ClientContext.DocumentClient.PartitionKeyRangeLocation,
                            isReadOnlyOrMultiMaster: true);

                        if (attemptCount > consecutiveFailureCount + 1)
                        {
                            if (connectionMode == ConnectionMode.Direct)
                            {
                                Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the consecutive failure count reaches the threshold, the partition was failed over to the next region, and the subsequent read request/s were successful on the next region.");
                                Assert.IsTrue(contactedRegions.Contains(region2));
                            }

                            Assert.AreEqual(this.readRegionsMapping[region2], failoverInfo.Current);
                        }
                        else
                        {
                            if (connectionMode == ConnectionMode.Direct)
                            {
                                Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the read request succeeds before the consecutive failure count reaches the threshold, the partition didn't over to the next region, and the request was retried on the next region.");
                                Assert.IsTrue(contactedRegions.Contains(region1) && contactedRegions.Contains(region2));
                            }

                            if (attemptCount > consecutiveFailureCount)
                            {
                                Assert.AreEqual(this.readRegionsMapping[region2], failoverInfo.Current);
                            }
                            else
                            {
                                Assert.AreEqual(this.readRegionsMapping[region1], failoverInfo.Current);
                            }
                        }
                    }
                    catch (CosmosException)
                    {
                        Assert.Fail("Read Item operation should succeed.");
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during ReadItemAsync call. Message: {ex.Message}");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, null);

                await this.TryDeleteItems(itemsList);
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        [DataRow(ConnectionMode.Direct, DisplayName ="Direct Mode")]
        [DataRow(ConnectionMode.Gateway, DisplayName = "Gateway Mode")]
        [Owner("nalutripician")]
        [Timeout(70000)]
        public async Task ReadItemAsync_WithCircuitBreakerEnabledAndTimeoutCounterOverwritten(
            ConnectionMode connectionMode)
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerTimeoutCounterResetWindowInMinutes, "0.0833"); // setting to 5 seconds

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = connectionMode,
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
            };

            List<CosmosIntegrationTestObject> itemsList = new()
            {
                new() { Id = Guid.NewGuid().ToString(), Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(3000);

                int readErrorCount = 0;
                PartitionKeyRangeFailoverInfo failoverInfo;

                for (int i = 1; i <= 3; i++)
                {
                    try
                    {
                        ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                            id: itemsList[0].Id,
                            partitionKey: new PartitionKey(itemsList[0].Pk));

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                        Assert.AreEqual(
                            expected: HttpStatusCode.OK,
                            actual: readResponse.StatusCode);

                        Assert.IsNotNull(contactedRegions);

                        failoverInfo = TestCommon.GetFailoverInfoForFirstPartitionUsingReflection(
                            globalPartitionEndpointManager: cosmosClient.ClientContext.DocumentClient.PartitionKeyRangeLocation,
                            isReadOnlyOrMultiMaster: true);

                        failoverInfo.SnapshotConsecutiveRequestFailureCount(out readErrorCount, out _);

                        Assert.IsTrue(readErrorCount > 0);
                    }
                    catch (CosmosException)
                    {
                        Assert.Fail("Read Item operation should succeed.");
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during ReadItemAsync call. Message: {ex.Message}");
                    }
                }

                await Task.Delay(6000); // Wait for the timeout counter to reset

                try
                {
                    ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                            id: itemsList[0].Id,
                            partitionKey: new PartitionKey(itemsList[0].Pk));
                }
                catch (CosmosException)
                {
                    Assert.Fail("Read Item operation should succeed after the timeout counter is overwritten.");
                }

                failoverInfo = TestCommon.GetFailoverInfoForFirstPartitionUsingReflection(
                            globalPartitionEndpointManager: cosmosClient.ClientContext.DocumentClient.PartitionKeyRangeLocation,
                            isReadOnlyOrMultiMaster: true);

                failoverInfo.SnapshotConsecutiveRequestFailureCount(out int currentReadErrorCount, out _);

                Assert.AreEqual(1, currentReadErrorCount, "The read error count should be reset after the timeout counter is overwritten. Then after one more failure it should be incremented by 1.");
                Assert.IsTrue(readErrorCount > currentReadErrorCount, "The read error count should be greater than the current before the timeout counter is overwritten.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerTimeoutCounterResetWindowInMinutes, null);
                await this.TryDeleteItems(itemsList);
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        [Owner("dkunda")]
        [Timeout(70000)]
        public async Task ReadItemAsync_WithCircuitBreakerEnabledAndSingleMasterAccountAndServiceUnavailableReceivedFromTwoRegions_ShouldApplyPartitionLevelOverrideToThridRegion()
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, "10");

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId1 = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule1 = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId1,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            string serviceUnavailableRuleId2 = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule2 = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId2,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(region2)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            serviceUnavailableRule1.Disable();
            serviceUnavailableRule2.Disable();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule1, serviceUnavailableRule2 };
            FaultInjector faultInjector = new FaultInjector(rules);

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
            };

            List<CosmosIntegrationTestObject> itemsList = new()
            {
                new() { Id = "smTestId1", Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new (connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(5000);

                bool isRegion1Available = true;
                bool isRegion2Available = true;

                int thresholdCounter = 0;
                int totalIterations = 40;
                int ppcbDefaultThreshold = 10;
                int firstRegionServiceUnavailableAttempt = 3;
                int secondRegionServiceUnavailableAttempt = 28;

                for (int attemptCount = 1; attemptCount <= totalIterations; attemptCount++)
                {
                    try
                    {
                        ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                            id: itemsList[0].Id,
                            partitionKey: new PartitionKey(itemsList[0].Pk));

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                        Assert.AreEqual(
                            expected: HttpStatusCode.OK,
                            actual: readResponse.StatusCode);

                        Assert.IsNotNull(contactedRegions);

                        if (isRegion1Available && isRegion2Available)
                        {
                            Assert.IsTrue(contactedRegions.Count == 1, "Assert that, when no failure happened, the read request is being served from region 1.");
                            Assert.IsTrue(contactedRegions.Contains(region1));

                            // Simulating service unavailable on region 1.
                            if (attemptCount == firstRegionServiceUnavailableAttempt)
                            {
                                isRegion1Available = false;
                                serviceUnavailableRule1.Enable();
                            }
                        }
                        else if (isRegion2Available) 
                        {
                            if (thresholdCounter <= ppcbDefaultThreshold)
                            {
                                Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the read request succeeds before the consecutive failure count reaches the threshold, the partition didn't fail over to the next region, and the request was retried.");
                                Assert.IsTrue(contactedRegions.Contains(region1) && contactedRegions.Contains(region2));
                                thresholdCounter++;
                            }
                            else
                            {
                                Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the consecutive failure count reaches the threshold, the partition was failed over to the next region, and the subsequent read request/s were successful on the next region.");
                                Assert.IsTrue(contactedRegions.Contains(region2));
                            }

                            // Simulating service unavailable on region 2.
                            if (attemptCount == secondRegionServiceUnavailableAttempt)
                            {
                                isRegion2Available = false;
                                serviceUnavailableRule2.Enable();
                            }
                        }
                        else
                        {
                            if (thresholdCounter <= ppcbDefaultThreshold + 1)
                            {
                                Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the read request fails on the second region, the partition did over to the next region, and the request was retried on the next region.");
                                Assert.IsTrue(contactedRegions.Contains(region2) && contactedRegions.Contains(region3));
                                thresholdCounter++;
                            }
                            else
                            {
                                Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the consecutive failure count reaches the threshold, the partition was failed over to the third region, and the subsequent read request/s were successful on the third region.");
                                Assert.IsTrue(contactedRegions.Contains(region3));
                            }
                        }
                    }
                    catch (CosmosException)
                    {
                        Assert.Fail("Read Item operation should succeed.");
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during ReadItemAsync call. Message: {ex.Message}");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, null);

                await this.TryDeleteItems(itemsList);
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        [Owner("dkunda")]
        [Timeout(70000)]
        public async Task ReadItemAsync_WithNoPreferredRegionsAndCircuitBreakerEnabledAndSingleMasterAccountAndServiceUnavailableReceived_ShouldApplyPartitionLevelOverride()
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, "10");

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
            };

            List<CosmosIntegrationTestObject> itemsList = new()
            {
                new() { Id = Guid.NewGuid().ToString(), Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(3000);

                int consecutiveFailureCount = 10;
                int totalIterations = 15;

                for (int attemptCount = 1; attemptCount <= totalIterations; attemptCount++)
                {
                    try
                    {
                        ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                            id: itemsList[0].Id,
                            partitionKey: new PartitionKey(itemsList[0].Pk));

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                        Assert.AreEqual(
                            expected: HttpStatusCode.OK,
                            actual: readResponse.StatusCode);

                        Assert.IsNotNull(contactedRegions);

                        PartitionKeyRangeFailoverInfo failoverInfo = TestCommon.GetFailoverInfoForFirstPartitionUsingReflection(
                            globalPartitionEndpointManager: cosmosClient.ClientContext.DocumentClient.PartitionKeyRangeLocation,
                            isReadOnlyOrMultiMaster: true);

                        if (attemptCount > consecutiveFailureCount + 1)
                        {
                            Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the consecutive failure count reaches the threshold, the partition was failed over to the next region, and the subsequent read request/s were successful on the next region.");
                            Assert.IsTrue(contactedRegions.Contains(region2));
                            Assert.AreEqual(this.readRegionsMapping[region2], failoverInfo.Current);
                        }
                        else
                        {
                            Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the read request succeeds before the consecutive failure count reaches the threshold, the partition didn't over to the next region, and the request was retried on the next region.");
                            Assert.IsTrue(contactedRegions.Contains(region1) && contactedRegions.Contains(region2));

                            if (attemptCount > consecutiveFailureCount)
                            {
                                Assert.AreEqual(this.readRegionsMapping[region2], failoverInfo.Current);
                            }
                            else
                            {
                                Assert.AreEqual(this.readRegionsMapping[region1], failoverInfo.Current);
                            }
                        }
                    }
                    catch (CosmosException)
                    {
                        Assert.Fail("Read Item operation should succeed.");
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during ReadItemAsync call. Message: {ex.Message}");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, null);

                await this.TryDeleteItems(itemsList);
            }
        }

        [TestMethod]
        [Owner("dkunda")]
        [TestCategory("MultiRegion")]
        [Timeout(70000)]
        public async Task ReadItemAsync_WithCircuitBreakerDisabledAndSingleMasterAccountAndServiceUnavailableReceived_ShouldNotApplyPartitionLevelOverride()
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "False");

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
            };

            List<CosmosIntegrationTestObject> itemsList = new()
            {
                new() { Id = "smTestId1", Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(5000);

                int consecutiveFailureCount = 10;
                for (int attemptCount = 1; attemptCount <= consecutiveFailureCount; attemptCount++)
                {
                    try
                    {
                        ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                            id: itemsList[0].Id,
                            partitionKey: new PartitionKey(itemsList[0].Pk));

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                        Assert.AreEqual(
                            expected: HttpStatusCode.OK,
                            actual: readResponse.StatusCode);

                        Assert.IsNotNull(contactedRegions);
                        Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the read request succeeds after failover, the partition was failed over to the next region, after the failures reaches the threshold.");
                        Assert.IsTrue(contactedRegions.Contains(region1) && contactedRegions.Contains(region2));
                    }
                    catch (CosmosException)
                    {
                        Assert.Fail("Read Item operation should succeed.");
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during ReadItemAsync call. Message: {ex.Message}");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);
                await this.TryDeleteItems(itemsList);
            }
        }

        [TestMethod]
        [Owner("dkunda")]
        [TestCategory("MultiRegion")]
        [Timeout(70000)]
        public async Task CreateItemAsync_WithCircuitBreakerEnabledAndSingleMasterAccountAndServiceUnavailableReceived_ShouldNotApplyPartitionLevelOverride()
        {
            // Arrange.
            int circuitBreakerConsecutiveFailureCount = 10;
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, $"{circuitBreakerConsecutiveFailureCount}");

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
            };

            using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);

            Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
            Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

            try
            {
                // Act and Assert.
                for (int attemptCount = 1; attemptCount <= circuitBreakerConsecutiveFailureCount; attemptCount++)
                {
                    try
                    {
                        CosmosIntegrationTestObject testItem = new() { Id = "testId5", Pk = "pk5" };
                        ItemResponse<CosmosIntegrationTestObject> createResponse = await container.CreateItemAsync<CosmosIntegrationTestObject>(testItem);
                        Assert.Fail("Create Item operation should not succeed.");
                    }
                    catch (CosmosException ex)
                    {
                        Assert.AreEqual(
                            expected: HttpStatusCode.ServiceUnavailable,
                            actual: ex.StatusCode);

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = ex.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                        Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when a 503 Service Unavailable happens, the partition was not failed over to the next region, since writes are not supported in a single master account, when circuit breaker is enabled.");
                        Assert.IsTrue(contactedRegions.Contains(region1));
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during ReadItemAsync call. Message: {ex.Message}");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, null);
            }
        }

        [TestMethod]
        [Owner("dkunda")]
        [TestCategory("MultiMaster")]
        [DataRow(ConnectionMode.Direct, "15", "10", DisplayName = "Direct Mode - Scenario whtn the total iteration count is 15 and circuit breaker consecutive failure threshold is set to 10.")]
        [DataRow(ConnectionMode.Direct, "25", "20", DisplayName = "Direct Mode - Scenario whtn the total iteration count is 25 and circuit breaker consecutive failure threshold is set to 20.")]
        [DataRow(ConnectionMode.Direct, "35", "30", DisplayName = "Direct Mode - Scenario whtn the total iteration count is 35 and circuit breaker consecutive failure threshold is set to 30.")]
        [DataRow(ConnectionMode.Gateway, "15", "10", DisplayName = "Gateway Mode - Scenario when the total iteration count is 15 and circuit breaker consecutive failure threshold is set to 10.")]
        [DataRow(ConnectionMode.Gateway, "25", "20", DisplayName = "Gateway Mode - Scenario when the total iteration count is 25 and circuit breaker consecutive failure threshold is set to 20.")]
        [DataRow(ConnectionMode.Gateway, "35", "30", DisplayName = "Gateway Mode - Scenario when the total iteration count is 35 and circuit breaker consecutive failure threshold is set to 30.")]
        [Timeout(70000)]
        public async Task CreateItemAsync_WithCircuitBreakerEnabledAndMultiMasterAccountAndServiceUnavailableReceived_ShouldApplyPartitionLevelOverride(
            ConnectionMode connectionMode,
            string iterationCount,
            string circuitBreakerConsecutiveFailureCount)
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForWrites, circuitBreakerConsecutiveFailureCount);

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            Random random = new ();
            List<CosmosIntegrationTestObject> itemsCleanupList = new();
            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
                Serializer = this.cosmosSystemTextJsonSerializer,
                ConnectionMode = connectionMode,
            };

            try
            {
                // Act and Assert.
                int totalIterations = int.Parse(iterationCount);
                int consecutiveFailureCount = int.Parse(circuitBreakerConsecutiveFailureCount);

                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                for (int attemptCount = 1; attemptCount <= totalIterations; attemptCount++)
                {
                    try
                    {
                        CosmosIntegrationTestObject testItem = new()
                        {
                            Id = $"mmTestId{random.Next()}",
                            Pk = $"mmpk{random.Next()}"
                        };

                        ItemResponse<CosmosIntegrationTestObject> createResponse = await container.CreateItemAsync<CosmosIntegrationTestObject>(testItem);
                        itemsCleanupList.Add(testItem);

                        Assert.AreEqual(
                            expected: HttpStatusCode.Created,
                            actual: createResponse.StatusCode);

                        PartitionKeyRangeFailoverInfo failoverInfo = TestCommon.GetFailoverInfoForFirstPartitionUsingReflection(
                            globalPartitionEndpointManager: cosmosClient.ClientContext.DocumentClient.PartitionKeyRangeLocation,
                            isReadOnlyOrMultiMaster: true);

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = createResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));
                        Assert.IsNotNull(contactedRegions);

                        if (attemptCount > consecutiveFailureCount + 1)
                        {
                            if (connectionMode == ConnectionMode.Direct)
                            {
                                Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the consecutive failure count reaches the threshold, the partition was failed over to the next region, and the subsequent write request/s were successful on the next region.");
                                Assert.IsTrue(contactedRegions.Contains(region2));
                            }

                            Assert.AreEqual(this.readRegionsMapping[region2], failoverInfo.Current);
                        }
                        else
                        {
                            if (connectionMode == ConnectionMode.Direct)
                            {
                                Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the write requests succeeds before the consecutive failure count reaches the threshold, the partition didn't over to the next region, and the request was retried on the next region.");
                                Assert.IsTrue(contactedRegions.Contains(region1) && contactedRegions.Contains(region2));
                            }
                            if (attemptCount > consecutiveFailureCount)
                            {
                                Assert.AreEqual(this.readRegionsMapping[region2], failoverInfo.Current);
                            }
                            else
                            {
                                Assert.AreEqual(this.readRegionsMapping[region1], failoverInfo.Current);
                            }
                        }
                    }
                    catch (CosmosException)
                    {
                        Assert.Fail("Create Item operation should succeed.");
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during CreateItemAsync call. Message: {ex.Message}");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForWrites, null);

                foreach (CosmosIntegrationTestObject item in itemsCleanupList)
                {
                    await this.container.DeleteItemAsync<CosmosIntegrationTestObject>(item.Id, new PartitionKey(item.Pk));
                }
            }
        }

        [TestMethod]
        [Owner("dkunda")]
        [TestCategory("MultiMaster")]
        [Timeout(70000)]
        public async Task CreateAndReadItemAsync_WithCircuitBreakerEnabledAndMultiMasterAccountAndDefaultThresholdServiceUnavailableReceived_ShouldApplyPartitionLevelOverride()
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId1 = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule1 = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId1,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithRegion(region1)
                       .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            string serviceUnavailableRuleId2 = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule2 = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId2,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(region1)
                       .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule1, serviceUnavailableRule2 };
            FaultInjector faultInjector = new FaultInjector(rules);

            List<CosmosIntegrationTestObject> itemsCleanupList = new();
            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
                Serializer = this.cosmosSystemTextJsonSerializer
            };

            try
            {
                // Act and Assert.
                Random random = new ();
                int totalIterations = 20;
                int consecutiveFailureCountForReads = 10;
                int consecutiveFailureCountForWrites = 5;

                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                for (int attemptCount = 1; attemptCount <= totalIterations; attemptCount++)
                {
                    try
                    {
                        CosmosIntegrationTestObject testItem = new()
                        {
                            Id = $"mmTestId{random.Next()}",
                            Pk = $"mmpk{random.Next()}"
                        };

                        ItemResponse<CosmosIntegrationTestObject> createResponse = await container.CreateItemAsync<CosmosIntegrationTestObject>(testItem);
                        itemsCleanupList.Add(testItem);

                        Assert.AreEqual(
                            expected: HttpStatusCode.Created,
                            actual: createResponse.StatusCode);

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMappingForWrites = createResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegionsForWrite = new(contactedRegionMappingForWrites.Select(r => r.regionName));
                        Assert.IsNotNull(contactedRegionsForWrite);

                        if (attemptCount > consecutiveFailureCountForWrites + 1)
                        {
                            Assert.IsTrue(contactedRegionsForWrite.Count == 1, "Asserting that when the consecutive failure count reaches the write threshold, the partition was failed over to the next region, and the subsequent write request/s were successful on the next region.");
                            Assert.IsTrue(contactedRegionsForWrite.Contains(region2));
                        }
                        else
                        {
                            Assert.IsTrue(contactedRegionsForWrite.Count == 2, "Asserting that when the write requests succeeds before the consecutive failure count reaches the write threshold, the partition didn't over to the next region, and the request was retried on the next region.");
                            Assert.IsTrue(contactedRegionsForWrite.Contains(region1) && contactedRegionsForWrite.Contains(region2));
                        }

                        ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                            id: testItem.Id,
                            partitionKey: new PartitionKey(testItem.Pk));

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMappingForReads = readResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegionsForReads = new(contactedRegionMappingForReads.Select(r => r.regionName));

                        Assert.AreEqual(
                            expected: HttpStatusCode.OK,
                            actual: readResponse.StatusCode);

                        Assert.IsNotNull(contactedRegionsForReads);

                        if (attemptCount > consecutiveFailureCountForReads + 1)
                        {
                            Assert.IsTrue(contactedRegionsForReads.Count == 1, "Asserting that when the consecutive failure count reaches the read threshold, the partition was failed over to the next region, and the subsequent read request/s were successful on the next region.");
                            Assert.IsTrue(contactedRegionsForReads.Contains(region2));
                        }
                        else
                        {
                            Assert.IsTrue(contactedRegionsForReads.Count == 2, "Asserting that when the read request succeeds before the consecutive failure count reaches the read threshold, the partition didn't over to the next region, and the request was retried on the next region.");
                            Assert.IsTrue(contactedRegionsForReads.Contains(region1) && contactedRegionsForReads.Contains(region2));
                        }
                    }
                    catch (CosmosException ex)
                    {
                        Assert.Fail($"Create and Read Item operations should succeed. Message: { ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during CreateItemAsync call. Message: {ex.Message}");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);

                foreach (CosmosIntegrationTestObject item in itemsCleanupList)
                {
                    await this.container.DeleteItemAsync<CosmosIntegrationTestObject>(item.Id, new PartitionKey(item.Pk));
                }
            }
        }

        [TestMethod]
        [Owner("dkunda")]
        [TestCategory("MultiRegion")]
        [Timeout(70000)]
        [DataRow(true, DisplayName = "Test scenario when PPAF is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when PPAF is disabled at client level.")]
        public async Task ReadItemAsync_WithPPAFEnabledAndSingleMasterAccountWithResponseDelay_ShouldHedgeRequestToMultipleRegions(
            bool enablePartitionLevelFailover)
        {
            // Arrange.
            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(3000))
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            // Now that the ppaf enablement flag is returned from gateway, we need to intercept the response and remove the flag from the response, so that
            // the environment variable set above is honored.
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper()
            {
                ResponseIntercepter = async (response, request) =>
                {
                    string json = await response?.Content?.ReadAsStringAsync();
                    if (json.Length > 0 && json.Contains("enablePerPartitionFailoverBehavior"))
                    {
                        JObject parsedDatabaseAccountResponse = JObject.Parse(json);
                        parsedDatabaseAccountResponse.Property("enablePerPartitionFailoverBehavior").Value = enablePartitionLevelFailover.ToString();

                        HttpResponseMessage interceptedResponse = new()
                        {
                            StatusCode = response.StatusCode,
                            Content = new StringContent(parsedDatabaseAccountResponse.ToString()),
                            Version = response.Version,
                            ReasonPhrase = response.ReasonPhrase,
                            RequestMessage = response.RequestMessage,
                        };

                        return interceptedResponse;
                    }

                    return response;
                },
            };

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
            };

            List<CosmosIntegrationTestObject> itemsList = new()
            {
                new() { Id = "smTestId1", Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(3000);

                ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                    id: itemsList[0].Id,
                    partitionKey: new PartitionKey(itemsList[0].Pk));

                IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                Assert.AreEqual(
                    expected: HttpStatusCode.OK,
                    actual: readResponse.StatusCode);

                CosmosTraceDiagnostics traceDiagnostic = readResponse.Diagnostics as CosmosTraceDiagnostics;
                Assert.IsNotNull(traceDiagnostic);

                traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContext);

                if (enablePartitionLevelFailover)
                {
                    Assert.IsNotNull(hedgeContext);
                    List<string> hedgedRegions = ((IEnumerable<string>)hedgeContext).ToList();

                    Assert.IsTrue(hedgedRegions.Count > 1, "Since the first region is not available, the request should atleast hedge to the next region.");
                    Assert.IsTrue(hedgedRegions.Contains(region1) && (hedgedRegions.Contains(region2) || hedgedRegions.Contains(region3)));
                }
                else
                {
                    Assert.IsNull(hedgeContext);
                }

                Assert.IsNotNull(contactedRegions);
                Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the read request succeeds on any region, given that there were no availability loss.");
            }
            finally
            {
                await this.TryDeleteItems(itemsList);
            }
        }

        [TestMethod]
        [Owner("ntripician")]
        [TestCategory("MultiRegion")]
        [Timeout(70000 *100)]
        [DataRow(ConnectionMode.Direct, false, DisplayName = "Test dynamic PPAF enablement with Direct mode.")]
        public async Task ReadItemAsync_WithPPAFDynamicOverride_ShouldEnableOrDisablePPAFInSDK(
            ConnectionMode connectionMode,
            bool isThinClientEnabled)
        {
            // Arrange.
            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            bool enablePPAF = false;

            // Now that the ppaf enablement flag is returned from gateway, we need to intercept the response and remove the flag from the response, so that
            // the environment variable set above is honored.
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper()
            {
                ResponseIntercepter = async (response, request) =>
                {
                    string json = await response?.Content?.ReadAsStringAsync();
                    if (json.Length > 0 && json.Contains("enablePerPartitionFailoverBehavior"))
                    {
                        if (enablePPAF)
                        {
                            JObject parsedDatabaseAccountResponse = JObject.Parse(json);
                            parsedDatabaseAccountResponse.Property("enablePerPartitionFailoverBehavior").Value = true;

                            HttpResponseMessage interceptedResponse = new()
                            {
                                StatusCode = response.StatusCode,
                                Content = new StringContent(parsedDatabaseAccountResponse.ToString()),
                                Version = response.Version,
                                ReasonPhrase = response.ReasonPhrase,
                                RequestMessage = response.RequestMessage,
                            };

                            return interceptedResponse;
                        }
                        else
                        {
                            JObject parsedDatabaseAccountResponse = JObject.Parse(json);
                            parsedDatabaseAccountResponse.Property("enablePerPartitionFailoverBehavior").Value = false;

                            HttpResponseMessage interceptedResponse = new()
                            {
                                StatusCode = response.StatusCode,
                                Content = new StringContent(parsedDatabaseAccountResponse.ToString()),
                                Version = response.Version,
                                ReasonPhrase = response.ReasonPhrase,
                                RequestMessage = response.RequestMessage,
                            };

                            return interceptedResponse;
                        }
                        
                    }

                    return response;
                },
            };

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
                ConnectionMode = connectionMode,
                ApplicationName = "ppafDynamicOverrideTest",
            };

            List<CosmosIntegrationTestObject> itemsList = new()
            {
                new() { Id = "smTestId1", Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(3000);

                ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                    id: itemsList[0].Id,
                    partitionKey: new PartitionKey(itemsList[0].Pk));

                IReadOnlyList<(string regionName, Uri uri)>  contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                Assert.AreEqual(
                    expected: HttpStatusCode.OK,
                    actual: readResponse.StatusCode);

                CosmosTraceDiagnostics traceDiagnostic = readResponse.Diagnostics as CosmosTraceDiagnostics;
                Assert.IsNotNull(traceDiagnostic);

                traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContextNoPPAF);

                Assert.IsNull(hedgeContextNoPPAF);
                Assert.IsNull(cosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy);
                Assert.IsFalse(cosmosClient.DocumentClient.PartitionKeyRangeLocation.IsPartitionLevelAutomaticFailoverEnabled());

                // Enable PPAF At the Gateway Layer.
                enablePPAF = true;

                //force database account refresh
                await cosmosClient.DocumentClient.GlobalEndpointManager.RefreshLocationAsync(true);

                readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                    id: itemsList[0].Id,
                    partitionKey: new PartitionKey(itemsList[0].Pk));

                contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                Assert.AreEqual(
                    expected: HttpStatusCode.OK,
                    actual: readResponse.StatusCode);

                traceDiagnostic = readResponse.Diagnostics as CosmosTraceDiagnostics;
                Assert.IsNotNull(traceDiagnostic);

                traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContext);

                // When PPAF is enabled, the primary request handles failover internally
                // (retrying to another region at the transport layer). No cross-region
                // hedging occurs, so HedgeContext should be absent.
                Assert.IsNull(hedgeContext);
                Assert.IsTrue(cosmosClient.DocumentClient.PartitionKeyRangeLocation.IsPartitionLevelAutomaticFailoverEnabled());

                // Disable PPAF At the Gateway Layer.
                enablePPAF = false;

                //force database account refresh
                await cosmosClient.DocumentClient.GlobalEndpointManager.RefreshLocationAsync(true);

                readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                    id: itemsList[0].Id,
                    partitionKey: new PartitionKey(itemsList[0].Pk));

                contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                Assert.AreEqual(
                    expected: HttpStatusCode.OK,
                    actual: readResponse.StatusCode);

                traceDiagnostic = readResponse.Diagnostics as CosmosTraceDiagnostics;
                Assert.IsNotNull(traceDiagnostic);

                traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContextNoPPAF2);

                Assert.IsNull(hedgeContextNoPPAF2);
                Assert.IsNull(cosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy);
                Assert.IsFalse(cosmosClient.DocumentClient.PartitionKeyRangeLocation.IsPartitionLevelAutomaticFailoverEnabled());
            }
            finally
            {
                await this.TryDeleteItems(itemsList);

                if (isThinClientEnabled)
                {
                    Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, null);
                }
            }
        }

        [TestMethod]
        [Owner("nalutripician")]
        [TestCategory("MultiRegion")]
        [Timeout(70000)]
        [DataRow(true, DisplayName = "Test scenario when PPAF is enabled at client level.")]
        [DataRow(false, DisplayName = "Test scenario when PPAF is disabled at client level.")]
        public async Task ReadItemAsync_WithPPAFDiableOverride(
    bool enablePartitionLevelFailover)
        {
            // Arrange.
            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromMilliseconds(3000))
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            // Now that the ppaf enablement flag is returned from gateway, we need to intercept the response and remove the flag from the response, so that
            // the environment variable set above is honored.
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper()
            {
                ResponseIntercepter = async (response, request) =>
                {
                    string json = await response?.Content?.ReadAsStringAsync();
                    if (json.Length > 0 && json.Contains("enablePerPartitionFailoverBehavior"))
                    {
                        JObject parsedDatabaseAccountResponse = JObject.Parse(json);
                        parsedDatabaseAccountResponse.Property("enablePerPartitionFailoverBehavior").Value = enablePartitionLevelFailover.ToString();

                        HttpResponseMessage interceptedResponse = new()
                        {
                            StatusCode = response.StatusCode,
                            Content = new StringContent(parsedDatabaseAccountResponse.ToString()),
                            Version = response.Version,
                            ReasonPhrase = response.ReasonPhrase,
                            RequestMessage = response.RequestMessage,
                        };

                        return interceptedResponse;
                    }

                    return response;
                },
            };

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
                DisablePartitionLevelFailover = true, // This will disable the PPAF override for this test.
            };

            List<CosmosIntegrationTestObject> itemsList = new()
            {
                new() { Id = "smTestId1", Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(3000);

                ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                    id: itemsList[0].Id,
                    partitionKey: new PartitionKey(itemsList[0].Pk));

                IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                Assert.AreEqual(
                    expected: HttpStatusCode.OK,
                    actual: readResponse.StatusCode);

                CosmosTraceDiagnostics traceDiagnostic = readResponse.Diagnostics as CosmosTraceDiagnostics;
                Assert.IsNotNull(traceDiagnostic);

                traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContext);

                Assert.IsNull(hedgeContext);

                Assert.IsNotNull(contactedRegions);
                Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the read request succeeds on any region, given that there were no availability loss.");
            }
            finally
            {
                await this.TryDeleteItems(itemsList);
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        [Owner("ntripician")]
        public async Task AddressRefreshInternalServerErrorTest()
        {
            FaultInjectionRule internalServerError = new FaultInjectionRuleBuilder(
                id: "rule1",
                condition: new FaultInjectionConditionBuilder()
                    .WithOperationType(FaultInjectionOperationType.MetadataRefreshAddresses)
                    .WithRegion(region1)
                    .Build(),
                result:
                   FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.InternalServerError)
                    .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { internalServerError };
            FaultInjector faultInjector = new FaultInjector(rules);

            internalServerError.Disable();

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                Serializer = this.cosmosSystemTextJsonSerializer,
                ApplicationRegion = region1,
            };

            using (CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
            {
                Database database = faultInjectionClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                internalServerError.Enable();

                try
                {
                    ItemResponse<CosmosIntegrationTestObject> response = await container.ReadItemAsync<CosmosIntegrationTestObject>("testId", new PartitionKey("pk"));
                    Assert.IsTrue(internalServerError.GetHitCount() > 0);
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                }
                catch (CosmosException ex)
                {
                    Assert.Fail(ex.Message);
                }
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        [Ignore("We will enable this test once the test staging account used for multi master validation starts supporting thin proxy.")]
        [DataRow(ConnectionMode.Gateway, "15", "10", DisplayName = "Thin Client Mode - Scenario when the total iteration count is 15 and circuit breaker consecutive failure threshold is set to 10.")]
        [DataRow(ConnectionMode.Gateway, "25", "20", DisplayName = "Thin Client Mode - Scenario when the total iteration count is 25 and circuit breaker consecutive failure threshold is set to 20.")]
        [DataRow(ConnectionMode.Gateway, "35", "30", DisplayName = "Thin Client Mode - Scenario when the total iteration count is 35 and circuit breaker consecutive failure threshold is set to 30.")]
        [Owner("dkunda")]
        [Timeout(70000)]
        public async Task ReadItemAsync_WithThinClientCircuitBreakerEnabledAndSingleMasterAccountAndServiceUnavailableReceived_ShouldApplyPartitionLevelOverride(
            ConnectionMode connectionMode,
            string iterationCount,
            string circuitBreakerConsecutiveFailureCount)
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, circuitBreakerConsecutiveFailureCount);

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.ReadItem)
                        .WithRegion(Regions.WestUS)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            List<string> preferredRegions = new List<string> { Regions.WestUS, Regions.EastAsia };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = connectionMode,
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
            };

            List<CosmosIntegrationTestObject> itemsList = new()
            {
                new() { Id = "smTestId1", Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                AccountProperties accountInfo = await cosmosClient.ReadAccountAsync();

                Assert.IsTrue(cosmosClient.DocumentClient.GlobalEndpointManager.ThinClientReadEndpoints.Count() >= 2);
                this.thinClientreadRegionalEndpoints = cosmosClient.DocumentClient.GlobalEndpointManager.ThinClientReadEndpoints;

                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(3000);

                int consecutiveFailureCount = int.Parse(circuitBreakerConsecutiveFailureCount);
                int totalIterations = int.Parse(iterationCount);

                for (int attemptCount = 1; attemptCount <= totalIterations; attemptCount++)
                {
                    try
                    {
                        ItemResponse<CosmosIntegrationTestObject> readResponse = await container.ReadItemAsync<CosmosIntegrationTestObject>(
                            id: itemsList[0].Id,
                            partitionKey: new PartitionKey(itemsList[0].Pk));

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = readResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                        Assert.AreEqual(
                            expected: HttpStatusCode.OK,
                            actual: readResponse.StatusCode);

                        Assert.IsNotNull(contactedRegions);

                        PartitionKeyRangeFailoverInfo failoverInfo = TestCommon.GetFailoverInfoForFirstPartitionUsingReflection(
                            globalPartitionEndpointManager: cosmosClient.ClientContext.DocumentClient.PartitionKeyRangeLocation,
                            isReadOnlyOrMultiMaster: true);

                        if (attemptCount > consecutiveFailureCount)
                        {
                            Assert.AreEqual(this.thinClientreadRegionalEndpoints[1], failoverInfo.Current);
                        }
                        else
                        {
                            if (attemptCount == consecutiveFailureCount)
                            {
                                Assert.AreEqual(this.thinClientreadRegionalEndpoints[1], failoverInfo.Current);
                            }
                            else
                            {
                                Assert.AreEqual(this.thinClientreadRegionalEndpoints[0], failoverInfo.Current);
                            }
                        }
                    }
                    catch (CosmosException ce)
                    {
                        Assert.Fail("Read Item operation should succeed." + ce);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during ReadItemAsync call. Message: {ex.Message}");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, null);

                await this.TryDeleteItems(itemsList);
            }
        }

        [TestMethod]
        [TestCategory("MultiMaster")]
        [Ignore ("We will enable this test once the test staging account used for multi master validation starts supporting thin proxy.")]
        [DataRow(ConnectionMode.Gateway, "15", "10", DisplayName = "Thin Client Mode - Scenario when the total iteration count is 15 and circuit breaker consecutive failure threshold is set to 10.")]
        [DataRow(ConnectionMode.Gateway, "25", "20", DisplayName = "Thin Client Mode - Scenario when the total iteration count is 25 and circuit breaker consecutive failure threshold is set to 20.")]
        [DataRow(ConnectionMode.Gateway, "35", "30", DisplayName = "Thin Client Mode - Scenario when the total iteration count is 35 and circuit breaker consecutive failure threshold is set to 30.")]
        [Owner("dkunda")]
        [Timeout(70000)]
        public async Task CreateItemAsync_WithThinClientEnabledAndCircuitBreakerEnabledAndMultiMasterAccountAndServiceUnavailableReceived_ShouldApplyPartitionLevelOverride(
            ConnectionMode connectionMode,
            string iterationCount,
            string circuitBreakerConsecutiveFailureCount)
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, circuitBreakerConsecutiveFailureCount);

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceUnavailableRuleId = "503-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceUnavailableRule = new FaultInjectionRuleBuilder(
                id: serviceUnavailableRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.CreateItem)
                        .WithRegion(Regions.WestUS)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ServiceUnavailable)
                        .Build())
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceUnavailableRule };
            FaultInjector faultInjector = new FaultInjector(rules);

            Random random = new();
            List<CosmosIntegrationTestObject> itemsCleanupList = new();
            List<string> preferredRegions = new List<string> { Regions.WestUS, Regions.EastAsia };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = connectionMode,
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                RequestTimeout = TimeSpan.FromSeconds(5),
                ApplicationPreferredRegions = preferredRegions,
            };

            List<CosmosIntegrationTestObject> itemsList = new()
            {
                new() { Id = "smTestId1", Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                AccountProperties accountInfo = await cosmosClient.ReadAccountAsync();

                Assert.IsTrue(cosmosClient.DocumentClient.GlobalEndpointManager.ThinClientReadEndpoints.Count() >= 2);
                this.thinClientreadRegionalEndpoints = cosmosClient.DocumentClient.GlobalEndpointManager.ThinClientReadEndpoints;

                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(3000);

                int consecutiveFailureCount = int.Parse(circuitBreakerConsecutiveFailureCount);
                int totalIterations = int.Parse(iterationCount);

                for (int attemptCount = 1; attemptCount <= totalIterations; attemptCount++)
                {
                    try
                    {
                        CosmosIntegrationTestObject testItem = new()
                        {
                            Id = $"mmTestId{random.Next()}",
                            Pk = $"mmpk{random.Next()}"
                        };

                        ItemResponse<CosmosIntegrationTestObject> createResponse = await container.CreateItemAsync<CosmosIntegrationTestObject>(testItem);
                        itemsCleanupList.Add(testItem);

                        Assert.AreEqual(
                            expected: HttpStatusCode.Created,
                            actual: createResponse.StatusCode);

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = createResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                        Assert.AreEqual(
                            expected: HttpStatusCode.OK,
                            actual: createResponse.StatusCode);

                        Assert.IsNotNull(contactedRegions);

                        PartitionKeyRangeFailoverInfo failoverInfo = TestCommon.GetFailoverInfoForFirstPartitionUsingReflection(
                            globalPartitionEndpointManager: cosmosClient.ClientContext.DocumentClient.PartitionKeyRangeLocation,
                            isReadOnlyOrMultiMaster: true);

                        if (attemptCount > consecutiveFailureCount)
                        {
                            Assert.AreEqual(this.thinClientreadRegionalEndpoints[1], failoverInfo.Current);
                        }
                        else
                        {
                            if (attemptCount == consecutiveFailureCount)
                            {
                                Assert.AreEqual(this.thinClientreadRegionalEndpoints[1], failoverInfo.Current);
                            }
                            else
                            {
                                Assert.AreEqual(this.thinClientreadRegionalEndpoints[0], failoverInfo.Current);
                            }
                        }
                    }
                    catch (CosmosException ce)
                    {
                        Assert.Fail("Create Item operation should succeed." + ce);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during CreateItemAsync call. Message: {ex.Message}");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, null);

                await this.TryDeleteItems(itemsList);
            }
        }

        [TestMethod]
        [Owner("ntripician")]
        [TestCategory("MultiRegion")]
        [Timeout(70000)]
        public async Task ClinetOverrides0msRequestTimeoutValueForPPAF()
        {
            // Arrange.

            // Now that the ppaf enablement flag is returned from gateway, we need to intercept the response and remove the flag from the response, so that
            // the environment variable set above is honored.
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper()
            {
                ResponseIntercepter = async (response, request) =>
                {
                    string json = await response?.Content?.ReadAsStringAsync();
                    if (json.Length > 0 && json.Contains("enablePerPartitionFailoverBehavior"))
                    {
                        JObject parsedDatabaseAccountResponse = JObject.Parse(json);
                        parsedDatabaseAccountResponse.Property("enablePerPartitionFailoverBehavior").Value = "true";

                        HttpResponseMessage interceptedResponse = new()
                        {
                            StatusCode = response.StatusCode,
                            Content = new StringContent(parsedDatabaseAccountResponse.ToString()),
                            Version = response.Version,
                            ReasonPhrase = response.ReasonPhrase,
                            RequestMessage = response.RequestMessage,
                        };

                        return interceptedResponse;
                    }

                    return response;
                },
            };

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                RequestTimeout = TimeSpan.FromSeconds(0),
                ApplicationPreferredRegions = preferredRegions,
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
            };


            using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
            Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
            Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

            try
            {
                //request to start document client initiation 
                _ = await container.ReadItemAsync<CosmosIntegrationTestObject>("id", new PartitionKey("pk1"));
            }
            catch { }

            // Act and Assert.

            CrossRegionHedgingAvailabilityStrategy strat = cosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy as CrossRegionHedgingAvailabilityStrategy;
            Assert.IsNotNull(strat);
            Assert.AreNotEqual(0, strat.Threshold);
        }
        
        
        [TestMethod]
        [TestCategory("MultiRegion")]
        [Owner("trivediyash")]
        [Description("Scenario: When a document is created, then updated, and finally deleted, the operations must reflect on Change Feed.")]
        public async Task WhenADocumentIsCreatedThenUpdatedThenDeletedCFPTests()
        {
            string testId = "testDoc" + Guid.NewGuid().ToString("N");
            string testPk = "testPk" + Guid.NewGuid().ToString("N");

            try
            {
                // Create the document
                CosmosIntegrationTestObject createItem = new CosmosIntegrationTestObject
                {
                    Id = testId,
                    Pk = testPk,
                    Other = "original test"
                };

                ItemResponse<CosmosIntegrationTestObject> createResponse = await this.container.CreateItemAsync(
                    createItem,
                    new PartitionKey(testPk));

                Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
                Assert.IsNotNull(createResponse.Resource);
                Assert.AreEqual(testId, createResponse.Resource.Id);
                Assert.AreEqual(testPk, createResponse.Resource.Pk);
                Assert.AreEqual("original test", createResponse.Resource.Other);

                // Wait 1 second to ensure different timestamps
                await Task.Delay(1000);

                // Update the document
                CosmosIntegrationTestObject updateItem = new CosmosIntegrationTestObject
                {
                    Id = testId,
                    Pk = testPk,
                    Other = "test after replace"
                };

                ItemResponse<CosmosIntegrationTestObject> updateResponse = await this.container.ReplaceItemAsync(
                    updateItem,
                    testId,
                    new PartitionKey(testPk));

                Assert.AreEqual(HttpStatusCode.OK, updateResponse.StatusCode);
                Assert.IsNotNull(updateResponse.Resource);
                Assert.AreEqual(testId, updateResponse.Resource.Id);
                Assert.AreEqual(testPk, updateResponse.Resource.Pk);
                Assert.AreEqual("test after replace", updateResponse.Resource.Other);

                // Verify the ETag changed
                Assert.AreNotEqual(createResponse.ETag, updateResponse.ETag);

                // Wait 1 second to ensure different timestamps
                await Task.Delay(1000);

                // Delete the document
                ItemResponse<CosmosIntegrationTestObject> deleteResponse = await this.container.DeleteItemAsync<CosmosIntegrationTestObject>(
                    testId,
                    new PartitionKey(testPk));

                Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);

                // Verify the document no longer exists
                try
                {
                    await this.container.ReadItemAsync<CosmosIntegrationTestObject>(testId, new PartitionKey(testPk));
                    Assert.Fail("Document should not exist after deletion");
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Expected - document was successfully deleted
                }
            }
            finally
            {
                // Cleanup in case test failed before deletion
                try
                {
                    await this.container.DeleteItemAsync<CosmosIntegrationTestObject>(testId, new PartitionKey(testPk));
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    // Ignore - document already deleted
                }
            }
        }

        [TestMethod]
        [TestCategory("MultiRegion")]
        [Owner("pkolluri")]
        [Timeout(70000)]
        public async Task QueryItemAsync_WithCircuitBreakerEnabledMultiRegionAndServiceResponseDelay_ShouldFailOverToNextRegionAsync()
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, "1");

            // Enabling fault injection rule to simulate a 503 service unavailable scenario.
            string serviceResponseDelayRuleId = "response-delay-rule-" + Guid.NewGuid().ToString();
            FaultInjectionRule serviceResponseDelayRuleFromRegion1 = new FaultInjectionRuleBuilder(
                id: serviceResponseDelayRuleId,
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(FaultInjectionOperationType.QueryItem)
                        .WithConnectionType(FaultInjectionConnectionType.Gateway)
                        .WithRegion(region1)
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                        .WithDelay(TimeSpan.FromSeconds(70))
                        .Build())
                .Build();

            serviceResponseDelayRuleFromRegion1.Disable();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule> { serviceResponseDelayRuleFromRegion1};
            FaultInjector faultInjector = new FaultInjector(rules);

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Session,
                FaultInjector = faultInjector,
                ApplicationPreferredRegions = preferredRegions,
                ConnectionMode = ConnectionMode.Gateway,
            };

            List<CosmosIntegrationTestObject> itemsList = new()
            {
                new() { Id = "smTestId2", Pk = "smpk1" },
            };

            try
            {
                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(3000);

                bool isRegion1Available = true;
                bool isRegion2Available = true;

                int thresholdCounter = 0;
                int totalIterations = 7;
                int ppcbDefaultThreshold = 1;
                int firstRegionServiceUnavailableAttempt = 1;

                for (int attemptCount = 1; attemptCount <= totalIterations; attemptCount++)
                {
                    try
                    {
                        string sqlQueryText = $"SELECT * FROM c WHERE c.id = '{itemsList[0].Id}'";
                        using FeedIterator<CosmosIntegrationTestObject> feedIterator = container.GetItemQueryIterator<CosmosIntegrationTestObject>(sqlQueryText, requestOptions: new QueryRequestOptions());

                        while (feedIterator.HasMoreResults)
                        {
                            FeedResponse<CosmosIntegrationTestObject> response = await feedIterator.ReadNextAsync();
                            Assert.AreEqual(System.Net.HttpStatusCode.OK, response.StatusCode);
                            IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = response.Diagnostics.GetContactedRegions();
                            HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                            if (isRegion1Available && isRegion2Available)
                            {
                                Assert.IsTrue(contactedRegions.Count == 1, "Assert that, when no failure happened, the query request is being served from region 1.");
                                Assert.IsTrue(contactedRegions.Contains(region1));

                                // Simulating service unavailable on region 1.
                                if (attemptCount == firstRegionServiceUnavailableAttempt)
                                {
                                    isRegion1Available = false;
                                    serviceResponseDelayRuleFromRegion1.Enable();
                                }
                            }
                            else if (isRegion2Available)
                            {
                                if (thresholdCounter <= ppcbDefaultThreshold)
                                {
                                    Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the query request succeeds before the consecutive failure count reaches the threshold, the partition didn't fail over to the next region, and the request was retried.");
                                    Assert.IsTrue(contactedRegions.Contains(region1) && contactedRegions.Contains(region2), "Asserting that both region 1 and region 2 were contacted.");
                                    thresholdCounter++;
                                }
                                else
                                {
                                    Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the consecutive failure count reaches the threshold, the partition was failed over to the next region, and the subsequent query request/s were successful on the next region");
                                }
                            }
                        }
                    }
                    catch (CosmosException ce)
                    {
                        Assert.Fail("Query operation should succeed with successful failover to next region." + ce.Diagnostics.ToString());
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unhandled Exception was thrown during Query operation call. Message: {ex.Message}");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, null);
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, null);

                await this.TryDeleteItems(itemsList);
            }
        }

        /// <summary>
        /// ============================================================================================
        /// Truth Table: ReadItemAsync_WithPPAFEnabledAccountShouldAddHubHeader_On4041002FromHub
        /// ============================================================================================
        ///
        /// Parameters: connectionMode (Direct/Gateway), enablePartitionLevelFailover (PPAF), enableHubRegionProcessing
        ///
        /// Backend simulation (when hub processing is enabled):
        ///   Request #1 -> 404/1002 (ReadSessionNotAvailable)
        ///   Request #2 -> 404/1002 (ReadSessionNotAvailable)   -- SDK triggers hub header after 2nd 404/1002
        ///   Request #3 -> 403/3   (WriteForbidden)             -- non-hub region rejects hub-only request
        ///   Request #4 -> 200 OK  (pass-through, no injection) -- hub region serves the request
        ///
        /// +------+------------+-------+------------+----------------------------------------------------+-------------+-------------+-------------+-----------------+-------+---------+
        /// | Case | Connection | PPAF  | Hub        | Backend Response                                   | Hub Header  | Hub Header  | Hub Header  | Expected        | 404   | Min Req |
        /// |  #   | Mode       |       | Processing | Sequence                                           | on Req #1   | on Req #3   | on Req #4   | Outcome         | Count | Count   |
        /// +------+------------+-------+------------+----------------------------------------------------+-------------+-------------+-------------+-----------------+-------+---------+
        /// |  1   | Direct     | true  | true       |   404 (preferred read region) ->                   | NOT present | Present     | Present     | 200 OK          |   2   |  >= 3   |
        /// |      |            |       |            |   404/1002 (from account or cached hub             |             |             |             |                 |       |         |
        /// |      |            |       |            |   region, no header present) ->                    |             |             |             |                 |       |         |
        /// |      |            |       |            |   403.3 (from account or cached hub                |             |             |             |                 |       |         |
        /// |      |            |       |            |   region, hub region header present) ->            |             |             |             |                 |       |         |
        /// |      |            |       |            |   200 (response from new hub region.               |             |             |             |                 |       |         |
        /// |      |            |       |            |   This will be cached as primary                   |             |             |             |                 |       |         |
        /// |      |            |       |            |   hub/write region for the partition)              |             |             |             |                 |       |         |
        /// +------+------------+-------+------------+----------------------------------------------------+-------------+-------------+-------------+-----------------+-------+---------+
        /// |  2   | Gateway    | true  | true       |   404 (preferred read region) ->                   | NOT present | Present     | Present     | 200 OK          |   2   |  >= 3   |
        /// |      |            |       |            |   404/1002 (from account or cached hub             |             |             |             |                 |       |         |
        /// |      |            |       |            |   region, no header present) ->                    |             |             |             |                 |       |         |
        /// |      |            |       |            |   403.3 (from account or cached hub                |             |             |             |                 |       |         |
        /// |      |            |       |            |   region, hub region header present) ->            |             |             |             |                 |       |         |
        /// |      |            |       |            |   200 (response from new hub region.               |             |             |             |                 |       |         |
        /// |      |            |       |            |   This will be cached as primary                   |             |             |             |                 |       |         |
        /// |      |            |       |            |   hub/write region for the partition)              |             |             |             |                 |       |         |
        /// +------+------------+-------+------------+----------------------------------------------------+-------------+-------------+-------------+-----------------+-------+---------+
        /// |  3   | Direct     | false | true       |   404 (preferred read region) ->                   | NOT present | Present     | Present     | 200 OK          |   2   |  >= 3   |
        /// |      |            |       |            |   404/1002 (from account or cached hub             |             |             |             |                 |       |         |
        /// |      |            |       |            |   region, no header present) ->                    |             |             |             |                 |       |         |
        /// |      |            |       |            |   403.3 (from account or cached hub                |             |             |             |                 |       |         |
        /// |      |            |       |            |   region, hub region header present) ->            |             |             |             |                 |       |         |
        /// |      |            |       |            |   200 (response from new hub region.               |             |             |             |                 |       |         |
        /// |      |            |       |            |   This will be cached as primary                   |             |             |             |                 |       |         |
        /// |      |            |       |            |   hub/write region for the partition)              |             |             |             |                 |       |         |
        /// +------+------------+-------+------------+----------------------------------------------------+-------------+-------------+-------------+-----------------+-------+---------+
        /// |  4   | Gateway    | false | true       |   404 (preferred read region) ->                   | NOT present | Present     | Present     | 200 OK          |   2   |  >= 3   |
        /// |      |            |       |            |   404/1002 (from account or cached hub             |             |             |             |                 |       |         |
        /// |      |            |       |            |   region, no header present) ->                    |             |             |             |                 |       |         |
        /// |      |            |       |            |   403.3 (from account or cached hub                |             |             |             |                 |       |         |
        /// |      |            |       |            |   region, hub region header present) ->            |             |             |             |                 |       |         |
        /// |      |            |       |            |   200 (response from new hub region.               |             |             |             |                 |       |         |
        /// |      |            |       |            |   This will be cached as primary                   |             |             |             |                 |       |         |
        /// |      |            |       |            |   hub/write region for the partition)              |             |             |             |                 |       |         |
        /// +------+------------+-------+------------+----------------------------------------------------+-------------+-------------+-------------+-----------------+-------+---------+
        /// |  5   | Direct     | true  | false      | 404 -> 404 -> 404/1002                             | NOT present | N/A         | N/A         | CosmosException | N/A   |  N/A    |
        /// |      |            |       |            | (final state)                                      |             |             |             | 404/1002        |       |         |
        /// +------+------------+-------+------------+----------------------------------------------------+-------------+-------------+-------------+-----------------+-------+---------+
        /// |  6   | Gateway    | true  | false      | 404 -> 404 -> 404/1002                             | NOT present | N/A         | N/A         | CosmosException | N/A   |  N/A    |
        /// |      |            |       |            | (final state)                                      |             |             |             | 404/1002        |       |         |
        /// +------+------------+-------+------------+----------------------------------------------------+-------------+-------------+-------------+-----------------+-------+---------+
        /// |  7   | Direct     | false | false      | 404 -> 404 -> 404/1002                             | NOT present | N/A         | N/A         | CosmosException | N/A   |  N/A    |
        /// |      |            |       |            | (final state)                                      |             |             |             | 404/1002        |       |         |
        /// +------+------------+-------+------------+----------------------------------------------------+-------------+-------------+-------------+-----------------+-------+---------+
        /// |  8   | Gateway    | false | false      | 404 -> 404 -> 404/1002                             | NOT present | N/A         | N/A         | CosmosException | N/A   |  N/A    |
        /// |      |            |       |            | (final state)                                      |             |             |             | 404/1002        |       |         |
        /// +------+------------+-------+------------+----------------------------------------------------+-------------+-------------+-------------+-----------------+-------+---------+
        ///
        /// Key observations:
        ///   - Cases 1-4 (Hub Processing = true): Hub region caching works identically regardless of ConnectionMode
        ///     or PPAF. After 2x 404/1002, the SDK sets the hub header (x-ms-cosmos-hub-region-processing-only),
        ///     cycles through regions (403/3 from non-hub), and succeeds on the actual hub (200 OK).
        ///     The hub header persists on the 4th request, proving the hub is cached.
        ///   - Cases 5-8 (Hub Processing = false): When hub region processing is disabled, the SDK does NOT add
        ///     the hub header after 404/1002. The 404/1002 is surfaced directly to the caller as a CosmosException
        ///     with StatusCode = NotFound and SubStatusCode = ReadSessionNotAvailable (1002).
        ///   - PPAF (enablePartitionLevelFailover) has no effect on hub region behavior -- hub caching is orthogonal
        ///     to partition-level automatic failover. However any update on the cache would eventually impact the PPAF writes.
        ///   - ConnectionMode (Direct vs Gateway) uses different interception mechanisms (TransportClientWrapper vs
        ///     HttpClientHandlerHelper) but the retry logic and assertions are identical.
        /// </summary>
        [TestMethod]
        [Owner("aavasthy")]
        [TestCategory("MultiRegion")]
        [DataRow(ConnectionMode.Direct, true, true, DisplayName = "Scenario when direct mode is selected, partition level failover is enabled and hub region processing is enabled.")]
        [DataRow(ConnectionMode.Gateway, true, true, DisplayName = "Scenario when gateway mode is selected, partition level failover is enabled and hub region processing is enabled.")]
        [DataRow(ConnectionMode.Direct, false, true, DisplayName = "Scenario when direct mode is selected, partition level failover is disabled and hub region processing is enabled.")]
        [DataRow(ConnectionMode.Gateway, false, true, DisplayName = "Scenario when gateway mode is selected, partition level failover is disabled and hub region processing is enabled.")]
        [DataRow(ConnectionMode.Direct, true, false, DisplayName = "Scenario when direct mode is selected, partition level failover is enabled and hub region processing is disabled.")]
        [DataRow(ConnectionMode.Gateway, true, false, DisplayName = "Scenario when gateway mode is selected, partition level failover is enabled and hub region processing is disabled.")]
        [DataRow(ConnectionMode.Direct, false, false, DisplayName = "Scenario when direct mode is selected, partition level failover is disabled and hub region processing is disabled.")]
        [DataRow(ConnectionMode.Gateway, false, false, DisplayName = "Scenario when gateway mode is selected, partition level failover is disabled and hub region processing is disabled.")]
        public async Task ReadItemAsync_WithPPAFEnabledAccountShouldAddHubHeader_On4041002FromHub(
            ConnectionMode connectionMode,
            bool enablePartitionLevelFailover,
            bool enableHubRegionProcessing)
        {
            Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, enableHubRegionProcessing.ToString());

            try
            {
                int requestCount = 0;
                int return404Count = 0;
                const int maxReturn404 = 2;
                bool hubHeaderOnFourthRequest = false;

                HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
                {
                    RequestCallBack = (request, cancellationToken) =>
                    {
                        if (request.Method == HttpMethod.Get &&
                            request.RequestUri != null &&
                            request.RequestUri.AbsolutePath.Contains("/docs/"))
                        {
                            requestCount++;

                            bool hasHubHeader = request.Headers.TryGetValues(HubRegionHeader, out IEnumerable<string> values)
                                && values.Any();

                            // Verify hub header is NOT present on first request.
                            if (requestCount == 1)
                            {
                                Assert.IsFalse(hasHubHeader, $"Hub header should NOT be present on request {requestCount}");
                            }

                            // Verify hub header is present on third request.
                            if (requestCount == 3)
                            {
                                Assert.IsTrue(hasHubHeader, $"Hub header should be present on request {requestCount}");
                            }

                            // Check if hub header is present on 4th request
                            if (requestCount == 4)
                            {
                                hubHeaderOnFourthRequest = hasHubHeader;
                            }

                            // Flow is: Request sent on preferred read region >> Request gets 404/1002 >> Request retried on account
                            // hub region without hub header >> Request gets 403/3 >> Request retried again on account hub region with hub header
                            // >> Request succeeds or gets 404/1002 or 403.3. In this test we are simulating a 403.3 from the account hub region.
                            // This will trigger a hub region discovery.
                            if (requestCount == 3)
                            {
                                HttpResponseMessage writeForbiddenResponse = new HttpResponseMessage(HttpStatusCode.Forbidden)
                                {
                                    Content = new StringContent(
                                        JsonConvert.SerializeObject(new { code = "WriteForbidden", message = "The requested operation cannot be performed at this region" }),
                                        Encoding.UTF8,
                                        "application/json")
                                };

                                writeForbiddenResponse.Headers.Add("x-ms-substatus", "3");
                                writeForbiddenResponse.Headers.Add("x-ms-activity-id", Guid.NewGuid().ToString());
                                writeForbiddenResponse.Headers.Add("x-ms-request-charge", "1.0");

                                return Task.FromResult(writeForbiddenResponse);
                            }
                            else if (return404Count < maxReturn404)
                            {
                                return404Count++;

                                HttpResponseMessage notFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
                                {
                                    Content = new StringContent(
                                        JsonConvert.SerializeObject(new { code = "NotFound", message = "Simulated 404/1002" }),
                                        Encoding.UTF8,
                                        "application/json")
                                };

                                notFoundResponse.Headers.Add("x-ms-substatus", "1002");
                                notFoundResponse.Headers.Add("x-ms-activity-id", Guid.NewGuid().ToString());
                                notFoundResponse.Headers.Add("x-ms-request-charge", "1.0");

                                return Task.FromResult(notFoundResponse);
                            }
                        }

                        return Task.FromResult<HttpResponseMessage>(null);
                    },
                    ResponseIntercepter = async (response, request) =>
                    {
                        string json = await response?.Content?.ReadAsStringAsync();
                        if (json.Length > 0 && json.Contains("enablePerPartitionFailoverBehavior"))
                        {
                            JObject parsedDatabaseAccountResponse = JObject.Parse(json);
                            parsedDatabaseAccountResponse.Property("enablePerPartitionFailoverBehavior").Value = enablePartitionLevelFailover.ToString();

                            HttpResponseMessage interceptedResponse = new()
                            {
                                StatusCode = response.StatusCode,
                                Content = new StringContent(parsedDatabaseAccountResponse.ToString()),
                                Version = response.Version,
                                ReasonPhrase = response.ReasonPhrase,
                                RequestMessage = response.RequestMessage,
                            };

                            return interceptedResponse;
                        }

                        return response;
                    },
                };

                List<string> preferredRegions = new List<string> { region2, region1, region3 };
                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConnectionMode = connectionMode,
                    ConsistencyLevel = ConsistencyLevel.Session,
                    RequestTimeout = TimeSpan.FromSeconds(0),
                    ApplicationPreferredRegions = preferredRegions,
                    AvailabilityStrategy = AvailabilityStrategy.DisabledStrategy(),
                };

                if (connectionMode == ConnectionMode.Gateway)
                {
                    cosmosClientOptions.HttpClientFactory = () => new HttpClient(httpHandler);
                }
                else if(connectionMode == ConnectionMode.Direct)
                {
                    // In Direct mode, SessionTokenMismatchRetryPolicy retries at the transport layer
                    // upon receiving 404/1002 responses. Each retry goes through this interceptor,
                    // so we cannot rely on requestCount for state transitions. Instead, we use
                    // the hub header presence as the state driver: after 2× 404/1002, ClientRetryPolicy
                    // sets the hub header, signaling the interceptor to advance to the 403/3 phase.
                    bool returned403InDirect = false;

                    cosmosClientOptions.TransportClientHandlerFactory = (transport) => new TransportClientWrapper(
                        transport,
                        interceptorAfterResult: (request, storeResponse) =>
                        {
                            if (request.ResourceType == Documents.ResourceType.Document &&
                                request.OperationType == Documents.OperationType.Read)
                            {
                                requestCount++;

                                bool.TryParse(request.Headers.Get(HubRegionHeader), out bool hasHubHeader);

                                if (hasHubHeader && !returned403InDirect)
                                {
                                    // Phase 2: Hub header is present → SDK completed 2× 404/1002 phase.
                                    // Return 403/3 (WriteForbidden) once to trigger hub region discovery.
                                    returned403InDirect = true;

                                    storeResponse.Headers.Set(Documents.WFConstants.BackendHeaders.SubStatus, ((int)Documents.SubStatusCodes.WriteForbidden).ToString());
                                    storeResponse.Headers.Set(Documents.HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());
                                    storeResponse.Headers.Set(Documents.HttpConstants.HttpHeaders.RequestCharge, "1.0");

                                    Documents.StoreResponse forbiddenResponse = new Documents.StoreResponse()
                                    {
                                        Status = 403,
                                        Headers = storeResponse.Headers,
                                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes("The requested operation cannot be performed at this region"))
                                    };

                                    storeResponse = forbiddenResponse;
                                }
                                else if (!hasHubHeader)
                                {
                                    // Phase 1: No hub header → return 404/1002 (ReadSessionNotAvailable).
                                    // This may fire multiple times due to SessionTokenMismatchRetryPolicy
                                    // retries at the transport layer — that's expected.
                                    return404Count++;

                                    storeResponse.Headers.Set(Documents.WFConstants.BackendHeaders.SubStatus, ((int)Documents.SubStatusCodes.ReadSessionNotAvailable).ToString());
                                    storeResponse.Headers.Set(Documents.HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());
                                    storeResponse.Headers.Set(Documents.HttpConstants.HttpHeaders.RequestCharge, "1.0");

                                    Documents.StoreResponse notFoundResponse = new Documents.StoreResponse()
                                    {
                                        Status = 404,
                                        Headers = storeResponse.Headers,
                                        ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes($"Lease not found: Gone, rule: {0}"))
                                    };

                                    storeResponse = notFoundResponse;
                                }
                                else
                                {
                                    // Phase 3: Hub header present and 403/3 already returned → passthrough.
                                    // The real server response (200 OK) goes through to ClientRetryPolicy.
                                    hubHeaderOnFourthRequest = hasHubHeader;
                                }
                            }

                            return storeResponse;
                        });
                }

                using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Create a test item first
                ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
                await container.CreateItemAsync(testItem, new PartitionKey(testItem.pk));

                if (enableHubRegionProcessing)
                {
                    // This should trigger 2x 404/1002, then succeed on 3rd attempt with hub header
                    ItemResponse<ToDoActivity> response = await container.ReadItemAsync<ToDoActivity>(
                        testItem.id,
                        new PartitionKey(testItem.pk));

                    // Verify the request succeeded
                    Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                    Assert.IsNotNull(response.Resource);
                    Assert.AreEqual(testItem.id, response.Resource.id);

                    //Verify request counts
                    Assert.IsTrue(return404Count >= 2, $"Should have returned 404/1002 at least twice, got {return404Count}");
                    Assert.IsTrue(requestCount >= 3, $"Should have made at least 3 requests, but made {requestCount}");

                    // Hub header should be present on the 3rd request
                    Assert.IsTrue(hubHeaderOnFourthRequest,
                        "Hub region header MUST be present on 3rd request after 2x 404/1002. This proves the feature works.");
                }
                else
                {
                    // When hub region processing is disabled and the hubregion fails with 404/1002 then verify the read operation throws cosmos exception with 404/1002
                    // and does not retry to the next region.
                    CosmosException cosmosException = await Assert.ThrowsExceptionAsync<CosmosException>(async () => await container.ReadItemAsync<ToDoActivity>(
                        testItem.id,
                        new PartitionKey(testItem.pk)));

                    Assert.AreEqual(HttpStatusCode.NotFound, cosmosException.StatusCode);
                    Assert.AreEqual(Documents.SubStatusCodes.ReadSessionNotAvailable, (Documents.SubStatusCodes)cosmosException.SubStatusCode);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, null);
            }
        }

        [TestMethod]
        [Owner("aavasthy")]
        [TestCategory("MultiRegion")]
        [Description("Simulates full hub region discovery flow: 2x 404/1002 → hub header → 403/3 from non-hub → retry → success. " +
                     "Verifies hub header persists through 403/3 retries and request eventually succeeds.")]
        public async Task ReadItemAsync_HubRegionDiscovery_FullFlow_With403_3_Retry()
        {
            // Ensure hub region processing is enabled for this test
            Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, "True");

            try
            {
            int docReadRequestCount = 0;
            int return404Count = 0;
            int return403Count = 0;
            const int maxReturn404 = 2;
            const int maxReturn403 = 1;
            bool hubHeaderOn403Request = false;
            bool hubHeaderOnFinalRequest = false;

            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellationToken) =>
                {
                    // Only intercept document read requests
                    if (request.Method == HttpMethod.Get
                        && request.RequestUri != null
                        && request.RequestUri.AbsolutePath.Contains("/docs/"))
                    {
                        docReadRequestCount++;

                        bool hasHubHeader = request.Headers.TryGetValues(HubRegionHeader, out IEnumerable<string> values)
                            && values.Any();

                        // Step 1 & 2: Return 404/1002 for first two requests
                        if (return404Count < maxReturn404)
                        {
                            Assert.IsFalse(hasHubHeader,
                                $"Hub header should NOT be present on request {docReadRequestCount} (before 2x 404/1002 completes).");

                            return404Count++;

                            HttpResponseMessage notFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
                            {
                                Content = new StringContent(
                                    JsonConvert.SerializeObject(new { code = "NotFound", message = "Simulated 404/1002" }),
                                    Encoding.UTF8,
                                    "application/json")
                            };
                            notFoundResponse.Headers.Add("x-ms-substatus", "1002");
                            notFoundResponse.Headers.Add("x-ms-activity-id", Guid.NewGuid().ToString());
                            notFoundResponse.Headers.Add("x-ms-request-charge", "1.0");

                            return Task.FromResult(notFoundResponse);
                        }

                        // Step 3: After hub header is set, return 403/3 (WriteForbidden)
                        // to simulate hitting a non-hub region
                        if (return403Count < maxReturn403)
                        {
                            hubHeaderOn403Request = hasHubHeader;

                            return403Count++;

                            HttpResponseMessage forbiddenResponse = new HttpResponseMessage(HttpStatusCode.Forbidden)
                            {
                                Content = new StringContent(
                                    JsonConvert.SerializeObject(new { code = "Forbidden", message = "Simulated 403/3 WriteForbidden - not hub region" }),
                                    Encoding.UTF8,
                                    "application/json")
                            };
                            forbiddenResponse.Headers.Add("x-ms-substatus", ((int)Documents.SubStatusCodes.WriteForbidden).ToString());
                            forbiddenResponse.Headers.Add("x-ms-activity-id", Guid.NewGuid().ToString());
                            forbiddenResponse.Headers.Add("x-ms-request-charge", "1.0");

                            return Task.FromResult(forbiddenResponse);
                        }

                        // Step 4: Let the request pass through to the emulator (simulates reaching the hub region)
                        hubHeaderOnFinalRequest = hasHubHeader;
                    }

                    // Return null to let the request proceed to the real emulator
                    return Task.FromResult<HttpResponseMessage>(null);
                }
            };

            List<string> preferredRegions = new List<string> { region1, region2, region3 };
            CosmosClientOptions clientOptions = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Gateway,
                ApplicationPreferredRegions = preferredRegions,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Session,
                HttpClientFactory = () => new HttpClient(httpHandler)
            };


            using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions);
            Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
            Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

            // Create a test item using the default client (not intercepted)
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await container.CreateItemAsync(testItem, new Cosmos.PartitionKey(testItem.pk));

            // Act: Read the item — triggers the full hub discovery flow
            ItemResponse<ToDoActivity> response = await container.ReadItemAsync<ToDoActivity>(
                testItem.id,
                new Cosmos.PartitionKey(testItem.pk));

            // Assert: Request succeeded after the full retry chain
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
            Assert.IsNotNull(response.Resource);
            Assert.AreEqual(testItem.id, response.Resource.id);

            // Verify the retry sequence occurred correctly
            Assert.AreEqual(maxReturn404, return404Count,
                "Should have returned 404/1002 exactly twice.");
            Assert.AreEqual(maxReturn403, return403Count,
                "Should have returned 403/3 exactly once (simulating non-hub region).");
            Assert.IsTrue(docReadRequestCount >= 4,
                $"Expected at least 4 document read requests (2x 404/1002 + 1x 403/3 + 1x success), got {docReadRequestCount}.");

            // Verify hub header was present on the 403/3 request
            Assert.IsTrue(hubHeaderOn403Request,
                "Hub region header MUST be present on the request that received 403/3 (it was sent to a non-hub region with the header).");

            // Verify hub header persisted through 403/3 retry to the final successful request
            Assert.IsTrue(hubHeaderOnFinalRequest,
                "Hub region header MUST persist on the successful request after 403/3 retry.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, null);
            }
        }


        [TestMethod]
        [Owner("aavasthy")]
        [TestCategory("MultiRegion")]
        [DataRow(ConnectionMode.Direct, true, DisplayName = "Direct mode with PPAF enabled: 404/1002 from hub with hub header returns NoRetry.")]
        [DataRow(ConnectionMode.Gateway, true, DisplayName = "Gateway mode with PPAF enabled: 404/1002 from hub with hub header returns NoRetry.")]
        [DataRow(ConnectionMode.Direct, false, DisplayName = "Direct mode with PPAF disabled: 404/1002 from hub with hub header returns NoRetry.")]
        [DataRow(ConnectionMode.Gateway, false, DisplayName = "Gateway mode with PPAF disabled: 404/1002 from hub with hub header returns NoRetry.")]
        [Description("Simulates hub returning 404/1002 even after hub header is active. " +
                     "This proves the SDK treats the hub as source of truth — if the hub says " +
                     "session not available, the document genuinely doesn't exist in this session " +
                     "and the SDK surfaces the exception to the user (NoRetry). " +
                     "Flow: 404/1002 x2 (no hub header) → hub header set → 404/1002 (with hub header) → CosmosException to caller.")]
        public async Task ReadItemAsync_HubRegion4041002_WithHubHeader_ReturnsNoRetry(
            ConnectionMode connectionMode,
            bool enablePartitionLevelFailover)
        {
            // Ensure hub region processing is enabled for this test
            Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, "True");

            try
            {
            // Track all document read requests and their hub header state
            int requestCount = 0;
            int return404Count = 0;
            const int maxReturn404BeforeHubHeader = 2; // First two 404/1002 trigger hub header
            bool hubHeaderSeenOnFinalRequest = false;

            HttpClientHandlerHelper httpHandler = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellationToken) =>
                {
                    if (request.Method == HttpMethod.Get &&
                        request.RequestUri != null &&
                        request.RequestUri.AbsolutePath.Contains("/docs/"))
                    {
                        requestCount++;

                        bool hasHubHeader = request.Headers.TryGetValues(HubRegionHeader, out IEnumerable<string> values)
                            && values.Any();

                        // Requests 1 & 2: Return 404/1002 WITHOUT hub header.
                        // After 2nd 404/1002, SDK sets addHubRegionProcessingOnlyHeader = true.
                        if (return404Count < maxReturn404BeforeHubHeader)
                        {
                            Assert.IsFalse(hasHubHeader,
                                $"Hub header should NOT be present on request {requestCount} (before hub discovery).");

                            return404Count++;
                            HttpResponseMessage notFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
                            {
                                Content = new StringContent(
                                    JsonConvert.SerializeObject(new { code = "NotFound", message = "Simulated 404/1002 from non-hub region" }),
                                    Encoding.UTF8,
                                    "application/json")
                            };
                            notFoundResponse.Headers.Add("x-ms-substatus", "1002");
                            notFoundResponse.Headers.Add("x-ms-activity-id", Guid.NewGuid().ToString());
                            notFoundResponse.Headers.Add("x-ms-request-charge", "1.0");

                            return Task.FromResult(notFoundResponse);
                        }

                        // Request 3+: Hub header should be present. Return 404/1002 again.
                        // Since addHubRegionProcessingOnlyHeader is true and hub returns 404/1002,
                        // ShouldRetryOnSessionNotAvailable returns NoRetry — exception surfaces to user.
                        hubHeaderSeenOnFinalRequest = hasHubHeader;
                        return404Count++;

                        HttpResponseMessage hubNotFoundResponse = new HttpResponseMessage(HttpStatusCode.NotFound)
                        {
                            Content = new StringContent(
                                JsonConvert.SerializeObject(new { code = "NotFound", message = "Simulated 404/1002 from hub region — document genuinely not found" }),
                                Encoding.UTF8,
                                "application/json")
                        };
                        hubNotFoundResponse.Headers.Add("x-ms-substatus", "1002");
                        hubNotFoundResponse.Headers.Add("x-ms-activity-id", Guid.NewGuid().ToString());
                        hubNotFoundResponse.Headers.Add("x-ms-request-charge", "1.0");

                        return Task.FromResult(hubNotFoundResponse);
                    }

                    return Task.FromResult<HttpResponseMessage>(null);
                },
                ResponseIntercepter = async (response, request) =>
                {
                    string json = await response?.Content?.ReadAsStringAsync();
                    if (json.Length > 0 && json.Contains("enablePerPartitionFailoverBehavior"))
                    {
                        JObject parsedDatabaseAccountResponse = JObject.Parse(json);
                        parsedDatabaseAccountResponse.Property("enablePerPartitionFailoverBehavior").Value = enablePartitionLevelFailover.ToString();

                        HttpResponseMessage interceptedResponse = new()
                        {
                            StatusCode = response.StatusCode,
                            Content = new StringContent(parsedDatabaseAccountResponse.ToString()),
                            Version = response.Version,
                            ReasonPhrase = response.ReasonPhrase,
                            RequestMessage = response.RequestMessage,
                        };

                        return interceptedResponse;
                    }

                    return response;
                },
            };

            List<string> preferredRegions = new List<string> { region2, region1, region3 };
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConnectionMode = connectionMode,
                ConsistencyLevel = ConsistencyLevel.Session,
                RequestTimeout = TimeSpan.FromSeconds(0),
                ApplicationPreferredRegions = preferredRegions,
                AvailabilityStrategy = AvailabilityStrategy.DisabledStrategy(),
            };

            if (connectionMode == ConnectionMode.Gateway)
            {
                cosmosClientOptions.HttpClientFactory = () => new HttpClient(httpHandler);
            }
            else if (connectionMode == ConnectionMode.Direct)
            {
                cosmosClientOptions.TransportClientHandlerFactory = (transport) => new TransportClientWrapper(
                    transport,
                    interceptorAfterResult: (request, storeResponse) =>
                    {
                        if (request.ResourceType == Documents.ResourceType.Document &&
                            request.OperationType == Documents.OperationType.Read)
                        {
                            requestCount++;

                            bool.TryParse(request.Headers.Get(HubRegionHeader), out bool hasHubHeader);

                            // In Direct mode, SessionTokenMismatchRetryPolicy retries 404/1002
                            // at the transport layer, inflating requestCount and return404Count.
                            // Use the hub header as state discriminator instead of absolute counts.
                            if (hasHubHeader)
                            {
                                hubHeaderSeenOnFinalRequest = true;
                            }

                            return404Count++;

                            // Always return 404/1002 for all document reads.
                            // Without hub header: triggers hub discovery (ClientRetryPolicy retries)
                            // With hub header: hub is source of truth -> NoRetry at ClientRetryPolicy level
                            storeResponse.Headers.Set(Documents.WFConstants.BackendHeaders.NumberOfReadRegions, "3");
                            storeResponse.Headers.Set(Documents.WFConstants.BackendHeaders.SubStatus, ((int)Documents.SubStatusCodes.ReadSessionNotAvailable).ToString());
                            storeResponse.Headers.Set(Documents.HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());
                            storeResponse.Headers.Set(Documents.HttpConstants.HttpHeaders.RequestCharge, "1.0");

                            Documents.StoreResponse notFoundResponse = new Documents.StoreResponse()
                            {
                                Status = 404,
                                Headers = storeResponse.Headers,
                                ResponseBody = new MemoryStream(Encoding.UTF8.GetBytes(
                                    hasHubHeader
                                        ? "Hub region: session not available - document genuinely not found"
                                        : "Simulated 404/1002 from non-hub region"))
                            };

                            storeResponse = notFoundResponse;
                        }

                        return storeResponse;
                    });
            }

            using CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
            Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
            Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

            // Create a test item first (using the intercepted client — create goes through fine
            // because the interceptor only targets GET /docs/ requests)
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            await container.CreateItemAsync(testItem, new PartitionKey(testItem.pk));

            // Act: Read the item — this will get 404/1002 twice (hub header off),
            // then 404/1002 once more (hub header ON), and the SDK should NOT retry.
            // The SDK must throw CosmosException with status 404 and substatus 1002.
            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(async () => await container.ReadItemAsync<ToDoActivity>(
                    testItem.id,
                    new PartitionKey(testItem.pk)));

            // Assert: The SDK surfaced the 404/1002 to the user (NoRetry from hub)
            Assert.AreEqual(HttpStatusCode.NotFound, ex.StatusCode,
                "Expected 404 (NotFound) since the hub region returned 404/1002 — document doesn't exist in this session.");
            Assert.AreEqual((int)Documents.SubStatusCodes.ReadSessionNotAvailable, ex.SubStatusCode,
                "Expected substatus 1002 (ReadSessionNotAvailable) from hub region.");

            // Verify the correct number of 404/1002 responses were returned.
            // In Direct mode, SessionTokenMismatchRetryPolicy retries 404/1002 at the transport layer,
            // inflating return404Count beyond the 3 logical requests. Use >= 3 to account for this.
            Assert.IsTrue(return404Count >= 3,
                $"Should have returned at least 3x 404/1002: 2 without hub header (triggering hub discovery) + 1 with hub header (NoRetry), got {return404Count}.");

            // Verify the hub header was present on the final request (the one from the hub)
            Assert.IsTrue(hubHeaderSeenOnFinalRequest,
                "Hub region header MUST be present on the 3rd request (sent to hub). " +
                "This proves the SDK set the hub header after 2x 404/1002 and the hub returned 404/1002 — causing NoRetry.");

            // Clean up the test item
            await container.DeleteItemAsync<ToDoActivity>(testItem.id, new PartitionKey(testItem.pk));
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.HubRegionProcessingEnabled, null);
            }
        }

        private async Task TryCreateItems(List<CosmosIntegrationTestObject> testItems)
        {
            foreach (CosmosIntegrationTestObject item in testItems)
            {
                await this.TryCreateItem(item);
            }
        }

        private async Task TryCreateItem(CosmosIntegrationTestObject testItem)
        {
            try
            {
                await this.container.CreateItemAsync<CosmosIntegrationTestObject>(testItem);
            }
            catch (CosmosException ce)
            {
                Assert.Fail($"Failed to create item with id: {testItem.Id}, message: {ce.Message}");
            }
        }

        private async Task TryDeleteItems(List<CosmosIntegrationTestObject> testItems)
        {
            foreach (CosmosIntegrationTestObject item in testItems)
            {
                await this.TryDeleteItem(item);
            }
        }

        private async Task TryDeleteItem(CosmosIntegrationTestObject testItem)
        {
            try
            {
                await this.container.DeleteItemAsync<CosmosIntegrationTestObject>(
                    testItem.Id,
                    new PartitionKey(testItem.Pk));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignore
            }
        }

        public sealed class TestCosmosItem
        {
            [Newtonsoft.Json.JsonConstructor]
            public TestCosmosItem(
                string id,
                string pk,
                string title,
                string email,
                string body,
                DateTime createdUtc,
                DateTime modifiedUtc,
                DateTime[] extraDates)
            {
                this.id = id;
                this.pk = pk;
                this.title = title;
                this.email = email;
                this.body = body;
                this.CreatedUtc = createdUtc;
                this.ModifiedUtc = modifiedUtc;
                this.ExtraDates = extraDates;
            }

#pragma warning disable IDE1006
            public string id { get; }
            public string pk { get; }
            public string title { get; }
            public string email { get; }
            public string body { get; }
#pragma warning restore IDE1006 // Naming Styles
            public DateTime CreatedUtc { get; }
            public DateTime ModifiedUtc { get; }
            public DateTime[] ExtraDates { get; }
        }
    }
}
