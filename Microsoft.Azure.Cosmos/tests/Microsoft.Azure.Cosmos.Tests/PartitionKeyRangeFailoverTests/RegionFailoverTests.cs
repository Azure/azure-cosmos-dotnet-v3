//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class RegionFailoverTests
    {
        [TestMethod]
        public async Task TestHttpRequestExceptionScenarioAsync()
        {
            // testhost.dll.config sets it to 2 seconds which causes it to always expire before retrying. Remove the override.
            System.Configuration.ConfigurationManager.AppSettings["UnavailableLocationsExpirationTimeInSeconds"] = "500";

            string accountName = nameof(TestHttpRequestExceptionScenarioAsync);
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

            List<AccountRegion> writeRegionFailedOver = new List<AccountRegion>()
            {
                new AccountRegion()
                {
                    Name = "West US",
                    Endpoint = $"{secondaryRegionEndpiont}:443/"
                }
            };

            List<AccountRegion> readRegionsFailedOver = new List<AccountRegion>()
            {

                new AccountRegion()
                {
                    Name = "West US",
                    Endpoint = $"{secondaryRegionEndpiont}:443/"
                },
                new AccountRegion()
                {
                    Name = "East US",
                    Endpoint = $"{primaryRegionEndpoint}:443/"
                },
            };

            // Create a mock http handler to inject gateway responses.
            // MockBehavior.Strict ensures that only the mocked APIs get called
            Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>(MockBehavior.Strict);


            mockHttpHandler.Setup(x => x.SendAsync(
                It.Is<HttpRequestMessage>(m => m.RequestUri == globalEndpointUri || m.RequestUri.ToString().Contains(primaryRegionNameForUri)),
                It.IsAny<CancellationToken>())).Throws(new HttpRequestException("Mock HttpRequestException to simulate region being down"));

            int count = 0;
            mockHttpHandler.Setup(x => x.SendAsync(
               It.Is<HttpRequestMessage>(x => x.RequestUri == new Uri(secondaryRegionEndpiont)),
               It.IsAny<CancellationToken>()))
               .Returns<HttpRequestMessage, CancellationToken>((request, cancellationToken) =>
               {
                   // Simulate the legacy gateway being down. After 40 requests simulate the write region pointing to new location.
                   count++;
                   if (count < 2)
                   {
                       return Task.FromResult(MockSetupsHelper.CreateStrongAccount(accountName, writeRegion, readRegions, shouldEnablePPAF: true));
                   }
                   else
                   {
                       return Task.FromResult(MockSetupsHelper.CreateStrongAccount(accountName, writeRegionFailedOver, readRegionsFailedOver, shouldEnablePPAF: true));
                   }
               });


            MockSetupsHelper.SetupContainerProperties(
                mockHttpHandler: mockHttpHandler,
                regionEndpoint: secondaryRegionEndpiont,
                databaseName: databaseName,
                containerName: containerName,
                containerRid: containerRid);

            MockSetupsHelper.SetupPartitionKeyRanges(
                mockHttpHandler: mockHttpHandler,
                regionEndpoint: secondaryRegionEndpiont,
                containerResourceId: containerResourceId,
                partitionKeyRangeIds: out IReadOnlyList<string> secondaryRegionPartitionKeyRangeIds);

            MockSetupsHelper.SetupAddresses(
                mockHttpHandler: mockHttpHandler,
                partitionKeyRangeId: secondaryRegionPartitionKeyRangeIds.First(),
                regionEndpoint: secondaryRegionEndpiont,
                regionName: secondaryRegionNameForUri,
                containerResourceId: containerResourceId,
                primaryReplicaUri: out TransportAddressUri secondaryRegionprimaryReplicaUri);

            Mock<TransportClient> mockTransport = new Mock<TransportClient>(MockBehavior.Strict);

            MockSetupsHelper.SetupRequestTimeoutException(
                mockTransport,
                secondaryRegionprimaryReplicaUri);

            // Partition key ranges are the same in both regions so the SDK
            // does not need to go the secondary to get the partition key ranges.
            // Only the addresses need to be mocked on the secondary
            MockSetupsHelper.SetupAddresses(
                mockHttpHandler: mockHttpHandler,
                partitionKeyRangeId: secondaryRegionPartitionKeyRangeIds.First(),
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
                mockTransport.Setup(x => x.Dispose());

                // Reset it back to the override to avoid impacting other tests.
                System.Configuration.ConfigurationManager.AppSettings["UnavailableLocationsExpirationTimeInSeconds"] = "2";
            }

            await Task.Delay(TimeSpan.FromMinutes(2));
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Read Item Scenario without PPAF and PPCB.")]
        [DataRow(true, DisplayName = "Read Item Scenario with PPAF and PPCB.")]
        public async Task ReadItemAsync_WithThinClientEnabledAndServiceUnavailableReceived_ShouldRetryOnNextPreferredRegions(
            bool enablePartitionLevelFailover)
        {
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, "True");
                string accountName = nameof(TestHttpRequestExceptionScenarioAsync);
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

                // Create a mock http handler to inject proxy responses.
                // MockBehavior.Strict ensures that only the mocked APIs get called
                List<string> regionsVisited = new List<string>();
                Mock<IHttpHandler> mockHttpHandler = new Mock<IHttpHandler>(MockBehavior.Strict);
                string readResponseHexStringWith503Status = "2a000000F70100000000000000000000000000000000000035000201000000000000011c000200000000480000007b22636f6465223a2022343039222c226d657373616765223a2022416e206572726f72206f63637572726564207768696c6520726f7574696e67207468652072657175657374227d";
                string readResponseHexStringWith200Status = "2a000000C80000000000000000000000000000000000000035000201000000000000011c000200000000480000007b22636f6465223a2022343039222c226d657373616765223a2022416e206572726f72206f63637572726564207768696c6520726f7574696e67207468652072657175657374227d";
                mockHttpHandler.Setup(x => x.SendAsync(
                   It.Is<HttpRequestMessage>(m => m.RequestUri == globalEndpointUri || m.RequestUri.ToString().Contains(primaryRegionNameForUri) || m.RequestUri.ToString().Contains(secondaryRegionNameForUri)),
                   It.IsAny<CancellationToken>()))
                   .Returns<HttpRequestMessage, CancellationToken>((request, cancellationToken) =>
                   {
                       if (request.Version == new Version(2, 0))
                       {
                           if (request.RequestUri.ToString().Contains("eastus"))
                           {
                               regionsVisited.Add(Regions.EastUS);
                               return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                               {
                                   RequestMessage = request,
                                   Content = new StreamContent(new MemoryStream(Convert.FromHexString(readResponseHexStringWith503Status)))
                               });
                           }
                           else if (request.RequestUri.ToString().Contains("westus"))
                           {
                               regionsVisited.Add(Regions.WestUS);
                               return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                               {
                                   RequestMessage = request,
                                   Content = new StreamContent(new MemoryStream(Convert.FromHexString(readResponseHexStringWith200Status)))
                               });
                           }
                       }

                       return Task.FromResult(MockSetupsHelper.CreateStrongAccount(accountName, writeRegion, readRegions, shouldEnableThinClient: true, shouldEnablePPAF: enablePartitionLevelFailover));
                   });

                MockSetupsHelper.SetupContainerProperties(
                    mockHttpHandler: mockHttpHandler,
                    regionEndpoint: primaryRegionEndpoint,
                    databaseName: databaseName,
                    containerName: containerName,
                    containerRid: containerRid);

                CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
                {
                    ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                    ApplicationPreferredRegions = new List<string>()
                    {
                        Regions.EastUS,
                        Regions.WestUS
                    },
                    ConnectionMode = ConnectionMode.Gateway,
                    HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(mockHttpHandler.Object)),
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

                    ItemResponse<ToDoActivity> readResponse = await container.ReadItemAsync<ToDoActivity>(toDoActivity.Id, new Cosmos.PartitionKey(toDoActivity.Pk));
                    Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
                    Assert.IsTrue(regionsVisited.Count == 2);
                    Assert.AreEqual(Regions.EastUS, regionsVisited[0]);
                    Assert.AreEqual(Regions.WestUS, regionsVisited[1]);

                    CosmosTraceDiagnostics traceDiagnostic = readResponse.Diagnostics as CosmosTraceDiagnostics;
                    Assert.IsNotNull(traceDiagnostic);

                    traceDiagnostic.Value.Data.TryGetValue("Hedge Context", out object hedgeContext);

                    if (enablePartitionLevelFailover)
                    {
                        Assert.IsNotNull(hedgeContext);
                        List<string> hedgedRegions = ((IEnumerable<string>)hedgeContext).ToList();

                        Assert.IsTrue(hedgedRegions.Count >= 1, "Since the first region is not available, the request should atleast hedge to the next region.");
                        Assert.IsTrue(hedgedRegions.Contains(Regions.EastUS));
                    }
                    else
                    {
                        Assert.IsNull(hedgeContext);
                    }

                    mockHttpHandler.VerifyAll();
                }
            }
            finally
            {
                // Reset the environment variable to avoid impacting other tests.
                Environment.SetEnvironmentVariable(ConfigurationManager.ThinClientModeEnabled, null);
            }
        }
    }
}