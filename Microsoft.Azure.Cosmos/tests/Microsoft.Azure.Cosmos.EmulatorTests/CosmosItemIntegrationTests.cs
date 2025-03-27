namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
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

            IDictionary<string, Uri> readRegions = this.client.DocumentClient.GlobalEndpointManager.GetAvailableReadEndpointsByLocation();
            Assert.IsTrue(readRegions.Count() >= 3);

            region1 = readRegions.Keys.ElementAt(0);
            region2 = readRegions.Keys.ElementAt(1);
            region3 = readRegions.Keys.ElementAt(2);
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
        [DataRow("15", "10", DisplayName = "Scenario whtn the total iteration count is 15 and circuit breaker consecutive failure threshold is set to 10.")]
        [DataRow("25", "20", DisplayName = "Scenario whtn the total iteration count is 25 and circuit breaker consecutive failure threshold is set to 20.")]
        [DataRow("35", "30", DisplayName = "Scenario whtn the total iteration count is 35 and circuit breaker consecutive failure threshold is set to 30.")]
        [Owner("dkunda")]
        [Timeout(70000)]
        public async Task ReadItemAsync_WithCircuitBreakerEnabledAndSingleMasterAccountAndServiceUnavailableReceived_ShouldApplyPartitionLevelOverride(
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

                        if (attemptCount > consecutiveFailureCount + 1)
                        {
                            Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the consecutive failure count reaches the threshold, the partition was failed over to the next region, and the subsequent read request/s were successful on the next region.");
                            Assert.IsTrue(contactedRegions.Contains(region2));
                        }
                        else
                        {
                            Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the read request succeeds before the consecutive failure count reaches the threshold, the partition didn't over to the next region, and the request was retried on the next region.");
                            Assert.IsTrue(contactedRegions.Contains(region1) && contactedRegions.Contains(region2));
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
        [DataRow("15", "10", DisplayName = "Scenario whtn the total iteration count is 15 and circuit breaker consecutive failure threshold is set to 10.")]
        [DataRow("25", "20", DisplayName = "Scenario whtn the total iteration count is 25 and circuit breaker consecutive failure threshold is set to 20.")]
        [DataRow("35", "30", DisplayName = "Scenario whtn the total iteration count is 35 and circuit breaker consecutive failure threshold is set to 30.")]
        [Timeout(70000)]
        public async Task CreateItemAsync_WithCircuitBreakerEnabledAndMultiMasterAccountAndServiceUnavailableReceived_ShouldApplyPartitionLevelOverride(
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
                Serializer = this.cosmosSystemTextJsonSerializer
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

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = createResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));
                        Assert.IsNotNull(contactedRegions);

                        if (attemptCount > consecutiveFailureCount + 1)
                        {
                            Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the consecutive failure count reaches the threshold, the partition was failed over to the next region, and the subsequent write request/s were successful on the next region.");
                            Assert.IsTrue(contactedRegions.Contains(region2));
                        }
                        else
                        {
                            Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the write requests succeeds before the consecutive failure count reaches the threshold, the partition didn't over to the next region, and the request was retried on the next region.");
                            Assert.IsTrue(contactedRegions.Contains(region1) && contactedRegions.Contains(region2));
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
    }
}
