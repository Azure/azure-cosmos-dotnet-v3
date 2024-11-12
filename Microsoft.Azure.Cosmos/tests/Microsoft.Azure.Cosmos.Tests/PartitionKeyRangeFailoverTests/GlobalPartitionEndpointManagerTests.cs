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
                EnablePartitionLevelFailover = true,
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
                EnablePartitionLevelFailover = true,
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
        /// initialization should throw an argument exception and fail.
        /// </summary>
        [TestMethod]
        public void CreateItemAsync_WithNoPreferredRegionsAndServiceUnavailable_ShouldThrowArgumentException()
        {
            GlobalPartitionEndpointManagerTests.SetupAccountAndCacheOperations(
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
                EnablePartitionLevelFailover = true,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
                TransportClientHandlerFactory = (original) => mockTransport.Object,
            };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => new CosmosClient(
                 globalEndpoint,
                 Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                 cosmosClientOptions));

            Assert.AreEqual(
                expected: "ApplicationPreferredRegions is required when EnablePartitionLevelFailover is enabled.",
                actual: exception.Message);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task TestRequestTimeoutExceptionScenarioAsync()
        {
            GlobalPartitionEndpointManagerTests.SetupAccountAndCacheOperations(
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
                EnablePartitionLevelFailover = true,
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

        private static void SetupAccountAndCacheOperations(
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
                readRegions: readRegions);

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