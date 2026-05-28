//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class GlobalPartitionEndpointManagerTests
    {
        [TestMethod]
        [Timeout(10000)]
        public async Task TestWriteForbiddenScenarioAsync()
        {
            GlobalPartitionEndpointManagerTests.SetupAccountAndCacheOperations(
                shouldEnablePPAF: true,
                out string secondaryRegionNameForUri,
                out string globalEndpoint,
                out string secondaryRegionEndpiont,
                out string databaseName,
                out string containerName,
                out ResourceId containerResourceId,
                out Mock<IHttpHandler> mockHttpHandler,
                out IReadOnlyList<string> primaryRegionPartitionKeyRangeIds,
                out TransportAddressUri primaryRegionprimaryReplicaUri);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);

            MockSetupsHelper.SetupWriteForbiddenException(
                mockTransport,
                primaryRegionprimaryReplicaUri);

            // Partition key ranges are the same in both regions so the SDK
            // does not need to go the secondary to get the partition key ranges.
            // Only the addresses need to be mocked on the secondary
            MockSetupsHelper.SetupAddresses(
                mockHttpHandler: mockHttpHandler,
                partitionKeyRangeId: primaryRegionPartitionKeyRangeIds.First(),
                regionEndpoint: secondaryRegionEndpiont,
                regionName: secondaryRegionNameForUri,
                containerResourceId: containerResourceId,
                primaryReplicaUri: out TransportAddressUri secondaryRegionPrimaryReplicaUri);

            MockSetupsHelper.SetupCreateItemResponse(
                mockTransport,
                secondaryRegionPrimaryReplicaUri);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                ApplicationPreferredRegions = new List<string>()
                {
                    Regions.EastUS,
                    Regions.WestUS
                },
                HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                TransportClientHandlerFactory = (original) => mockTransport.Object,
            };

            using (CosmosClient customClient = new CosmosClient(
                globalEndpoint,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                cosmosClientOptions))
            {
                Container container = customClient.GetContainer(databaseName, containerName);

                ToDoActivity toDoActivity = new ToDoActivity()
                {
                    Id = "TestItem",
                    Pk = "TestPk"
                };

                ItemResponse<ToDoActivity> response = await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.Pk));
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                mockTransport.VerifyAll();
                mockHttpHandler.VerifyAll();

                // Clears all the setups. No network calls should be done on the next operation.
                mockHttpHandler.Reset();
                mockTransport.Reset();
                mockTransport.Setup(x => x.Dispose());

                MockSetupsHelper.SetupCreateItemResponse(
                    mockTransport,
                    secondaryRegionPrimaryReplicaUri);

                ToDoActivity toDoActivity2 = new ToDoActivity()
                {
                    Id = "TestItem2",
                    Pk = "TestPk"
                };

                response = await container.CreateItemAsync(toDoActivity2, new Cosmos.PartitionKey(toDoActivity2.Pk));
                Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            }
        }

        /// <summary>
        /// Test to validate that when the partition level failover is enabled with the preferred regions list provided, if the first
        /// region is unavailable for write, then the write should eventually get retried to the next preferred region.
        /// </summary>
        [TestMethod]
        [Timeout(10000)]
        public async Task CreateItemAsync_WithPreferredRegionsAndServiceUnavailableForFirstPreferredRegion_ShouldRetryAndSucceedToTheNextPreferredRegion()
        {
            GlobalPartitionEndpointManagerTests.SetupAccountAndCacheOperations(
                shouldEnablePPAF: true,
                out string secondaryRegionNameForUri,
                out string globalEndpoint,
                out string secondaryRegionEndpiont,
                out string databaseName,
                out string containerName,
                out ResourceId containerResourceId,
                out Mock<IHttpHandler> mockHttpHandler,
                out IReadOnlyList<string> primaryRegionPartitionKeyRangeIds,
                out TransportAddressUri primaryRegionprimaryReplicaUri);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);

            MockSetupsHelper.SetupServiceUnavailableException(
                mockTransport,
                primaryRegionprimaryReplicaUri);

            // Partition key ranges are the same in both regions so the SDK
            // does not need to go the secondary to get the partition key ranges.
            // Only the addresses need to be mocked on the secondary
            MockSetupsHelper.SetupAddresses(
                mockHttpHandler: mockHttpHandler,
                partitionKeyRangeId: primaryRegionPartitionKeyRangeIds.First(),
                regionEndpoint: secondaryRegionEndpiont,
                regionName: secondaryRegionNameForUri,
                containerResourceId: containerResourceId,
                primaryReplicaUri: out TransportAddressUri secondaryRegionPrimaryReplicaUri);

            MockSetupsHelper.SetupCreateItemResponse(
                mockTransport,
                secondaryRegionPrimaryReplicaUri);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                ApplicationPreferredRegions = new List<string>()
                {
                    Regions.EastUS,
                    Regions.WestUS
                },
                HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                TransportClientHandlerFactory = (original) => mockTransport.Object,
            };

            using CosmosClient customClient = new CosmosClient(
                    globalEndpoint,
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                    cosmosClientOptions);

            Container container = customClient.GetContainer(databaseName, containerName);

            ToDoActivity toDoActivity = new ToDoActivity()
            {
                Id = "TestItem",
                Pk = "TestPk"
            };

            await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.Pk));

            ItemResponse<ToDoActivity> response = await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.Pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            mockTransport.VerifyAll();
            mockHttpHandler.VerifyAll();

            // Clears all the setups. No network calls should be done on the next operation.
            mockHttpHandler.Reset();
            mockTransport.Reset();
            mockTransport.Setup(x => x.Dispose());

            MockSetupsHelper.SetupCreateItemResponse(
                mockTransport,
                secondaryRegionPrimaryReplicaUri);

            ToDoActivity toDoActivity2 = new ToDoActivity()
            {
                Id = "TestItem2",
                Pk = "TestPk"
            };

            response = await container.CreateItemAsync(toDoActivity2, new Cosmos.PartitionKey(toDoActivity2.Pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        /// <summary>
        /// Test to validate that when the partition level failover is enabled with the preferred regions list is missing, then the client
        /// initialization should succeed without throwing any argument exception.
        /// </summary>
        [TestMethod]
        [DataRow(true, DisplayName = "Validate that when an explict availability strategy is provided, the same will be honored by bypassing the default one, when PPAF is enabled.")]
        [DataRow(false, DisplayName = "Validate that when no explict availability strategy is provided, a default availability strategy will be applied, when PPAF is enabled.")]
        public void CreateItemAsync_WithNoPreferredRegionsAndServiceUnavailable_ShouldNotThrowArgumentException(
            bool isExplictAvailabilityStrategyProvided)
        {
            TimeSpan explictAvailabilityStrategyThreshold = TimeSpan.FromMilliseconds(2000);
            TimeSpan explictAvailabilityStrategyThresholdStep = TimeSpan.FromMilliseconds(500);

            GlobalPartitionEndpointManagerTests.SetupAccountAndCacheOperations(
                shouldEnablePPAF: true,
                out string secondaryRegionNameForUri,
                out string globalEndpoint,
                out string secondaryRegionEndpiont,
                out string databaseName,
                out string containerName,
                out ResourceId containerResourceId,
                out Mock<IHttpHandler> mockHttpHandler,
                out IReadOnlyList<string> primaryRegionPartitionKeyRangeIds,
                out TransportAddressUri primaryRegionprimaryReplicaUri);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);

            MockSetupsHelper.SetupServiceUnavailableException(
                mockTransport,
                primaryRegionprimaryReplicaUri);

            mockTransport.Setup(x => x.Dispose());

            // Partition key ranges are the same in both regions so the SDK
            // does not need to go the secondary to get the partition key ranges.
            // Only the addresses need to be mocked on the secondary
            MockSetupsHelper.SetupAddresses(
                mockHttpHandler: mockHttpHandler,
                partitionKeyRangeId: primaryRegionPartitionKeyRangeIds.First(),
                regionEndpoint: secondaryRegionEndpiont,
                regionName: secondaryRegionNameForUri,
                containerResourceId: containerResourceId,
                primaryReplicaUri: out TransportAddressUri secondaryRegionPrimaryReplicaUri);

            MockSetupsHelper.SetupCreateItemResponse(
                mockTransport,
                secondaryRegionPrimaryReplicaUri);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                TransportClientHandlerFactory = (original) => mockTransport.Object,
            };

            if (isExplictAvailabilityStrategyProvided)
            {
                cosmosClientOptions.AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                    threshold: explictAvailabilityStrategyThreshold,
                    thresholdStep: explictAvailabilityStrategyThresholdStep);
            }

            CosmosClient cosmosClient = new CosmosClient(
                globalEndpoint,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                cosmosClientOptions);

            Assert.IsNotNull(cosmosClient,
                message: "ApplicationPreferredRegions or ApplicationRegion is no longer mandatory fields, hence the client initialization should succeed.");

            Assert.IsNotNull(cosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy);

            CrossRegionHedgingAvailabilityStrategy crossRegionHedgingStrategy = (CrossRegionHedgingAvailabilityStrategy)cosmosClient.DocumentClient.ConnectionPolicy.AvailabilityStrategy;

            Assert.IsNotNull(crossRegionHedgingStrategy);

            if (isExplictAvailabilityStrategyProvided)
            {
                // Explict availability strategy values.
                Assert.AreEqual(explictAvailabilityStrategyThreshold, crossRegionHedgingStrategy.Threshold);
                Assert.AreEqual(explictAvailabilityStrategyThresholdStep, crossRegionHedgingStrategy.ThresholdStep);
            }
            else
            {
                // Default availability strategy values.
                Assert.AreEqual(TimeSpan.FromMilliseconds(1000), crossRegionHedgingStrategy.Threshold);
                Assert.AreEqual(TimeSpan.FromMilliseconds(500), crossRegionHedgingStrategy.ThresholdStep);
            }
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task TestRequestTimeoutExceptionScenarioAsync()
        {
            GlobalPartitionEndpointManagerTests.SetupAccountAndCacheOperations(
                shouldEnablePPAF: true,
                out string secondaryRegionNameForUri,
                out string globalEndpoint,
                out string secondaryRegionEndpiont,
                out string databaseName,
                out string containerName,
                out ResourceId containerResourceId,
                out Mock<IHttpHandler> mockHttpHandler,
                out IReadOnlyList<string> primaryRegionPartitionKeyRangeIds,
                out TransportAddressUri primaryRegionprimaryReplicaUri);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);

            MockSetupsHelper.SetupRequestTimeoutException(
                mockTransport,
                primaryRegionprimaryReplicaUri);

            // Partition key ranges are the same in both regions so the SDK
            // does not need to go the secondary to get the partition key ranges.
            // Only the addresses need to be mocked on the secondary
            MockSetupsHelper.SetupAddresses(
                mockHttpHandler: mockHttpHandler,
                partitionKeyRangeId: primaryRegionPartitionKeyRangeIds.First(),
                regionEndpoint: secondaryRegionEndpiont,
                regionName: secondaryRegionNameForUri,
                containerResourceId: containerResourceId,
                primaryReplicaUri: out TransportAddressUri secondaryRegionPrimaryReplicaUri);

            MockSetupsHelper.SetupCreateItemResponse(
                mockTransport,
                secondaryRegionPrimaryReplicaUri);

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                ApplicationPreferredRegions = new List<string>()
                {
                    Regions.EastUS,
                    Regions.WestUS
                },
                HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                TransportClientHandlerFactory = (original) => mockTransport.Object,
            };

            using CosmosClient customClient = new CosmosClient(
                globalEndpoint,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                cosmosClientOptions);

            Container container = customClient.GetContainer(databaseName, containerName);

            ToDoActivity toDoActivity = new ToDoActivity()
            {
                Id = "TestItem",
                Pk = "TestPk"
            };

            // First create will fail because it is not certain if the payload was sent or not.
            try
            {
                await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.Pk));
                Assert.Fail("Should throw an exception");
            }
            catch (CosmosException ce) when (ce.StatusCode == HttpStatusCode.RequestTimeout)
            {
                Assert.IsNotNull(ce);
            }

            ItemResponse<ToDoActivity> response = await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.Pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            mockTransport.VerifyAll();
            mockHttpHandler.VerifyAll();

            // Clears all the setups. No network calls should be done on the next operation.
            mockHttpHandler.Reset();
            mockTransport.Reset();
            mockTransport.Setup(x => x.Dispose());

            MockSetupsHelper.SetupCreateItemResponse(
                mockTransport,
                secondaryRegionPrimaryReplicaUri);

            ToDoActivity toDoActivity2 = new ToDoActivity()
            {
                Id = "TestItem2",
                Pk = "TestPk"
            };

            response = await container.CreateItemAsync(toDoActivity2, new Cosmos.PartitionKey(toDoActivity2.Pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        [TestMethod]
        [Timeout(20000)]
        [Owner("dkunda")]
        [DataRow(false, null, DisplayName = "Scenario when PPAF is not disabled at client level and not set at the service level.")]
        [DataRow(false, true, DisplayName = "Scenario when PPAF is not disabled at client level and enabled at service level.")]
        [DataRow(false, false, DisplayName = "Scenario when PPAF is not disabled at client level and disabled at service level.")]
        [DataRow(true, null, DisplayName = "Scenario when PPAF is disabled at client level and not set at the service level.")]
        [DataRow(true, true, DisplayName = "Scenario when PPAF is disabled at client level and enabled at service level.")]
        [DataRow(true, false, DisplayName = "Scenario when PPAF is disabled at client level and disabled at service level.")]
        public async Task TestPPAFClientAndServerEnablementCombinationScenariosAsync(
            bool ppafDisabledFromClient,
            bool? ppafEnabledFromService)
        {
            try
            {
                GlobalPartitionEndpointManagerTests.SetupAccountAndCacheOperations(
                    shouldEnablePPAF: ppafEnabledFromService,
                    out string secondaryRegionNameForUri,
                    out string globalEndpoint,
                    out string secondaryRegionEndpiont,
                    out string databaseName,
                    out string containerName,
                    out ResourceId containerResourceId,
                    out Mock<IHttpHandler> mockHttpHandler,
                    out IReadOnlyList<string> primaryRegionPartitionKeyRangeIds,
                    out TransportAddressUri primaryRegionprimaryReplicaUri);

                Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);

                MockSetupsHelper.SetupServiceUnavailableException(
                    mockTransport,
                    primaryRegionprimaryReplicaUri);

                // Partition key ranges are the same in both regions so the SDK
                // does not need to go the secondary to get the partition key ranges.
                // Only the addresses need to be mocked on the secondary
                MockSetupsHelper.SetupAddresses(
                    mockHttpHandler: mockHttpHandler,
                    partitionKeyRangeId: primaryRegionPartitionKeyRangeIds.First(),
                    regionEndpoint: secondaryRegionEndpiont,
                    regionName: secondaryRegionNameForUri,
                    containerResourceId: containerResourceId,
                    primaryReplicaUri: out TransportAddressUri secondaryRegionPrimaryReplicaUri);

                MockSetupsHelper.SetupCreateItemResponse(
                    mockTransport,
                    secondaryRegionPrimaryReplicaUri);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                    ApplicationPreferredRegions = new List<string>()
                    {
                        Regions.EastUS,
                        Regions.WestUS
                    },
                    HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                    TransportClientHandlerFactory = (original) => mockTransport.Object,
                    DisablePartitionLevelFailover = ppafDisabledFromClient,
                };

                using CosmosClient customClient = new CosmosClient(
                        globalEndpoint,
                        Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                        cosmosClientOptions);

                Container container = customClient.GetContainer(databaseName, containerName);

                ToDoActivity toDoActivity = new ToDoActivity()
                {
                    Id = "TestItem",
                    Pk = "TestPk"
                };

                if (!ppafDisabledFromClient && ppafEnabledFromService.HasValue && ppafEnabledFromService.Value)
                {
                    ItemResponse<ToDoActivity> response = await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.Pk));

                    Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
                    Assert.IsTrue(response.Diagnostics.GetContactedRegions().Count > 1);

                    mockTransport.VerifyAll();
                    mockHttpHandler.VerifyAll();
                }
                else
                {
                    try
                    {
                        await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.Pk));
                        Assert.Fail("Should throw an exception");
                    }
                    catch (CosmosException ce)
                    {
                        // Clears all the setups. No network calls should be done on the next operation.
                        Assert.IsNotNull(ce);
                        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, ce.StatusCode);
                    }
                }

                mockHttpHandler.Reset();
                mockTransport.Reset();
                mockTransport.Setup(x => x.Dispose());
            }
            finally
            {
                // Reset the environment variable to avoid affecting other tests.
                Environment.SetEnvironmentVariable(ConfigurationManager.PartitionLevelFailoverEnabled, null);
            }
        }

        private static void SetupAccountAndCacheOperations(
            bool? shouldEnablePPAF,
            out string secondaryRegionNameForUri,
            out string globalEndpoint,
            out string secondaryRegionEndpiont,
            out string databaseName,
            out string containerName,
            out ResourceId containerResourceId,
            out Mock<IHttpHandler> mockHttpHandler,
            out IReadOnlyList<string> primaryRegionPartitionKeyRangeIds,
            out TransportAddressUri primaryRegionprimaryReplicaUri,
            [CallerMemberName] string accountName = nameof(GlobalPartitionEndpointManagerTests))
        {
            string primaryRegionNameForUri = "eastus";
            secondaryRegionNameForUri = "westus";
            globalEndpoint = $"https://{accountName}.documents.azure.com:443/";
            Uri globalEndpointUri = new Uri(globalEndpoint);
            string primaryRegionEndpoint = $"https://{accountName}-{primaryRegionNameForUri}.documents.azure.com";
            secondaryRegionEndpiont = $"https://{accountName}-{secondaryRegionNameForUri}.documents.azure.com";
            databaseName = "testDb";
            containerName = "testContainer";
            string containerRid = "ccZ1ANCszwk=";
            containerResourceId = ResourceId.Parse(containerRid);

            List<AccountRegion> writeRegion = new List<AccountRegion>()
            {
                new AccountRegion()
                {
                    Name = "East US",
                    Endpoint = $"{primaryRegionEndpoint}:443/"
                }
            };

            List<AccountRegion> readRegions = new List<AccountRegion>()
            {
                new AccountRegion()
                {
                    Name = "East US",
                    Endpoint = $"{primaryRegionEndpoint}:443/"
                },
                new AccountRegion()
                {
                    Name = "West US",
                    Endpoint = $"{secondaryRegionEndpiont}:443/"
                }
            };

            // Create a mock http handler to inject gateway responses.
            // MockBehavior.Strict ensures that only the mocked APIs get called
            mockHttpHandler = new Mock<IHttpHandler>(MockBehavior.Strict);
            MockSetupsHelper.SetupStrongAccountProperties(
                mockHttpClientHandler: mockHttpHandler,
                endpoint: globalEndpointUri.ToString(),
                accountName: accountName,
                writeRegions: writeRegion,
                readRegions: readRegions,
                shouldEnablePPAF: shouldEnablePPAF);

            MockSetupsHelper.SetupContainerProperties(
                mockHttpHandler: mockHttpHandler,
                regionEndpoint: primaryRegionEndpoint,
                databaseName: databaseName,
                containerName: containerName,
                containerRid: containerRid);

            MockSetupsHelper.SetupPartitionKeyRanges(
                mockHttpHandler: mockHttpHandler,
                regionEndpoint: primaryRegionEndpoint,
                containerResourceId: containerResourceId,
                partitionKeyRangeIds: out primaryRegionPartitionKeyRangeIds);

            MockSetupsHelper.SetupAddresses(
                mockHttpHandler: mockHttpHandler,
                partitionKeyRangeId: primaryRegionPartitionKeyRangeIds.First(),
                regionEndpoint: primaryRegionEndpoint,
                regionName: primaryRegionNameForUri,
                containerResourceId: containerResourceId,
                primaryReplicaUri: out primaryRegionprimaryReplicaUri);
        }
    }
}