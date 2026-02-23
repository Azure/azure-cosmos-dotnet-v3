namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Fluent;
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
            await this.TestInit();

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
            { (this.database.Id, "ClientCreateAndInitializeContainer")};

            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions
            {
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
            };

            CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(endpoint, authKey, containers, cosmosClientOptions);
            Assert.IsNotNull(cosmosClient);
            int httpCallsMadeAfterCreation = httpCallsMade;

            ContainerInternal container = (ContainerInternal)cosmosClient.GetContainer(this.database.Id, "ClientCreateAndInitializeContainer");
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
            { (this.database.Id, "ClientCreateAndInitializeContainer")};

            CosmosClientBuilder builder = new CosmosClientBuilder(endpoint, authKey).WithHttpClientFactory(() => new HttpClient(httpClientHandlerHelper));
            CosmosClient cosmosClient = await builder.BuildAndInitializeAsync(containers);
            Assert.IsNotNull(cosmosClient);
            int httpCallsMadeAfterCreation = httpCallsMade;

            ContainerInternal container = (ContainerInternal)cosmosClient.GetContainer(this.database.Id, "ClientCreateAndInitializeContainer");
            ItemResponse<ToDoActivity> readResponse = await container.ReadItemAsync<ToDoActivity>("1", new Cosmos.PartitionKey("Status1"));
            Assert.AreEqual(httpCallsMade, httpCallsMadeAfterCreation);
            cosmosClient.Dispose();
        }

        [TestMethod]
        [ExpectedException(typeof(HttpRequestException))]
        public async Task AuthIncorrectTest()
        {
            List<(string databaseId, string containerId)> containers = new List<(string databaseId, string containerId)>
            { (this.database.Id, "ClientCreateAndInitializeContainer")};
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
                throw;
            }
        }

        [TestMethod]
        [ExpectedException(typeof(CosmosException))]
        public async Task ContainerIncorrectTest()
        {
            List<(string databaseId, string containerId)> containers = new List<(string databaseId, string containerId)>
            { (this.database.Id, "IncorrectContainer")};
            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            try
            {
                CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(endpoint, authKey, containers);
            }
            catch (CosmosException ex)
            {
                Assert.IsTrue(ex.StatusCode == HttpStatusCode.NotFound);
                throw;
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

        /// <summary>
        /// Test to validate that when <see cref="CosmosClient.CreateAndInitializeAsync()"/> is called with a
        /// valid database id and a container id that exists in the database, an attempt is made to open
        /// the rntbd connections to the backend replicas and the connections are opened successfully.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task CreateAndInitializeAsync_WithValidDatabaseAndContainer_ShouldOpenRntbdConnectionsToBackendReplicas()
        {
            // Arrange.
            int httpCallsMade = 0, maxRequestsPerConnection = 6;
            HttpClientHandlerHelper httpClientHandlerHelper = new ()
            {
                RequestCallBack = (request, cancellationToken) =>
                {
                    httpCallsMade++;
                    return null;
                }
            };
            List<(string, string)> containers = new () 
            { 
                (
                this.database.Id,
                "ClientCreateAndInitializeContainer"
                )
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClientOptions cosmosClientOptions = new ()
            {
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
                ConnectionMode = ConnectionMode.Direct,
                MaxRequestsPerTcpConnection = maxRequestsPerConnection,
            };

            // Act.
            CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: authKey,
                containers: containers,
                cosmosClientOptions: cosmosClientOptions);

            // Assert.
            Assert.AreEqual(5, httpCallsMade);

            IStoreClientFactory factory = (IStoreClientFactory)cosmosClient.DocumentClient.GetType()
                            .GetField("storeClientFactory", BindingFlags.NonPublic | BindingFlags.Instance)
                            .GetValue(cosmosClient.DocumentClient);
            StoreClientFactory storeClientFactory = (StoreClientFactory) factory;

            TransportClient client = (TransportClient)storeClientFactory.GetType()
                            .GetField("transportClient", BindingFlags.NonPublic | BindingFlags.Instance)
                            .GetValue(storeClientFactory);
            Documents.Rntbd.TransportClient transportClient = (Documents.Rntbd.TransportClient) client;

            Documents.Rntbd.ChannelDictionary channelDict = (Documents.Rntbd.ChannelDictionary)transportClient.GetType()
                            .GetField("channelDictionary", BindingFlags.NonPublic | BindingFlags.Instance)
                            .GetValue(transportClient);

            ConcurrentDictionary<Documents.Rntbd.ServerKey, Documents.Rntbd.IChannel> allChannels = (ConcurrentDictionary<Documents.Rntbd.ServerKey, Documents.Rntbd.IChannel>)channelDict.GetType()
                .GetField("channels", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(channelDict);

            Assert.AreEqual(1, allChannels.Count);

            Documents.Rntbd.LoadBalancingChannel loadBalancingChannel = (Documents.Rntbd.LoadBalancingChannel)allChannels[allChannels.Keys.First()];
            Documents.Rntbd.LoadBalancingPartition loadBalancingPartition = (Documents.Rntbd.LoadBalancingPartition)loadBalancingChannel.GetType()
                                    .GetField("singlePartition", BindingFlags.NonPublic | BindingFlags.Instance)
                                    .GetValue(loadBalancingChannel);

            Assert.IsNotNull(loadBalancingPartition);

            int channelCapacity = (int)loadBalancingPartition.GetType()
                .GetField("capacity", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(loadBalancingPartition);

            List<Documents.Rntbd.LbChannelState> openChannels = (List<Documents.Rntbd.LbChannelState>)loadBalancingPartition.GetType()
                                    .GetField("openChannels", BindingFlags.NonPublic | BindingFlags.Instance)
                                    .GetValue(loadBalancingPartition);

            Assert.IsNotNull(openChannels);
            Assert.AreEqual(1, openChannels.Count, "Here the expected value 1 explains how many TCP connections were opened by the LoadBalancingPartition.OpenChannelAsync()." +
                "The emulator by default returns 12 partitions, and each partition has 4 replicas, and by behavior the emulator uses the same URI for each of these replica," +
                "hence 12 * 4 = 48 times we call the OpenChannelAsync(). However, the number of TCP connections established would be just one per each unique endpoint.");
            Assert.AreEqual(openChannels.Count * maxRequestsPerConnection, channelCapacity);

            Documents.Rntbd.LbChannelState channelState = openChannels.First();

            Assert.IsNotNull(channelState);
            Assert.IsTrue(channelState.DeepHealthy);
        }

        /// <summary>
        /// Test to validate that when <see cref="CosmosClient.CreateAndInitializeAsync()"/> is called with
        /// the Gateway Mode enabled, the operation should not open any Rntbd connections to the backend replicas.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task CreateAndInitializeAsync_WithGatewayModeEnabled_ShouldNotOpenConnectionToBackendReplicas()
        {
            // Arrange.
            int httpCallsMade = 0;
            HttpClientHandlerHelper httpClientHandlerHelper = new()
            {
                RequestCallBack = (request, cancellationToken) =>
                {
                    httpCallsMade++;
                    return null;
                }
            };
            List<(string, string)> containers = new()
            {
                (
                this.database.Id,
                "ClientCreateAndInitializeContainer"
                )
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClientOptions cosmosClientOptions = new()
            {
                HttpClientFactory = () => new HttpClient(httpClientHandlerHelper),
                ConnectionMode = ConnectionMode.Gateway,
            };

            // Act.
            CosmosClient cosmosClient = await CosmosClient.CreateAndInitializeAsync(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: authKey,
                containers: containers,
                cosmosClientOptions: cosmosClientOptions);

            // Assert.
            Assert.IsNotNull(cosmosClient);
            Assert.AreEqual(1, httpCallsMade);
        }

        /// <summary>
        /// Test to validate that when <see cref="CosmosClient.CreateAndInitializeAsync()"/> is called with a
        /// valid database id and an invalid container that doesn't exists in the database, the cosmos
        /// client initialization fails and a <see cref="CosmosException"/> is thrown with a 404 status code.
        /// </summary>
        [TestMethod]
        [Owner("dkunda")]
        public async Task CreateAndInitializeAsync_WithValidDatabaseAndInvalidContainer_ShouldThrowException()
        {
            // Arrange.
            List<(string, string)> containers = new()
            {
                (
                this.database.Id,
                "ClientCreateAndInitializeInvalidContainer"
                )
            };

            (string endpoint, string authKey) = TestCommon.GetAccountInfo();
            CosmosClientOptions cosmosClientOptions = new();

            // Act.
            CosmosException ce = await Assert.ThrowsExceptionAsync<CosmosException>(() => CosmosClient.CreateAndInitializeAsync(
                accountEndpoint: endpoint,
                authKeyOrResourceToken: authKey,
                containers: containers,
                cosmosClientOptions: cosmosClientOptions));

            // Assert.
            Assert.IsNotNull(ce);
            Assert.AreEqual(HttpStatusCode.NotFound, ce.StatusCode);
        }

        /// <summary>
        /// Test to validate write item operations with transport interceptor that modifies
        /// GlobalNRegionCommittedGLSN header and HTTP interceptor that sets EnableNRegionSynchronousCommit
        /// in AccountProperties response.
        /// </summary>
        [TestMethod]
        [Owner("aavasthy")]
        public async Task CreateAndInitializeAsync_WriteItemOperationWithNRegionCommitTest()
        {
            // Custom GlobalNRegionCommittedGLSN value to inject
            const long customGlobalNRegionCommittedGLSN = 100;

            // HTTP handler to intercept AccountProperties response and set EnableNRegionSynchronousCommit = true
            HttpClientHandlerHelper httpClientHandlerHelper = new HttpClientHandlerHelper
            {
                ResponseIntercepter = async (response, request) =>
                {
                    if (request.RequestUri.AbsolutePath.EndsWith("/", StringComparison.OrdinalIgnoreCase) ||
                        request.RequestUri.AbsolutePath.Equals(string.Empty))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            string content = await response.Content.ReadAsStringAsync();
                            Newtonsoft.Json.Linq.JObject accountJson = Newtonsoft.Json.Linq.JObject.Parse(content);

                            accountJson[Constants.Properties.EnableNRegionSynchronousCommit] = true;

                            string modifiedContent = accountJson.ToString();
                            response.Content = new StringContent(modifiedContent, System.Text.Encoding.UTF8, "application/json");
                        }
                    }
                    return response;
                }
            };

            await this.TestInit(
                customizeClientBuilder: (builder) =>
                {
                    builder.WithConnectionModeDirect();
                    builder.WithHttpClientFactory(() => new HttpClient(httpClientHandlerHelper));
                    builder.WithTransportClientHandlerFactory(transportClient =>
                        new TransportClientHelper.TransportClientWrapper(
                            client: transportClient,
                            interceptorAfterResult: (request, storeResponse) =>
                            {
                                // Override GlobalNRegionCommittedGLSN header in the response for Document operations
                                if (request.ResourceType == ResourceType.Document)
                                {
                                    storeResponse.Headers.Set(
                                        WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN,
                                        customGlobalNRegionCommittedGLSN.ToString(CultureInfo.InvariantCulture));
                                }
                                return storeResponse;
                            }));
                });

            string containerName = "NRegionCommitTestContainer_" + Guid.NewGuid().ToString("N");
            ContainerResponse containerResponse = await this.database.CreateContainerAsync(
                new ContainerProperties(id: containerName, partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(containerResponse);

            Container container = containerResponse.Container;

            try
            {
                for (int i = 0; i < 2; i++)
                {
                    ToDoActivity item = new()
                    {
                        id = Guid.NewGuid().ToString(),
                        pk = "testPartition",
                        description = $"Test item {i}",
                    };

                    // Act
                    ItemResponse<ToDoActivity> writeResponse = await container.CreateItemAsync(
                        item: item,
                        partitionKey: new Cosmos.PartitionKey(item.pk));

                    // Assert
                    Assert.AreEqual(HttpStatusCode.Created, writeResponse.StatusCode);

                    // Assert: Verify GlobalNRegionCommittedGLSN is present in diagnostics with value 100
                    string diagnostics = writeResponse.Diagnostics.ToString();

                    Assert.IsTrue(
                        diagnostics.Contains($"\"GlobalNRegionCommittedGLSN\":{customGlobalNRegionCommittedGLSN}") ||
                        diagnostics.Contains($"\"GlobalNRegionCommittedGLSN\": {customGlobalNRegionCommittedGLSN}"),
                        $"Expected GlobalNRegionCommittedGLSN to be {customGlobalNRegionCommittedGLSN} in diagnostics. Actual diagnostics: {diagnostics}");

                }
            }
            finally
            {
                // Cleanup: Delete the test container
                await container.DeleteContainerAsync();
            }
        }
    }
}
