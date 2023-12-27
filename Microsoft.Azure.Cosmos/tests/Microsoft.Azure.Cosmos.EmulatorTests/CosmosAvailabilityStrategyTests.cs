namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using PartitionKey = PartitionKey;

    [TestClass]
    public class CosmosAvailabilityStrategyTests
    {

        private CosmosClient client = null;
        private Cosmos.Database database = null;

        private Container container = null;
        private ContainerProperties containerProperties = null;

        public async Task TestInitialize()
        {
           
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            this.client = new CosmosClient(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: authKey);

            this.database = await this.client.CreateDatabaseIfNotExistsAsync("testDb");

            this.containerProperties = new ContainerProperties("test", "/pk");
            this.container = await this.database.CreateContainerAsync(this.containerProperties);

            await this.container.CreateItemAsync(new { id = "1", pk = "1" });
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await this.database.DeleteAsync();
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

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("test")
            };

            Mock<CosmosHttpClient> mockHttpClient = new Mock<CosmosHttpClient>();
            mockHttpClient.SetupSequence(x =>
                x.SendHttpAsync(
                    It.IsAny<Func<ValueTask<HttpRequestMessage>>>(),
                    It.Is<ResourceType>(rType => rType == ResourceType.Document),
                    It.IsAny<HttpTimeoutPolicy>(),
                    It.IsAny<IClientSideRequestStatistics>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.Delay(TimeSpan.FromSeconds(5)).ContinueWith(_ => response))
                .Returns(Task.FromResult(response));

            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ConnectionMode = ConnectionMode.Gateway,
                ApplicationPreferredRegions = new List<string>() { "East US", "West US" },
                AvailabilityStrategyOptions = new AvailabilityStrategyOptions(
                    AvailabilityStrategyType.ParallelHedging,
                    threshold: TimeSpan.FromMilliseconds(100),
                    step: TimeSpan.FromMilliseconds(50))
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>(endpoint, authKey, clientOptions);

            Container testContainer = mockClient.Object.GetContainer(this.database.Id, this.container.Id);
            CosmosDiagnostics diagnostics = (await testContainer.ReadItemAsync<dynamic>("1", new PartitionKey("1"))).Diagnostics;
            Console.WriteLine(diagnostics.ToString());
        }

        //Test avaialvility strategy does not trigger 
        //test that availability strategy triggers 
        //test that availability strategy triggers and original region returns first 
        //fabian test case

    }
}
