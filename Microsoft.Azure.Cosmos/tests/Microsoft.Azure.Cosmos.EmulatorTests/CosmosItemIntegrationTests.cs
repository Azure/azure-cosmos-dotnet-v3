namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
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
                //Serializer = this.cosmosSystemTextJsonSerializer,
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
        [DataRow("True", "10", DisplayName = "Scenario whtn the circuit breaker consecutive failure threshold is set to 10.")]
        [DataRow("True", "20", DisplayName = "Scenario whtn the circuit breaker consecutive failure threshold is set to 20.")]
        [DataRow("True", "30", DisplayName = "Scenario whtn the circuit breaker consecutive failure threshold is set to 30.")]
        [Owner("dkunda")]
        [Timeout(70000)]
        public async Task ReadItemAsync_WithCircuitBreakerEnabledAndSingleMasterAccountAndServiceUnavailableReceived_ShouldApplyPartitionLevelOverride(
            string circuitBreakerenabled,
            string circuitBreakerConsecutiveFailureCount)
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, circuitBreakerenabled);
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCount, circuitBreakerConsecutiveFailureCount);

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
                CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                // Act and Assert.
                await this.TryCreateItems(itemsList);

                //Must Ensure the data is replicated to all regions
                await Task.Delay(3000);

                int consecutiveFailureCount = int.Parse(circuitBreakerConsecutiveFailureCount);
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

                        Assert.IsTrue(attemptCount > consecutiveFailureCount / 2);
                        Assert.IsNotNull(contactedRegions);

                        if (attemptCount == (consecutiveFailureCount / 2) + 1)
                        {
                            Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the read request succeeds after failover, the partition was failed over to the next region, after the failures reaches the threshold.");
                            Assert.IsTrue(contactedRegions.Contains(region1) && contactedRegions.Contains(region2));
                        }
                        else
                        {
                            Assert.IsTrue(contactedRegions.Count == 1);
                            Assert.IsTrue(contactedRegions.Contains(region2));
                        }
                    }
                    catch (CosmosException ex)
                    {
                        Assert.AreEqual(
                            expected: HttpStatusCode.ServiceUnavailable,
                            actual: ex.StatusCode);

                        Assert.IsTrue(attemptCount <= consecutiveFailureCount / 2);

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = ex.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));

                        Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when a 503 Service Unavailable happens, the partition was not failed over to the next region, until the failures reaches the threshold.");
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
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCount, null);

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
                CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
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
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCount, null);

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
            Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCount, $"{circuitBreakerConsecutiveFailureCount}");

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

            CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);

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
                Environment.SetEnvironmentVariable(ConfigurationManager.CircuitBreakerConsecutiveFailureCount, null);
            }
        }

        [TestMethod]
        [Owner("dkunda")]
        [TestCategory("MultiMaster")]
        [Timeout(70000)]
        public async Task CreateItemAsync_WithCircuitBreakerEnabledAndMultiMasterAccountAndServiceUnavailableReceived_ShouldApplyPartitionLevelOverride()
        {
            // Arrange.
            Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelCircuitBreakerEnabled, "True");

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
                int totalAttempts = 10;
                CosmosClient cosmosClient = new(connectionString: this.connectionString, clientOptions: cosmosClientOptions);
                Database database = cosmosClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = database.GetContainer(MultiRegionSetupHelpers.containerName);

                for (int attemptCount = 1; attemptCount <= totalAttempts; attemptCount++)
                {
                    try
                    {
                        CosmosIntegrationTestObject testItem = new()
                        {
                            Id = $"mmTestId{attemptCount}",
                            Pk = $"mmpk{attemptCount}"
                        };

                        ItemResponse<CosmosIntegrationTestObject> createResponse = await container.CreateItemAsync<CosmosIntegrationTestObject>(testItem);
                        itemsCleanupList.Add(testItem);

                        Assert.AreEqual(
                            expected: HttpStatusCode.Created,
                            actual: createResponse.StatusCode);

                        IReadOnlyList<(string regionName, Uri uri)> contactedRegionMapping = createResponse.Diagnostics.GetContactedRegions();
                        HashSet<string> contactedRegions = new(contactedRegionMapping.Select(r => r.regionName));
                        Assert.IsNotNull(contactedRegions);

                        if (attemptCount == 1)
                        {
                            Assert.IsTrue(contactedRegions.Count == 2, "Asserting that when the write request fails ue to 503, the partition will be failed over to the next region and there will be 2 retry attempts.");
                            Assert.IsTrue(contactedRegions.Contains(region1) && contactedRegions.Contains(region2));
                        }
                        else
                        {
                            Assert.IsTrue(contactedRegions.Count == 1, "Asserting that when the first write request succeeds after failover, the partition was indeed failed over to the next region and the subsequent writes will be routed to the next region.");
                            Assert.IsTrue(contactedRegions.Contains(region2));
                        }
                    }
                    catch (CosmosException ex)
                    {
                        Console.WriteLine(ex.Diagnostics);
                        Assert.Fail("Create Item operation should not fail.");
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
