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
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Routing.GlobalPartitionEndpointManagerCore;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.MultiRegionSetupHelpers;

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
                this.container.DeleteItemAsync<CosmosIntegrationTestObject>("deleteMe", new PartitionKey("MMWrite"));
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Ignore
            }
            finally
            {
                //Do not delete the resources (except MM Write test object), georeplication is slow and we want to reuse the resources
                this.client?.Dispose();
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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, null);

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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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
                await Task.Delay(3000);

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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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
                await Task.Delay(3000);

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
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCountForReads, null);

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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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
                        .WithDelay(TimeSpan.FromMilliseconds(2))
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
                        .WithDelay(TimeSpan.FromMilliseconds(2))
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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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

                Assert.IsNotNull(hedgeContext);
                List<string> hedgedRegions = ((IEnumerable<string>)hedgeContext).ToList();

                Assert.IsTrue(hedgedRegions.Count >= 1, "Since the first region is not available, the request should atleast hedge to the next region.");
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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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
                        .WithDelay(TimeSpan.FromMilliseconds(10))
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
            [JsonConstructor]
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
