namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class CosmosAvailabilityStrategyTests
    {

        private CosmosClient client = null;

        [TestInitialize]
        public void TestInitialize()
        {
           
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string>() { "East US", "West US" },
                AvailabilityStrategyOptions = new AvailabilityStrategyOptions(
                    AvailabilityStrategyType.ParallelHedging,
                    threshold: TimeSpan.FromMilliseconds(100),
                    step: TimeSpan.FromMilliseconds(50))
            };

            this.client = new CosmosClient(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: authKey,
                clientOptions: clientOptions);
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.client.Dispose();
        }

        [TestMethod]
        public async Task AvailabilityStrategyTest()
        {
            //static Task<HttpResponseMessage> sendFunc(HttpRequestMessage request, CancellationToken cancellationToken)
            //{
            //    HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
            //    {
            //        Content = new StringContent("test")
            //    };
            //    return Task.FromResult(response);
            //}

            //HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            //{
            //    Content = new StringContent("test")
            //};

            //Mock<CosmosHttpClient> mockHttpClient = new Mock<CosmosHttpClient>();
            //mockHttpClient.SetupSequence(x =>
            //    x.SendHttpAsync(
            //        It.IsAny<Func<ValueTask<HttpRequestMessage>>>(),
            //        It.Is<ResourceType>(rType => rType == ResourceType.Document),
            //        It.IsAny<HttpTimeoutPolicy>(),
            //        It.IsAny<IClientSideRequestStatistics>(),
            //        It.IsAny<CancellationToken>()))
            //    .Returns(Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => response))
            //    .Returns(Task.FromResult(response));           

            //(string endpoint, string authKey) = TestCommon.GetAccountInfo();


            //Mock<CosmosClient> mockClient = new Mock<CosmosClient>(endpoint, authKey, clientOptions);
            //CosmosClient mockClient = new CosmosClient(endpoint, authKey, clientOptions);
            //Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            //mockClient.Setup(x => x.ClientOptions).Returns(clientOptions);
            //mockClient.Setup(x => x.DocumentClient.httpClient).Returns(mockHttpClient.Object);
            //mockClient.Setup(x => x.Endpoint).Returns(new Uri(endpoint));
            //mockClient.Setup(x => x.AuthorizationTokenProvider).Returns(
            //    AuthorizationTokenProvider.CreateWithResourceTokenOrAuthKey(authKey));
            //Container testContainer = mockClient.GetContainer(this.database.Id, this.container.Id);

            AccountProperties accountProperties = new AccountProperties()
            {
                ReadLocationsInternal = new Collection<AccountRegion>()
                    {
                        new AccountRegion()
                        {
                            Name = "East US",
                            Endpoint = "https://eastus.documents.azure.com:443/"
                        },
                        new AccountRegion()
                        {
                            Name = "West US",
                            Endpoint = "https://westus.documents.azure.com:443/"
                        }
                    },
                WriteLocationsInternal = new Collection<AccountRegion>()
                    {
                        new AccountRegion()
                        {
                            Name = "South Central US",
                            Endpoint = "https://127.0.0.1:8081/"
                        },
                    },
                EnableMultipleWriteLocations = false
            };

            Mock<IDocumentClientInternal> mockedClient = new Mock<IDocumentClientInternal>();
            mockedClient.Setup(owner => owner.ServiceEndpoint).Returns(new Uri("https://default.documents.azure.com:443/"));
            mockedClient.Setup(owner => owner.GetDatabaseAccountInternalAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>())).ReturnsAsync(accountProperties);

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                EnableEndpointDiscovery = false,
                UseMultipleWriteLocations = false,
            };
            connectionPolicy.PreferredLocations.Add("East US");
            connectionPolicy.PreferredLocations.Add("West US");

            GlobalEndpointManager globalEndpointManager = new GlobalEndpointManager(mockedClient.Object, connectionPolicy);
            globalEndpointManager.UpdateLocationCache(accountProperties);
            
            ResponseMessage responseMessageOK = new ResponseMessage(HttpStatusCode.OK);
            ResponseMessage responseMessageServiceUnavailable = new ResponseMessage(HttpStatusCode.ServiceUnavailable);

            Mock<RequestInvokerHandler> mockRequestInvokerHandler = new Mock<RequestInvokerHandler>(
                this.client,
                null);
            mockRequestInvokerHandler.SetupSequence(x =>
                x.BaseSendAsync(
                    It.IsAny<RequestMessage>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(TimeSpan.FromMilliseconds(500)).ContinueWith(_ => responseMessageServiceUnavailable))
                .Returns(Task.FromResult(responseMessageOK));

            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Get,
                new Uri("https://eastus.documents.azure.com:443/testDb/test/1"))
            {
                ResourceType = ResourceType.Document,
                OperationType = OperationType.Read,
                DatabaseId = "testDb",
                ContainerId = "test"
            };

            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "\"1\"");

            mockRequestInvokerHandler.Object.SetGlobalEndpointManager(globalEndpointManager);
            CancellationToken cancellationToken = new CancellationToken();
            ResponseMessage rm = await mockRequestInvokerHandler.Object
                .SendWithAvailabilityStrategyAsync(requestMessage, cancellationToken);
            Console.WriteLine(rm.StatusCode);
        }

        //Test avaialvility strategy does not trigger 
        //test that availability strategy triggers 
        //test that availability strategy triggers and original region returns first 
        //fabian test case

    }
}
