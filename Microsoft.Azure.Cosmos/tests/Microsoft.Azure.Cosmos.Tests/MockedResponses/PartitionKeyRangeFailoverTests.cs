//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class PartitionKeyRangeFailoverTests
    {
        [TestMethod]
        public async Task TestWriteForbiddenScenarioAsync()
        {
            string accountName = "testAccount";
            string primaryRegionNameForUri = "eastus";
            string secondaryRegionNameForUri = "westus";
            string globalEndpoint = $"https://{accountName}.documents.azure.com:443/";
            Uri globalEndpointUri = new Uri(globalEndpoint);
            string primaryRegionEndpoint = $"https://{accountName}-{primaryRegionNameForUri}.documents.azure.com";
            string secondaryRegionEndpiont = $"https://{accountName}-{secondaryRegionNameForUri}.documents.azure.com";
            string databaseName = "testDb";
            string containerName = "testContainer";
            
            string containerRid = "ccZ1ANCszwk=";
            ResourceId containerResourceId = ResourceId.Parse(containerRid);
            string databaseRid = containerResourceId.DatabaseId.ToString();

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
            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>(MockBehavior.Strict);
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
                partitionKeyRangeIds: out IReadOnlyList<string> primaryRegionPartitionKeyRangeIds);

            MockSetupsHelper.SetupAddresses(
                mockHttpHandler: mockHttpHandler,
                partitionKeyRangeId: primaryRegionPartitionKeyRangeIds.First(),
                regionEndpoint: primaryRegionEndpoint,
                regionName: primaryRegionNameForUri,
                containerResourceId: containerResourceId,
                primaryReplicaUri: out Uri primaryRegionprimaryReplicaUri);

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
                primaryReplicaUri: out Uri secondaryRegionPrimaryReplicaUri);

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

            CosmosClient customClient = new CosmosClient(
                globalEndpoint,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                cosmosClientOptions);

            Container container = customClient.GetContainer(databaseName, containerName);

            ToDoActivity toDoActivity = new ToDoActivity()
            {
                id = "TestItem",
                pk = "TestPk"
            };

            ItemResponse<ToDoActivity> response = await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            mockTransport.VerifyAll();
            mockHttpHandler.VerifyAll();

            // Clears all the setups. No network calls should be done on the next operation.
            mockHttpHandler.Reset();
            mockTransport.Reset();

            MockSetupsHelper.SetupCreateItemResponse(
                mockTransport,
                secondaryRegionPrimaryReplicaUri);

            ToDoActivity toDoActivity2 = new ToDoActivity()
            {
                id = "TestItem2",
                pk = "TestPk"
            };

            response = await container.CreateItemAsync(toDoActivity2, new Cosmos.PartitionKey(toDoActivity2.pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        [TestMethod]
        public async Task TestServiceUnavailableExceptionScenarioAsync()
        {
            string accountName = "testAccount";
            string primaryRegionNameForUri = "eastus";
            string secondaryRegionNameForUri = "westus";
            string globalEndpoint = $"https://{accountName}.documents.azure.com:443/";
            Uri globalEndpointUri = new Uri(globalEndpoint);
            string primaryRegionEndpoint = $"https://{accountName}-{primaryRegionNameForUri}.documents.azure.com";
            string secondaryRegionEndpiont = $"https://{accountName}-{secondaryRegionNameForUri}.documents.azure.com";
            string databaseName = "testDb";
            string containerName = "testContainer";

            string containerRid = "ccZ1ANCszwk=";
            ResourceId containerResourceId = ResourceId.Parse(containerRid);
            string databaseRid = containerResourceId.DatabaseId.ToString();

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
            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>(MockBehavior.Strict);
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
                partitionKeyRangeIds: out IReadOnlyList<string> primaryRegionPartitionKeyRangeIds);

            MockSetupsHelper.SetupAddresses(
                mockHttpHandler: mockHttpHandler,
                partitionKeyRangeId: primaryRegionPartitionKeyRangeIds.First(),
                regionEndpoint: primaryRegionEndpoint,
                regionName: primaryRegionNameForUri,
                containerResourceId: containerResourceId,
                primaryReplicaUri: out Uri primaryRegionprimaryReplicaUri);

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
                primaryReplicaUri: out Uri secondaryRegionPrimaryReplicaUri);

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

            CosmosClient customClient = new CosmosClient(
                globalEndpoint,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString())),
                cosmosClientOptions);

            Container container = customClient.GetContainer(databaseName, containerName);

            ToDoActivity toDoActivity = new ToDoActivity()
            {
                id = "TestItem",
                pk = "TestPk"
            };

            try
            {
                await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.pk));
                Assert.Fail("Should throw an exception");
            }
            catch(CosmosException ce) when (ce.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                Assert.IsNotNull(ce);
            }

            ItemResponse<ToDoActivity> response = await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            mockTransport.VerifyAll();
            mockHttpHandler.VerifyAll();

            // Clears all the setups. No network calls should be done on the next operation.
            mockHttpHandler.Reset();
            mockTransport.Reset();

            MockSetupsHelper.SetupCreateItemResponse(
                mockTransport,
                secondaryRegionPrimaryReplicaUri);

            ToDoActivity toDoActivity2 = new ToDoActivity()
            {
                id = "TestItem2",
                pk = "TestPk"
            };

            response = await container.CreateItemAsync(toDoActivity2, new Cosmos.PartitionKey(toDoActivity2.pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        private class ToDoActivity
        {
            public string id { get; set; }
            public string pk { get; set; }

            public override bool Equals(object obj)
            {
                if (!(obj is ToDoActivity input))
                {
                    return false;
                }

                return string.Equals(this.id, input.id)
                    && string.Equals(this.pk, input.pk);
            }
        }
    }
}
