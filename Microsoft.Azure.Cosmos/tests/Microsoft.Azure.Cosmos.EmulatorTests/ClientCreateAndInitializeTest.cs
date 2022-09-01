﻿namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Moq.Protected;

    [TestClass]
    public class ClientCreateAndInitializeTest : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;
        private const string PartitionKey = "/pk";

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.cosmosClient = TestCommon.CreateCosmosClient(useGateway: false);
            this.database = await this.cosmosClient.CreateDatabaseAsync(
                   id: "ClientCreateAndInitializeDatabase");
            ContainerResponse response = await this.database.CreateContainerAsync(
                        new ContainerProperties(id: "ClientCreateAndInitializeContainer", partitionKeyPath: PartitionKey),
                        throughput: 20000,
                        cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = (ContainerInlineCore)response;

            // Create items with different
            for (int i = 0; i < 500; i++)
            {
                ToDoActivity item = ToDoActivity.CreateRandomToDoActivity();
                item.pk = "Status" + i.ToString();
                item.id = i.ToString();
                ItemResponse<ToDoActivity> itemResponse = await this.Container.CreateItemAsync(item);
                Assert.AreEqual(HttpStatusCode.Created, itemResponse.StatusCode);
            }
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CreateAndInitializeTest()
        {
            int httpCallsMade = 0;
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellationToken) =>
                {
                    httpCallsMade++;
                    return null;
                }
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            List<(string, string)> containers = new List<(string, string)> 
            { ("ClientCreateAndInitializeDatabase", "ClientCreateAndInitializeContainer")};

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
            };

            CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(endpoint, authKey, containers, cosmosClientOptions);
            Assert.IsNotNull(cosmosClient);
            int httpCallsMadeAfterCreation = httpCallsMade;

            ContainerInternal container = (ContainerInternal)cosmosClient.GetContainer("ClientCreateAndInitializeDatabase", "ClientCreateAndInitializeContainer");
            ItemResponse<ToDoActivity> readResponse = await container.ReadItemAsync<ToDoActivity>("1", new Cosmos.PartitionKey("Status1"));
            string diagnostics = readResponse.Diagnostics.ToString();
            Assert.IsTrue(diagnostics.Contains("\"ConnectionMode\":\"Direct\""));
            Assert.AreEqual(httpCallsMade, httpCallsMadeAfterCreation);
            cosmosClient.Dispose();
        }

        [TestMethod]
        public async Task CreateAndInitializeWithCosmosClientBuilderTest()
        {
            int httpCallsMade = 0;
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper
            {
                RequestCallBack = (request, cancellationToken) =>
                {
                    httpCallsMade++;
                    return null;
                }
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            List<(string, string)> containers = new List<(string, string)>
            { ("ClientCreateAndInitializeDatabase", "ClientCreateAndInitializeContainer")};

            CosmosClientBuilder builder = new CosmosClientBuilder(endpoint, authKey).WithHttpClientFactory(() => new HttpClient(httpClientHandlerHelper));
            CosmosClient cosmosClient = await builder.BuildAndInitializeAsync(containers);
            Assert.IsNotNull(cosmosClient);
            int httpCallsMadeAfterCreation = httpCallsMade;

            ContainerInternal container = (ContainerInternal)cosmosClient.GetContainer("ClientCreateAndInitializeDatabase", "ClientCreateAndInitializeContainer");
            ItemResponse<ToDoActivity> readResponse = await container.ReadItemAsync<ToDoActivity>("1", new Cosmos.PartitionKey("Status1"));
            Assert.AreEqual(httpCallsMade, httpCallsMadeAfterCreation);
            cosmosClient.Dispose();
        }

        [TestMethod]
        [ExpectedException(typeof(HttpRequestException))]
        public async Task AuthIncorrectTest()
        {
            List<(string databaseId, string containerId)> containers = new List<(string databaseId, string containerId)>
            { ("ClientCreateAndInitializeDatabase", "ClientCreateAndInitializeContainer")};
            string authKey = TestCommon.GetAccountInfo().authKey;
            CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync("https://127.0.0.1:0000/", authKey, containers);
            cosmosClient.Dispose();
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public async Task DatabaseIncorrectTest()
        {
            List<(string databaseId, string containerId)> containers = new List<(string databaseId, string containerId)>
            { ("IncorrectDatabase", "ClientCreateAndInitializeContainer")};
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            try
            {
                CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(endpoint, authKey, containers);
            }
            catch (CosmosException ex)
            {
                Assert.IsTrue(ex.StatusCode == HttpStatusCode.NotFound);
                throw ex;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public async Task ContainerIncorrectTest()
        {
            List<(string databaseId, string containerId)> containers = new List<(string databaseId, string containerId)>
            { ("ClientCreateAndInitializeDatabase", "IncorrectContainer")};
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            try
            {
                CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(endpoint, authKey, containers);
            }
            catch (CosmosException ex)
            {
                Assert.IsTrue(ex.StatusCode == HttpStatusCode.NotFound);
                throw ex;
            }
        }

        [TestMethod]
        public async Task InitializeContainersAsync_WhenThrowsException_ShouldDisposeCosmosClient()
        {
            // Arrange.
            List<(string databaseId, string containerId)> containers = new()
            { ("IncorrectDatabase", "ClientCreateAndInitializeContainer")};

            CosmosException cosmosException = new (
                statusCode: HttpStatusCode.NotFound,
                message: "Test",
                stackTrace: null,
                headers: null,
                trace: default,
                error: null,
                innerException: null);

            Mock<CosmosClient> cosmosClient = new ();
            cosmosClient
                .Setup(x => x.GetContainer(It.IsAny<string>(), It.IsAny<string>()))
                .Throws(cosmosException);

            cosmosClient
                .Protected()
                .Setup("Dispose", ItExpr.Is<bool>(x => x))
                .Verifiable();

            // Act.
            CosmosException ex = await Assert.ThrowsExceptionAsync<CosmosException>(() => cosmosClient.Object.InitializeContainersAsync(containers, this.cancellationToken));

            // Assert.
            Assert.IsNotNull(ex);
            Assert.IsTrue(ex.StatusCode == HttpStatusCode.NotFound);
            cosmosClient.Verify();
        }
    }
}
